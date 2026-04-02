using iTextSharp.text;

namespace PCFC.DocGen.Api.Helpers
{
    public static class PageSizeResolver
    {
        public static Rectangle Resolve(string pageSize, float? pageWidth, float? pageHeight, bool isLandscape)
        {
            Rectangle rectangle;
            if (pageWidth.HasValue && pageHeight.HasValue)
            {
                rectangle = new Rectangle(pageWidth.Value, pageHeight.Value);
            }
            else
            {
                switch ((pageSize ?? string.Empty).ToUpperInvariant())
                {
                    case "A3":
                        rectangle = PageSize.A3;
                        break;
                    case "LETTER":
                        rectangle = PageSize.LETTER;
                        break;
                    case "LEGAL":
                        rectangle = PageSize.LEGAL;
                        break;
                    case "A4":
                    default:
                        rectangle = PageSize.A4;
                        break;
                }
            }

            return isLandscape ? rectangle.Rotate() : rectangle;
        }
    }
}
