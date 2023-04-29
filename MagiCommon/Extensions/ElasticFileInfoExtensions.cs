using MagiCommon.Models;
using System;
using System.Linq;

namespace MagiCommon.Extensions
{
    public static class ElasticFileInfoExtensions
    {
        /// <summary>
        /// Get the full path with the entire folder structure and extension.
        /// </summary>
        /// <param name="file">Source file</param>
        /// <returns>The full path with extension.</returns>
        public static string GetFullPath(this ElasticFileInfo file) 
            => GetDirectory(file).TrimEnd('/') + "/" + GetFileName(file);

        /// <summary>
        /// Get just the directory part of the path
        /// </summary>
        /// <param name="file">Source file</param>
        /// <returns>Directory portion of the path</returns>
        public static string GetDirectory(this ElasticFileInfo file)
        {
            var split = file.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 1)
            {
                // Lob off the filename part of the path
                return "/" + string.Join("/", split.Take(split.Length-1));
            }
            return "/";
        }

        /// <summary>
        /// Get the base file name plus extension without folder info.
        /// </summary>
        /// <param name="file">Source file</param>
        /// <returns>File name with extension</returns>
        public static string GetFileName(this ElasticFileInfo file)
        {
            var name = file.Name.Split('/').Last();
            return !string.IsNullOrWhiteSpace(file.Extension) ? $"{name}.{file.Extension}" : name;
        }

        /// <summary>
        /// Formats the URI to download the given file
        /// </summary>
        /// <param name="file">Source file</param>
        /// <param name="download">Boolean. Set true to download with pretty name. False when trying to view in browser.</param>
        /// <returns>Partial path to the file download API</returns>
        public static string GetFileContentUri(this ElasticFileInfo file, bool download = false) 
            => $"api/filecontent/{file.Id}" + (download ? $"?download={download}" : string.Empty);

        /// <summary>
        /// Create a "context" string for sending to OpenAI. This is a short representation of the file info
        /// </summary>
        /// <param name="file">Source file</param>
        /// <returns>Context string</returns>
        public static string ToContextString(this ElasticFileInfo file)
            => $"ID={file.Id},N={file.GetFullPath()},P={(file.IsPublic ? 1 : 0)},U={file.LastUpdated:yyyyMMddTHHmm},S={file.Size}";
    }
}
