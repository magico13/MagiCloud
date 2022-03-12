using System.IO;
using System.Threading.Tasks;

namespace MagiCloud.OCR
{
    public interface IOcrEngine
    {
        Task<string> OcrStreamAsync(Stream stream);
    }
}
