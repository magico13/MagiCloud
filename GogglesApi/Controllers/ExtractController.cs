using Goggles;
using Microsoft.AspNetCore.Mvc;

namespace GogglesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExtractController(ILens lens) : ControllerBase
{
    private const int MaxFileSize = 2_147_483_647; //2GB

    [HttpPost]
    [Route("text")]
    [RequestSizeLimit(MaxFileSize)]
    [RequestFormLimits(ValueLengthLimit = MaxFileSize, MultipartBodyLengthLimit = MaxFileSize)]
    public async Task<IActionResult> PostAsync(IFormFile file)
    {
        using var fileStream = file.OpenReadStream();
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) 
            || file.ContentType == "application/octet-stream"
            ? lens.DetermineContentType(file.FileName) 
            : file.ContentType;
        var result = await lens.ExtractTextAsync(fileStream, file.FileName, contentType);
        return new JsonResult(result);
    }

    [HttpGet]
    [Route("contentType")]
    public IActionResult Get([FromQuery] string extension) 
        => new JsonResult(new
            {
                ContentType = lens.DetermineContentType(extension)
            });
}
