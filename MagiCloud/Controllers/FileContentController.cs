using MagiCloud.DataManager;
using MagiCloud.Services;
using MagiCommon;
using MagiCommon.Extensions;
using MagiCommon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MagiCloud.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
public class FileContentController : ControllerBase
{
    private readonly ILogger<FileContentController> _logger;
    private readonly IElasticManager _elastic;
    private readonly IDataManager _dataManager;
    private readonly FileStorageService _storageService;

    public FileContentController(
        ILogger<FileContentController> logger,
        IElasticManager elastic,
        IDataManager dataManager,
        FileStorageService storageService)
    {
        _logger = logger;
        _elastic = elastic;
        _dataManager = dataManager;
        _storageService = storageService;
    }


    [HttpGet]
    [AllowAnonymous]
    [Route("{id}")]
    public async Task<IActionResult> GetFile(string id, [FromQuery] bool download = false)
    {
        Stream stream = null;
        try
        {
            var userId = User.GetUserId();
            var (result, doc) = await _elastic.GetDocumentAsync(userId, id, false);
            
            if (result is FileAccessResult.FullAccess or FileAccessResult.ReadOnly
                && doc != null && !string.IsNullOrWhiteSpace(doc.Id) && !doc.IsDeleted)
            {
                // document exists in db, pull from file system
                if (_dataManager.FileExists(doc.Id))
                {
                    stream = _dataManager.GetFile(doc.Id);
                    if (string.IsNullOrWhiteSpace(doc.MimeType))
                    {
                        new FileExtensionContentTypeProvider().TryGetContentType(doc.GetFileName(), out var type);
                        type ??= "application/octet-stream";
                        doc.MimeType = type;
                        _logger.LogWarning("MimeType data missing for document {DocId}, using type {ContentType}", doc.Id, doc.MimeType);

                    }
                    if (download)
                    {
                        return File(stream,
                            contentType: doc.MimeType,
                            fileDownloadName: doc.GetFileName(),
                            enableRangeProcessing: true);
                    }
                    else
                    {
                        return File(stream,
                            contentType: doc.MimeType,
                            lastModified: doc.LastModified,
                            entityTag: new EntityTagHeaderValue($"\"{doc.Hash}\""));
                    }
                }
                else
                {
                    return NotFound(new
                    {
                        message = "Document content not found."
                    });
                }
            }
            if (result == FileAccessResult.NotFound)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }
            else if (result == FileAccessResult.NotPermitted)
            {
                return Forbid();
            }
            else
            {
                return NotFound();
            }
        }
        catch (Exception ex)
        {
            stream?.Close();
            _logger.LogError(ex, "Exception while getting content for document {DocId}.", id);
            return StatusCode(500);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("{id}/text")]
    public async Task<IActionResult> GetFileTextAsync(string id)
    {
        try
        {
            var userId = User.GetUserId();
            var (result, doc) = await _elastic.GetDocumentAsync(userId, id, true);

            if (result is FileAccessResult.FullAccess or FileAccessResult.ReadOnly
                && doc != null && !string.IsNullOrWhiteSpace(doc.Id) 
                && !doc.IsDeleted && !string.IsNullOrWhiteSpace(doc.Text))
            {
                return File(Encoding.UTF8.GetBytes(doc.Text), "text/plain");
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting document list.");
            return StatusCode(500);
        }
    }

    [HttpPut]
    [Route("{id}")]
    [RequestSizeLimit(int.MaxValue)] //About 2GB, chunk larger files through PutFilePart
    [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
    public async Task<IActionResult> PutFile(string id, IFormFile file)
    {
        try
        {
            //get the file info from the db, upload the file data, update the info in the db
            var userId = User.GetUserId();
            if (userId == null) { return Forbid(); }
            if (file is null || file.Length < 0)
            {
                return BadRequest();
            }

            using var stream = file.OpenReadStream();
            var result = await _storageService.StoreFile(userId, id, stream);
            if (result == FileAccessResult.FullAccess)
            {
                return NoContent();
            }
            else if (result == FileAccessResult.NotFound)
            {
                return NotFound();
            }
            else
            {
                return Forbid();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while uploading content for document {DocId}.", id);
            return StatusCode(500);
        }
    }

    // Removed temporarily while I rewrite it
    //[HttpPut]
    //[Route("{id}/{part}")]
    //[RequestSizeLimit(104857600)] // 100MB limit
    //[RequestFormLimits(ValueLengthLimit = 104857600, MultipartBodyLengthLimit = 104857600)]
    //public async Task<IActionResult> PutFilePart(string id, int part, IFormFile file, [FromQuery] bool final = false)
    //{
    //    try
    //    {
    //        //get the file info from the db, upload the file data, update the info in the db
    //        var userId = User.GetUserId();
    //        if (userId == null) { return Forbid(); }
    //        if (file is null || file.Length < 0)
    //        {
    //            return BadRequest();
    //        }

    //        await _elastic.SetupIndicesAsync();
    //        var (result, doc) = await _elastic.GetDocumentAsync(userId, id, false);
    //        if (result == FileAccessResult.FullAccess && doc != null && !string.IsNullOrWhiteSpace(doc.Id))
    //        {
    //            _logger.LogInformation("Writing part {Part} of file {DocId}. Final? {Final}", part, id, final);
    //            // document exists in db, pull from file system
    //            using var stream = file.OpenReadStream();

    //            //write file to data folder
    //            await _dataManager.WriteFilePartAsync(doc.Id, stream);

    //            if (final)
    //            {
    //                using var fullFile = _dataManager.GetFile(id);
    //                await UpdateFileAttributesAsync(
    //                    fullFile, doc, doc.MimeType ?? file.ContentType);
    //            }

    //            return NoContent();
    //        }
    //        if (result == FileAccessResult.NotFound)
    //        {
    //            return NotFound();
    //        }
    //        else
    //        {
    //            return Forbid();
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Exception while uploading Part {Part} content for document {DocId}.", part, id);
    //        return StatusCode(500);
    //    }
    //}
}
