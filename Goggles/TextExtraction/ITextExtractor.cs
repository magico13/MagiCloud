using System.IO;
using System.Threading.Tasks;

namespace Goggles.TextExtraction
{
    public interface ITextExtractor
    {
        Task<string> ExtractTextAsync(Stream stream);
        bool IsValidForMimeType(string mimeType);
        bool UsesOCR { get; }
    }
}
