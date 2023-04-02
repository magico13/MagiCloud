﻿using Goggles;
using MagiCloud.DataManager;
using MagiCommon.Extensions;
using System.IO;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class ExtractionHelper
{
    private readonly IElasticManager _elasticManager;
    private readonly IDataManager _dataManager;
    private readonly ILens _lens;

    public ExtractionHelper(
        ILens lens,
        IElasticManager elasticManager,
        IDataManager dataManager)
    {
        _lens = lens;
        _elasticManager = elasticManager;
        _dataManager = dataManager;
    }

    public Task<string> ExtractTextAsync(Stream stream, string filename, string contentType)
        => _lens.ExtractTextAsync(stream, filename, contentType);

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
