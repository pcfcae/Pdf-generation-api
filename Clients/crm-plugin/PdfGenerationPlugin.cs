using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;

namespace PdfGenerationPlugin
{
    public class GeneratePdfPlugin : IPlugin
    {
        private const string ApiBaseUrl = "https://your-api-url";
        private const int StatusCompleted = 100000001;
        private const int StatusResubmit = 100000002;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);
            var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (!(context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity entity))
                    return;

                string reportType = entity.GetAttributeValue<string>("new_reporttype");
                if (string.IsNullOrEmpty(reportType))
                {
                    tracingService.Trace("No report type specified on the Case.");
                    return;
                }

                var caseRecord = service.Retrieve(
                    entity.LogicalName,
                    entity.Id,
                    new ColumnSet(true));

                var template = LoadTemplate(service, reportType, tracingService);
                if (template == null)
                {
                    throw new InvalidPluginExecutionException(
                        string.Format("No report template configured for type '{0}'.", reportType));
                }

                string bodyHtml = BindPlaceholders(
                    template.GetAttributeValue<string>("new_bodyhtmlcontent") ?? string.Empty,
                    caseRecord);

                string headerHtml = BindPlaceholders(
                    template.GetAttributeValue<string>("new_headerhtmlcontent") ?? string.Empty,
                    caseRecord);

                string footerHtml = BindPlaceholders(
                    template.GetAttributeValue<string>("new_footerhtmlcontent") ?? string.Empty,
                    caseRecord);

                var payload = new
                {
                    HtmlContent = bodyHtml,
                    FileName = string.Format("{0}_report.pdf", reportType),
                    HeaderHtml = headerHtml,
                    FooterHtml = footerHtml,
                    BaseUrl = (string)null,
                    PdfOptions = new
                    {
                        PageSize = "A4",
                        PageWidth = (float?)null,
                        PageHeight = (float?)null,
                        MarginLeft = 35f,
                        MarginRight = 25f,
                        MarginTop = 85f,
                        MarginBottom = 15f,
                        IsLandscape = false,
                        HeaderImageHeight = (float?)0f,
                        FooterImageHeight = (float?)45f,
                        FooterSpacing = (float?)4f
                    },
                    WatermarkOptions = new
                    {
                        WithStamp = false,
                        HeaderWatermarkImageUrl = string.Empty,
                        FooterWatermarkImageUrl = string.Empty,
                        HeaderWatermarkMarginLeft = 0f,
                        HeaderWatermarkMarginTop = 10f,
                        StampImageUrl = string.Empty,
                        StampBottomOffset = 30f
                    }
                };

                string jsonPayload = JsonConvert.SerializeObject(payload);
                tracingService.Trace("Calling PDF API for report type: {0}", reportType);

                string base64Pdf = CallPdfApi(jsonPayload, tracingService);

                if (string.IsNullOrEmpty(base64Pdf))
                {
                    throw new InvalidPluginExecutionException("PDF API returned empty content.");
                }

                SavePdfAsNote(service, caseRecord, base64Pdf, reportType);
                tracingService.Trace("PDF saved as annotation on Case.");

                UpdateCaseStatus(service, caseRecord, reportType, tracingService);
                tracingService.Trace("Case status updated for report type: {0}", reportType);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error: {0}", ex.ToString());
                throw new InvalidPluginExecutionException("PDF generation failed.", ex);
            }
        }

        private Entity LoadTemplate(IOrganizationService service, string reportType, ITracingService tracing)
        {
            var query = new QueryExpression("new_pdftemplate")
            {
                ColumnSet = new ColumnSet("new_bodyhtmlcontent", "new_headerhtmlcontent", "new_footerhtmlcontent"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("new_templatekey", ConditionOperator.Equal, reportType)
                    }
                },
                TopCount = 1
            };

            var results = service.RetrieveMultiple(query);
            if (results.Entities.Count == 0)
            {
                tracing.Trace("Template lookup returned no results for: {0}", reportType);
                return null;
            }

            return results.Entities[0];
        }

        private string BindPlaceholders(string html, Entity caseEntity)
        {
            if (string.IsNullOrEmpty(html))
                return html;

            return Regex.Replace(html, @"\{\{(\w+)\}\}", match =>
            {
                string fieldName = match.Groups[1].Value;
                if (!caseEntity.Contains(fieldName))
                    return match.Value;

                object value = caseEntity[fieldName];
                if (value == null)
                    return string.Empty;

                if (value is EntityReference entityRef)
                    return entityRef.Name ?? entityRef.Id.ToString();

                if (value is OptionSetValue optionSet)
                    return optionSet.Value.ToString();

                if (value is Money money)
                    return money.Value.ToString("N2");

                if (value is DateTime dateTime)
                    return dateTime.ToString("dd-MMM-yyyy");

                return value.ToString();
            });
        }

        private string CallPdfApi(string jsonPayload, ITracingService tracingService)
        {
            string url = ApiBaseUrl.TrimEnd('/') + "/api/pdf/generateBase64";
            tracingService.Trace("PDF API URL: {0}", url);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Timeout = 120000;

            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonPayload);
            request.ContentLength = bodyBytes.Length;

            using (var requestStream = request.GetRequestStream())
            {
                requestStream.Write(bodyBytes, 0, bodyBytes.Length);
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    tracingService.Trace("PDF API returned status: {0}", response.StatusCode);
                    throw new InvalidPluginExecutionException(
                        string.Format("PDF API returned HTTP {0}.", (int)response.StatusCode));
                }

                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    var result = JsonConvert.DeserializeObject<GenerateBase64Response>(json);

                    if (result == null || string.IsNullOrEmpty(result.Base64))
                    {
                        tracingService.Trace("PDF API response missing base64 content.");
                        return null;
                    }

                    tracingService.Trace("PDF received: {0}", result.FileName);
                    return result.Base64;
                }
            }
        }

        private void SavePdfAsNote(IOrganizationService service, Entity caseEntity, string base64Content, string reportType)
        {
            var note = new Entity("annotation")
            {
                ["subject"] = string.Format("Generated {0} report", reportType),
                ["filename"] = string.Format("{0}_report.pdf", reportType),
                ["mimetype"] = "application/pdf",
                ["documentbody"] = base64Content,
                ["objectid"] = caseEntity.ToEntityReference()
            };

            service.Create(note);
        }

        private void UpdateCaseStatus(IOrganizationService service, Entity caseEntity, string reportType, ITracingService tracing)
        {
            var update = new Entity(caseEntity.LogicalName, caseEntity.Id);

            string lowerType = reportType.ToLower();

            switch (lowerType)
            {
                // Technical reports / notifications -> Resubmit
                case "technical_report":
               
                    update["statuscode"] = new OptionSetValue(StatusResubmit);
                    tracing.Trace("Setting Case status to Resubmit.");
                    break;

                // Permits -> Completed
               
                case "modification_report":
                case "completion_certificate":
                case "general_report":
                    update["statuscode"] = new OptionSetValue(StatusCompleted);
                    tracing.Trace("Setting Case status to Completed.");
                    break;

                default:
                    tracing.Trace("No status mapping for report type: {0}. Skipping status update.", reportType);
                    return;
            }

            service.Update(update);
        }

        private class GenerateBase64Response
        {
            [JsonProperty("fileName")]
            public string FileName { get; set; }

            [JsonProperty("contentType")]
            public string ContentType { get; set; }

            [JsonProperty("base64")]
            public string Base64 { get; set; }
        }
    }
}
