using MagiCloud.DataManager;
using MagiCommon;
using MagiCommon.Models;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class FileStorageService
{
    public IDataManager DataManager { get; }
    public IElasticManager Elastic { get; }
    public IHashService HashService { get; }
    public ILogger<FileStorageService> Logger { get; }
    public TextExtractionQueueHelper ExtractionQueue { get; }

    public FileStorageService(
        IDataManager dataManager,
        IElasticManager elastic,
        IHashService hashService,
        ILogger<FileStorageService> logger,
        TextExtractionQueueHelper extractionQueue)
    {
        DataManager = dataManager;
        Elastic = elastic;
        HashService = hashService;
        Logger = logger;
        ExtractionQueue = extractionQueue;
    }

    public async Task<FileAccessResult> StoreFile(string userId, string docId, Stream stream)
    {
        var (result, doc) = await Elastic.GetDocumentAsync(userId, docId, true);
        doc.Id = docId;
        return result != FileAccessResult.FullAccess 
            ? result 
            : await StoreFile(userId, doc, stream);
    }

    public async Task<FileAccessResult> StoreFile(string userId, ElasticFileInfo fileInfo, Stream stream)
    {
        if (stream is null || stream.Length < 0)
        {
            return FileAccessResult.Unknown;
        }
        await Elastic.SetupIndicesAsync();

        var docId = await Elastic.IndexDocumentAsync(userId, fileInfo);
        var (result, doc) = await Elastic.GetDocumentAsync(userId, docId, false);
        doc.Id = docId;
        if (result == FileAccessResult.FullAccess
            && doc != null
            && !string.IsNullOrWhiteSpace(doc.Id))
        {
            // document exists in db, write data to filesystem
            using var fileStream = await DataManager.WriteFileAsync(doc.Id, stream);

            // Update indexed document
            await UpdateFileAttributesAsync(fileStream, doc);
            return result;
        }
        else if (result == FileAccessResult.NotFound)
        {
            return result;
        }
        return FileAccessResult.NotPermitted;
    }

    private async Task UpdateFileAttributesAsync(Stream stream,
        ElasticFileInfo doc)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }
        var hash = HashService.GenerateContentHash(stream, true);
        var oldHash = doc.Hash;
        var hashesChanged = hash != oldHash;
        doc.Hash = hash;
        doc.MimeType = doc.MimeType;
        doc.Size = stream.Length;
        Logger.LogInformation("Hashed file. New: {NewHash} Old: {Hash}", hash, oldHash);
        if (hashesChanged)
        {
            ExtractionQueue.AddFileToQueue(doc.Id);
        }

        await Elastic.UpdateFileAttributesAsync(doc);
    }
}
