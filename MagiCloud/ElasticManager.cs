using MagiCloud.Configuration;
using MagiCommon.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MagiCloud
{
    public interface IElasticManager
    {
        IElasticClient Client { get; set; }
        Task<bool> SetupIndicesAsync();
        Task<FileList> GetDocumentsAsync();
        Task<ElasticFileInfo> GetDocumentAsync(string id);
        Task<string> IndexDocumentAsync(ElasticFileInfo file);
        Task<bool> DeleteFileAsync(ElasticFileInfo file);
        Task UpdateFileAttributesAsync(ElasticFileInfo file);

        Task CreateUserAsync(User user);
    }


    public class ElasticManager : IElasticManager
    {
        public const string FILES_INDEX = "magicloud_files";
        public const string USER_INDEX = "magicloud_users";

        public IElasticClient Client { get; set; }

        private readonly ElasticSettings _settings;
        private readonly ILogger<ElasticManager> _logger;
        
        public ElasticManager(IOptionsSnapshot<ElasticSettings> options, ILogger<ElasticManager> logger)
        {
            _settings = options.Value;
            _logger = logger;
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
                .DefaultMappingFor<User>(i => i
                    .IndexName(USER_INDEX)
                    .IdProperty(p => p.Id)
                )
                .EnableDebugMode()
                .PrettyJson()
                .RequestTimeout(TimeSpan.FromMinutes(2))
                .ApiKeyAuthentication(_settings.ApiKeyId, _settings.ApiKey);

            Client = new ElasticClient(connectionSettings);
        }

        public async Task<bool> SetupIndicesAsync()
        {
            Setup();

            foreach (var indexName in new string[] { FILES_INDEX, USER_INDEX})
            {
                var index = Indices.Index(indexName);
                var exists = await Client.Indices.ExistsAsync(index);
                if (!exists.Exists)
                {
                    _logger.LogInformation("Index '{Name}' not found, creating.", FILES_INDEX);
                    var create = await Client.Indices.CreateAsync(index);
                    if (!create.IsValid)
                    {
                        if (create.OriginalException != null)
                        {
                            throw create.OriginalException;
                        }
                        throw new Exception(create.ServerError.ToString());
                    }
                }
            }
            return true;
        }

        public async Task<FileList> GetDocumentsAsync()
        {
            Setup();
            var result = await Client.SearchAsync<ElasticFileInfo>(s =>
            {
                return s.Size(10000).MatchAll(); //10k items currently supported, TODO paginate
            });
            if (result.IsValid)
            {
                var list = new List<ElasticFileInfo>();
                foreach (var hit in result.Hits)
                {
                    var source = hit.Source;
                    source.Id = hit.Id;
                    list.Add(source);
                }
                return new FileList { Files = list };
            }
            _logger.LogError("Invalid GetDocuments call. {ServerError}", result.ServerError);
            return new FileList() { Files = new List<ElasticFileInfo>() };
        }

        public async Task<ElasticFileInfo> GetDocumentAsync(string id)
        {
            Setup();
            var result = await Client.GetAsync<ElasticFileInfo>(id);
            if (result.IsValid)
            {
                var source = result.Source;
                source.Id = result.Id;
                return source;
            }
            _logger.LogError("Invalid GetDocument call. {ServerError}", result.ServerError);
            return null;
        }

        public async Task<string> IndexDocumentAsync(ElasticFileInfo file)
        {
            Setup();
            if (file.LastModified == default)
            {
                file.LastModified = DateTimeOffset.Now;
            }
            file.LastUpdated = DateTimeOffset.Now;
            file.Hash = null;
            // if an id is provided, check if that file actually exists, if not throw that out
            if (!string.IsNullOrWhiteSpace(file.Id))
            {
                var existing = await GetDocumentAsync(file.Id);
                if (existing is null)
                {
                    file.Id = null;
                }
                else
                {
                    EnsureFileAttributes(file, existing);
                }
            }
            if (string.IsNullOrEmpty(file.MimeType))
            {
                file.MimeType = GetMimeType(file);
            }
            

            var result = await Client.IndexDocumentAsync(file);
            if (result.IsValid)
            {
                return result.Id;
            }
            _logger.LogError("Invalid IndexDocument call. {ServerError}", result.ServerError);
            return null;
        }


        public async Task<bool> DeleteFileAsync(ElasticFileInfo file)
        {
            Setup();
            var result = await Client.DeleteAsync<ElasticFileInfo>(file.Id);
            if (result.IsValid)
            {
                return true;
            }
            else if (result.ApiCall.HttpStatusCode == 404)
            {
                return false;
            }
            _logger.LogError("Invalid Delete call. {ServerError}", result.ServerError);
            return false;
        }

        public async Task UpdateFileAttributesAsync(ElasticFileInfo file)
        {
            var existing = await GetDocumentAsync(file.Id);
            if (existing is null)
            {
                throw new FileNotFoundException("Can not find file with id " + file.Id, file.Id);
            }
            existing.Hash = file.Hash;
            existing.Size = file.Size;
            existing.MimeType = file.MimeType;
            var result = await Client.IndexDocumentAsync(existing);
            if (!result.IsValid)
            {
                if (result.OriginalException != null)
                {
                    throw result.OriginalException;
                }
                _logger.LogError("Error while updating the attributes of document {DocId}.", file.Id);
            }
        }

        private void EnsureFileAttributes(ElasticFileInfo newFile, ElasticFileInfo existingFile)
        {
            newFile.Hash = existingFile?.Hash;
            newFile.Size = existingFile?.Size ?? 0;
            newFile.MimeType = existingFile?.MimeType;
        }

        private string GetMimeType(ElasticFileInfo file)
        {
            if (string.IsNullOrWhiteSpace(file.MimeType))
            {
                new FileExtensionContentTypeProvider().TryGetContentType($"{file.Name}.{file.Extension}", out string type);
                if (type is null)
                {
                    type = "application/octet-stream";
                }
                return type;
            }
            return null;
        }

        public async Task CreateUserAsync(User user)
        {
            var result = await Client.SearchAsync<User>(s =>
            {
                return s; // Check if a user with the provided username exists, if so throw
            });
        }
    }
}
