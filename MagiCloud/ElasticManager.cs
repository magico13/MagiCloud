using MagiCloud.Configuration;
using MagiCommon;
using MagiCommon.Extensions;
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
        Task<FileList> GetDocumentsAsync(string userId, bool? deleted);
        Task<(FileAccessResult, ElasticFileInfo)> GetDocumentAsync(string userId, string id, bool includeText);
        Task<string> IndexDocumentAsync(string userId, ElasticFileInfo file);
        Task<FileAccessResult> DeleteFileAsync(string userId, string id);
        Task UpdateFileAttributesAsync(string userId, ElasticFileInfo file);

        Task<User> GetUserAsync(string userId);
        Task<User> CreateUserAsync(User user);
        Task<AuthToken> LoginUserAsync(LoginRequest request);
        Task<AuthToken> VerifyTokenAsync(string token);
        Task RemoveExpiredTokensAsync();

        FileAccessResult VerifyFileAccess(string userId, ElasticFileInfo file);
    }


    public class ElasticManager : IElasticManager
    {
        public const string FILES_INDEX = "magicloud_files";
        public const string USER_INDEX = "magicloud_users";
        public const string TOKEN_INDEX = "magicloud_tokens";

        public IElasticClient Client { get; set; }

        private readonly ElasticSettings _settings;
        private readonly ILogger<ElasticManager> _logger;
        private readonly IHashService _hashService;

        public ElasticManager(IOptionsSnapshot<ElasticSettings> options, ILogger<ElasticManager> logger, IHashService hashService)
        {
            _settings = options.Value;
            _logger = logger;
            _hashService = hashService;
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

        public async Task<FileList> GetDocumentsAsync(string userId, bool? deleted = null)
        {
            Setup();
            var result = await Client.SearchAsync<ElasticFileInfo>(s =>
            {
                return s.Size(10000) //10k items currently supported, TODO paginate
                .Source(filter => filter.Excludes(e => e.Field(f => f.Text)))
                .Query(q =>
                {
                    var qc = q
                        .Match(m => m
                            .Field(f => f.UserId)
                            .Query(userId)
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
                });
            
            });
            if (result.IsValid)
            {
                var list = new List<ElasticFileInfo>();
                foreach (var hit in result.Hits)
                {
                    var source = hit.Source;
                    source.Id = hit.Id;
                    // omit the text from the result returned to the web
                    // TODO: make this optional?
                    source.Text = null;
                    list.Add(source);
                }
                return new FileList { Files = list };
            }
            _logger.LogError("Invalid GetDocuments call. {ServerError}", result.ServerError);
            return new FileList() { Files = new List<ElasticFileInfo>() };
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
                var (getResult, existing) = await GetDocumentAsync(userId, file.Id, false);
                if (getResult == FileAccessResult.FullAccess) //if existing file (that we can access) overwrite it
                {
                    EnsureFileAttributes(file, existing);
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

        public async Task UpdateFileAttributesAsync(string userId, ElasticFileInfo file)
        {
            var (_, existing) = await GetDocumentAsync(userId, file.Id, true);
            if (existing is null)
            {
                throw new FileNotFoundException("Can not find file with id " + file.Id, file.Id);
            }
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
                new FileExtensionContentTypeProvider().TryGetContentType(file.GetFileName(), out string type);
                if (type is null)
                {
                    type = "application/octet-stream";
                }
                return type;
            }
            return file.MimeType;
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

        public async Task<User> GetUserAsync(string userId)
        {
            Setup();
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var userResult = await Client.GetAsync<User>(userId);
                if (!userResult.Found)
                {
                    return null;
                }
                ThrowIfInvalid(userResult);
                var user = userResult.Source;
                user.Password = null;
                user.Id = userId;
                return user;
            }
            return null;
        }

        public async Task<AuthToken> LoginUserAsync(LoginRequest request)
        {
            Setup();
            User found = null;
            var result = await Client.SearchAsync<User>(s =>
                s.Query(q =>
                    q.Match(m =>
                        m.Field(f => f.Username)
                        .Query(request.Username)
                    )
                )
            );
            if (result.IsValid && result.Total > 0)
            {
                var hit = result.Hits.FirstOrDefault();
                found = hit.Source;
                found.Id = hit.Id;
            }

            //verification
            var valid = !string.IsNullOrWhiteSpace(found?.Id)
                && !found.IsLocked
                && string.Equals(found.Username, request.Username, StringComparison.Ordinal)
                && string.Equals(found.Password, request.Password, StringComparison.Ordinal);

            if (!valid)
            {
                _logger.LogInformation("Invalid login for username {Username}", request.Username);
            }

            if (!valid && found?.IsLocked == false)
            {
                // If there is an account, increase the failed login counter
                // If failed logins > 3, lock the account
                found.LoginFailures++;
                if (found.LoginFailures > 3)
                {
                    _logger.LogWarning("Locking account {Id} due to login failures.", found.Id);
                    found.IsLocked = true;
                }
                ThrowIfInvalid(await Client.IndexDocumentAsync(found));
            }
            else if (valid)
            {
                if (found.LoginFailures > 0)
                {
                    _logger.LogInformation("Resetting login failures for account {Id}. Previous Failures: {Count}", found.Id, found.LoginFailures);
                    found.LoginFailures = 0;
                    ThrowIfInvalid(await Client.IndexDocumentAsync(found));
                }

                var rawToken = Guid.NewGuid().ToString();
                var token = new AuthToken
                {
                    Id = _hashService.GeneratePasswordHash(rawToken), //we store the hash of the guid, not the guid itself
                    Creation = DateTimeOffset.Now,
                    LinkedUserId = found.Id,
                    Name = request.TokenName,
                    LastUpdated = DateTimeOffset.Now,
                    Timeout = request.DesiredTimeout,
                    Expiration = request.DesiredExpiration
                };
                var storeTokenResult = await Client.IndexDocumentAsync(token);
                ThrowIfInvalid(storeTokenResult);
                token.Id = rawToken; //callers must pass the original guid as the token so we give it to them here. We cannot retrieve it, only check it
                return token;
            }
            return null;

        }

        public async Task<AuthToken> VerifyTokenAsync(string token)
        {
            Setup();
            var hashedToken = _hashService.GeneratePasswordHash(token); //caller passes original guid token, we compare hashes. Never have original token stored.
            var result = await Client.GetAsync<AuthToken>(hashedToken);
            if (result.ApiCall.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Token with id {Id} not found.", hashedToken);
                return null;
            }
            ThrowIfInvalid(result);
            var fullToken = result.Source;
            var expired = false;
            if (fullToken.Timeout > 0)
            {
                if (DateTimeOffset.Now > fullToken.LastUpdated + TimeSpan.FromSeconds(fullToken.Timeout.Value))
                {
                    _logger.LogWarning("Token {Id} has expired from inactivity.", hashedToken);
                    expired = true;
                }
            }
            if (DateTimeOffset.Now > fullToken.Expiration)
            {
                _logger.LogWarning("Token {Id} has expired.", hashedToken);
                expired = true;
            }
            if (expired)
            {
                var response = await Client.DeleteAsync<AuthToken>(hashedToken);
                ThrowIfInvalid(response);
                return null;
            }
            fullToken.LastUpdated = DateTimeOffset.Now;
            var updateResult = await Client.IndexDocumentAsync(fullToken);
            ThrowIfInvalid(updateResult);
            return fullToken;
        }

        public FileAccessResult VerifyFileAccess(string userId, ElasticFileInfo file)
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

        public async Task RemoveExpiredTokensAsync()
        {
            Setup();
            var result = await Client.SearchAsync<AuthToken>(s =>
            {
                return s.Size(10000)
                .Query(q => q
                    .DateRange(c => c
                        .Name("date_expiration")
                        .Field(f => f.Expiration)
                        .GreaterThan("2000-01-01")
                        .LessThan(DateMath.Now)
                    ) || q
                    .LongRange(e => e
                        .Name("timeout_exists")
                        .Field(f => f.Timeout)
                        .GreaterThan(0)
                    )
                );
            });
            if (result.IsValid)
            {
                var list = new List<AuthToken>();
                foreach (var hit in result.Hits)
                {
                    var fullToken = hit.Source;
                    var expired = false;
                    if (fullToken.Timeout > 0)
                    {
                        if (DateTimeOffset.Now > fullToken.LastUpdated + TimeSpan.FromSeconds(fullToken.Timeout.Value))
                        {
                            _logger.LogWarning("Token {Id} has expired from inactivity.", fullToken.Id);
                            expired = true;
                        }
                    }
                    if (DateTimeOffset.Now > fullToken.Expiration)
                    {
                        _logger.LogWarning("Token {Id} has expired.", fullToken.Id);
                        expired = true;
                    }
                    if (expired)
                    {
                        var response = await Client.DeleteAsync<AuthToken>(fullToken.Id);
                    }
                }
            }
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
