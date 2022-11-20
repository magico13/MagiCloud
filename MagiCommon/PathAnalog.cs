using System.Linq;

namespace MagiCommon
{
    public static class PathAnalog
    {
        public static string Combine(string path1, string path2)
        {
            if (string.Equals(path2, ".."))
            {
                // Go up a directory, ie chop off the last part of the path
                var newPath = string.Join('/', path1.Split('/')[..^1]);
                return !string.IsNullOrWhiteSpace(newPath) ? newPath : "/";
            }
            // This might need smart "relative" pathing logic if path2 is a whole path

            return path1.TrimEnd('/') + $"/{path2}";
        }

        public static string GetFileNameWithoutExtension(string path) 
            => path.Split('/', System.StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Split('.').FirstOrDefault();

        public static string GetExtension(string path)
            => path.Split('.').LastOrDefault();
    }
}
