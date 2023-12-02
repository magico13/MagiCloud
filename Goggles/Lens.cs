using Goggles.TextExtraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Goggles;

public class Lens(
    ILogger<Lens> logger,
    IEnumerable<ITextExtractor> extractors,
    IOptions<GogglesConfiguration> configuration) : ILens
{
    private GogglesConfiguration Config { get; } = configuration.Value;

    public bool SupportsOCR => Config.EnableOCR;
    public bool SupportsAudioTranscription => Config.EnableAudioTranscription;

    public string DetermineContentType(string filename) => ContentTypeAnalyzer.DetermineContentType(filename);

    public string DetermineExtension(string contentType) => ContentTypeAnalyzer.DetermineExtension(contentType);

    public async Task<ExtractionResult> ExtractTextAsync(Stream stream, string filename, string? contentType = null)
    {
        if (stream == null || stream == Stream.Null)
        {
            return new ExtractionResult(null, contentType, null);
        }
        if (string.IsNullOrWhiteSpace(contentType)) 
        { 
            contentType = DetermineContentType(filename);
        }

        foreach (var extractor in extractors)
        {
            if (extractor.IsValidForContentType(contentType)
                && (Config.EnableOCR || !extractor.UsesOCR)
                && (Config.EnableAudioTranscription || !extractor.UsesAudioTranscription))
            {
                var result = await ExtractViaExtractor(extractor, stream, filename, contentType);
                if (!string.IsNullOrWhiteSpace(result.Text) || !string.IsNullOrWhiteSpace(result.Description))
                {
                    // Must have populated Text or Description to conider it a success
                    return result;
                }
                else
                {
                    logger.LogInformation("Extraction via {Class} returned empty string.", extractor.GetType());
                    // Basically a failure, allow trying a different extractor that will possibly work better
                }
            }
        }
        // If we're here then we didn't have a good fit, try to check if it's non-binary, and if so treat as plain text
        var plainTextExtractor = extractors.FirstOrDefault(ext => ext is PlainTextExtractor);
        if (plainTextExtractor is not null)
        {
            var plainTextResult = await ExtractViaExtractor(plainTextExtractor, stream, filename, contentType);
            // Two null chars in a row means it's probably a binary file and we don't want to return that string
            return string.IsNullOrWhiteSpace(plainTextResult.Text) || plainTextResult.Text.Contains("\0\0") ? new(null, contentType, null) : plainTextResult;
        }
        return new(null, contentType, null);
    }

    private async Task<ExtractionResult> ExtractViaExtractor(ITextExtractor extractor, Stream stream, string filename, string contentType)
    {
        logger.LogInformation(
            "Found suitable extractor {Class} for mimetype {MimeType}",
            extractor.GetType(),
            contentType);
        try
        {
            var result = await extractor.ExtractTextAsync(stream, filename, contentType);
            var text = result.Text;
            var description = result.Description;
            if (!string.IsNullOrWhiteSpace(text))
            {
                logger.LogInformation("Text extraction complete. Length: {Count}", text.Length);
                // If it succeeded, return the text. Otherwise maybe a later extractor in the list will work (eg PDF by text or by OCR)
                var maxLength = Config.MaxTextLength;
                if (maxLength > 0 && text.Length > maxLength)
                {
                    logger.LogInformation("Trimming text to {Count} characters.", maxLength);
                    text = text[..maxLength];
                }
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                // trim the description as well
                var maxLength = Config.MaxTextLength;
                if (maxLength > 0 && description.Length > maxLength)
                {
                    logger.LogInformation("Trimming description to {Count} characters.", maxLength);
                    description = description[..maxLength];
                }
            }
            return new(text, contentType, description);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract text using extractor {Class}", extractor);
        }
        return new(null, contentType, null);
    }
}
