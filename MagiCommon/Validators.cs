using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MagiCommon
{
    public static class Validators
    {
        public static bool? IsValidURI(string uri) => string.IsNullOrWhiteSpace(uri) ? null : (bool?)Uri.TryCreate(uri, UriKind.Absolute, out var _);
        public static bool? IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var invalidChars = Path.GetInvalidPathChars().ToList();
            invalidChars.AddRange(new List<char>() { '<', '>', ':', '"', '/', '|', '?', '*', '\\' });
            return fileName.IndexOfAny(invalidChars.ToArray()) < 0; // -1 means not found
        }

        public static bool? IsValidFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            var fileName = filePath.Split('/').Last();
            var validFileName = IsValidFileName(fileName);
            if (!validFileName.GetValueOrDefault())
            {
                return false;
            }

            var invalidChars = Path.GetInvalidPathChars().ToList();
            invalidChars.AddRange(new List<char>() { '<', '>', ':', '"', '|', '?', '*', '\\' });
            invalidChars.Remove('/'); // allow /
            return filePath.IndexOfAny(invalidChars.ToArray()) < 0; // -1 means not found
        }
    }
}
