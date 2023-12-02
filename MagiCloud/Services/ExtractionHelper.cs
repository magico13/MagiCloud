using MagiCloud.Configuration;
using MagiCloud.DataManager;
using MagiCommon.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class ExtractionHelper
{
    private readonly HttpClient _generalClient;
    private readonly HttpClient _statusClient;
    private readonly ILogger<ExtractionHelper> _logger;
    private readonly IOptions<ExtractionSettings> _settings;
    private readonly IElasticFileRepo _elasticManager;
    private readonly IDataManager _dataManager;

    public ExtractionHelper(
        IHttpClientFactory httpClientFactory,
        ILogger<ExtractionHelper> logger,
        IOptions<ExtractionSettings> settings,
        IElasticFileRepo elasticManager,
        IDataManager dataManager)
    {
        _statusClient = httpClientFactory.CreateClient("statusClient");
        _statusClient.Timeout = TimeSpan.FromSeconds(5);
        _generalClient = httpClientFactory.CreateClient("generalClient");
        _generalClient.Timeout = TimeSpan.FromMinutes(30);
        _logger = logger;
        _settings = settings;
        _elasticManager = elasticManager;
        _dataManager = dataManager;
    }

    public async Task<string> ExtractTextAsync(Stream stream, string filename, string contentType)
    {
        // Call the Goggles API to extract text from the file
        var content = new MultipartFormDataContent
        {
            { new StreamContent(stream), "file", filename }
        };

        // loop through the extractors and see if any of them can handle this content type
        foreach (var uri in _settings.Value.GogglesAPIEndpoints)
        {
            var baseUri = new Uri(uri);
            var statusUri = new Uri(baseUri, $"api/status/support?contentType={contentType}");
            HttpResponseMessage statusResponse = null;
            try
            {
                statusResponse = await _statusClient.GetAsync(statusUri);
            }
            catch (Exception ex)
            {
                // If the status check fails, then we can't use this extractor
                _logger.LogWarning(ex, "Extractor at {Uri} failed to respond to status check.", statusUri);
            }
            try
            {
                if (statusResponse?.IsSuccessStatusCode is true)
                {
                    // check if the extractor supports this content type
                    var status = await statusResponse.Content.ReadFromJsonAsync<Dictionary<string, bool>>();

                    if (status.TryGetValue("supported", out var supported) && supported as bool? is true)
                    {
                        // if the extractor supports this content type, then call the API to extract the text
                        var extractUri = new Uri(baseUri, "api/extract/text");
                        var response = await _generalClient.PostAsync(extractUri, content);
                        if (response.IsSuccessStatusCode)
                        {
                            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

                            //We'll merge the text and description together into one text blob
                            var finalText = string.Empty;
                            if (result.TryGetValue("text", out var text) && !string.IsNullOrWhiteSpace(text))
                            {
                                finalText = text;
                            }
                            if (result.TryGetValue("description", out var description) && !string.IsNullOrWhiteSpace(description))
                            {
                                
                                finalText += $"\n{description}";
                            }

                            // If we got text, return it. Otherwise we try the next server
                            if (!string.IsNullOrWhiteSpace(finalText))
                            {
                                return finalText.Trim();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Extractor at {Uri} completed failed to extract file {Filename} with content type {ContentType}", uri, filename, contentType);
            }
        }
        return null;
    }

    public async Task<(bool updated, string text)> ExtractTextAsync(string userId, string docId, bool force = false)
    {
        var (permission, doc) = await _elasticManager.GetDocumentAsync(userId, docId, !force);
        // get document. If we are forcing an update then we don't care about the current text
        // if not forcing, then we might return the existing text instead
        if (permission == FileAccessResult.FullAccess)
        {
            if (!string.IsNullOrWhiteSpace(doc.Text))
            {
                return (false, doc.Text);
            }
            using var fileStream = _dataManager.GetFile(doc.Id);
            return (true, await ExtractTextAsync(fileStream, doc.GetFileName(), doc.MimeType));
        }
        else
        {
            return (false, null);
        }
    }

    internal async Task<(bool updated, string text)> ExtractTextAsync(string docId, bool force = false)
    {
        var doc = await _elasticManager.GetDocumentByIdAsync(docId, !force);
        if (doc is null)
        {
            return (false, null);
        }
        // get document. If we are forcing an update then we don't care about the current text
        // if not forcing, then we might return the existing text instead
        if (!string.IsNullOrWhiteSpace(doc.Text))
        {
            return (false, doc.Text);
        }
        using var fileStream = _dataManager.GetFile(doc.Id);
        return (true, await ExtractTextAsync(fileStream, doc.GetFileName(), doc.MimeType));
    }
}
