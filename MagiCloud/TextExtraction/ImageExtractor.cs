using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using IronOcr;

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
                    var Ocr = new IronTesseract();
                    using (var input = new OcrInput())
                    {
                        input.AddImage(stream);
                        var Result = Ocr.Read(input);
                        return Result.Text;
                    }
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
