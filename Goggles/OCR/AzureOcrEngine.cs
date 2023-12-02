using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Goggles.OCR;

internal class AzureOcrEngine : IOcrEngine
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly AzureOCRConfiguration _azureConfig;

    public AzureOcrEngine(
        HttpClient httpClient,
        ILogger<AzureOcrEngine> logger,
        IOptions<GogglesConfiguration> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _azureConfig = options.Value.AzureOCRConfiguration;
    }

    public async Task<OcrResult> ExtractText(Stream stream, string filename, string contentType)
    {
        if (string.IsNullOrWhiteSpace(_azureConfig?.VisionEndpoint) || string.IsNullOrWhiteSpace(_azureConfig?.SubscriptionKey))
        {
            _logger.LogWarning("Azure OCR is not configured.");
            return new(null, null);
        }

        var endpoint = $"{_azureConfig.VisionEndpoint}/computervision/imageanalysis:analyze?api-version=2023-04-01-preview&features=read,caption";

        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _azureConfig.SubscriptionKey);

        var httpContent = new StreamContent(stream);
        httpContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        var response = await _httpClient.PostAsync(endpoint, httpContent);
        response.EnsureSuccessStatusCode();
        var ocrResponse = await response.Content.ReadFromJsonAsync<AzureOcrResponse>();
        return new OcrResult(ocrResponse?.ReadResult?.Content, ocrResponse?.CaptionResult?.Text);
        
    }

    public class AzureOcrResponse
    {
        public string? ModelVersion { get; set; }
        public Metadata? Metadata { get; set; }
        public ReadResult? ReadResult { get; set; }
        public CaptionResult? CaptionResult { get; set; }
    }

    public class Metadata
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class CaptionResult
    {
        public string? Text { get; set; }
        public float Confidence { get; set; }
    }

    public class ReadResult
    {
        public string? StringIndexType { get; set; }
        public string? Content { get; set; }
        public Page[]? Pages { get; set; }
        public object[]? Styles { get; set; }
        public string? ModelVersion { get; set; }
    }

    public class Page
    {
        public float Height { get; set; }
        public float Width { get; set; }
        public float Angle { get; set; }
        public int PageNumber { get; set; }
        public Word[]? Words { get; set; }
        public Span[]? Spans { get; set; }
        public Line[]? Lines { get; set; }
    }
    public class Line
    {
        public string? Content { get; set; }
        public float[]? BoundingBox { get; set; }
        public Span[]? Spans { get; set; }
    }

    public class Word
    {
        public string? Content { get; set; }
        public float[]? BoundingBox { get; set; }
        public float Confidence { get; set; }
        public Span? Span { get; set; }
    }

    public class Span
    {
        public int Offset { get; set; }
        public int Length { get; set; }
    }
}
