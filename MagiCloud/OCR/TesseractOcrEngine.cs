using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Tesseract;

namespace MagiCloud.OCR
{
    public class TesseractOcrEngine : IOcrEngine, IDisposable
    {
        private readonly ILogger<TesseractOcrEngine> _logger;
        private readonly TesseractEngine _engine;

        public TesseractOcrEngine(ILogger<TesseractOcrEngine> logger)
        {
            _logger = logger;

            _engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing TesseractOcrEngine");
            _engine?.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<string> OcrStreamAsync(Stream stream)
        {
            byte[] imageBytes = new byte[stream.Length];
            await stream.ReadAsync(imageBytes);
            using var img = Pix.LoadFromMemory(imageBytes);
            using var page = _engine.Process(img);
            var text = page.GetText();
            _logger.LogDebug("Tesseract extracted {Count} characters.", text.Length);
            return text;
        }
    }
}
