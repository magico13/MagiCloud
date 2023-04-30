using MagiCloud.Configuration;
using MagiCommon.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using System;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class BaseElasticRepo : IElasticRepository
{
    public const string FILES_INDEX = "magicloud_files";
    public const string FOLDERS_INDEX = "magicloud_folders";

    public IElasticClient Client { get; set; }

    protected readonly ElasticSettings _settings;
    protected readonly ILogger _logger;

    public BaseElasticRepo(
        IOptions<ElasticSettings> options,
        ILogger logger)
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
            .DefaultMappingFor<ElasticFolder>(i => i
                .IndexName(FOLDERS_INDEX)
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

        foreach (var indexName in new string[] { FILES_INDEX, FOLDERS_INDEX })
        {
            var index = Indices.Index(indexName);
            var exists = await Client.Indices.ExistsAsync(index);
            if (!exists.Exists)
            {
                _logger.LogInformation("Index '{Name}' not found, creating.", indexName);
                var create = await Client.Indices.CreateAsync(index);
                ThrowIfInvalid(create);
            }
        }
        return true;
    }

    public static FileAccessResult DetermineAccessForUser(string userId, ElasticObject toCheck)
    {
        if (string.Equals(toCheck.UserId, userId, StringComparison.Ordinal))
        {
            return FileAccessResult.FullAccess;
        }
        else if (toCheck.IsPublic)
        {
            return FileAccessResult.ReadOnly;
        }
        return FileAccessResult.NotPermitted;
    }

    protected void ThrowIfInvalid(ResponseBase response)
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
