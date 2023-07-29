using Microsoft.AspNetCore.StaticFiles;
using System.Collections.Generic;
using System.Linq;

namespace Goggles;

internal class ContentTypeAnalyzer
{
    private readonly static FileExtensionContentTypeProvider _extensionTypeProvider = new();

    internal static Dictionary<string, string> CustomExtensionMapping { get; } = new Dictionary<string, string>
    {
        // Text files
        ["csv"] = "text/csv",
        ["gcode"] = "text/x-gcode",
        ["ini"] = "text/plain",
        ["ino"] = "text/plain",
        ["log"] = "text/plain",
        ["ofx"] = "text/plain",
        ["py"] = "text/x-python",
        ["yaml"] = "text/x-yaml",
        ["yml"] = "text/x-yaml",
        // Image files
        ["xcf"] = "image/x-xcf",
        // "Application" types
        ["msg"] = "application/vnd.ms-outlook",
        ["odp"] = "application/vnd.oasis.opendocument.presentation",
        ["ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        ["odt"] = "application/vnd.oasis.opendocument.text",
        ["sql"] = "application/sql",
        ["url"] = "application/internet-shortcut"
    };

    internal static string DetermineContentType(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return string.Empty;
        }
        var extension = filename.Split('.').LastOrDefault() ?? filename;

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

    internal static string DetermineExtension(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }
        var customMappedExt = CustomExtensionMapping
            .FirstOrDefault(x => string.Equals(x.Value, contentType, System.StringComparison.OrdinalIgnoreCase)).Key;

        if (!string.IsNullOrWhiteSpace(customMappedExt))
        {
            return customMappedExt;
        }

        var extension = _extensionTypeProvider.Mappings
            .FirstOrDefault(x => string.Equals(x.Value, contentType, System.StringComparison.OrdinalIgnoreCase)).Key;
        return extension?.TrimStart('.');
    }
}