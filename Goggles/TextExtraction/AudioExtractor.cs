using Goggles.Transcription;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Goggles.TextExtraction;
internal class AudioExtractor(ITranscriptionService transcriptionService, ILogger<AudioExtractor> logger) : ITextExtractor
{
    public bool UsesOCR => false;
    public bool UsesAudioTranscription => true;
    public bool IsValidForContentType(string contentType) 
        => contentType.StartsWith("audio") || contentType.StartsWith("video");

    public async Task<ExtractionResult> ExtractTextAsync(Stream stream, string filename, string contentType)
    {
        try
        {
            var text = await transcriptionService.TranscribeStreamAsync(stream, filename, contentType);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return new(text, contentType, null);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to transcribe audio. Content type was {ContentType}.", contentType);
        }
        return new(null, contentType, null);
    }
}
