using System.IO;
using System.Threading.Tasks;

namespace Goggles.Transcription;
public interface ITranscriptionService
{
    public Task<string> TranscribeStreamAsync(Stream stream, string filename, string contentType);
}
