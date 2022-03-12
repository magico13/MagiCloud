using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MagiCloud.OCR;
using Microsoft.Extensions.Logging;

namespace MagiCloud.TextExtraction
{
    public class ImageExtractor : ITextExtractor
    {
        private readonly ILogger<ImageExtractor> _logger;
        private readonly IOcrEngine _ocrEngine;

        public ImageExtractor(ILogger<ImageExtractor> logger, IOcrEngine ocrEngine)
        {
            _logger = logger;
            _ocrEngine = ocrEngine;
        }

        public bool IsValidForMimeType(string mimeType)
            => mimeType?.StartsWith("image/") == true;

        public bool UsesOCR => true;

        public async Task<string> ExtractTextAsync(Stream stream)
        {
            try
            {
                var rawText = await _ocrEngine.OcrStreamAsync(stream);
                if (string.IsNullOrWhiteSpace(rawText))
                {
                    return null;
                }
                // Remove any lines that are completely empty
                var lines = rawText.Split();
                StringBuilder builder = new();
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        builder.AppendLine(line);
                    }
                }
                return builder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract text from image");
                return null;
            }
        }
    }
}
