using MagiCloud.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace MagiCloud.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
public class AdminController : ControllerBase
{
    private ILogger<AdminController> Logger { get; }
    private IElasticManager Elastic { get; }
    private TextExtractionQueueHelper ExtractionQueue { get; }

    public AdminController(
        ILogger<AdminController> logger, 
        IElasticManager elastic,
        TextExtractionQueueHelper extractionQueue)
    {
        Logger = logger;
        Elastic = elastic;
        ExtractionQueue = extractionQueue;
    }

    [HttpPost]
    [Route("extractText")]
    public async Task<IActionResult> ExtractText([FromQuery] bool force = false, [FromQuery] string mimeTypes = null)
    {
        // run text extraction on all docs that are missing text
        // if force is true then do it for docs that already have text as well
        var userId = User.Identity.Name;
        var docList = await Elastic.GetDocumentsAsync(userId, false); //TODO: Do it for all docs, not just ours
        var filteredList = docList;
        if (!string.IsNullOrWhiteSpace(mimeTypes))
        {
            var split = mimeTypes.Split(';');
            filteredList = docList.FindAll(f => split.Contains(f.MimeType));
        }
        Logger.LogInformation(
            "Performing text extraction on up to {Count} documents. Force is {Flag}",
            filteredList.Count,
            force);

        filteredList.ForEach(doc =>
        {
            if (force || string.IsNullOrWhiteSpace(doc.Text))
            {
                ExtractionQueue.AddFileToQueue(doc.Id);
            }
        });

        return Ok(filteredList.Count);
    }
}
