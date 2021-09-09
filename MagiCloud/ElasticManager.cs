﻿using MagiCloud.Configuration;
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
        Task SetHashAsync(string id, string hash);
    }


    public class ElasticManager : IElasticManager
    {
        public const string FILES_INDEX = "magicloud_files";

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
                .EnableDebugMode()
                .PrettyJson()
                .RequestTimeout(TimeSpan.FromMinutes(2))
                .ApiKeyAuthentication(_settings.ApiKeyId, _settings.ApiKey);

            Client = new ElasticClient(connectionSettings);
        }

        public async Task<bool> SetupIndicesAsync()
        {
            Setup();

            var index = Indices.Index(FILES_INDEX);
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
            return true;
        }

        public async Task<FileList> GetDocumentsAsync()
        {
            Setup();
            var result = await Client.SearchAsync<ElasticFileInfo>(s => s.MatchAll());
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
            file.LastModified = DateTimeOffset.Now;
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
                    EnsureHash(file, existing);
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

        public async Task SetHashAsync(string id, string hash)
        {
            var file = await GetDocumentAsync(id);
            if (file is null)
            {
                throw new FileNotFoundException("Can not find file with id " + id, id);
            }
            file.Hash = hash;
            var result = await Client.IndexDocumentAsync(file);
            if (!result.IsValid)
            {
                if (result.OriginalException != null)
                {
                    throw result.OriginalException;
                }
                _logger.LogError("Error while updating the hash of document {DocId}.", id);
            }
        }

        private void EnsureHash(ElasticFileInfo newFile, ElasticFileInfo existingFile)
        {
            newFile.Hash = existingFile?.Hash;
        }

        private string GetMimeType(ElasticFileInfo file)
        {
            if (string.IsNullOrWhiteSpace(file.MimeType))
            {
                new FileExtensionContentTypeProvider().TryGetContentType($"{file.Name}.{file.Extension}", out string type);
                return type;
            }
            return null;
        }
    }
}
