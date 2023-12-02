using Goggles.TextExtraction;
using System.IO;
using System.Threading.Tasks;

namespace Goggles;

public interface ILens
{
    Task<ExtractionResult> ExtractTextAsync(Stream stream, string filename, string? contentType = null);
    string DetermineContentType(string filename);
    string DetermineExtension(string contentType);

    bool SupportsOCR { get; }
    bool SupportsAudioTranscription { get; }
}
