using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class CircleShapeResolverTests
    {
        [Test]
        public void Resolve_UsesContinuousFootprintOverlapAndStableOrdering()
        {
            var actors = new List<CombatSpatialSnapshot>
            {
                Hostile("b", 1.2, 0.0, 0.25, 0),
                Hostile("a", 1.2, 0.0, 0.25, 0),
                Hostile("center", 0.0, 0.0, 0.25, 0),
                Hostile("outside", 1.26, 0.0, 0.25, 0),
            };

            List<CombatAreaHit> hits = CircleShapeResolver.Resolve(
                new CombatPoint2(0.0, 0.0), 1.0, 0, CombatFaction.Hostile, actors);

            CollectionAssert.AreEqual(new[] { "center", "a", "b" }, hits.ConvertAll(hit => hit.ActorId));
        }

        [Test]
        public void Resolve_FiltersFloorFactionAliveAndTargetableBeforeGeometry()
        {
            var wrongFloor = Hostile("floor", 0.0, 0.0, 0.2, 1);
            var dead = Hostile("dead", 0.0, 0.0, 0.2, 0);
            dead.Alive = false;
            var untargetable = Hostile("untargetable", 0.0, 0.0, 0.2, 0);
            untargetable.Targetable = false;
            var player = Hostile("player", 0.0, 0.0, 0.2, 0);
            player.Faction = CombatFaction.Player;

            List<CombatAreaHit> hits = CircleShapeResolver.Resolve(
                new CombatPoint2(), 1.0, 0, CombatFaction.Hostile,
                new[] { wrongFloor, dead, untargetable, player });

            Assert.That(hits, Is.Empty);
        }

        private static CombatSpatialSnapshot Hostile(
            string id,
            double x,
            double y,
            double footprint,
            int floor) => new CombatSpatialSnapshot
        {
            ActorId = id,
            GroundPosition = new CombatPoint2(x, y),
            Floor = floor,
            FootprintRadius = footprint,
            Faction = CombatFaction.Hostile,
            Alive = true,
            Targetable = true,
        };
    }
}
