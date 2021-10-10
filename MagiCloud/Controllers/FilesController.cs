using MagiCloud.DataManager;
using MagiCommon.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MagiCloud.Controllers
{
    [Route("api/[controller]")]
    public class FilesController : Controller
    {
        private readonly ILogger<FilesController> _logger;
        private readonly IElasticManager _elastic;
        private readonly IDataManager _dataManager;

        public FilesController(ILogger<FilesController> logger, IElasticManager elastic, IDataManager dataManager)
        {
            _logger = logger;
            _elastic = elastic;
            _dataManager = dataManager;
        }

        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetAsync()
        {
            try
            {
                var token = await Request.VerifyAuthToken(_elastic);
                if (token is null)
                {
                    return Unauthorized();
                }
                var docs = await _elastic.GetDocumentsAsync(token.LinkedUserId);
                return Json(docs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while getting document list.");
                return StatusCode(500);
            }
        }

        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetAsync(string id)
        {
            try
            {
                var token = await Request.VerifyAuthToken(_elastic);
                if (token is null)
                {
                    return Unauthorized();
                }
                var file = await _elastic.GetDocumentAsync(token.LinkedUserId, id);
                return Json(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while getting document {DocId}.", id);
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("")]
        public async Task<IActionResult> PostAsync([FromBody]ElasticFileInfo file)
        {
            try
            {
                var token = await Request.VerifyAuthToken(_elastic);
                if (token is null)
                {
                    return Unauthorized();
                }
                await _elastic.SetupIndicesAsync();
                var docId = await _elastic.IndexDocumentAsync(token.LinkedUserId, file);
                var doc = await _elastic.GetDocumentAsync(token.LinkedUserId, docId);
                doc.Id = docId;
                return Json(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while getting document list.");
                return StatusCode(500);
            }
        }

        // DELETE: files/5
        [HttpDelete]
        [Route("{id}")]
        public async Task<IActionResult> DeleteAsync(string id)
        {
            try
            {
                var token = await Request.VerifyAuthToken(_elastic);
                if (token is null)
                {
                    return Unauthorized();
                }
                _dataManager.DeleteFile(id);
                if (await _elastic.DeleteFileAsync(token.LinkedUserId, new ElasticFileInfo { Id = id }))
                {
                    return NoContent();
                }
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while deleting document {DocId}.", id);
                return StatusCode(500);
            }
        }
    }
}
