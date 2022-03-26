using Goggles.TextExtraction;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Goggles
{
    public class Lens : ILens
    {
        private ILogger<Lens> Logger { get; }
        private IEnumerable<ITextExtractor> Extractors { get; }
        private GogglesConfiguration Config { get; }

        private static readonly FileExtensionContentTypeProvider _extensionTypeProvider 
            = new FileExtensionContentTypeProvider();

        public Lens(
            ILogger<Lens> logger,
            IEnumerable<ITextExtractor> extractors,
            IOptions<GogglesConfiguration> configuration)
        {
            Logger = logger;
            Extractors = extractors;
            Config = configuration.Value;
        }

        public static string DetermineContentType(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return string.Empty;
            }
            var extension = filename.Contains(".") 
                ? Path.GetExtension(filename).TrimStart('.') 
                : filename;

            switch (extension.ToLower())
            { //known overrides
                case "py": return "text/x-python";
                case "csv": return "text/csv";
                case "xcf": return "image/x-xcf";
                case "ofx":
                case "ino": return "text/plain";
                case "gcode": return "text/x-gcode";
            }
            if (!_extensionTypeProvider.TryGetContentType("file." + extension, out var type) 
                || string.IsNullOrEmpty(type))
            {
                type = "application/octet-stream";
            }
            return type;
        }

        public async Task<string> ExtractTextAsync(Stream stream, string contentType)
        {
            if (stream == null || stream == Stream.Null)
            {
                return null;
            }
            foreach (var extractor in Extractors)
            {
                if (extractor.IsValidForContentType(contentType)
                    && (Config.EnableOCR || !extractor.UsesOCR))
                {
                    Logger.LogInformation(
                        "Found suitable extractor {Class} for mimetype {MimeType}",
                        extractor.GetType(),
                        contentType);
                    try
                    {
                        string text = await extractor.ExtractTextAsync(stream);
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            Logger.LogInformation("Extraction did not fail using {Class} but returned empty string.", extractor.GetType());
                            // Basically a failure, allow trying a different extractor that will possibly work better
                        }
                        else
                        {
                            Logger.LogInformation("Text extraction complete. Length: {Count}", text.Length);
                            // If it succeeded, return the text. Otherwise maybe a later extractor in the list will work (eg PDF by text or by OCR)
                            var maxLength = Config.MaxTextLength;
                            if (maxLength > 0 && text.Length > maxLength)
                            {
                                Logger.LogInformation("Trimming text to {Count} characters.", maxLength);
                                return text.Substring(0, maxLength);
                            }
                            return text;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to extract text using extractor {Class}", extractor);
                    }
                }
            }
            return null;
        }
    }
}
