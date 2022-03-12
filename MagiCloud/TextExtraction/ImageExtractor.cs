using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tesseract;

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
            try
            {
                // TODO: Move engine to Singleton and DI it
                using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                {
                    byte[] imageBytes = new byte[stream.Length];
                    await stream.ReadAsync(imageBytes, 0, imageBytes.Length);
                    using (var img = Pix.LoadFromMemory(imageBytes))
                    {
                        using (var page = engine.Process(img))
                        {
                            return page.GetText();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract text from image");
                return null;
            }
        }
    }
}
