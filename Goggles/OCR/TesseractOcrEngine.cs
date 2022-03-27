using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Tesseract;

namespace Goggles.OCR
{
    public class TesseractOcrEngine : IOcrEngine, IDisposable
    {
        private readonly ILogger<TesseractOcrEngine> _logger;
        private readonly TesseractEngine _engine;

        private const string ALPHANUMERIC_WHITELIST = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789";
        private const string ALPHANUMERIC_WITH_PUNCTUATION = ALPHANUMERIC_WHITELIST + ".'?!-";

        public TesseractOcrEngine(ILogger<TesseractOcrEngine> logger)
        {
            _logger = logger;
            _logger.LogInformation("Current directory is "+System.Environment.CurrentDirectory);
            _engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);

            _engine.SetVariable("tessedit_char_whitelist", ALPHANUMERIC_WITH_PUNCTUATION);
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing TesseractOcrEngine");
            _engine?.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<string> ExtractText(Stream stream)
        {
            var imageBytes = new byte[stream.Length];
            await stream.ReadAsync(imageBytes, 0, imageBytes.Length);
            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = _engine.Process(img))
            {
                var text = page.GetText();
                _logger.LogDebug("Tesseract extracted {Count} characters.", text.Length);
                return text;
            }
        }
    }
}
