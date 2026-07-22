using UnityEngine;
using UnityEngine.Rendering;
using IdleCloud.View;

namespace Iso.Sorting
{
    /// Sorts the player against terrain by reusing the exact formula and IsoSortSettings asset
    /// TerrainBlockVisual applies to terrain blocks, via a SortingGroup on the player root. Reads
    /// PlayerController.LogicalPosition — the player's single 3D logical position — every frame;
    /// never infers height from terrain, sprite bounds, transform, or sortingOrder state.
    public class PlayerSortController : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField] private Grid grid;
        [SerializeField] private IsoSortSettings sortSettings;

        [Tooltip("Added to the actor's computed sort order so it wins the tie vs the floor tile of its own cell (same cell + height otherwise compute an identical order). Keep well under sortSettings.sortScale so it never crosses into a neighbouring cell's band.")]
        [SerializeField] private int sortingBias = 1;

        [Header("Debug")]
        [Tooltip("Live view of player.LogicalPosition (X/Y = ground world position, Z = height). Read-only, updates every frame in Play Mode.")]
        [SerializeField] private Vector3 debugLogicalPosition;
        [Tooltip("Live view of the ground cell derived from LogicalPosition.")]
        [SerializeField] private Vector2Int debugCell;
        [Tooltip("Live view of the sortingOrder currently applied to the SortingGroup.")]
        [SerializeField] private int debugSortingOrder;

        private int _lastOrder = int.MinValue;

        private void Reset()
        {
            player = GetComponent<PlayerController>();
            sortingGroup = GetComponentInChildren<SortingGroup>();
        }

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (sortingGroup == null) sortingGroup = GetComponentInChildren<SortingGroup>();
            if (grid == null) grid = FindFirstObjectByType<Grid>();
        }

        private void LateUpdate()
        {
            if (player == null || sortingGroup == null || grid == null || sortSettings == null) return;

            // The vector is the single source of truth: XY -> ground cell, Z -> height.
            Vector3 logical = player.LogicalPosition;
            Vector3Int cell = grid.WorldToCell(new Vector3(logical.x, logical.y, 0f));
            int height = Mathf.RoundToInt(logical.z);

            debugLogicalPosition = logical;
            debugCell = new Vector2Int(cell.x, cell.y);

            int order = IsoTerrainSortCalculator.CalculateSortingOrder(cell.x, cell.y, height, sortSettings) + sortingBias;
            debugSortingOrder = order;
            if (order == _lastOrder) return;
            _lastOrder = order;

            sortingGroup.sortingLayerName = sortSettings.terrainSortingLayerName;
            sortingGroup.sortingOrder = order;
        }
    }
}
