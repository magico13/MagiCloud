using Goggles.TextExtraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Goggles;

public class Lens : ILens
{
    private ILogger<Lens> Logger { get; }
    private IEnumerable<ITextExtractor> Extractors { get; }
    private GogglesConfiguration Config { get; }

    public bool SupportsOCR => Config.EnableOCR;
    public bool SupportsAudioTranscription => Config.EnableAudioTranscription;
    
    public Lens(
        ILogger<Lens> logger,
        IEnumerable<ITextExtractor> extractors,
        IOptions<GogglesConfiguration> configuration)
    {
        Logger = logger;
        Extractors = extractors;
        Config = configuration.Value;
    }

    public string DetermineContentType(string filename) => ContentTypeAnalyzer.DetermineContentType(filename);

    public string DetermineExtension(string contentType) => ContentTypeAnalyzer.DetermineExtension(contentType);

    public async Task<string> ExtractTextAsync(Stream stream, string filename, string contentType = null)
    {
        if (stream == null || stream == Stream.Null)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(contentType)) 
        { 
            contentType = DetermineContentType(filename);
        }

        foreach (var extractor in Extractors)
        {
            if (extractor.IsValidForContentType(contentType)
                && (Config.EnableOCR || !extractor.UsesOCR)
                && (Config.EnableAudioTranscription || !extractor.UsesAudioTranscription))
            {
                var text = await ExtractViaExtractor(extractor, stream, filename, contentType);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
                else
                {
                    Logger.LogInformation("Extraction via {Class} returned empty string.", extractor.GetType());
                    // Basically a failure, allow trying a different extractor that will possibly work better
                }
            }
        }
        // If we're here then we didn't have a good fit, try to check if it's non-binary, and if so treat as plain text
        var plainTextExtractor = Extractors.FirstOrDefault(ext => ext is PlainTextExtractor);
        if (plainTextExtractor is not null)
        {
            var plainText = await ExtractViaExtractor(plainTextExtractor, stream, filename, contentType);
            // Two null chars in a row means it's probably a binary file and we don't want to return that string
            return string.IsNullOrWhiteSpace(plainText) || plainText.Contains("\0\0") ? null : plainText;
        }
        return null;
    }

    private async Task<string> ExtractViaExtractor(ITextExtractor extractor, Stream stream, string filename, string contentType)
    {
        Logger.LogInformation(
            "Found suitable extractor {Class} for mimetype {MimeType}",
            extractor.GetType(),
            contentType);
        try
        {
            var text = await extractor.ExtractTextAsync(stream, filename, contentType);
            if (!string.IsNullOrWhiteSpace(text))
            {
                Logger.LogInformation("Text extraction complete. Length: {Count}", text.Length);
                // If it succeeded, return the text. Otherwise maybe a later extractor in the list will work (eg PDF by text or by OCR)
                var maxLength = Config.MaxTextLength;
                if (maxLength > 0 && text.Length > maxLength)
                {
                    Logger.LogInformation("Trimming text to {Count} characters.", maxLength);
                    return text[..maxLength];
                }
                return text;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to extract text using extractor {Class}", extractor);
        }
        return null;
    }
}
