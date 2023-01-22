using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Goggles.Transcription;
internal class WhisperTranscriptionService : ITranscriptionService
{
    public HttpClient Client { get; }
    public ILogger<WhisperTranscriptionService> Logger { get; }

    public WhisperTranscriptionService(HttpClient client, ILogger<WhisperTranscriptionService> logger)
    {
        Client = client;
        Logger = logger;
    }

    public async Task<string> TranscribeStreamAsync(Stream stream, string filename, string contentType)
    {
        // call the API defined by this docker container https://github.com/ahmetoner/whisper-asr-webservice
        // for now assume running locally, but could run on any machine
        var url = "asr?task=transcribe&language=en&output=vtt";
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        var content = new MultipartFormDataContent
        {
            { streamContent, "audio_file", filename }
        };
        var response = await Client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        var transcribed = await response.Content.ReadAsStringAsync();
        Logger.LogInformation("Whisper transcription completed with {Length} characters.", transcribed.Length);
        return transcribed;
    }
}
