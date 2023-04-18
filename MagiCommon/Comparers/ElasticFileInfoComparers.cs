using MagiCommon.Extensions;
using MagiCommon.Models;
using System;
using System.Collections.Generic;
using System.Linq;

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

    public class FilePathComparer : IComparer<ElasticFileInfo>
    {
        public int Compare(ElasticFileInfo x, ElasticFileInfo y)
        {
            if (x is null && y is null)
            {
                return 0;
            }
            else if (x is null || y is null)
            {
                return string.Compare(x?.GetFullPath(), y?.GetFullPath(), StringComparison.OrdinalIgnoreCase);
            }
            // Normalize the paths to ensure proper comparison
            var xNormalized = x.GetFullPath();
            var yNormalized = y.GetFullPath();

            // Split the paths into their individual components
            var xParts = xNormalized.Split('/');
            var yParts = yNormalized.Split('/');

            // Compare the folder depths
            var depthComparisonResult = xParts.Length.CompareTo(yParts.Length);
            if (depthComparisonResult != 0)
            {
                return depthComparisonResult;
            }

            // Compare each part of the paths, except for the last part (filename)
            for (var i = 0; i < xParts.Length - 1; i++)
            {
                var comparisonResult = string.Compare(xParts[i], yParts[i], StringComparison.OrdinalIgnoreCase);
                if (comparisonResult != 0)
                {
                    // If the parts are not equal, return the comparison result
                    return comparisonResult;
                }
            }

            // If the folder hierarchy is the same, compare the filenames
            return string.Compare(xParts.Last(), yParts.Last(), StringComparison.OrdinalIgnoreCase);
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
