using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.tool.xml;
using PCFC.DocGen.Api.Contracts;
using PCFC.DocGen.Api.Helpers;
using PCFC.DocGen.Api.Models;

namespace PCFC.DocGen.Api.Services
{
    public class PdfGenerationService : IPdfGenerationService
    {
        public PdfGenerationResult Generate(HtmlToPdfRequestDto request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (string.IsNullOrWhiteSpace(request.HtmlContent))
            {
                throw new ArgumentException("HtmlContent is required.");
            }

            var tempFiles = new List<string>();
            try
            {
                var processedHeaderHtml = ReplaceBase64ImageSources(request.HeaderHtml, tempFiles);
                var processedFooterHtml = ReplaceBase64ImageSources(request.FooterHtml, tempFiles);
                var processedHtmlContent = ReplaceBase64ImageSources(request.HtmlContent, tempFiles);

                var options = request.PdfOptions ?? new PdfOptionsDto();
                var pageSize = PageSizeResolver.Resolve(options.PageSize, options.PageWidth, options.PageHeight, options.IsLandscape);
                var headerImageUrl = ResolveResourceUrl(ExtractFirstImageUrl(processedHeaderHtml, request.BaseUrl), request.BaseUrl);
                var footerImageUrl = ResolveResourceUrl(ExtractFirstImageUrl(processedFooterHtml, request.BaseUrl), request.BaseUrl);
                var headerImageHeight = options.HeaderImageHeight.HasValue && options.HeaderImageHeight.Value > 0
                    ? options.HeaderImageHeight.Value
                    : ExtractFirstImageHeight(processedHeaderHtml);
                var footerImageHeight = options.FooterImageHeight.HasValue && options.FooterImageHeight.Value > 0
                    ? options.FooterImageHeight.Value
                    : ExtractFirstImageHeight(processedFooterHtml);
                var footerSpacing = options.FooterSpacing.HasValue && options.FooterSpacing.Value >= 0
                    ? options.FooterSpacing.Value
                    : 4f;
                var reservedFooterHeight = footerImageHeight > 0 ? footerImageHeight : Math.Max(45f, options.MarginBottom);
                var effectiveBottomMargin = string.IsNullOrWhiteSpace(footerImageUrl)
                    ? options.MarginBottom
                    : Math.Max(options.MarginBottom, reservedFooterHeight + footerSpacing);
                var bodyHeaderHtml = RemoveImageTags(processedHeaderHtml);
                var bodyFooterHtml = RemoveImageTags(processedFooterHtml);

                RegisterResourceFonts();

                using (var output = new MemoryStream())
                using (var document = new Document(
                    pageSize,
                    options.MarginLeft,
                    options.MarginRight,
                    options.MarginTop,
                    effectiveBottomMargin))
                {
                    var writer = PdfWriter.GetInstance(document, output);
                    writer.CloseStream = false;
                    writer.PageEvent = new WatermarkPageEvent(request.WatermarkOptions, request.BaseUrl, headerImageUrl, headerImageHeight, footerImageUrl, footerImageHeight);
                    document.Open();

                    var html = BuildHtml(bodyHeaderHtml, processedHtmlContent, bodyFooterHtml);
                    using (var reader = new StringReader(html))
                    {
                        try
                        {
                            XMLWorkerHelper.GetInstance().ParseXHtml(writer, document, reader);
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceError("XMLWorker parsing failed. BaseUrl={0}. Error={1}", request.BaseUrl ?? "<null>", ex);
                            throw;
                        }
                    }

                    document.Close();

                    var pdfBytes = output.ToArray();
                    pdfBytes = SecurePdfIfEnabled(pdfBytes);

                    return new PdfGenerationResult
                    {
                        Content = pdfBytes,
                        FileName = string.IsNullOrWhiteSpace(request.FileName) ? "document.pdf" : request.FileName
                    };
                }
            }
            finally
            {
                CleanupTempFiles(tempFiles);
            }
        }

        private static void RegisterResourceFonts()
        {
            try
            {
                var fontsPath = HostingEnvironment.MapPath("~/Resource/fonts");
                if (string.IsNullOrEmpty(fontsPath) || !Directory.Exists(fontsPath))
                {
                    var basePath = AppDomain.CurrentDomain.BaseDirectory;
                    fontsPath = Path.GetFullPath(Path.Combine(basePath, "..", "Resource", "fonts"));
                }

                if (Directory.Exists(fontsPath))
                {
                    FontFactory.RegisterDirectory(fontsPath);
                }
                else
                {
                    Trace.TraceWarning("Resource fonts directory not found: {0}", fontsPath ?? "<null>");
                }

                var resourcePath = HostingEnvironment.MapPath("~/Resource");
                if (string.IsNullOrEmpty(resourcePath) || !Directory.Exists(resourcePath))
                {
                    var basePath = AppDomain.CurrentDomain.BaseDirectory;
                    resourcePath = Path.GetFullPath(Path.Combine(basePath, "..", "Resource"));
                }

                if (Directory.Exists(resourcePath))
                {
                    FontFactory.RegisterDirectory(resourcePath);
                }
                else
                {
                    Trace.TraceWarning("Resource directory not found: {0}", resourcePath ?? "<null>");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Font registration failed. Error={0}", ex);
            }
        }

        private static string BuildHtml(string headerHtml, string htmlContent, string footerHtml)
        {
            return string.Concat(
                "<html><body>",
                string.IsNullOrWhiteSpace(headerHtml) ? string.Empty : headerHtml,
                htmlContent,
                string.IsNullOrWhiteSpace(footerHtml) ? string.Empty : footerHtml,
                "</body></html>");
        }

        private static string ExtractFirstImageUrl(string html, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var match = Regex.Match(html, "<img[^>]*?src\\s*=\\s*['\\\"](?<src>[^'\\\"]+)['\\\"][^>]*>", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return string.Empty;
            }

            var src = match.Groups["src"].Value;
            if (string.IsNullOrWhiteSpace(src))
            {
                return string.Empty;
            }

            if (src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                src.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                src.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return src;
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return src;
            }

            Uri baseUri;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri))
            {
                return src;
            }

            Uri absoluteUri;
            return Uri.TryCreate(baseUri, src, out absoluteUri) ? absoluteUri.ToString() : src;
        }

        private static string ResolveResourceUrl(string src, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(src))
            {
                return string.Empty;
            }

            Uri absoluteUri;
            if (Uri.TryCreate(src, UriKind.Absolute, out absoluteUri))
            {
                return absoluteUri.ToString();
            }

            if (src.StartsWith("~/", StringComparison.Ordinal))
            {
                var mapped = HostingEnvironment.MapPath(src);
                if (!string.IsNullOrWhiteSpace(mapped) && File.Exists(mapped))
                {
                    return new Uri(mapped).AbsoluteUri;
                }
            }

            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                Uri baseUri;
                if (Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri))
                {
                    Uri combined;
                    if (Uri.TryCreate(baseUri, src, out combined))
                    {
                        return combined.ToString();
                    }
                }
            }

            var normalized = src.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).TrimStart('~', Path.DirectorySeparatorChar);
            var rootPath = HostingEnvironment.MapPath("~/");
            if (!string.IsNullOrWhiteSpace(rootPath))
            {
                var localPath = Path.Combine(rootPath, normalized);
                if (File.Exists(localPath))
                {
                    return new Uri(localPath).AbsoluteUri;
                }
            }

            Trace.TraceWarning("Could not resolve image URL. Source={0}, BaseUrl={1}", src, baseUrl ?? "<null>");
            return src;
        }

        private static string RemoveImageTags(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            return Regex.Replace(html, "<img[^>]*>", string.Empty, RegexOptions.IgnoreCase);
        }

        private static float ExtractFirstImageHeight(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return 0f;
            }

            // Try direct height attribute first: <img height='45' ...>
            var attrMatch = Regex.Match(html, "<img[^>]*?height\\s*=\\s*['\\\"](?<h>[0-9]+(?:\\.[0-9]+)?)['\\\"][^>]*>", RegexOptions.IgnoreCase);
            if (attrMatch.Success)
            {
                float parsed;
                if (float.TryParse(attrMatch.Groups["h"].Value, out parsed))
                {
                    return parsed;
                }
            }

            // Then style declaration: style='...height:45px;...'
            var styleMatch = Regex.Match(html, "<img[^>]*?style\\s*=\\s*['\\\"][^'\\\"]*?height\\s*:\\s*(?<h>[0-9]+(?:\\.[0-9]+)?)(?:px)?[^'\\\"]*['\\\"][^>]*>", RegexOptions.IgnoreCase);
            if (styleMatch.Success)
            {
                float parsed;
                if (float.TryParse(styleMatch.Groups["h"].Value, out parsed))
                {
                    return parsed;
                }
            }

            return 0f;
        }

        private static string ReplaceBase64ImageSources(string html, ICollection<string> tempFiles)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            return Regex.Replace(
                html,
                "src\\s*=\\s*(['\\\"])(?<src>data:image/(?<subtype>[a-zA-Z0-9+.-]+);base64,(?<data>[^'\\\"]+))\\1",
                match =>
                {
                    var data = match.Groups["data"].Value;
                    if (string.IsNullOrWhiteSpace(data))
                    {
                        return match.Value;
                    }

                    try
                    {
                        var sanitizedBase64 = data.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
                        var bytes = Convert.FromBase64String(sanitizedBase64);
                        var subtype = match.Groups["subtype"].Value;
                        var extension = DetectImageExtension(bytes) ?? GetExtensionFromImageSubtype(subtype);
                        var directory = Path.Combine(Path.GetTempPath(), "DocGenApi", "inline-images");
                        Directory.CreateDirectory(directory);
                        var filePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + "." + extension);
                        File.WriteAllBytes(filePath, bytes);
                        tempFiles.Add(filePath);
                        var fileUri = new Uri(filePath).AbsoluteUri;
                        var quote = match.Groups[1].Value;
                        return "src=" + quote + fileUri + quote;
                    }
                    catch
                    {
                        return match.Value;
                    }
                },
                RegexOptions.IgnoreCase);
        }

        private static string GetExtensionFromImageSubtype(string subtype)
        {
            if (string.IsNullOrWhiteSpace(subtype))
            {
                return "png";
            }

            switch (subtype.Trim().ToLowerInvariant())
            {
                case "jpeg":
                case "jpg":
                case "pjpeg":
                    return "jpg";
                case "gif":
                    return "gif";
                case "bmp":
                    return "bmp";
                case "webp":
                    return "webp";
                case "png":
                default:
                    return "png";
            }
        }

        private static string DetectImageExtension(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4)
            {
                return null;
            }

            // PNG: 89 50 4E 47
            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                return "png";
            }

            // JPEG: FF D8 FF
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return "jpg";
            }

            // GIF: 47 49 46 38
            if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            {
                return "gif";
            }

            // BMP: 42 4D
            if (bytes[0] == 0x42 && bytes[1] == 0x4D)
            {
                return "bmp";
            }

            // WEBP: RIFF....WEBP
            if (bytes.Length >= 12 &&
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            {
                return "webp";
            }

            return null;
        }

        private static void CleanupTempFiles(IEnumerable<string> files)
        {
            if (files == null)
            {
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Intentionally ignore cleanup failures.
                }
            }
        }

        private static byte[] SecurePdfIfEnabled(byte[] originalPdfBytes)
        {
            if (originalPdfBytes == null || originalPdfBytes.Length == 0)
            {
                return originalPdfBytes;
            }

            var enabled = ReadBoolSetting("PdfSecurityEnabled", true);
            if (!enabled)
            {
                return originalPdfBytes;
            }

            var ownerPassword = ConfigurationManager.AppSettings["PdfOwnerPassword"];
            if (string.IsNullOrWhiteSpace(ownerPassword))
            {
                Trace.TraceWarning("PDF security is enabled but PdfOwnerPassword is missing; using default owner password.");
                ownerPassword = "DocGenOwner";
            }

            try
            {
                using (var reader = new PdfReader(originalPdfBytes))
                using (var output = new MemoryStream())
                {
                    // Apply owner-password security after generation, similar to legacy flow.
                    PdfEncryptor.Encrypt(
                        reader,
                        output,
                        true,
                        null,
                        ownerPassword,
                        PdfWriter.ALLOW_PRINTING);

                    return output.ToArray();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to secure generated PDF. Error={0}", ex);
                throw;
            }
        }

        private static bool ReadBoolSetting(string key, bool defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            bool parsed;
            return bool.TryParse(raw, out parsed) ? parsed : defaultValue;
        }

        private sealed class WatermarkPageEvent : PdfPageEventHelper
        {
            private readonly PdfWatermarkOptionsDto _options;
            private readonly string _baseUrl;
            private readonly string _headerImageUrl;
            private readonly float _headerHeight;
            private readonly string _footerImageUrl;
            private readonly float _footerHeight;

            public WatermarkPageEvent(PdfWatermarkOptionsDto options, string baseUrl, string headerImageUrl, float headerHeight, string footerImageUrl, float footerHeight)
            {
                _options = options;
                _baseUrl = baseUrl;
                _headerImageUrl = headerImageUrl;
                _headerHeight = headerHeight;
                _footerImageUrl = footerImageUrl;
                _footerHeight = footerHeight;
            }

            public override void OnEndPage(PdfWriter writer, Document document)
            {
                base.OnEndPage(writer, document);

                DrawHeader(writer, document, _headerImageUrl, _headerHeight);
                DrawFooter(writer, document, _footerImageUrl, _footerHeight);

                if (_options == null)
                {
                    return;
                }

                DrawTopRight(writer, document, ResolveResourceUrl(_options.HeaderWatermarkImageUrl, _baseUrl), _options.HeaderWatermarkMarginLeft, _options.HeaderWatermarkMarginTop, 1f);

                DrawBottomLeft(writer, document, ResolveResourceUrl(_options.FooterWatermarkImageUrl, _baseUrl), 0f, 0f, 1f);

                if (_options.WithStamp)
                {
                    DrawBottomCenter(writer, document, ResolveResourceUrl(_options.StampImageUrl, _baseUrl), _options.StampBottomOffset <= 0 ? 30f : _options.StampBottomOffset, 72f / 300f);
                }
            }

            private static void DrawTopRight(PdfWriter writer, Document document, string imageUrl, float marginLeft, float marginTop, float scalePercent)
            {
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    return;
                }

                var image = TryGetImage(imageUrl);
                if (image == null)
                {
                    return;
                }

                image.ScalePercent(scalePercent * 100f);
                image.SetAbsolutePosition(
                    document.PageSize.Width - image.ScaledWidth - marginLeft,
                    document.PageSize.Height - image.ScaledHeight - marginTop);
                writer.DirectContentUnder.AddImage(image);
            }

            private static void DrawBottomCenter(PdfWriter writer, Document document, string imageUrl, float bottomOffset, float scalePercent)
            {
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    return;
                }

                var image = TryGetImage(imageUrl);
                if (image == null)
                {
                    return;
                }

                image.ScalePercent(scalePercent * 100f);
                image.SetAbsolutePosition(
                    (document.PageSize.Width - image.ScaledWidth) / 2f,
                    document.PageSize.GetBottom(bottomOffset));
                writer.DirectContentUnder.AddImage(image);
            }

            private static void DrawHeader(PdfWriter writer, Document document, string imageUrl, float preferredHeight)
            {
                var image = TryGetImage(imageUrl);
                if (image == null)
                {
                    return;
                }

                var maxWidth = Math.Max(1f, document.PageSize.Width - document.LeftMargin - document.RightMargin);
                var maxHeight = preferredHeight > 0 ? preferredHeight : Math.Max(1f, document.TopMargin);
                image.ScaleToFit(maxWidth, maxHeight);
                image.SetAbsolutePosition(document.LeftMargin, document.PageSize.Height - image.ScaledHeight);
                writer.DirectContentUnder.AddImage(image);
            }

            private static void DrawFooter(PdfWriter writer, Document document, string imageUrl, float preferredHeight)
            {
                var image = TryGetImage(imageUrl);
                if (image == null)
                {
                    return;
                }

                var maxWidth = Math.Max(1f, document.PageSize.Width - document.LeftMargin - document.RightMargin);
                var maxHeight = preferredHeight > 0 ? preferredHeight : Math.Max(45f, document.BottomMargin);
                image.ScaleToFit(maxWidth, maxHeight);
                image.SetAbsolutePosition(document.LeftMargin, 0f);
                writer.DirectContentUnder.AddImage(image);
            }

            private static void DrawBottomLeft(PdfWriter writer, Document document, string imageUrl, float marginLeft, float marginBottom, float scalePercent)
            {
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    return;
                }

                var image = TryGetImage(imageUrl);
                if (image == null)
                {
                    return;
                }

                image.ScalePercent(scalePercent * 100f);
                image.SetAbsolutePosition(
                    document.PageSize.GetLeft(marginLeft),
                    document.PageSize.GetBottom(marginBottom));
                writer.DirectContentUnder.AddImage(image);
            }

            private static Image TryGetImage(string imageUrl)
            {
                try
                {
                    return Image.GetInstance(imageUrl);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("Failed to load image. Url={0}. Error={1}", imageUrl ?? "<null>", ex.Message);
                    return null;
                }
            }
        }
    }
}
