using System.IO;
using System.Threading.Tasks;

namespace Goggles
{
    public interface ILens
    {
        Task<string> ExtractTextAsync(Stream stream, string contentType);
    }
}
