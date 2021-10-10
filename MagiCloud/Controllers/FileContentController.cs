using MagiCloud.DataManager;
using MagiCommon;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MagiCloud.Controllers
{
    [Route("api/[controller]")]
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
        [Route("{id}")]
        public async Task<IActionResult> GetFile(string id)
        {
            try
            {
                var token = await Request.VerifyAuthToken(_elastic);
                if (token is null)
                {
                    return Unauthorized();
                }
                var doc = await _elastic.GetDocumentAsync(token.LinkedUserId, id);
                if (doc != null && !string.IsNullOrWhiteSpace(doc.Id))
                {
                    // document exists in db, pull from file system
                    if (_dataManager.FileExists(doc.Id))
                    {
                        var stream = _dataManager.GetFile(doc.Id);
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
                        return File(stream, doc.MimeType, doc.LastModified, new EntityTagHeaderValue($"\"{doc.Hash}\""));
                    }
                    else
                    {
                        return NotFound(new Dictionary<string, object>
                        {
                            ["message"] = "Document not found on disk."
                        });
                    }
                }
                return NotFound(new Dictionary<string, object>
                {
                    ["message"] = "Document not found in database."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while getting content for document {DocId}.", id);
                return StatusCode(500);
            }
        }

        [HttpPut]
        [Route("{id}")]
        [RequestSizeLimit(int.MaxValue)] //About 2GB, TODO support streaming/chunking larger files
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        public async Task<IActionResult> PutFile(string id, IFormFile file)
        {
            try
            {
                //get the file info from the db, upload the file data, update the info in the db
                var token = await Request.VerifyAuthToken(_elastic);
                if (token is null)
                {
                    return Unauthorized();
                }
                if (file is null || file.Length < 0)
                {
                    return BadRequest();
                }

                await _elastic.SetupIndicesAsync();
                var doc = await _elastic.GetDocumentAsync(token.LinkedUserId, id);
                if (doc != null && !string.IsNullOrWhiteSpace(doc.Id))
                {
                    // document exists in db, pull from file system
                    using var stream = file.OpenReadStream();
                    var hash = _hashService.GenerateContentHash(stream, false);
                    doc.Hash = hash;
                    doc.MimeType = file.ContentType ?? doc.MimeType;
                    doc.Size = file.Length;

                    //write file to data folder
                    await _dataManager.WriteFileAsync(doc.Id, stream);

                    await _elastic.UpdateFileAttributesAsync(token.LinkedUserId, doc);
                    //await _elastic.IndexDocumentAsync(doc); //superfluous?

                    return NoContent();
                }
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while uploading content for document {DocId}.", id);
                return StatusCode(500);
            }
        }
    }
}
