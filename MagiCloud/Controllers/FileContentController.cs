using MagiCloud.DataManager;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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

        public FileContentController(ILogger<FileContentController> logger, IElasticManager elastic, IDataManager dataManager)
        {
            _logger = logger;
            _elastic = elastic;
            _dataManager = dataManager;
        }


        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetFile(string id)
        {
            try
            {
                await _elastic.SetupIndicesAsync();
                var doc = await _elastic.GetDocumentAsync(id);
                if (doc != null && !string.IsNullOrWhiteSpace(doc.Id))
                {
                    // document exists in db, pull from file system
                    if (_dataManager.FileExists(doc.Id))
                    {
                        var stream = _dataManager.GetFile(doc.Id);
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
        public async Task<IActionResult> PutFile(string id, IFormFile file)
        {
            try
            {
                //get the file info from the db, upload the file data, update the info in the db

                if (file is null || file.Length <= 0)
                {
                    return BadRequest();
                }

                await _elastic.SetupIndicesAsync();
                var doc = await _elastic.GetDocumentAsync(id);
                if (doc != null && !string.IsNullOrWhiteSpace(doc.Id))
                {
                    // document exists in db, pull from file system
                    using HashAlgorithm alg = SHA256.Create();
                    using var stream = file.OpenReadStream();
                    var bytes = alg.ComputeHash(stream);
                    var hash = Convert.ToBase64String(bytes);

                    doc.Hash = hash;
                    doc.MimeType = file.ContentType;
                    doc.Size = file.Length;

                    //write file to data folder
                    await _dataManager.WriteFileAsync(doc.Id, stream);

                    await _elastic.SetHashAsync(id, hash);
                    await _elastic.IndexDocumentAsync(doc);

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
