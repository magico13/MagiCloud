using MagiCommon.Models;
using System;
using System.Collections.Generic;

namespace MagiCommon.Comparers.ElasticFileInfoComparers
{
    public class NameComparer : IComparer<ElasticObject>
    {
        public int Compare(ElasticObject x, ElasticObject y)
        {
            if (x is null && y is null)
            {
                return 0;
            }
            return string.Compare(x?.Name, y?.Name, StringComparison.OrdinalIgnoreCase);
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
