using MagiCloud.DataManager;
using MagiCommon;
using MagiCommon.Extensions;
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
    public ExtractionHelper ExtractionHelper { get; }

    public FileStorageService(
        IDataManager dataManager,
        IElasticManager elastic,
        IHashService hashService,
        ILogger<FileStorageService> logger,
        ExtractionHelper extractionHelper)
    {
        DataManager = dataManager;
        Elastic = elastic;
        HashService = hashService;
        Logger = logger;
        ExtractionHelper = extractionHelper;
    }

    public async Task<FileAccessResult> StoreFile(string userId, ElasticFileInfo fileInfo, Stream stream)
    {
        var docId = await Elastic.IndexDocumentAsync(userId, fileInfo);
        var (result, doc) = await Elastic.GetDocumentAsync(userId, docId, false);
        doc.Id = docId;
        if (fileInfo.Hash != doc.Hash || string.IsNullOrWhiteSpace(doc.Hash))
        {
            if (stream is null || stream.Length < 0)
            {
                // TODO: Throw exception
                return FileAccessResult.Unknown;
            }

            await Elastic.SetupIndicesAsync();
            if (result == FileAccessResult.FullAccess
                && doc != null
                && !string.IsNullOrWhiteSpace(doc.Id))
            {
                // document exists in db, write data to filesystem
                using var fileStream = await DataManager.WriteFileAsync(doc.Id, stream);

                // Update indexed document
                await UpdateFileAttributesAsync(fileStream, userId, doc, doc.MimeType);
                return result;
            }
            else if (result == FileAccessResult.NotFound)
            {
                //throw
                return result;
            }
            return FileAccessResult.NotPermitted;
        }
        return result;
    }

    private async Task UpdateFileAttributesAsync(Stream stream,
        string userId,
        ElasticFileInfo doc,
        string contentType)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }
        var hash = HashService.GenerateContentHash(stream, true);
        var oldHash = doc.Hash;
        var hashesChanged = hash != oldHash;
        doc.Hash = hash;
        doc.MimeType = contentType;
        doc.Size = stream.Length;
        Logger.LogInformation("Hashed file. New: {NewHash} Old: {Hash}", hash, oldHash);
        if (hashesChanged)
        {
            // copy filestream to memory so the file can be accessed if extraction is slow
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            stream.Close();
            // Not the biggest fan of closing out the file here but we don't want to pull it into memory unless we need to process it
            memoryStream.Seek(0, SeekOrigin.Begin); // rewind the memory stream for processing
            doc.Text = await ExtractionHelper.ExtractTextAsync(memoryStream, doc.GetFileName(), contentType);
        }

        await Elastic.UpdateFileAttributesAsync(userId, doc);
    }
}
