using Goggles;
using Microsoft.AspNetCore.Mvc;

namespace GogglesApi.Controllers;
[Route("api/[controller]")]
[ApiController]
public class StatusController : ControllerBase
{
    private readonly ILens _lens;

    public StatusController(ILens lens) 
        => _lens = lens;

    [HttpGet]
    public IActionResult Get() 
        => new JsonResult(new
        {
            Status = "Ok",
            SupportsOCR = _lens.SupportsOCR,
            SupportsAudioTranscription = _lens.SupportsAudioTranscription
        });

    [HttpGet]
    [Route("support")]
    public IActionResult Get([FromQuery] string contentType)
    {
        // check if this API can handle the provided content type
        var supported = true;
        // if it's an image but OCR isn't enabled, we don't support it
        if (contentType.StartsWith("image/") && !_lens.SupportsOCR)
        {
            supported = false;
        }
        // if it's audio or video but audio transcription isn't enabled, we don't support it
        if ((contentType.StartsWith("audio/") || contentType.StartsWith("video/")) && !_lens.SupportsAudioTranscription)
        {
            supported = false;
        }
        return new JsonResult(new
        {
            Supported = supported
        });
    }
}
