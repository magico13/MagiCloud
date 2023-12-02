using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Goggles.Transcription;
internal class WhisperTranscriptionService(HttpClient client, ILogger<WhisperTranscriptionService> logger) : ITranscriptionService
{
    public async Task<string> TranscribeStreamAsync(Stream stream, string filename, string contentType)
    {
        // call the API defined by this docker container https://github.com/ahmetoner/whisper-asr-webservice
        var url = "asr?task=transcribe&language=en&output=vtt";
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        var content = new MultipartFormDataContent
        {
            { streamContent, "audio_file", filename }
        };
        var response = await client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        var transcribed = await response.Content.ReadAsStringAsync();
        logger.LogInformation("Whisper transcription completed with {Length} characters.", transcribed.Length);
        return transcribed;
    }
}
