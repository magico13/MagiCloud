using System;
using System.IO;
using System.Threading.Tasks;
using Goggles.OCR;
using Microsoft.Extensions.Logging;

namespace Goggles.TextExtraction;

public class ImageExtractor(ILogger<ImageExtractor> logger, IOcrEngine ocrEngine) : ITextExtractor
{
    private readonly static char[] separator = ['\n'];

    public bool IsValidForContentType(string contentType)
        => contentType?.StartsWith("image/") == true;

    public bool UsesOCR => true;
    public bool UsesAudioTranscription => false;

    public async Task<ExtractionResult> ExtractTextAsync(Stream stream, string filename, string contentType)
    {
        try
        {
            var ocrResult = await ocrEngine.ExtractText(stream, filename, contentType);
            var rawText = ocrResult.Text;
            if (!string.IsNullOrWhiteSpace(rawText))
            {
                // Remove any lines that are completely empty
                var lines = rawText.Split(separator,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                rawText = string.Join("\n", lines);
            }
            return new(rawText, contentType, ocrResult.Description);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract text from image");
            return new(null, contentType, null);
        }
    }
}
