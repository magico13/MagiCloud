using Goggles.Transcription;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Goggles.TextExtraction;
internal class AudioExtractor : ITextExtractor
{
    public ITranscriptionService TranscriptionService { get; }
    public ILogger<AudioExtractor> Logger { get; }

    public bool UsesOCR => false;
    public bool UsesAudioTranscription => true;
    public bool IsValidForContentType(string contentType) 
        => contentType.StartsWith("audio") || contentType.StartsWith("video");

    public AudioExtractor(ITranscriptionService transcriptionService, ILogger<AudioExtractor> logger)
    {
        TranscriptionService = transcriptionService;
        Logger = logger;
    }

    public async Task<string> ExtractTextAsync(Stream stream, string filename, string contentType)
    {
        try
        {
            var text = await TranscriptionService.TranscribeStreamAsync(stream, filename, contentType);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to transcribe audio. Content type was {ContentType}.", contentType);
        }
        return null;
    }
}
