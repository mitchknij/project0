using System.Collections.Generic;
using IdleCloud.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleCloud.View
{
    public interface ITerrainHeightProvider
    {
        bool TryGetHeight(int x, int y, out int height);
    }

    public static class TerrainHeightService
    {
        public static ITerrainHeightProvider Current;
    }

    [DefaultExecutionOrder(-10)]
    public class GridPathfinder : MonoBehaviour
    {
        [Tooltip("Safety cap on A* search steps per FindPath call. Aborts the search (returning no path) if exceeded, rather than hanging on an unreachable or very large map.")]
        public int maxIterations = 800;
        [Tooltip("Max |height delta| allowed between adjacent cells in a single pathfinding step. Blocks walking straight up/down a cliff face taller than this; bigger height changes need a ramp/stairs path instead.")]
        public int maxHeightStepPerMove = 1;
        [Tooltip("Cost of a climb move (skips one covered cliff-face cell to reach a ledge exactly ±1 level up/down). Spans 2 cells, so 2 keeps A* admissible relative to two normal 1-cost steps.")]
        public float climbCost = 2f;
        [Tooltip("Debug: logs IsStandable/height for the resolved start/end cells and a fixed probe set on every FindPath call. Off by default; enable to verify coverage exclusion in the Console.")]
        public bool logCoverage = false;
        private Grid _grid;
        private Tilemap[] _tilemaps;

        // Key = (cellX, cellY). 
        private readonly Dictionary<Vector2Int, Column> _columns = new();

        private static readonly Vector2Int[] Neighbors8 =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new(1, 1), new(-1, 1), new(1, -1), new(-1, -1),
        };

        // 2-cell climb offsets (all 8 directions): skip the single covered face cell between
        // current and nb to reach a ledge exactly one level up/down. Diagonals mirror Neighbors8.
        // See the climb loop in FindPath for the standability/height checks that gate a climb.
        private static readonly Vector2Int[] ClimbNeighbors =
        {
            new(2, 0), new(-2, 0), new(0, 2), new(0, -2),
            new(2, 2), new(-2, 2), new(2, -2), new(-2, -2),
        };

        void Awake()
        {
            _grid = GetComponent<Grid>();
            if (_grid == null) _grid = FindFirstObjectByType<Grid>();

            if (_grid == null)
            {
                Debug.LogWarning("GridPathfinder: no Grid found in scene; pathfinding disabled.");
                _tilemaps = System.Array.Empty<Tilemap>();
                return;
            }

            _tilemaps = _grid.GetComponentsInChildren<Tilemap>(includeInactive: false);
            BakeColumns();
        }

        private void BakeColumns()
        {
            _columns.Clear();
            foreach (var tilemap in _tilemaps)
            {
                var bounds = tilemap.cellBounds;
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                    for (int y = bounds.yMin; y < bounds.yMax; y++)
                    {
                        var cell = new Vector3Int(x, y, 0);
                        if (!tilemap.HasTile(cell)) continue;

                        var key = new Vector2Int(x, y);
                        // A painted floor tile means this cell is walkable ground. (Previously
                        // gated on ColliderType.None, but every block tile carries a Sprite/Grid
                        // collider, so no cell was ever walkable.)
                        _columns[key] = new Column { walkable = true };
                    }
            }
        }

        public bool IsWalkable(Vector3 world)
        {
            if (_grid == null) return false;
            return _columns.TryGetValue(ToCellXY(world), out var col) && col.walkable;
        }

        private Vector2Int ToCellXY(Vector3 world)
        {
            var c = _grid.WorldToCell(world);
            return new Vector2Int(c.x, c.y);
        }

        // Reuses the terrain builder's own baked height data (via TerrainHeightService) instead of
        // maintaining a second height source; defaults to 0 for cells with no registered/baked height.
        private static int GetHeight(Vector2Int cell)
        {
            int height = 0;
            TerrainHeightService.Current?.TryGetHeight(cell.x, cell.y, out height);
            return height;
        }

        // A cell is standable only if it has a tile AND is not buried under a taller block whose
        // body renders directly above it on screen. In this isometric grid (cellLayout: Isometric),
        // the neighbour (x+1, y+1) shares the same world X and sits one cell-height higher in world
        // Y, so a taller block there visually overlaps/covers this cell. Flat ground => neighbour
        // height == this height => never covered (no-op). Queried live (not baked) since terrain
        // height data isn't populated until TerrainVisualBuilder.Start(), which runs before any
        // click can reach this pathfinder (see execution-order note on TryGetHeightAt below).
        private bool IsStandable(Vector2Int cell)
        {
            if (!_columns.TryGetValue(cell, out var col) || !col.walkable) return false;
            return GetHeight(new Vector2Int(cell.x + 1, cell.y + 1)) <= GetHeight(cell);
        }

        // Lets callers resolve a live terrain height for an arbitrary world position (e.g. an actor's
        // spawn point) without duplicating the world->cell->height lookup chain themselves.
        public bool TryGetHeightAt(Vector3 world, out int height)
        {
            height = 0;
            if (_grid == null || TerrainHeightService.Current == null) return false;
            Vector2Int cell = ToCellXY(world);
            return TerrainHeightService.Current.TryGetHeight(cell.x, cell.y, out height);
        }

        public bool TryGetCell(Vector3 world, out Vector2Int cell)
        {
            cell = default;
            if (_grid == null) return false;
            cell = ToCellXY(world);
            return true;
        }

        /// <summary>Converts an authoritative Core tile and floor through the same Grid seam used by path waypoints.</summary>
        public bool TryGetTileWorldPosition(CombatTileCoordinate tile, int floor, out Vector3 world)
        {
            world = default;
            if (_grid == null) return false;
            world = _grid.GetCellCenterWorld(new Vector3Int(tile.X, tile.Y, 0));
            world.z = floor;
            return true;
        }

        public int CellDistance(Vector3 a, Vector3 b)
        {
            if (!TryGetCell(a, out Vector2Int cellA) || !TryGetCell(b, out Vector2Int cellB))
                return int.MaxValue;

            return Mathf.Max(Mathf.Abs(cellA.x - cellB.x), Mathf.Abs(cellA.y - cellB.y));
        }

        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            if (_grid == null || TerrainHeightService.Current == null) return true;
            if (!TryGetCell(from, out Vector2Int fromCell) || !TryGetCell(to, out Vector2Int toCell)) return true;

            int dx = Mathf.Abs(toCell.x - fromCell.x);
            int dy = Mathf.Abs(toCell.y - fromCell.y);
            if (Mathf.Max(dx, dy) <= 1) return true; // same or adjacent cell: no intermediate cells

            int stepX = toCell.x > fromCell.x ? 1 : -1;
            int stepY = toCell.y > fromCell.y ? 1 : -1;
            int maxHeight = Mathf.Max(GetHeight(fromCell), GetHeight(toCell));
            int x = fromCell.x;
            int y = fromCell.y;
            int ix = 0;
            int iy = 0;

            // Supercover traversal: visits every cell the ideal segment passes through, by
            // comparing the segment's next X vs Y grid-boundary crossing times
            // ((ix + 0.5) / dx vs (iy + 0.5) / dy, cross-multiplied to stay integer). Unlike
            // plain Bresenham this is direction-independent — sight facts must be symmetric.
            while (ix < dx || iy < dy)
            {
                long crossing = (long)(1 + 2 * ix) * dy - (long)(1 + 2 * iy) * dx;
                if (crossing == 0)
                {
                    // Exact corner crossing: the segment touches both side cells; check both
                    // so reversing from/to can never change the result.
                    var sideA = new Vector2Int(x + stepX, y);
                    var sideB = new Vector2Int(x, y + stepY);
                    if (sideA != toCell && GetHeight(sideA) > maxHeight) return false;
                    if (sideB != toCell && GetHeight(sideB) > maxHeight) return false;
                    x += stepX; ix++;
                    y += stepY; iy++;
                }
                else if (crossing < 0)
                {
                    x += stepX; ix++;
                }
                else
                {
                    y += stepY; iy++;
                }

                if (x == toCell.x && y == toCell.y) return true;
                if (GetHeight(new Vector2Int(x, y)) > maxHeight) return false;
            }

            return true;
        }

        // The cell itself if walkable, else the closest walkable cell by Manhattan distance.
        // Sentinel (int.MinValue, ...) when no walkable cell exists.
        private Vector2Int NearestWalkable(Vector2Int cell)
        {
            if (IsStandable(cell)) return cell;

            Vector2Int best = new Vector2Int(int.MinValue, int.MinValue);
            int bestDist = int.MaxValue;
            foreach (var kv in _columns)
            {
                if (!IsStandable(kv.Key)) continue;
                int d = Mathf.Abs(kv.Key.x - cell.x) + Mathf.Abs(kv.Key.y - cell.y);
                if (d < bestDist) { bestDist = d; best = kv.Key; }
            }
            return best;
        }

        public List<Vector3> FindPath(Vector3 from, Vector3 to)
            => FindPath(from, to, null);

        /// <summary>
        /// Finds a path while allowing callers to exclude dynamic actor occupancy. The predicate
        /// receives candidate cells and must return true when that cell is currently blocked.
        /// </summary>
        public List<Vector3> FindPath(Vector3 from, Vector3 to, System.Func<Vector2Int, bool> isDynamicallyBlocked)
        {
            if (_grid == null || _columns.Count == 0) return new List<Vector3>();

            Vector2Int startXY = NearestWalkable(ToCellXY(from));
            // Snap off-floor clicks (gaps, edges, background) to the nearest reachable tile instead
            // of dropping them — a click should always produce a path when any floor exists.
            Vector2Int endXY = NearestWalkable(ToCellXY(to));

            if (startXY.x == int.MinValue) return new List<Vector3>(); // no walkable ground at all
            if (endXY.x == int.MinValue) return new List<Vector3>(); // no walkable ground anywhere

            if (logCoverage) LogCoverageDebug(startXY, endXY);

            var open = new Dictionary<Vector2Int, Node>();
            var closed = new HashSet<Vector2Int>();

            open[startXY] = new Node(startXY, null, 0f, Heuristic(startXY, endXY));

            int iterations = 0;
            while (open.Count > 0 && iterations++ < maxIterations)
            {
                var current = LowestF(open);
                if (current.XY == endXY) return BuildPath(current);

                open.Remove(current.XY);
                closed.Add(current.XY);

                foreach (var dir in Neighbors8)
                {
                    var nb = current.XY + dir;
                    if (closed.Contains(nb) || !IsStandable(nb) ||
                        (nb != endXY && isDynamicallyBlocked != null && isDynamicallyBlocked(nb))) continue;
                    if (Mathf.Abs(GetHeight(nb) - GetHeight(current.XY)) > maxHeightStepPerMove) continue;

                    float g = current.G + (Mathf.Abs(dir.x) + Mathf.Abs(dir.y) > 1 ? 1.414f : 1f);
                    Relax(open, current, nb, g, endXY);
                }

                // Climb moves: step over the single covered face cell between current and nb
                // to reach a ledge exactly one level up/down (e.g. a cliff face that Neighbors8
                // + IsStandable's coverage exclusion would otherwise wall off entirely — see
                // docs/STATE.md 2026-07-10 "player-can-climb" entry).
                foreach (var dir in ClimbNeighbors)
                {
                    var nb = current.XY + dir;          // the ledge we'd climb onto
                    var mid = current.XY + dir / 2;     // the single face cell we skip over
                    if (closed.Contains(nb) || !IsStandable(nb) ||
                        (nb != endXY && isDynamicallyBlocked != null && isDynamicallyBlocked(nb))) continue;
                    // mid must be a solid covered cliff face: a real column that IsStandable
                    // rejects (buried behind the plateau) — not a gap (no column at all) and
                    // not itself standable (that would just be a normal 1-cell step already
                    // handled by Neighbors8 above).
                    if (!_columns.ContainsKey(mid) || IsStandable(mid)) continue;
                    if (Mathf.Abs(GetHeight(nb) - GetHeight(current.XY)) != 1) continue;

                    // Cardinal offsets sum |dx|+|dy|=2, diagonal offsets sum=4 — mirrors the
                    // 1f/1.414f split Neighbors8 uses for normal moves, scaled by climbCost
                    // since a climb spans 2 cells instead of 1.
                    float g = current.G + climbCost * (Mathf.Abs(dir.x) + Mathf.Abs(dir.y) > 2 ? 1.414f : 1f);
                    Relax(open, current, nb, g, endXY);
                }
            }
            return new List<Vector3>();
        }

        private static void Relax(Dictionary<Vector2Int, Node> open, Node current, Vector2Int nb, float g, Vector2Int endXY)
        {
            if (open.TryGetValue(nb, out var ex) && ex.G <= g) return;
            open[nb] = new Node(nb, current, g, Heuristic(nb, endXY));
        }

        // Fixed probe set plus the resolved start/end cells, so the coverage exclusion (IsStandable)
        // introduced for the (12,4,1)->(14,5,2) bug is visible in the Console without attaching a
        // debugger. logCoverage-gated only; no effect on pathfinding.
        private void LogCoverageDebug(Vector2Int startXY, Vector2Int endXY)
        {
            var probes = new List<Vector2Int> { startXY, endXY, new(12, 4), new(13, 4), new(13, 5), new(14, 5) };
            foreach (var cell in probes)
            {
                Debug.Log($"[GridPathfinder] cell=({cell.x},{cell.y}) height={GetHeight(cell)} standable={IsStandable(cell)}");
            }
        }

        private List<Vector3> BuildPath(Node endNode)
        {
            var nodes = new List<Node>();
            for (var n = endNode; n != null; n = n.Parent) nodes.Add(n);
            nodes.Reverse();

            var path = new List<Vector3>(nodes.Count);
            foreach (var n in nodes)
            {
                Vector3 world = _grid.GetCellCenterWorld(new Vector3Int(n.XY.x, n.XY.y, 0));
                int height = 0;
                TerrainHeightService.Current?.TryGetHeight(n.XY.x, n.XY.y, out height);
                world.z = height;
                path.Add(world);
            }
            return path;
        }

        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return dx + dy - 0.586f * Mathf.Min(dx, dy);
        }

        private static Node LowestF(Dictionary<Vector2Int, Node> open)
        {
            Node best = null;
            foreach (var n in open.Values) if (best == null || n.F < best.F) best = n;
            return best;
        }

        private struct Column { public bool walkable; }

        private sealed class Node
        {
            public readonly Vector2Int XY;
            public readonly Node Parent;
            public readonly float G, H;
            public float F => G + H;
            public Node(Vector2Int xy, Node parent, float g, float h)
            { XY = xy; Parent = parent; G = g; H = h; }
        }
    }
}
