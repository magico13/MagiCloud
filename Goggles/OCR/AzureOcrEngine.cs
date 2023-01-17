using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
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

    public async Task<string> ExtractText(Stream stream, string contentType)
    {
        if (string.IsNullOrWhiteSpace(_azureConfig?.VisionEndpoint) || string.IsNullOrWhiteSpace(_azureConfig?.SubscriptionKey))
        {
            _logger.LogWarning("Azure OCR is not configured.");
            return null;
        }

        var endpoint = $"{_azureConfig.VisionEndpoint}/vision/v3.2/ocr?language=unk&detectOrientation=true&model-version=latest";

        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _azureConfig.SubscriptionKey);

        var imageBytes = new byte[stream.Length];
        await stream.ReadAsync(imageBytes);
        var httpContent = new ByteArrayContent(imageBytes);
        httpContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        var response = await _httpClient.PostAsync(endpoint, httpContent);
        response.EnsureSuccessStatusCode();
        var ocrResponse = await response.Content.ReadFromJsonAsync<AzureOcrResponse>();
        // take all the words and append them together
        StringBuilder finalString = new();
        foreach (var region in ocrResponse.Regions)
        {
            foreach (var line in region.Lines)
            {
                foreach (var word in line.Words)
                {
                    finalString.Append($"{word.Text} ");
                }
                finalString.AppendLine();
            }
        }
        return finalString.ToString();
    }

    public class AzureOcrResponse
    {
        public string Language { get; set; }
        public float TextAngle { get; set; }
        public string Orientation { get; set; }
        public Region[] Regions { get; set; }
        public string ModelVersion { get; set; }
    }

    public class Region
    {
        public string BoundingBox { get; set; }
        public Line[] Lines { get; set; }
    }

    public class Line
    {
        public string BoundingBox { get; set; }
        public Word[] Words { get; set; }
    }

    public class Word
    {
        public string BoundingBox { get; set; }
        public string Text { get; set; }
    }

}
