using System.Collections.Generic;
using UnityEngine;

namespace Iso.Sorting
{
    public static class IsoTerrainSortCalculator
    {
        // groundDepth = average( -(groundX + groundY) ) over occupied cells (1 cell for a single terrain block;
        // ready for multi-cell object footprints later).
        // heightDepth = height * elevationSortWeight
        // sortKey     = groundDepth + heightDepth

        /// <summary>Ground-plane depth for a single cell. Equivalent to CalculateGroundDepth with one cell.</summary>
        public static float CalculateGroundDepth(int groundX, int groundY)
        {
            return (groundX + groundY) * -1f;
        }

        /// <summary>Ground-plane depth averaged over multiple occupied cells (multi-cell object footprints).</summary>
        public static float CalculateGroundDepth(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < cells.Count; i++)
            {
                sum += CalculateGroundDepth(cells[i].x, cells[i].y);
            }
            return sum / cells.Count;
        }

        public static float CalculateSortKey(int groundX, int groundY, int height, IsoSortSettings settings)
        {
            float groundDepth = CalculateGroundDepth(groundX, groundY);
            float heightDepth = height * settings.elevationSortWeight;
            return groundDepth + heightDepth;
        }

        public static int CalculateSortingOrder(int groundX, int groundY, int height, IsoSortSettings settings)
        {
            float sortKey = CalculateSortKey(groundX, groundY, height, settings);
            int baseOrder = Mathf.RoundToInt(sortKey * settings.sortScale) + settings.baseSortingOffset;
            int finalOrder = baseOrder + height; // small deterministic tie-breaker
            if (settings.invertSortSign) finalOrder = -finalOrder;
            return finalOrder;
        }
    }
}
