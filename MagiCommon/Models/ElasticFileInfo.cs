using System;
using System.Collections.Generic;
using System.IO;

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
        public string UserId { get; set; }

        public class NameComparer : IComparer<ElasticFileInfo>
        {
            public int Compare(ElasticFileInfo x, ElasticFileInfo y)
            {
                if (x is null && y is null)
                {
                    return 0;
                }
                return string.Compare(x?.GetFullPath(), y?.GetFullPath(), StringComparison.OrdinalIgnoreCase);
            }
        }

        public class SizeComparer : IComparer<ElasticFileInfo>
        {
            public int Compare(ElasticFileInfo x, ElasticFileInfo y)
            {
                if (x is null && y is null)
                {
                    return 0;
                }
                return x?.Size.CompareTo(y?.Size) ?? 0;
            }
        }

        public string GetFullPath()
        {
            return Path.GetDirectoryName(Name) + "/" + GetFileName();
        }

        public string GetFileName()
        {
            if (!string.IsNullOrWhiteSpace(Extension))
            {
                return Path.GetFileName(Name) + "." + Extension;
            }
            else
            {
                return Path.GetFileName(Name);
            }
        }
    } 
}
