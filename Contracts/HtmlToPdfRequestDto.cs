namespace PCFC.DocGen.Api.Contracts
{
    public class HtmlToPdfRequestDto
    {
        public string HtmlContent { get; set; }

        public string FileName { get; set; }

        public PdfOptionsDto PdfOptions { get; set; }

        public string HeaderHtml { get; set; }

        public string FooterHtml { get; set; }

        public string BaseUrl { get; set; }

        public PdfWatermarkOptionsDto WatermarkOptions { get; set; }
    }

    public class PdfOptionsDto
    {
        public string PageSize { get; set; }

        public float? PageWidth { get; set; }

        public float? PageHeight { get; set; }

        public float MarginLeft { get; set; }

        public float MarginRight { get; set; }

        public float MarginTop { get; set; }

        public float MarginBottom { get; set; }

        public bool IsLandscape { get; set; }

        public float? HeaderImageHeight { get; set; }

        public float? FooterImageHeight { get; set; }

        public float? FooterSpacing { get; set; }
    }

    public class PdfWatermarkOptionsDto
    {
        public bool WithStamp { get; set; }

        public string HeaderWatermarkImageUrl { get; set; }

        public string FooterWatermarkImageUrl { get; set; }

        public string StampImageUrl { get; set; }

        public float HeaderWatermarkMarginLeft { get; set; }

        public float HeaderWatermarkMarginTop { get; set; }

        public float StampBottomOffset { get; set; }
    }
}
