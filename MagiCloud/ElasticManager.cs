using MagiCloud.Configuration;
using MagiCommon.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MagiCloud
{
    public interface IElasticManager
    {
        IElasticClient Client { get; set; }
        Task<bool> SetupIndicesAsync();
        Task<FileList> GetDocumentsAsync(string userId);
        Task<ElasticFileInfo> GetDocumentAsync(string userId, string id);
        Task<string> IndexDocumentAsync(string userId, ElasticFileInfo file);
        Task<bool> DeleteFileAsync(string userId, ElasticFileInfo file);
        Task UpdateFileAttributesAsync(string userId, ElasticFileInfo file);

        Task<User> CreateUserAsync(User user);
        Task<AuthToken> LoginUserAsync(User user);
        Task<AuthToken> VerifyTokenAsync(string token);
    }


    public class ElasticManager : IElasticManager
    {
        public const string FILES_INDEX = "magicloud_files";
        public const string USER_INDEX = "magicloud_users";
        public const string TOKEN_INDEX = "magicloud_tokens";

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
                .DefaultMappingFor<AuthToken>(i => i
                    .IndexName(TOKEN_INDEX)
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

            foreach (var indexName in new string[] { FILES_INDEX, USER_INDEX, TOKEN_INDEX})
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

        public async Task<FileList> GetDocumentsAsync(string userId)
        {
            Setup();
            var result = await Client.SearchAsync<ElasticFileInfo>(s =>
            {
                return s.Size(10000) //10k items currently supported, TODO paginate
                .Query(q => q
                    .Match(m => m
                        .Field(f => f.UserId)
                        .Query(userId)
                        )
                    );
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

        public async Task<ElasticFileInfo> GetDocumentAsync(string userId, string id)
        {
            Setup();
            var result = await Client.GetAsync<ElasticFileInfo>(id);
            if (result.ApiCall.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Document with id {Id} not found.", id);
                return null;
            }
            ThrowIfInvalid(result);
            var source = result.Source;
            source.Id = result.Id;
            if (string.Equals(result.Source.UserId, userId, StringComparison.Ordinal))
            {
                return source;
            }
            return null;
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
            // if an id is provided, check if that file actually exists, if not throw that out
            if (!string.IsNullOrWhiteSpace(file.Id))
            {
                var existing = await GetDocumentAsync(userId, file.Id);
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
            ThrowIfInvalid(result);
            return result.Id;
        }


        public async Task<bool> DeleteFileAsync(string userId, ElasticFileInfo file)
        {
            Setup();
            var doc = await GetDocumentAsync(userId, file.Id);
            if (doc is null)
            {
                return false;
            }
            var result = await Client.DeleteAsync<ElasticFileInfo>(file.Id);
            if (result.IsValid)
            {
                return true;
            }
            else if (result.ApiCall.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                return false;
            }
            ThrowIfInvalid(result);
            return false;
        }

        public async Task UpdateFileAttributesAsync(string userId, ElasticFileInfo file)
        {
            var existing = await GetDocumentAsync(userId, file.Id);
            if (existing is null)
            {
                throw new FileNotFoundException("Can not find file with id " + file.Id, file.Id);
            }
            existing.Hash = file.Hash;
            existing.Size = file.Size;
            existing.MimeType = file.MimeType;
            var result = await Client.IndexDocumentAsync(existing);
            ThrowIfInvalid(result);
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
                switch (file.Extension.ToLower())
                { //known overrides
                    case "py": return "text/x-python";
                    case "csv": return "text/csv";
                    case "xcf": return "image/x-xcf";
                    case "ofx": return "text/plain";
                }
                new FileExtensionContentTypeProvider().TryGetContentType($"{file.Name}.{file.Extension}", out string type);
                if (type is null)
                {
                    type = "application/octet-stream";
                }
                return type;
            }
            return null;
        }

        public async Task<User> CreateUserAsync(User user)
        {
            Setup();
            // Check if a user with the provided username exists, if so throw
            var result = await Client.SearchAsync<User>(s =>
                s.Query(q =>
                    q.Match(m =>
                        m.Field(f => f.Username)
                        .Query(user.Username)
                    )
                )
            );

            if (result.Total > 0)
            {
                // exists
                return null;
            }

            var createResult = await Client.IndexDocumentAsync(user);
            ThrowIfInvalid(createResult);
            
            user.Id = createResult.Id;
            user.Password = null;

            return user;
        }

        public async Task<AuthToken> LoginUserAsync(User user)
        {
            Setup();
            User found = null;
            if (!string.IsNullOrWhiteSpace(user.Id))
            {
                //if they gave us the id then we can check that first
                var response = await Client.GetAsync<User>(user.Id);
                if (response.IsValid)
                {
                    found = response.Source;
                    found.Id = response.Id;
                }
            }
            if (found is null)
            {
                var result = await Client.SearchAsync<User>(s =>
                    s.Query(q =>
                        q.Match(m =>
                            m.Field(f => f.Username)
                            .Query(user.Username)
                        )
                    )
                );
                if (result.IsValid && result.Total > 0)
                {
                    var hit = result.Hits.FirstOrDefault();
                    found = hit.Source;
                    found.Id = hit.Id;
                }
            }

            //verification
            var valid = found != null && string.Equals(found.Username, user.Username, StringComparison.Ordinal)
                && string.Equals(found.Password, user.Password, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(found.Id);

            if (valid)
            {
                var token = new AuthToken
                {
                    Creation = DateTimeOffset.Now,
                    Id = Guid.NewGuid().ToString(),
                    LinkedUserId = found.Id
                    //Expiration = null //doesn't expire
                };
                var storeTokenResult = await Client.IndexDocumentAsync(token);
                ThrowIfInvalid(storeTokenResult);
                return token;
            }
            return null;

        }

        public async Task<AuthToken> VerifyTokenAsync(string token)
        {
            Setup();
            var result = await Client.GetAsync<AuthToken>(token);
            if (result.ApiCall.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Token with id {Id} not found.", token);
                return null;
            }
            ThrowIfInvalid(result);
            return result.Source;
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
}
