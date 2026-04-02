using PCFC.DocGen.Api.Contracts;
using PCFC.DocGen.Api.Models;

namespace PCFC.DocGen.Api.Services
{
    public interface IPdfGenerationService
    {
        PdfGenerationResult Generate(HtmlToPdfRequestDto request);
    }
}
