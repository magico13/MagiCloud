using MagiCloud.DataManager;
using MagiCommon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MagiCloud.Controllers;

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
    public async Task<IActionResult> GetAsync([FromQuery] bool? deleted)
    {
        try
        {
            var userId = User.FindFirst(ClaimsIdentity.DefaultNameClaimType)?.Value;
            if (userId == null) { return Forbid(); }
            var docs = await _elastic.GetDocumentsAsync(userId, deleted);
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
    public async Task<IActionResult> GetAsync(string id, [FromQuery] bool includeText = false)
    {
        try
        {
            var userId = User.FindFirst(ClaimsIdentity.DefaultNameClaimType)?.Value;
            if (userId == null) { return Forbid(); }
            var (result, file) = await _elastic.GetDocumentAsync(userId, id, includeText);
            if (result is FileAccessResult.FullAccess or FileAccessResult.ReadOnly)
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
            var userId = User.FindFirst(ClaimsIdentity.DefaultNameClaimType)?.Value;
            if (userId == null) { return Forbid(); }
            await _elastic.SetupIndicesAsync();
            var docId = await _elastic.IndexDocumentAsync(userId, file);
            var (_, doc) = await _elastic.GetDocumentAsync(userId, docId, false);
            doc.Id = docId;
            return Json(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting document list.");
            return StatusCode(500);
        }
    }

    // DELETE: files/5?permanent=true
    [HttpDelete]
    [Route("{id}")]
    public async Task<IActionResult> DeleteAsync(string id, [FromQuery]bool permanent=false)
    {
        try
        {
            var userId = User.FindFirst(ClaimsIdentity.DefaultNameClaimType)?.Value;
            if (userId == null) { return Forbid(); }
            var (result, doc) = await _elastic.GetDocumentAsync(userId, id, false);
            if (result == FileAccessResult.FullAccess)
            {
                // Mark file as deleted but don't permanently delete the file
                if (!permanent)
                {
                    doc.IsDeleted = true;
                    await _elastic.IndexDocumentAsync(userId, doc);
                }
                else
                {
                    _dataManager.DeleteFile(id);
                    await _elastic.DeleteFileAsync(userId, id);
                }
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
