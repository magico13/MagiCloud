using System.IO;
using System.Threading.Tasks;

namespace Goggles.OCR;

public interface IOcrEngine
{
    Task<OcrResult> ExtractText(Stream stream, string filename, string contentType);
}
