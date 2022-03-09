using System.IO;
using System.Threading.Tasks;

namespace MagiCloud.TextExtraction
{
    public class PlainTextExtractor : ITextExtractor
    {
        public bool IsValidForMimeType(string mimeType)
            => mimeType?.StartsWith("text/") == true;

        public async Task<string> ExtractTextAsync(Stream stream)
        {
            // Plain text, so we just extract the content as-is
            // Maybe filter out extra whitespace?
            var streamReader = new StreamReader(stream);
            return await streamReader.ReadToEndAsync();
        }
    }
}
