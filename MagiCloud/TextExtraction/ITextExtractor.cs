using System.IO;
using System.Threading.Tasks;

namespace MagiCloud.TextExtraction
{
    public interface ITextExtractor
    {
        Task<string> ExtractTextAsync(Stream stream);
        bool IsValidForMimeType(string mimeType);
        bool UsesOCR => false;
    }
}
