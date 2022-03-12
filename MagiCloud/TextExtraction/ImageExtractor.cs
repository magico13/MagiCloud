using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MagiCloud.TextExtraction
{
    public class ImageExtractor : ITextExtractor
    {
        private ILogger<ImageExtractor> _logger;

        public ImageExtractor(ILogger<ImageExtractor> logger)
        {
            _logger = logger;
        }

        public bool IsValidForMimeType(string mimeType)
            => mimeType?.StartsWith("image/") == true;

        public async Task<string> ExtractTextAsync(Stream stream)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return string.Empty;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract text from image");
                    return null;
                }
            });
        }
    }
}
