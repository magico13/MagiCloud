using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ExtractionHelper _extractionHelper;

        public AdminController(
            ILogger<AdminController> logger, 
            IElasticManager elastic,
            ExtractionHelper extractionHelper)
        {
            _logger = logger;
            _elastic = elastic;
            _extractionHelper = extractionHelper;
        }

        [HttpPost]
        [Route("cleanTokens")]
        public async Task<IActionResult> RemoveExpiredTokensAsync()
        {
            await _elastic.SetupIndicesAsync();
            await _elastic.RemoveExpiredTokensAsync();
            return Ok();
        }

        [HttpPost]
        [Route("extractText")]
        public async Task<IActionResult> ExtractText([FromQuery] bool force = false, [FromQuery] string mimeTypes = null)
        {
            // run text extraction on all docs that are missing text
            // if force is true then do it for docs that already have text as well
            var userId = User.Identity.Name;
            var docList = await _elastic.GetDocumentsAsync(userId, false); //TODO: Do it for all docs, not just ours
            var filteredList = docList;
            if (!string.IsNullOrWhiteSpace(mimeTypes))
            {
                var split = mimeTypes.Split(';');
                filteredList = docList.FindAll(f => split.Contains(f.MimeType));
            }
            _logger.LogInformation(
                "Performing text extraction on up to {Count} documents. Force is {Flag}",
                filteredList.Count,
                force);
            var updatedDocs = new List<string>();
            foreach (var doc in filteredList)
            {
                var (updated, text) = await _extractionHelper.ExtractTextAsync(userId, doc.Id, force);
                if (updated)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _logger.LogInformation(
                            "File {FileId} extracted {Length} characters. Type is {ContentType}.",
                            doc.Id,
                            text.Length,
                            doc.MimeType);
                        doc.Text = text;
                        updatedDocs.Add(doc.Id);
                        await _elastic.UpdateFileAttributesAsync(userId, doc);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "File {FileId} failed to extract text. Type is {ContentType}.",
                            doc.Id,
                            doc.MimeType);
                    }
                }
                else
                {
                    _logger.LogInformation(
                             "File {FileId} already had text with {Length} characters. Type is {ContentType}.",
                             doc.Id,
                             text.Length,
                             doc.MimeType);
                }
            }

            return Ok(updatedDocs);
        }
    }
}
