using Goggles;
using Microsoft.AspNetCore.Mvc;

namespace GogglesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExtractController : ControllerBase
{
    private readonly ILogger<ExtractController> _logger;
    private readonly ILens _lens;

    public ExtractController(ILogger<ExtractController> logger, ILens lens)
    {
        _logger = logger;
        _lens = lens;
    }

    [HttpPost]
    [Route("text")]
    [RequestSizeLimit(104857600)] //100MB
    [RequestFormLimits(ValueLengthLimit = 104857600, MultipartBodyLengthLimit = 104857600)]
    public async Task<IActionResult> PostAsync(IFormFile file)
    {
        using var fileStream = file.OpenReadStream();
        var contentType = file.ContentType != "application/octet-stream"
            ? file.ContentType 
            : _lens.DetermineContentType(file.FileName);
        return new JsonResult(new 
        {
            Text = await _lens.ExtractTextAsync(fileStream, file.FileName, contentType),
            contentType 
        });
    }

    [HttpGet]
    [Route("contentType")]
    public IActionResult Get([FromQuery] string extension) 
        => new JsonResult(new
            {
                ContentType = _lens.DetermineContentType(extension)
            });
}
