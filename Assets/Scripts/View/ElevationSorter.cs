using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace IdleCloud.View
{
    /// Sets an actor's Sorting Layer to the elevation floor it stands on. Each floor is a tilemap
    /// assigned to that floor's Sorting Layer (e.g. "floor1_ground"); this copies the Sorting Layer
    /// of the topmost floor tilemap under the feet onto the actor. Depth WITHIN a floor (actor vs
    /// same-floor tiles/actors) is handled separately by the camera's Custom Axis (0,1,0) Y-sort.
    public class ElevationSorter : MonoBehaviour
    {
        [Tooltip("Transform whose cell decides the floor. Defaults to this transform (the feet).")]
        [SerializeField] private Transform anchor;

        private Grid _grid;
        private Tilemap[] _floors;   // topmost Sorting Layer first
        private int[] _layerIds;     // parallel: each floor tilemap's sortingLayerID
        private SortingGroup _group;
        private SpriteRenderer _renderer;
        private int _last = int.MinValue;

        void Awake()
        {
            if (anchor == null) anchor = transform;
            _group = GetComponentInChildren<SortingGroup>();
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _grid = FindFirstObjectByType<Grid>();
            if (_grid != null) CacheFloors();
            else Debug.LogWarning($"ElevationSorter on '{name}': no Grid found in scene; elevation-based sorting is disabled.");
        }

        private void CacheFloors()
        {
            _floors = _grid.GetComponentsInChildren<Tilemap>(false);
            // Topmost first: higher sorting-layer value (later in Tags & Layers) renders on top.
            System.Array.Sort(_floors, (a, b) => LayerValue(b).CompareTo(LayerValue(a)));
            _layerIds = new int[_floors.Length];
            for (int i = 0; i < _floors.Length; i++)
            {
                var r = _floors[i].GetComponent<TilemapRenderer>();
                _layerIds[i] = r != null ? r.sortingLayerID : 0;
            }
        }

        private static int LayerValue(Tilemap t)
        {
            var r = t.GetComponent<TilemapRenderer>();
            return r != null ? SortingLayer.GetLayerValueFromID(r.sortingLayerID) : int.MinValue;
        }

        void LateUpdate()
        {
            if (_grid == null || _floors == null || _floors.Length == 0) return;

            var cell = _grid.WorldToCell(anchor.position);
            cell.z = 0; // tiles are painted at z=0 (matches GridPathfinder)

            int layerId = _layerIds[_layerIds.Length - 1]; // off-map fallback: lowest floor
            for (int i = 0; i < _floors.Length; i++)        // topmost floor with a tile here wins
            {
                if (_floors[i].HasTile(cell))
                {
                    layerId = _layerIds[i];
                    break;
                }
            }

            if (layerId == _last) return;
            _last = layerId;

            if (_group != null) _group.sortingLayerID = layerId;
            else if (_renderer != null) _renderer.sortingLayerID = layerId;
        }
    }
}
