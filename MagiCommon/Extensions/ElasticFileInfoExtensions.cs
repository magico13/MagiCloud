using MagiCommon.Models;
using System.IO;

namespace MagiCommon.Extensions
{
    public static class ElasticFileInfoExtensions
    {
        public static string GetFullPath(this ElasticFileInfo file)
        {
            return Path.GetDirectoryName(file.Name) + "/" + GetFileName(file);
        }

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
