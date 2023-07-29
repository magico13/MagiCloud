using MagiCommon.Models;

namespace MagiCommon.Extensions
{
    public static class ElasticFileInfoExtensions
    {
        /// <summary>
        /// Get the base file name plus extension without folder info.
        /// </summary>
        /// <param name="file">Source file</param>
        /// <returns>File name with extension</returns>
        public static string GetFileName(this ElasticFileInfo file) 
            => !string.IsNullOrWhiteSpace(file.Extension) ? $"{file.Name}.{file.Extension}" : file.Name;

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
            => $"ID={file.Id},N={file.GetFileName()},P={(file.IsPublic ? 1 : 0)},U={file.LastUpdated:yyyyMMddTHHmm},S={file.Size}";
    }
}
