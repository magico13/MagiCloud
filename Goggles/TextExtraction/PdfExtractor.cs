using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Goggles.TextExtraction;

public class PdfExtractor : ITextExtractor
{
    private readonly ILogger<PdfExtractor> _logger;

    public PdfExtractor(ILogger<PdfExtractor> logger) => _logger = logger;

    public bool IsValidForContentType(string contentType)
        => string.Equals(contentType, "application/pdf", System.StringComparison.OrdinalIgnoreCase);

    public bool UsesOCR => false;

    public async Task<string> ExtractTextAsync(Stream stream, string filename, string contentType) =>
        // Spin up in a separate Task to run in the background
        await Task.Run(() =>
        {
            try
            {
                var builder = new StringBuilder();
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
