using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Linq;
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

        var endpoint = $"{_azureConfig.VisionEndpoint}/computervision/imageanalysis:analyze?api-version=2024-02-01&model-version=latest&features=read,caption";

        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _azureConfig.SubscriptionKey);

        var httpContent = new StreamContent(stream);
        httpContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        var response = await _httpClient.PostAsync(endpoint, httpContent);
        response.EnsureSuccessStatusCode();
        var ocrResponse = await response.Content.ReadFromJsonAsync<AzureOcrResponse>();
        return new OcrResult(ocrResponse?.ReadResult?.Text, ocrResponse?.CaptionResult?.Text);
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
        public DetectedTextBlock[] Blocks { get; set; } = [];

        public string Text => string.Join("\n", Blocks.SelectMany(b => b.Lines.Select(l => l.Text)));
    }

    public class DetectedTextBlock
    {
        public DetectedTextLine[] Lines { get; set; } = [];
    }

    public class DetectedTextLine
    {
        public string Text { get; set; } = string.Empty;
        public ImagePoint[] BoundingPolygon { get; set; } = [];
        public DetectedTextWord[] Words { get; set; } = [];
    }

    public class DetectedTextWord
    {
        public string Text { get; set; } = string.Empty;
        public ImagePoint[] BoundingPolygon { get; set; } = [];
        public double Confidence { get; set; }
    }

    public class ImagePoint
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
