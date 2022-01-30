using MagiCloud.DataManager;
using MagiCommon;
using MagiCommon.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MagiCloud.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class FileContentController : ControllerBase
    {
        private readonly ILogger<FileContentController> _logger;
        private readonly IElasticManager _elastic;
        private readonly IDataManager _dataManager;
        private readonly IHashService _hashService;

        public FileContentController(ILogger<FileContentController> logger, IElasticManager elastic, IDataManager dataManager, IHashService hashService)
        {
            _logger = logger;
            _elastic = elastic;
            _dataManager = dataManager;
            _hashService = hashService;
        }


        [HttpGet]
        [AllowAnonymous]
        [Route("{id}")]
        public async Task<IActionResult> GetFile(string id, [FromQuery] bool download = false)
        {
            Stream stream = null;
            try
            {
                var userId = User?.Identity?.Name;
                var (result, doc) = await _elastic.GetDocumentAsync(userId, id);
                
                if ((result == FileAccessResult.FullAccess || result == FileAccessResult.ReadOnly)
                    && doc != null && !string.IsNullOrWhiteSpace(doc.Id) && !doc.IsDeleted)
                {
                    // document exists in db, pull from file system
                    if (_dataManager.FileExists(doc.Id))
                    {
                        stream = _dataManager.GetFile(doc.Id);
                        if (string.IsNullOrWhiteSpace(doc.MimeType))
                        {
                            new FileExtensionContentTypeProvider().TryGetContentType($"{doc.Name}.{doc.Extension}", out string type);
                            if (type is null)
                            {
                                type = "application/octet-stream";
                            }
                            doc.MimeType = type;
                            _logger.LogWarning("MimeType data missing for document {DocId}, using type {ContentType}", doc.Id, doc.MimeType);

                        }
                        if (download)
                        {
                            return File(stream, contentType: doc.MimeType, fileDownloadName: doc.GetFileName(), enableRangeProcessing: true);
                        }
                        else
                        {
                            return File(stream, contentType: doc.MimeType, lastModified: doc.LastModified, entityTag: new EntityTagHeaderValue($"\"{doc.Hash}\""));
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

        [HttpPut]
        [Route("{id}")]
        [RequestSizeLimit(int.MaxValue)] //About 2GB, chunk larger files through PutFilePart
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        public async Task<IActionResult> PutFile(string id, IFormFile file)
        {
            try
            {
                //get the file info from the db, upload the file data, update the info in the db
                var userId = User.Identity.Name;
                if (file is null || file.Length < 0)
                {
                    return BadRequest();
                }

                await _elastic.SetupIndicesAsync();
                var (result, doc) = await _elastic.GetDocumentAsync(userId, id);
                if (result == FileAccessResult.FullAccess && doc != null && !string.IsNullOrWhiteSpace(doc.Id))
                {
                    // document exists in db, pull from file system
                    using var stream = file.OpenReadStream();
                    var hash = _hashService.GenerateContentHash(stream, false);
                    doc.Hash = hash;
                    doc.MimeType = file.ContentType ?? doc.MimeType;
                    doc.Size = file.Length;

                    //write file to data folder
                    await _dataManager.WriteFileAsync(doc.Id, stream);

                    await _elastic.UpdateFileAttributesAsync(userId, doc);
                    //await _elastic.IndexDocumentAsync(doc); //superfluous?

                    return NoContent();
                }
                if (result == FileAccessResult.NotFound)
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

        [HttpPut]
        [Route("{id}/{part}")]
        [RequestSizeLimit(104857600)] // 100MB limit
        [RequestFormLimits(ValueLengthLimit = 104857600, MultipartBodyLengthLimit = 104857600)]
        public async Task<IActionResult> PutFilePart(string id, int part, IFormFile file, [FromQuery] bool final = false)
        {
            try
            {
                //get the file info from the db, upload the file data, update the info in the db
                var userId = User.Identity.Name;
                if (file is null || file.Length < 0)
                {
                    return BadRequest();
                }

                await _elastic.SetupIndicesAsync();
                var (result, doc) = await _elastic.GetDocumentAsync(userId, id);
                if (result == FileAccessResult.FullAccess && doc != null && !string.IsNullOrWhiteSpace(doc.Id))
                {
                    _logger.LogInformation("Writing part {Part} of file {DocId}. Final? {Final}", part, id, final);
                    // document exists in db, pull from file system
                    using var stream = file.OpenReadStream();

                    //write file to data folder
                    await _dataManager.WriteFilePartAsync(doc.Id, stream);

                    if (final)
                    {
                        using var fullFile = _dataManager.GetFile(id);
                        var hash = _hashService.GenerateContentHash(fullFile, false);
                        doc.Hash = hash;
                        doc.MimeType = file.ContentType ?? doc.MimeType;
                        doc.Size = fullFile.Length;

                        await _elastic.UpdateFileAttributesAsync(userId, doc);
                    }

                    return NoContent();
                }
                if (result == FileAccessResult.NotFound)
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
                _logger.LogError(ex, "Exception while uploading Part {Part} content for document {DocId}.", part, id);
                return StatusCode(500);
            }
        }
    }
}
