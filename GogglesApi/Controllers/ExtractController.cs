using Goggles;
using Microsoft.AspNetCore.Mvc;

namespace GogglesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExtractController : ControllerBase
{
    private readonly ILens _lens;
    private const int MaxFileSize = 2_147_483_647; //2GB

    public ExtractController(ILens lens)
    {
        _lens = lens;
    }

    [HttpPost]
    [Route("text")]
    [RequestSizeLimit(MaxFileSize)]
    [RequestFormLimits(ValueLengthLimit = MaxFileSize, MultipartBodyLengthLimit = MaxFileSize)]
    public async Task<IActionResult> PostAsync(IFormFile file)
    {
        using var fileStream = file.OpenReadStream();
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) 
            || file.ContentType == "application/octet-stream"
            ? _lens.DetermineContentType(file.FileName) 
            : file.ContentType;
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
