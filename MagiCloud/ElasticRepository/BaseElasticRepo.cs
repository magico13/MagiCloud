using MagiCloud.Configuration;
using MagiCommon.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class BaseElasticRepo(
    IOptions<ElasticSettings> options,
    ILogger logger) : IElasticRepository
{
    public const string FILES_INDEX = "magicloud_files";
    public const string FOLDERS_INDEX = "magicloud_folders";

    public IElasticClient Client { get; set; }

    public string Thumbprint {get; private set;}

    protected readonly ElasticSettings _settings = options.Value;
    protected readonly ILogger _logger = logger;

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
            _logger.LogInformation("Using API key authentication.");
            connectionSettings.ApiKeyAuthentication(_settings.ApiKeyId, _settings.ApiKey);
        }

        if (!string.IsNullOrWhiteSpace(_settings.Thumbprint))
        {
            _logger.LogInformation("Using certificate authentication with thumbprint '{Thumbprint}'.", _settings.Thumbprint);
            Thumbprint = _settings.Thumbprint;
            connectionSettings.ServerCertificateValidationCallback(ValidateCertificate);
        }

        if (!string.IsNullOrWhiteSpace(_settings.CertificatePath))
        {
            _logger.LogInformation("Using certificate path authentication with path '{Path}'.", _settings.CertificatePath);
            // check if the file exists
            if (!File.Exists(_settings.CertificatePath))
            {
                throw new FileNotFoundException($"Certificate file not found at path: {_settings.CertificatePath}");
            }
            var cert = new X509Certificate2(_settings.CertificatePath);
            if (string.IsNullOrWhiteSpace(cert.Thumbprint))
            {
                throw new InvalidOperationException("Certificate thumbprint is empty.");
            }
            Thumbprint = cert.Thumbprint;
            connectionSettings.ServerCertificateValidationCallback(ValidateCertificate);
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
                throw new InvalidOperationException("Exception during processing. " + response.ServerError?.ToString());
            }
        }
    }

     public bool ValidateCertificate(
        object _,
        X509Certificate cert,
        X509Chain __,
        System.Net.Security.SslPolicyErrors ___)
    {
        string actualThumbprint = cert.GetCertHashString();
        bool result = string.Equals(actualThumbprint, Thumbprint, StringComparison.OrdinalIgnoreCase);
        if (!result)
        {
            _logger.LogWarning("Certificate thumbprint does not match. Expected: {Expected}, Actual: {Actual}", Thumbprint, actualThumbprint);
        }
        return result;
    }
}
