using System;
using System.IO;
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
                return await _ocrEngine.OcrStreamAsync(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract text from image");
                return null;
            }
        }
    }
}
