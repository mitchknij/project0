using UnityEngine;
using UnityEngine.Rendering;
using IdleCloud.View;

namespace Iso.Sorting
{
    /// Sorts a ground-loot bag against terrain using the same logical-position bridge as enemies.
    public class LootBagSortController : MonoBehaviour
    {
        [SerializeField] private LootBagView lootBag;
        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField] private Grid grid;
        [SerializeField] private IsoSortSettings sortSettings;

        [Tooltip("Added to the bag's computed sort order so it wins the tie vs the floor tile of its own cell.")]
        [SerializeField] private int sortingBias = 1;

        [Header("Debug")]
        [SerializeField] private Vector3 debugLogicalPosition;
        [SerializeField] private Vector2Int debugCell;
        [SerializeField] private int debugSortingOrder;

        private int _lastOrder = int.MinValue;

        private void Reset()
        {
            lootBag = GetComponent<LootBagView>();
            sortingGroup = GetComponentInChildren<SortingGroup>();
        }

        private void Awake()
        {
            if (lootBag == null) lootBag = GetComponent<LootBagView>();
            if (sortingGroup == null) sortingGroup = GetComponentInChildren<SortingGroup>();
            if (grid == null) grid = FindFirstObjectByType<Grid>();
        }

        private void LateUpdate()
        {
            if (lootBag == null || sortingGroup == null || grid == null || sortSettings == null) return;

            Vector3 logical = lootBag.LogicalPosition;
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
