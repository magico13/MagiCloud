using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace MagiCloud.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly ILogger<AdminController> _logger;
        private readonly IElasticManager _elastic;

        public AdminController(ILogger<AdminController> logger, IElasticManager elastic)
        {
            _logger = logger;
            _elastic = elastic;
        }

        [HttpPost]
        [Route("cleanTokens")]
        public async Task<IActionResult> RemoveExpiredTokensAsync()
        {
            await _elastic.SetupIndicesAsync();
            await _elastic.RemoveExpiredTokensAsync();
            return Ok();
        }
    }
}
