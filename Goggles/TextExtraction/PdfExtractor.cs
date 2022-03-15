using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Goggles.TextExtraction
{
    public class PdfExtractor : ITextExtractor
    {
        private ILogger<PdfExtractor> _logger;

        public PdfExtractor(ILogger<PdfExtractor> logger)
        {
            _logger = logger;
        }

        public bool IsValidForMimeType(string mimeType)
            => string.Equals(mimeType, "application/pdf", System.StringComparison.OrdinalIgnoreCase);

        public bool UsesOCR => false;

        public async Task<string> ExtractTextAsync(Stream stream)
        {
            return await Task.Run(() =>
            {
                try
                {
                    StringBuilder builder = new StringBuilder();
                    using (var pdf = PdfDocument.Open(stream))
                    {
                        // if (pdf.IsEncrypted)
                        // {
                        //     _logger.LogError("PDF extraction faild: PDF encrypted");
                        //     return null;
                        // }
                        foreach (var page in pdf.GetPages())
                        {
                            var text = ContentOrderTextExtractor.GetText(page);
                            builder.Append(text);
                        }
                    }
                    return builder.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract text from PDF");
                    return null;
                }
            });
        }
    }
}
