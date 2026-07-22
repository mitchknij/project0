using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class TilePatternResolverTests
    {
        private static readonly CombatTileCoordinate Anchor = new CombatTileCoordinate(5, 5);

        [Test]
        public void ResolveTiles_SingleTile_ReturnsAnchorOnly()
        {
            List<CombatTileCoordinate> tiles = TilePatternResolver.ResolveTiles(
                Anchor, new TilePatternDef { PatternKind = TilePatternKind.SingleTile });

            CollectionAssert.AreEqual(new[] { Anchor }, tiles);
        }

        [Test]
        public void ResolveTiles_CrossSize1_ReturnsAnchorThenNorthEastSouthWest()
        {
            List<CombatTileCoordinate> tiles = TilePatternResolver.ResolveTiles(
                Anchor, new TilePatternDef { PatternKind = TilePatternKind.Cross, Size = 1 });

            CollectionAssert.AreEqual(
                new[]
                {
                    Anchor,
                    new CombatTileCoordinate(5, 6),
                    new CombatTileCoordinate(6, 5),
                    new CombatTileCoordinate(5, 4),
                    new CombatTileCoordinate(4, 5),
                },
                tiles);
        }

        [Test]
        public void ResolveTiles_CrossSize2_ReturnsRingsInDistanceThenNESWOrder()
        {
            List<CombatTileCoordinate> tiles = TilePatternResolver.ResolveTiles(
                Anchor, new TilePatternDef { PatternKind = TilePatternKind.Cross, Size = 2 });

            CollectionAssert.AreEqual(
                new[]
                {
                    Anchor,
                    new CombatTileCoordinate(5, 6),
                    new CombatTileCoordinate(6, 5),
                    new CombatTileCoordinate(5, 4),
                    new CombatTileCoordinate(4, 5),
                    new CombatTileCoordinate(5, 7),
                    new CombatTileCoordinate(7, 5),
                    new CombatTileCoordinate(5, 3),
                    new CombatTileCoordinate(3, 5),
                },
                tiles);
        }

        [Test]
        public void ResolveTiles_SquareRadius1_ReturnsAnchorFirstThenRowMajorNegativeYToPositiveY()
        {
            List<CombatTileCoordinate> tiles = TilePatternResolver.ResolveTiles(
                Anchor, new TilePatternDef { PatternKind = TilePatternKind.SquareRadius, Size = 1 });

            CollectionAssert.AreEqual(
                new[]
                {
                    Anchor,
                    new CombatTileCoordinate(4, 4),
                    new CombatTileCoordinate(5, 4),
                    new CombatTileCoordinate(6, 4),
                    new CombatTileCoordinate(4, 5),
                    new CombatTileCoordinate(6, 5),
                    new CombatTileCoordinate(4, 6),
                    new CombatTileCoordinate(5, 6),
                    new CombatTileCoordinate(6, 6),
                },
                tiles);
            Assert.That(tiles, Has.Count.EqualTo(9), "SquareRadius 1 must be a 3x3 Chebyshev pattern");
        }

        [Test]
        public void ResolveTiles_CustomOffsets_PreservesDeclarationOrderAndDedupsDefensively()
        {
            List<CombatTileCoordinate> tiles = TilePatternResolver.ResolveTiles(
                Anchor, new TilePatternDef
                {
                    PatternKind = TilePatternKind.CustomOffsets,
                    CustomOffsets = new List<CombatTileCoordinate>
                    {
                        new CombatTileCoordinate(2, 0),
                        new CombatTileCoordinate(0, 3),
                        new CombatTileCoordinate(2, 0),
                    },
                });

            CollectionAssert.AreEqual(
                new[]
                {
                    new CombatTileCoordinate(7, 5),
                    new CombatTileCoordinate(5, 8),
                },
                tiles);
        }

        [Test]
        public void ResolveActors_FiltersBySameFloorFactionAliveAndTargetable()
        {
            var tiles = new List<CombatTileCoordinate> { Anchor };
            var candidates = new List<CombatSpatialSnapshot>
            {
                Actor("wrong-floor", Anchor, 1, CombatFaction.Hostile, true, true),
                Actor("dead", Anchor, 0, CombatFaction.Hostile, false, true),
                Actor("untargetable", Anchor, 0, CombatFaction.Hostile, true, false),
                Actor("wrong-faction", Anchor, 0, CombatFaction.Player, true, true),
                Actor("valid", Anchor, 0, CombatFaction.Hostile, true, true),
            };

            List<CombatSpatialSnapshot> actors = TilePatternResolver.ResolveActors(
                tiles, 0, CombatFaction.Hostile, candidates);

            CollectionAssert.AreEqual(new[] { "valid" }, actors.ConvertAll(actor => actor.ActorId));
        }

        [Test]
        public void ResolveActors_OrdersByTileOrderThenActorId()
        {
            var north = new CombatTileCoordinate(5, 6);
            var tiles = new List<CombatTileCoordinate> { Anchor, north };
            var candidates = new List<CombatSpatialSnapshot>
            {
                Actor("b", north, 0, CombatFaction.Hostile, true, true),
                Actor("a", north, 0, CombatFaction.Hostile, true, true),
                Actor("z", Anchor, 0, CombatFaction.Hostile, true, true),
            };

            List<CombatSpatialSnapshot> actors = TilePatternResolver.ResolveActors(
                tiles, 0, CombatFaction.Hostile, candidates);

            CollectionAssert.AreEqual(new[] { "z", "a", "b" }, actors.ConvertAll(actor => actor.ActorId));
        }

        [Test]
        public void ResolveActors_AppliesMaxTargetsCapAfterOrdering()
        {
            var tiles = new List<CombatTileCoordinate> { Anchor };
            var candidates = new List<CombatSpatialSnapshot>
            {
                Actor("b", Anchor, 0, CombatFaction.Hostile, true, true),
                Actor("a", Anchor, 0, CombatFaction.Hostile, true, true),
                Actor("c", Anchor, 0, CombatFaction.Hostile, true, true),
            };

            List<CombatSpatialSnapshot> actors = TilePatternResolver.ResolveActors(
                tiles, 0, CombatFaction.Hostile, candidates, maxTargets: 2);

            CollectionAssert.AreEqual(new[] { "a", "b" }, actors.ConvertAll(actor => actor.ActorId));
        }

        [Test]
        public void ResolveActors_DuplicateActorIdAcrossCandidatesResolvesOnce()
        {
            var tiles = new List<CombatTileCoordinate> { Anchor };
            var candidates = new List<CombatSpatialSnapshot>
            {
                Actor("dup", Anchor, 0, CombatFaction.Hostile, true, true),
                Actor("dup", Anchor, 0, CombatFaction.Hostile, true, true),
            };

            List<CombatSpatialSnapshot> actors = TilePatternResolver.ResolveActors(
                tiles, 0, CombatFaction.Hostile, candidates);

            Assert.That(actors, Has.Count.EqualTo(1));
        }

        private static CombatSpatialSnapshot Actor(
            string id,
            CombatTileCoordinate tile,
            int floor,
            CombatFaction faction,
            bool alive,
            bool targetable) => new CombatSpatialSnapshot
        {
            ActorId = id,
            DefinitionId = "slime",
            Tile = tile,
            Floor = floor,
            Faction = faction,
            Alive = alive,
            Targetable = targetable,
        };
    }
}
