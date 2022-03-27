using Microsoft.AspNetCore.StaticFiles;
using System.Collections.Generic;
using System.IO;

namespace Goggles
{
    internal class ContentTypeAnalyzer
    {
        private readonly static FileExtensionContentTypeProvider _extensionTypeProvider
            = new FileExtensionContentTypeProvider();

        protected static Dictionary<string, string> CustomExtensionMapping { get; } = new Dictionary<string, string>
        {
            // Text files
            ["py"] = "text/x-python",
            ["csv"] = "text/csv",
            ["ofx"] = "text/plain",
            ["ino"] = "text/plain",
            ["gcode"] = "text/x-gcode",
            // Image files
            ["xcf"] = "image/x-xcf"
        };

        internal static string DetermineContentType(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return string.Empty;
            }
            var extension = filename.Contains(".")
                ? Path.GetExtension(filename).TrimStart('.')
                : filename;

            if (CustomExtensionMapping.TryGetValue(extension.ToLower(), out var mapping))
            {
                return mapping;
            }
            if (!_extensionTypeProvider.TryGetContentType("file." + extension, out var type)
                || string.IsNullOrEmpty(type))
            {
                type = "application/octet-stream";
            }
            return type;
        }
    }
}
