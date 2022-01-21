using MagiCloud.DataManager;
using MagiCommon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MagiCloud.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
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
                var userId = User.Identity.Name;
                var docs = await _elastic.GetDocumentsAsync(userId);
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
        [AllowAnonymous]
        public async Task<IActionResult> GetAsync(string id)
        {
            try
            {
                var userId = User?.Identity?.Name;
                var (result, file) = await _elastic.GetDocumentAsync(userId, id);
                if (result == FileAccessResult.FullAccess || result == FileAccessResult.ReadOnly)
                {
                    return Json(file);
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
                var userId = User.Identity.Name;
                await _elastic.SetupIndicesAsync();
                var docId = await _elastic.IndexDocumentAsync(userId, file);
                var (_, doc) = await _elastic.GetDocumentAsync(userId, docId);
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
                var userId = User.Identity.Name;
                var (result, doc) = await _elastic.GetDocumentAsync(userId, id);
                if (result == FileAccessResult.FullAccess)
                {
                    // Mark file as deleted but don't permanently delete the file
                    doc.IsDeleted = true;
                    await _elastic.IndexDocumentAsync(userId, doc);
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
                _logger.LogError(ex, "Exception while deleting document {DocId}.", id);
                return StatusCode(500);
            }
        }
    }
}
