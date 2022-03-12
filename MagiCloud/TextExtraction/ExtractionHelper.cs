using MagiCloud.DataManager;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MagiCloud.TextExtraction
{
    public class ExtractionHelper
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<ITextExtractor> _extractors;
        private readonly IElasticManager _elasticManager;
        private readonly IDataManager _dataManager;

        public ExtractionHelper(
            ILogger<ExtractionHelper> logger,
            IEnumerable<ITextExtractor> extractors,
            IElasticManager elasticManager,
            IDataManager dataManager)
        {
            _logger = logger;
            _extractors = extractors;
            _elasticManager = elasticManager;
            _dataManager = dataManager;
        }

        public async Task<string> ExtractTextAsync(Stream stream, string contentType)
        {
            if (stream == null || stream == Stream.Null)
            {
                return null;
            }
            foreach (var extractor in _extractors)
            {
                if (extractor.IsValidForMimeType(contentType))
                {
                    _logger.LogInformation(
                        "Found suitable extractor {Class} for mimetype {MimeType}",
                        extractor.GetType(),
                        contentType);
                    try
                    {
                        string text = await extractor.ExtractTextAsync(stream);
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            _logger.LogInformation("Extraction did not fail using {Class} but returned empty string.", extractor.GetType());
                            // Basically a failure, allow trying a different extractor that will possibly work better
                        }
                        else
                        {
                            _logger.LogInformation("Text extraction complete. Length: {Count}", text.Length);
                            // If it succeeded, return the text. Otherwise maybe a later extractor in the list will work (eg PDF by text or by OCR)
                            return text;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to extract text using extractor {Class}", extractor);
                    }
                }
            }
            return null;
        }

        public async Task<(bool, string)> ExtractTextAsync(string userId, string docId, bool force = false)
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
                return (true, await ExtractTextAsync(fileStream, doc.MimeType));
            }
            else
            {
                return (false, null);
            }
        }
    }
}
