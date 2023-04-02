using Goggles;
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

public interface IElasticManager
{
    IElasticClient Client { get; set; }
    Task<bool> SetupIndicesAsync();
    Task<List<SearchResult>> GetDocumentsAsync(string userId, bool? deleted);
    Task<(FileAccessResult, ElasticFileInfo)> GetDocumentAsync(string userId, string id, bool includeText);
    Task<ElasticFileInfo> GetDocumentByIdAsync(string id, bool includeText);
    Task<List<SearchResult>> SearchAsync(string userId, string query);
    Task<(FileAccessResult, ElasticFileInfo)> FindDocumentByNameAsync(string userId, string filename, string extension);
    Task<string> IndexDocumentAsync(string userId, ElasticFileInfo file);
    Task<FileAccessResult> DeleteFileAsync(string userId, string id);
    Task UpdateFileAttributesAsync(ElasticFileInfo file);
}


public class ElasticManager : IElasticManager
{
    public const string FILES_INDEX = "magicloud_files";
    public const string USER_INDEX = "magicloud_users";
    public const string TOKEN_INDEX = "magicloud_tokens";

    public IElasticClient Client { get; set; }

    private readonly ElasticSettings _settings;
    private readonly ILogger<ElasticManager> _logger;
    private readonly ILens _lens;

    public ElasticManager(
        IOptionsSnapshot<ElasticSettings> options,
        ILogger<ElasticManager> logger,
        ILens lens)
    {
        _settings = options.Value;
        _logger = logger;
        _lens = lens;
    }

    public void Setup()
    {
        if (Client != null)
        {
            return;
        }
        var connectionSettings = new ConnectionSettings(new Uri(_settings.Url))
            .DefaultMappingFor<ElasticFileInfo>(i => i
                .IndexName(FILES_INDEX)
                .IdProperty(p => p.Id)
            )
            .EnableDebugMode()
            .PrettyJson()
            .RequestTimeout(TimeSpan.FromMinutes(2));

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            connectionSettings.ApiKeyAuthentication(_settings.ApiKeyId, _settings.ApiKey);
        }
        
        if (!string.IsNullOrWhiteSpace(_settings.Thumbprint))
        {
            connectionSettings.ServerCertificateValidationCallback((caller, cert, chain, errors) 
                => string.Equals(cert.GetCertHashString(), _settings.Thumbprint, StringComparison.OrdinalIgnoreCase));
        }

        Client = new ElasticClient(connectionSettings);
    }

    public async Task<bool> SetupIndicesAsync()
    {
        Setup();

        foreach (var indexName in new string[] { FILES_INDEX, USER_INDEX, TOKEN_INDEX })
        {
            var index = Indices.Index(indexName);
            var exists = await Client.Indices.ExistsAsync(index);
            if (!exists.Exists)
            {
                _logger.LogInformation("Index '{Name}' not found, creating.", FILES_INDEX);
                var create = await Client.Indices.CreateAsync(index);
                ThrowIfInvalid(create);
            }
        }
        return true;
    }

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
        var accessLevel = VerifyFileAccess(userId, source);
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

    public async Task<(FileAccessResult, ElasticFileInfo)> FindDocumentByNameAsync(string userId, string filename, string extension)
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
                    var accessLevel = VerifyFileAccess(userId, source);
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

        // Check to see if a file already exists with that name for this user
        var (findResult, existingByName) = await FindDocumentByNameAsync(userId, file.Name, file.Extension);
        if (findResult != FileAccessResult.NotFound)
        {
            // A file with that name already exists, if we're using the same id then we can safely overwrite
            // if we have no id then we can overwrite and use the existing id
            if ((string.IsNullOrWhiteSpace(file.Id) || existingByName.Id == file.Id)
                && findResult == FileAccessResult.FullAccess)
            {
                file.Id = existingByName.Id;
            }
            else if (findResult == FileAccessResult.FullAccess)
            {
                // CONFLICT - Ids are different and there is a conflict in names.
                // If we have permission to overwrite, then we can just do so, but should log this
                _logger.LogWarning("Forcing provided file with id {Id} to id {Id2} because of name conflict '{Name}'",
                                   file.Id,
                                   existingByName.Id,
                                   file.GetFullPath());
                file.Id = existingByName.Id;
            }
            else
            {
                // If we can't overwrite we must throw an exception
                _logger.LogError(
                    "Unresolvable name conflict between new id {Id} and existing id {Id2} with name '{Name}'",
                    file.Id,
                    existingByName.Id,
                    file.GetFullPath());

                throw new ArgumentException($"File with name '{file.GetFullPath()}' cannot be indexed due to unresolvable id conflict.");
            }
        }
        // TODO: We should be able to avoid getting the document twice, if you provide an id then the name probably matches too
        // eg if the name didn't change then we don't really need to check for duplicate names at all

        // if an id is provided, check if that file actually exists, if not throw that out
        if (!string.IsNullOrWhiteSpace(file.Id))
        {
            var (getResult, existing) = await GetDocumentAsync(userId, file.Id, true);
            if (getResult == FileAccessResult.FullAccess) //if existing file (that we can access) overwrite it
            {
                CopyExistingAttributes(file, existing);
            }
            else
            {
                file.Id = null;
            }
        }
        if (string.IsNullOrEmpty(file.MimeType))
        {
            file.MimeType = GetMimeType(file);
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
    }

    private string GetMimeType(ElasticFileInfo file)
    {
        if (string.IsNullOrWhiteSpace(file.MimeType))
        {
            return _lens.DetermineContentType(file.Extension);
        }
        return file.MimeType;
    }

    private static FileAccessResult VerifyFileAccess(string userId, ElasticFileInfo file)
    {
        if (string.Equals(file.UserId, userId, StringComparison.Ordinal))
        {
            return FileAccessResult.FullAccess;
        }
        else if (file.IsPublic)
        {
            return FileAccessResult.ReadOnly;
        }
        return FileAccessResult.NotPermitted;
    }

    private void ThrowIfInvalid(ResponseBase response)
    {
        if (!response.IsValid)
        {
            if (!string.IsNullOrWhiteSpace(response.DebugInformation))
            {
                _logger.LogWarning(response.DebugInformation);
            }

            if (response.OriginalException != null)
            {
                throw response.OriginalException;
            }
            else
            {
                throw new Exception("Exception during processing. " + response.ServerError?.ToString());
            }
        }
    }
}
