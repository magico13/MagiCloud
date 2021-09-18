using System;

namespace MagiCommon.Models
{
    public class ElasticFileInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string MimeType { get; set; }
        public string Extension { get; set; }
        public long Size { get; set; }
        public DateTimeOffset LastModified { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public string Hash { get; set; }
        public string FileText { get; set; }
    }
}
