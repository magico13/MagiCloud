using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MagiCloud.Controllers;

[Route("api/")]
[ApiController]
public class DefaultController : ControllerBase
{
    public IElasticManager Elastic { get; }

    public DefaultController(IElasticManager elastic)
    {
        this.Elastic = elastic;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        var elasticReady = false;
        try
        {
             elasticReady = await Elastic.SetupIndicesAsync();
        }
        catch { }

        var obj = new Dictionary<string, object>
        {
            ["MagiCloud"] = "operational",
            ["Elasticsearch"] = elasticReady ? "operational" : "error"
        };


        return Ok(obj);
    }
}
