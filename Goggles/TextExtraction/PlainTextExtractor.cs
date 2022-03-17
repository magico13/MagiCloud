using System.IO;
using System.Threading.Tasks;

namespace Goggles.TextExtraction
{
    public class PlainTextExtractor : ITextExtractor
    {
        public bool IsValidForContentType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            { 
                return false; 
            }
            return contentType.StartsWith("text/") 
                || contentType.StartsWith("message/")
                || contentType == "application/json";
        }

        public bool UsesOCR => false;

        public async Task<string> ExtractTextAsync(Stream stream)
        {
            // Plain text, so we just extract the content as-is
            // Maybe filter out extra whitespace?
            var streamReader = new StreamReader(stream);
            return await streamReader.ReadToEndAsync();
        }
    }
}
