using MagiCommon.Extensions;
using MagiCommon.Models;
using System;
using System.Collections.Generic;

namespace MagiCommon.Comparers.ElasticFileInfoComparers
{
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
}
