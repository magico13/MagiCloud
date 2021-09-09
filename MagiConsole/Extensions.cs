using MagiCommon.Models;

namespace MagiConsole
{
    public static class Extensions
    {
        public static ElasticFileInfo ToElasticFileInfo(this FileData fileData)
        {
            return new ElasticFileInfo
            {
                Id = fileData.Id,
                Name = fileData.Name,
                Extension = fileData.Extension,
                LastModified = fileData.LastModified,
                Hash = fileData.Hash
            };
        }

        public static FileData ToFileData(this ElasticFileInfo fileInfo)
        {
            return new FileData
            {
                Id = fileInfo.Id,
                Name = fileInfo.Name,
                Extension = fileInfo.Extension,
                LastModified = fileInfo.LastModified,
                Hash = fileInfo.Hash
            };
        }
    }
}
