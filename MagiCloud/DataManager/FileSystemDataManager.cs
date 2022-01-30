using System.IO;
using System.Threading.Tasks;

namespace MagiCloud.DataManager
{
    public class FileSystemDataManager : IDataManager
    {
        private const string ROOT = "data";

        public void DeleteFile(string id)
        {
            var path = GetPath(id);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public bool FileExists(string id)
        {
            return File.Exists(GetPath(id));
        }

        public Stream GetFile(string id)
        {
            var path = GetPath(id);
            if (File.Exists(path))
            {
                return File.OpenRead(path);
            }
            return Stream.Null;
        }

        public async Task WriteFileAsync(string id, Stream file)
        {
            Directory.CreateDirectory(ROOT);
            var path = GetPath(id);
            using var filestream = File.Create(path);
            file.Seek(0, SeekOrigin.Begin);
            await file.CopyToAsync(filestream);
        }

        public async Task WriteFilePartAsync(string id, Stream filePart)
        {
            Directory.CreateDirectory(ROOT);
            var path = GetPath(id);
            using var filestream = File.OpenWrite(path);
            filestream.Seek(0, SeekOrigin.End);
            filePart.Seek(0, SeekOrigin.Begin);
            await filePart.CopyToAsync(filestream);
        }

        private string GetPath(string id)
        {
            return Path.Combine(ROOT, id);
        }
    }
}
