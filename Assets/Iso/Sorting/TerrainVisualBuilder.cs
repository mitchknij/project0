using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using IdleCloud.View;

namespace Iso.Sorting
{
    [DefaultExecutionOrder(-100)]
    public sealed class TerrainVisualBuilder : MonoBehaviour, ITerrainHeightProvider
    {
        [Tooltip("Grid containing the source floor tilemaps (named 'Floor 0', 'Floor 1', ...) to bake into per-cell TerrainBlockVisual instances.")]
        [SerializeField] private Grid grid;
        [Tooltip("Prefab instantiated once per occupied tile cell to render that cell's terrain block.")]
        [SerializeField] private TerrainBlockVisual blockVisualPrefab;
        [Tooltip("Shared sort-scale/elevation-weight settings asset used to compute each spawned block's sorting order.")]
        [SerializeField] private IsoSortSettings sortSettings;
        [Tooltip("Parent transform for spawned TerrainBlockVisual instances. Auto-created as a child named 'TerrainVisualRoot' if left unassigned.")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("Disable the TilemapRenderer on each source floor tilemap after baking, so only the spawned per-cell visuals are drawn.")]
        [SerializeField] private bool disableSourceTilemapRenderers = true;
        private const string FloorPrefix = "Floor ";

        private readonly List<TerrainBlockVisual> spawned = new();
        private readonly Dictionary<Vector2Int, int> _heightByCell = new();

        private void Awake()
        {
            TerrainHeightService.Current = this;
        }

        private void Start()
        {
            Build();
        }

        [ContextMenu("Rebuild Terrain Visuals")]
        public void Build()
        {
            ClearSpawned();

            if (grid == null)
            {
                Debug.LogError("[TerrainVisualBuilder] Grid reference is missing.");
                return;
            }
            if (blockVisualPrefab == null)
            {
                Debug.LogError("[TerrainVisualBuilder] TerrainBlockVisual prefab is missing.");
                return;
            }
            if (sortSettings == null)
            {
                Debug.LogError("[TerrainVisualBuilder] IsoSortSettings asset is missing.");
                return;
            }
            if (visualRoot == null)
            {
                GameObject rootGo = new GameObject("TerrainVisualRoot");
                rootGo.transform.SetParent(transform, false);
                visualRoot = rootGo.transform;
            }

            Tilemap[] tilemaps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);

            foreach (Tilemap tilemap in tilemaps)
            {
                if (!TryParseFloorIndex(tilemap.name, out int floorIndex)) continue;

                BoundsInt bounds = tilemap.cellBounds;
                foreach (Vector3Int cell in bounds.allPositionsWithin)
                {
                    if (!tilemap.HasTile(cell)) continue;

                    Sprite sprite = tilemap.GetSprite(cell);
                    // tileAnchor is in cell space; interpolate it through the grid layout
                    // so the sprite pivot lands exactly where TilemapRenderer anchors it.
                    Vector3 world = tilemap.LocalToWorld(tilemap.CellToLocalInterpolated(cell + tilemap.tileAnchor));

                    TerrainBlock block = new TerrainBlock(
                        groundX: cell.x,
                        groundY: cell.y,
                        height: floorIndex,
                        sprite: sprite,
                        worldPosition: world);

                    var cellKey = new Vector2Int(block.groundX, block.groundY);
                    if (!_heightByCell.TryGetValue(cellKey, out int existingHeight) || block.height > existingHeight)
                        _heightByCell[cellKey] = block.height;

                    TerrainBlockVisual visual = Instantiate(blockVisualPrefab, visualRoot);
                    visual.name = $"Block_{block.groundX}_{block.groundY}_{block.height}";
                    visual.Configure(block, sortSettings);
                    spawned.Add(visual);

                }

                if (disableSourceTilemapRenderers)
                {
                    TilemapRenderer tr = tilemap.GetComponent<TilemapRenderer>();
                    if (tr != null) tr.enabled = false;
                }
            }
        }

        private void ClearSpawned()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    if (Application.isPlaying) Destroy(spawned[i].gameObject);
                    else DestroyImmediate(spawned[i].gameObject);
                }
            }
            spawned.Clear();
            _heightByCell.Clear();
        }

        /// <summary>Highest floorIndex baked for ground cell (x, y) by the last Build(), if any.</summary>
        public bool TryGetHeight(int x, int y, out int height) => _heightByCell.TryGetValue(new Vector2Int(x, y), out height);

        private static bool TryParseFloorIndex(string tilemapName, out int floorIndex)
        {
            floorIndex = 0;
            if (string.IsNullOrEmpty(tilemapName)) return false;
            if (!tilemapName.StartsWith(FloorPrefix)) return false;
            string tail = tilemapName.Substring(FloorPrefix.Length).Trim();
            return int.TryParse(tail, out floorIndex);
        }

        // TODO: chunking / pooling / off-screen culling.
        // TODO: incremental updates when Floor N Tilemap tiles are edited.
        // TODO: hook actors/props/vegetation/buildings into the same IsoSortSettings and IsoTerrainSortCalculator.
        // TODO: multi-cell object footprint sorting.
        // TODO: median occupied-cell sorting for large objects.
        // TODO: weighted Y-axis sorting for large objects.
        // TODO: runtime horizontal slicing of tall objects.
    }
}
