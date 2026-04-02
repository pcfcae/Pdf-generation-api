using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.Hosting;
using PCFC.DocGen.Api.Contracts;
using PCFC.DocGen.Api.Services;

namespace PCFC.DocGen.Api.Controllers
{
    [RoutePrefix("api/pdf")]
    public class PcfcPdfGenerationController : ApiController
    {
        private readonly IPdfGenerationService _pdfGenerationService;

        public PcfcPdfGenerationController()
            : this(new PdfGenerationService())
        {
        }

        public PcfcPdfGenerationController(IPdfGenerationService pdfGenerationService)
        {
            _pdfGenerationService = pdfGenerationService;
        }

        [HttpPost]
        [Route("generate")]
        public HttpResponseMessage Generate([FromBody] HtmlToPdfRequestDto request)
        {
            var result = _pdfGenerationService.Generate(request);

            var fileName = NormalizeFileName(result.FileName);
            var savedFilePath = SaveToOutputFolder(result.Content, fileName);
            if (string.IsNullOrWhiteSpace(savedFilePath) || !File.Exists(savedFilePath))
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError,
                    "Generated PDF could not be saved to Output folder.");
            }

            var encodedName = Uri.EscapeDataString(fileName);
            var authority = Request.RequestUri.GetLeftPart(UriPartial.Authority);
            var downloadLink = string.Format("{0}/api/pdf/download?fileName={1}", authority, encodedName);

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                fileName = fileName,
                downloadLink = downloadLink
            });
        }

        [HttpPost]
        [Route("generateBase64")]
        public HttpResponseMessage GenerateBase64([FromBody] HtmlToPdfRequestDto request)
        {
            var result = _pdfGenerationService.Generate(request);
            var fileName = NormalizeFileName(result.FileName);
            var savedFilePath = SaveToOutputFolder(result.Content, fileName);

            if (string.IsNullOrWhiteSpace(savedFilePath) || !File.Exists(savedFilePath))
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError,
                    "Generated PDF could not be saved to Output folder.");
            }

            var responseBytes = File.ReadAllBytes(savedFilePath);
            var base64Content = Convert.ToBase64String(responseBytes);

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                fileName = fileName,
                contentType = "application/pdf",
                base64 = base64Content
            });
        }

        [HttpGet]
        [Route("download")]
        public HttpResponseMessage DownloadFromOutput([FromUri] string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "fileName is required.");
            }

            var normalizedFileName = NormalizeFileName(fileName);
            var outputPath = ResolveOutputFolderPath();
            var filePath = Path.Combine(outputPath, normalizedFileName);

            if (!File.Exists(filePath))
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Requested output file was not found.");
            }

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            response.Content.Headers.ContentLength = stream.Length;
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = normalizedFileName
            };

            return response;
        }

        private static string SaveToOutputFolder(byte[] content, string fileName)
        {
            if (content == null || content.Length == 0)
            {
                return null;
            }

            try
            {
                var outputPath = ResolveOutputFolderPath();

                Directory.CreateDirectory(outputPath);
                var targetPath = Path.Combine(outputPath, fileName);
                File.WriteAllBytes(targetPath, content);
                return targetPath;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Failed to save generated PDF to Output folder. FileName={0}. Error={1}", fileName, ex.Message);
                return null;
            }
        }

        private static string ResolveOutputFolderPath()
        {
            var outputPath = HostingEnvironment.MapPath("~/Output");
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                return outputPath;
            }

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(basePath, "..", "Output"));
        }

        private static string NormalizeFileName(string fileName)
        {
            var normalized = string.IsNullOrWhiteSpace(fileName) ? "document.pdf" : fileName.Trim();
            normalized = normalized.Trim('"', ' ', '_');
            normalized = normalized.Replace("\r", string.Empty).Replace("\n", string.Empty);

            // Fix malformed extension (e.g. .pdf_ or trailing underscores)
            if (normalized.EndsWith(".pdf_", System.StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            // Ensure .pdf extension
            normalized = normalized.EndsWith(".pdf", System.StringComparison.OrdinalIgnoreCase) ? normalized : normalized + ".pdf";

            // Sanitize for Content-Disposition: replace chars that get corrupted (brackets etc.)
            var sanitized = new System.Text.StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == ' ' || c == '(' || c == ')')
                {
                    sanitized.Append(c);
                }
                else if (c == '[' || c == ']')
                {
                    sanitized.Append('-');
                }
            }

            normalized = sanitized.ToString().Trim(' ', '_', '-');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "document.pdf";
            }

            return normalized;
        }
    }
}
