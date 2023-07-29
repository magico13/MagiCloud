using System;

namespace MagiCommon.Models
{
    public class ElasticFileInfo : ElasticObject
    {
        public string MimeType { get; set; }
        public string Extension { get; set; }
        public long Size { get; set; }
        public DateTimeOffset LastModified { get; set; }
        public string Hash { get; set; }
        public string Text { get; set; }
    } 
}
