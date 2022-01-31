using MagiCommon.Models;
using System.IO;

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
        {
            return Path.Combine(Path.GetDirectoryName(file.Name), GetFileName(file));
        }

        /// <summary>
        /// Get the base file name plus extension without folder info.
        /// </summary>
        /// <param name="file">Source file</param>
        /// <returns>File name with extension</returns>
        public static string GetFileName(this ElasticFileInfo file)
        {
            if (!string.IsNullOrWhiteSpace(file.Extension))
            {
                return $"{Path.GetFileName(file.Name)}.{file.Extension}";
            }
            else
            {
                return Path.GetFileName(file.Name);
            }
        }
    }
}
