using System.IO;
using System.Threading.Tasks;

namespace Goggles.OCR;

public interface IOcrEngine
{
    Task<string> ExtractText(Stream stream, string filename, string contentType);
}
