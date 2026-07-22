using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    /// <summary>
    /// Resolves deterministic, unrotated tile patterns and their authoritative occupants.
    /// Tile order is anchor-first; Cross uses North/East/South/West rings, while
    /// SquareRadius uses a negative-Y to positive-Y row-major scan.
    /// </summary>
    public static class TilePatternResolver
    {
        public static List<CombatTileCoordinate> ResolveTiles(
            CombatTileCoordinate anchor,
            TilePatternDef pattern)
        {
            var tiles = new List<CombatTileCoordinate>();
            if (pattern == null) return tiles;

            switch (pattern.PatternKind)
            {
                case TilePatternKind.SingleTile:
                    AddUnique(tiles, anchor);
                    break;
                case TilePatternKind.Cross:
                    AddUnique(tiles, anchor);
                    AddCrossRings(tiles, anchor, pattern.Size);
                    break;
                case TilePatternKind.SquareRadius:
                    AddUnique(tiles, anchor);
                    AddSquareRows(tiles, anchor, pattern.Size);
                    break;
                case TilePatternKind.CustomOffsets:
                    foreach (CombatTileCoordinate offset in pattern.CustomOffsets ??
                             new List<CombatTileCoordinate>())
                    {
                        if (TryOffset(anchor, offset, out CombatTileCoordinate tile))
                            AddUnique(tiles, tile);
                    }
                    break;
            }

            return tiles;
        }

        public static List<CombatSpatialSnapshot> ResolveActors(
            IReadOnlyList<CombatTileCoordinate> tiles,
            int anchorFloor,
            CombatFaction targetFaction,
            IReadOnlyList<CombatSpatialSnapshot> candidates,
            int maxTargets = 0)
        {
            var actors = new List<CombatSpatialSnapshot>();
            if (tiles == null || candidates == null) return actors;

            var seenActorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CombatTileCoordinate tile in tiles)
            {
                var actorsOnTile = new List<CombatSpatialSnapshot>();
                foreach (CombatSpatialSnapshot actor in candidates)
                {
                    if (actor == null || !actor.Alive || !actor.Targetable || actor.Floor != anchorFloor ||
                        actor.Faction != targetFaction || actor.Tile.X != tile.X || actor.Tile.Y != tile.Y)
                        continue;
                    actorsOnTile.Add(actor);
                }

                actorsOnTile.Sort((left, right) => string.CompareOrdinal(left.ActorId, right.ActorId));
                foreach (CombatSpatialSnapshot actor in actorsOnTile)
                {
                    if (!seenActorIds.Add(actor.ActorId)) continue;
                    actors.Add(actor);
                    if (maxTargets > 0 && actors.Count >= maxTargets)
                        return actors;
                }
            }

            return actors;
        }

        public static List<CombatSpatialSnapshot> ResolveActors(
            CombatTileCoordinate anchor,
            TilePatternDef pattern,
            int anchorFloor,
            CombatFaction targetFaction,
            IReadOnlyList<CombatSpatialSnapshot> candidates)
        {
            if (pattern == null || pattern.FloorPolicy != TilePatternFloorPolicy.SameFloor)
                return new List<CombatSpatialSnapshot>();
            return ResolveActors(
                ResolveTiles(anchor, pattern),
                anchorFloor,
                targetFaction,
                candidates,
                pattern.MaxTargets);
        }

        private static void AddCrossRings(
            List<CombatTileCoordinate> tiles,
            CombatTileCoordinate anchor,
            int size)
        {
            int boundedSize = Math.Min(Math.Max(size, 0), TilePatternDef.MaxSafeOffsetMagnitude);
            for (int distance = 1; distance <= boundedSize; distance++)
            {
                AddOffset(tiles, anchor, 0, distance);
                AddOffset(tiles, anchor, distance, 0);
                AddOffset(tiles, anchor, 0, -distance);
                AddOffset(tiles, anchor, -distance, 0);
            }
        }

        private static void AddSquareRows(
            List<CombatTileCoordinate> tiles,
            CombatTileCoordinate anchor,
            int radius)
        {
            int boundedRadius = Math.Min(Math.Max(radius, 0), TilePatternDef.MaxSafeOffsetMagnitude);
            for (int y = -boundedRadius; y <= boundedRadius; y++)
                for (int x = -boundedRadius; x <= boundedRadius; x++)
                    if (x != 0 || y != 0)
                        AddOffset(tiles, anchor, x, y);
        }

        private static void AddOffset(
            List<CombatTileCoordinate> tiles,
            CombatTileCoordinate anchor,
            int x,
            int y)
        {
            if (TryOffset(anchor, new CombatTileCoordinate(x, y), out CombatTileCoordinate tile))
                AddUnique(tiles, tile);
        }

        private static bool TryOffset(
            CombatTileCoordinate anchor,
            CombatTileCoordinate offset,
            out CombatTileCoordinate tile)
        {
            long x = (long)anchor.X + offset.X;
            long y = (long)anchor.Y + offset.Y;
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            {
                tile = default(CombatTileCoordinate);
                return false;
            }

            tile = new CombatTileCoordinate((int)x, (int)y);
            return true;
        }

        private static void AddUnique(List<CombatTileCoordinate> tiles, CombatTileCoordinate tile)
        {
            foreach (CombatTileCoordinate existing in tiles)
                if (existing.X == tile.X && existing.Y == tile.Y)
                    return;
            tiles.Add(tile);
        }
    }
}
