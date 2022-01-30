using System.IO;
using System.Threading.Tasks;

namespace MagiCloud.DataManager
{
    public interface IDataManager
    {
        bool FileExists(string id);
        Stream GetFile(string id);
        Task WriteFileAsync(string id, Stream file);
        Task WriteFilePartAsync(string id, Stream file);
        void DeleteFile(string id);

    }
}
