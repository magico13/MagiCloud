using System.IO;
using System.Threading.Tasks;

namespace Goggles.TextExtraction
{
    public interface ITextExtractor
    {
        Task<string> ExtractTextAsync(Stream stream);
        bool IsValidForContentType(string contentType);
        bool UsesOCR { get; }
    }
}
