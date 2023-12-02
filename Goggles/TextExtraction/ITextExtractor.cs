using System.IO;
using System.Threading.Tasks;

namespace Goggles.TextExtraction;

public interface ITextExtractor
{
    Task<ExtractionResult> ExtractTextAsync(Stream stream, string filename, string contentType);
    bool IsValidForContentType(string contentType);
    bool UsesOCR { get; }
    bool UsesAudioTranscription { get; }
}
