using System;
using System.IO;
using System.Threading.Tasks;
using Goggles.OCR;
using Microsoft.Extensions.Logging;

namespace Goggles.TextExtraction;

public class ImageExtractor : ITextExtractor
{
    private readonly ILogger<ImageExtractor> _logger;
    private readonly IOcrEngine _ocrEngine;

    public ImageExtractor(ILogger<ImageExtractor> logger, IOcrEngine ocrEngine)
    {
        _logger = logger;
        _ocrEngine = ocrEngine;
    }

    public bool IsValidForContentType(string contentType)
        => contentType?.StartsWith("image/") == true;

    public bool UsesOCR => true;

    public async Task<string> ExtractTextAsync(Stream stream, string filename, string contentType)
    {
        try
        {
            var rawText = await _ocrEngine.ExtractText(stream, filename, contentType);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return null;
            }
            // Remove any lines that are completely empty
            var lines = rawText.Split(new char[] { '\n' }, 
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from image");
            return null;
        }
    }
}
