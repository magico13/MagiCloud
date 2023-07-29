using MagiCloud.Configuration;
using MagiCommon.Extensions;
using MagiCommon.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class ElasticFileRepo : BaseElasticRepo, IElasticFileRepo
{

    public ElasticFileRepo(
        IOptions<ElasticSettings> options,
        ILogger<ElasticFileRepo> logger)
        : base(options, logger)
    { }

    public async Task<List<SearchResult>> GetDocumentsAsync(string userId, bool? deleted = null)
    {
        Setup();
        var result = await Client.SearchAsync<ElasticFileInfo>(s => s
            .Size(10000) //10k items currently supported, TODO paginate
            .Source(filter => filter.Excludes(e => e.Field(f => f.Text)))
            .Query(q =>
            {
                var qc = q
                    .Term(t => t
                        .Field(f => f.UserId.Suffix("keyword"))
                        .Value(userId)
                    );
                if (deleted != null)
                {
                    // if deleted is passed, include it in the query
                    var subq = q
                        .Term(t => t
                            .Field(f => f.IsDeleted)
                            .Value(deleted)
                        );

                    // if deleted is false then also include files where not yet set
                    if (deleted == false)
                    {
                        subq |= !q.
                            Exists(t => t
                                .Field(f => f.IsDeleted)
                            );
                    }
                    qc &= subq;
                }
                return qc;
            }));
        if (result.IsValid)
        {
            var list = new List<SearchResult>();
            foreach (var hit in result.Hits)
            {
                var source = new SearchResult(hit.Source)
                {
                    Id = hit.Id,
                    Text = null,
                    Highlights = null
                };
                list.Add(source);
            }
            return list;
        }
        _logger.LogError("Invalid GetDocuments call. {ServerError}", result.ServerError);
        return new List<SearchResult>();
    }

    public async Task<(FileAccessResult, ElasticFileInfo)> GetDocumentAsync(string userId, string id, bool includeText)
    {
        Setup();
        var getRequest = new GetRequest<ElasticFileInfo>(id);
        if (!includeText)
        {
            getRequest.SourceExcludes = new Field("text");
        }
        var result = await Client.GetAsync<ElasticFileInfo>(getRequest);
        if (result.ApiCall.HttpStatusCode == (int)HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Document with id {Id} not found.", id);
            return (FileAccessResult.NotFound, null);
        }
        ThrowIfInvalid(result);
        var source = result.Source;
        source.Id = result.Id;
        var accessLevel = DetermineAccessForUser(userId, source);
        return accessLevel switch
        {
            FileAccessResult.FullAccess or FileAccessResult.ReadOnly => (accessLevel, source),
            _ => (FileAccessResult.NotPermitted, null),
        };
    }

    public async Task<ElasticFileInfo> GetDocumentByIdAsync(string id, bool includeText)
    {
        Setup();
        var getRequest = new GetRequest<ElasticFileInfo>(id);
        if (!includeText)
        {
            getRequest.SourceExcludes = new Field("text");
        }
        var result = await Client.GetAsync<ElasticFileInfo>(getRequest);
        if (result.ApiCall.HttpStatusCode == (int)HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Document with id {Id} not found.", id);
            return null;
        }
        ThrowIfInvalid(result);
        var source = result.Source;
        source.Id = result.Id;
        return source;
    }

    public async Task<List<SearchResult>> SearchAsync(string userId, string query)
    {
        Setup();
        var result = await Client.SearchAsync<ElasticFileInfo>(s => s
            .Size(10000) //10k items currently supported, TODO paginate
            .Source(filter => filter.Excludes(e => e.Field(f => f.Text)))
            .Highlight(h => h
                .Fields(f => f.Field(i => i.Text))
                .PreTags("<mark>")
                .PostTags("</mark>")
            )
            .Query(q =>
            {
                var qc = q.QueryString(qs => qs
                    .Query(query)
                    .Fields(f => f
                        .Field(i => i.Name)
                        .Field(i => i.Text)
                    ));
                qc &= q
                    .Term(t => t
                        .Field(f => f.UserId.Suffix("keyword"))
                        .Value(userId)
                    );
                return qc;
            }));
        if (result.IsValid)
        {
            var list = new List<SearchResult>();
            foreach (var hit in result.Hits)
            {
                var info = new SearchResult(hit.Source)
                {
                    Id = hit.Id,
                    Text = null
                };
                if (hit.Highlight.TryGetValue(nameof(SearchResult.Text).ToLower(), out var value)
                    && value?.Any() == true)
                {
                    info.Highlights = value.ToArray();
                }
                list.Add(info);
            }
            return list;
        }
        _logger.LogError("Invalid Search call. {ServerError}", result.ServerError);
        return new List<SearchResult>();
    }

    public async Task<(FileAccessResult, ElasticFileInfo)> FindDocumentByNameAsync(string userId, string filename, string extension, string parentId)
    {
        Setup();
        var result = await Client.SearchAsync<ElasticFileInfo>(s => s
            .Size(1)
            .Source(filter => filter.Excludes(e => e.Field(f => f.Text)))
            .Query(q =>
            {
                var qc = q
                    .Term(t => t
                        .Field(f => f.UserId.Suffix("keyword"))
                        .Value(userId)
                    ) && q.Term(t => t
                        .Field(f => f.ParentId)
                        .Value(parentId)
                    ) && q.Match(m => m
                        .Field(f => f.Name)
                        .Query(filename)
                    ) && q.Match(m => m
                        .Field(f => f.Extension)
                        .Query(extension)
                    );
                // Note: Not using term for the filename checks bc term is case sensitive
                return qc;
            }));
        if (result.ApiCall.HttpStatusCode == (int)HttpStatusCode.NotFound
            || result.Total == 0)
        {
            _logger.LogInformation("Document with name {Name}.{Extension} not found.", filename, extension);
            return (FileAccessResult.NotFound, null);
        }
        if (result.IsValid)
        {
            foreach (var hit in result.Hits)
            {
                var source = hit.Source;
                if (string.Equals(source.Name, filename, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(source.Extension, extension, StringComparison.OrdinalIgnoreCase))
                {
                    source.Id = hit.Id;
                    var accessLevel = DetermineAccessForUser(userId, source);
                    return accessLevel switch
                    {
                        FileAccessResult.FullAccess or FileAccessResult.ReadOnly => (accessLevel, source),
                        _ => (FileAccessResult.NotPermitted, null),
                    };
                }
            }
        }
        return (FileAccessResult.NotFound, null);
    }

    public async Task<string> IndexDocumentAsync(string userId, ElasticFileInfo file)
    {
        Setup();
        if (file.LastModified == default)
        {
            file.LastModified = DateTimeOffset.Now;
        }
        file.LastUpdated = DateTimeOffset.Now;
        file.Hash = null;
        file.UserId = userId;

        var shouldCheckName = true;

        // if an id is provided, check if that file actually exists, if not throw that out
        if (!string.IsNullOrWhiteSpace(file.Id))
        {
            var (getResult, existing) = await GetDocumentAsync(userId, file.Id, true);
            if (getResult == FileAccessResult.FullAccess) //if existing file (that we can access) overwrite it
            {
                CopyExistingAttributes(file, existing);
                // We need to recheck the name if it changed or the parent changed
                shouldCheckName = string.Equals(file.Name, existing.Name, StringComparison.CurrentCultureIgnoreCase) is false
                    || string.Equals(file.ParentId, existing.ParentId, StringComparison.OrdinalIgnoreCase) is false;
            }
            else
            {
                // if we can't access the file, throw out the id and treat it as a new file
                file.Id = null;
            }
        }

        if (shouldCheckName)
        {
            // We only need to check for name conflicts if the name or parent has changed or the file is new
            var (findResult, existingByName) = await FindDocumentByNameAsync(userId, file.Name, file.Extension, file.ParentId);
            if (findResult != FileAccessResult.NotFound)
            {
                // A file with that name already exists in the folder. If we provided no id then we can assume we are overwriting it
                if (findResult == FileAccessResult.FullAccess &&
                    (string.IsNullOrEmpty(file.Id) || file.Id == existingByName.Id))
                {
                    file.Id = existingByName.Id;
                    CopyExistingAttributes(file, existingByName);
                    _logger.LogWarning(
                        "Overwriting existing file with id {Id} and name '{Name}'",
                        file.Id,
                        file.GetFileName());
                }
                else
                {
                    // If we can't overwrite we must throw an exception
                    _logger.LogError(
                        "Unresolvable name conflict between new id {Id} and existing id {Id2} with name '{Name}'",
                        file.Id,
                        existingByName.Id,
                        file.GetFileName());

                    throw new ArgumentException($"File with name '{file.GetFileName()}' cannot be indexed due to unresolvable id conflict.");
                }
            }
        }

        var result = await Client.IndexDocumentAsync(file);
        ThrowIfInvalid(result);
        return result.Id;
    }

    public async Task<FileAccessResult> DeleteFileAsync(string userId, string id)
    {
        Setup();
        var (getResult, doc) = await GetDocumentAsync(userId, id, false);
        if (getResult != FileAccessResult.FullAccess || doc is null)
        {
            return getResult;
        }
        var result = await Client.DeleteAsync<ElasticFileInfo>(id);
        if (result.IsValid)
        {
            return FileAccessResult.FullAccess;
        }
        else if (result.ApiCall.HttpStatusCode == (int)HttpStatusCode.NotFound)
        {
            return FileAccessResult.NotFound;
        }
        ThrowIfInvalid(result);
        return FileAccessResult.Unknown;
    }

    public async Task UpdateFileAttributesAsync(ElasticFileInfo file)
    {
        var existing = await GetDocumentByIdAsync(file.Id, true)
            ?? throw new FileNotFoundException("Can not find file with id " + file.Id, file.Id);
        existing.Hash = file.Hash;
        existing.Size = file.Size;
        existing.MimeType = file.MimeType;
        if (!string.IsNullOrEmpty(file.Text))
        {
            existing.Text = file.Text;
        }
        var result = await Client.IndexDocumentAsync(existing);
        ThrowIfInvalid(result);
    }

    private static void CopyExistingAttributes(ElasticFileInfo newFile, ElasticFileInfo existingFile)
    {
        newFile.Hash = existingFile?.Hash;
        newFile.Size = existingFile?.Size ?? 0;
        newFile.MimeType = existingFile?.MimeType;
        newFile.Text ??= existingFile?.Text;
        newFile.ParentId = existingFile?.ParentId;
    }
}
