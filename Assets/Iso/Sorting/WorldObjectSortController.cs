using UnityEngine;
using UnityEngine.Rendering;

namespace Iso.Sorting
{
    /// Sorts a statically-placed world object (e.g. a tree) against terrain by reusing the exact same
    /// formula and IsoSortSettings asset TerrainBlockVisual and PlayerSortController apply, via a
    /// SortingGroup on the object's root. Ground cell X/Y are derived automatically from
    /// transform.position via Grid.WorldToCell every recompute (same conversion PlayerSortController uses
    /// for the player) — only floorIndex is manually set in the Inspector, since elevation/floor placement
    /// isn't recoverable from the object's flat world position.
    ///
    /// The root Transform is kept grid-locked: every recompute snaps transform.position.x/y to the exact
    /// centre of whichever cell it's nearest to (Grid.GetCellCenterWorld) and mirrors floorIndex onto
    /// transform.position.z, so the Inspector never shows stray sub-cell decimals from free-hand dragging.
    /// floorIndex itself remains the single source of truth for sorting/height — the Z mirror is display
    /// only and gets overwritten by floorIndex on the next recompute if hand-edited.
    [ExecuteAlways]
    [RequireComponent(typeof(SortingGroup))]
    public class WorldObjectSortController : MonoBehaviour
    {
        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField] private Grid grid;
        [SerializeField] private IsoSortSettings sortSettings;

        [Header("Placement")]
        [Tooltip("Floor/elevation index used for sorting. Set manually — not recoverable from transform.position.")]
        [SerializeField] private int floorIndex;

        [Tooltip("Added to the computed sort order for manual tie-breaking against terrain/actors sharing the same cell and height. Keep well under sortSettings.sortScale so it never crosses into a neighbouring cell's band.")]
        [SerializeField] private int sortingBias = 0;

        [Header("Debug")]
        [Tooltip("Live view of the ground cell derived from transform.position via Grid.WorldToCell. Read-only.")]
        [SerializeField] private Vector2Int debugCell;
        [Tooltip("Live view of the raw sort key computed from the derived cell and floorIndex. Read-only.")]
        [SerializeField] private float debugSortKey;
        [Tooltip("Live view of the sortingOrder currently applied to the SortingGroup. Read-only.")]
        [SerializeField] private int debugSortingOrder;

        private int _lastOrder = int.MinValue;

        private void Reset()
        {
            sortingGroup = GetComponentInChildren<SortingGroup>();
            grid = FindFirstObjectByType<Grid>();
        }

        private void Awake()
        {
            if (sortingGroup == null) sortingGroup = GetComponentInChildren<SortingGroup>();
            if (grid == null) grid = FindFirstObjectByType<Grid>();
            Recompute();
        }

        private void OnValidate()
        {
            if (sortingGroup == null) sortingGroup = GetComponentInChildren<SortingGroup>();
            if (grid == null) grid = FindFirstObjectByType<Grid>();
            Recompute();
        }

        private void LateUpdate()
        {
            Recompute();
        }

        private const float SnapEpsilonSqr = 1e-8f;

        private void Recompute()
        {
            if (sortingGroup == null || grid == null || sortSettings == null) return;

            Vector3Int cell = grid.WorldToCell(transform.position);
            Vector3 snappedCenter = grid.GetCellCenterWorld(cell);
            Vector3 gridLockedPosition = new Vector3(snappedCenter.x, snappedCenter.y, floorIndex);
            if ((transform.position - gridLockedPosition).sqrMagnitude > SnapEpsilonSqr)
                transform.position = gridLockedPosition;

            debugCell = new Vector2Int(cell.x, cell.y);

            debugSortKey = IsoTerrainSortCalculator.CalculateSortKey(cell.x, cell.y, floorIndex, sortSettings);
            int order = IsoTerrainSortCalculator.CalculateSortingOrder(cell.x, cell.y, floorIndex, sortSettings) + sortingBias;
            debugSortingOrder = order;
            if (order == _lastOrder) return;
            _lastOrder = order;

            sortingGroup.sortingLayerName = sortSettings.terrainSortingLayerName;
            sortingGroup.sortingOrder = order;
        }
    }
}
