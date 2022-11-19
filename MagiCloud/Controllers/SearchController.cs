using MagiCommon.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MagiCloud.Controllers;

[Route("api/[controller]")]
[Authorize]
public class SearchController : Controller
{
    private readonly ILogger<SearchController> _logger;
    private readonly IElasticManager _elastic;

    public SearchController(ILogger<SearchController> logger, IElasticManager elastic)
    {
        _logger = logger;
        _elastic = elastic;
    }

    [HttpGet]
    [Route("")]
    public async Task<IActionResult> SearchAsync([FromQuery] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { Message = "Invalid query" });
            }
            var userId = User.GetUserId();
            if (userId == null) { return Forbid(); }
            var docs = await _elastic.SearchAsync(userId, query);
            return Json(docs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting document list.");
            return StatusCode(500);
        }
    }
}
