using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Goggles.TextExtraction;

public class PdfExtractor(ILogger<PdfExtractor> logger) : ITextExtractor
{
    public bool IsValidForContentType(string contentType)
        => string.Equals(contentType, "application/pdf", System.StringComparison.OrdinalIgnoreCase);

    public bool UsesOCR => false;
    public bool UsesAudioTranscription => false;

    public async Task<ExtractionResult> ExtractTextAsync(Stream stream, string filename, string contentType) =>
        // Spin up in a separate Task to run in the background
        await Task.Run(() =>
        {
            try
            {
                var builder = new StringBuilder();
                using (var pdf = PdfDocument.Open(stream))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        var text = ContentOrderTextExtractor.GetText(page);
                        builder.Append(text);
                    }
                }
                return new ExtractionResult(builder.ToString(), contentType, null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to extract text from PDF");
                return new(null, contentType, null);
            }
        });
}
