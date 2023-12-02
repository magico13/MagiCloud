using System.IO;
using System.Threading.Tasks;

namespace Goggles.TextExtraction;

public class PlainTextExtractor : ITextExtractor
{
    public bool IsValidForContentType(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        { 
            return false; 
        }
        return contentType.StartsWith("text/") 
            || contentType.StartsWith("message/")
            || contentType == "application/json";
    }

    public bool UsesOCR => false;
    public bool UsesAudioTranscription => false;

    public async Task<ExtractionResult> ExtractTextAsync(Stream stream, string filename, string contentType)
    {
        // Plain text, so we just extract the content as-is
        // Maybe filter out extra whitespace?
        var streamReader = new StreamReader(stream);
        var text = await streamReader.ReadToEndAsync();
        return new(text, contentType, null);
    }
}
