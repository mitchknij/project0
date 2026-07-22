using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class CircleShapeResolver
    {
        public const double GeometryEpsilon = 0.000001;

        public static List<CombatAreaHit> Resolve(
            CombatPoint2 center,
            double radius,
            int floor,
            CombatFaction targetFaction,
            IReadOnlyList<CombatSpatialSnapshot> candidates)
        {
            if (radius < 0.0) throw new ArgumentOutOfRangeException(nameof(radius));
            var hits = new List<CombatAreaHit>();
            if (candidates == null) return hits;

            foreach (CombatSpatialSnapshot actor in candidates)
            {
                if (actor == null || !actor.Alive || !actor.Targetable || actor.Faction != targetFaction ||
                    actor.Floor != floor || actor.FootprintRadius < 0.0)
                    continue;

                double dx = actor.GroundPosition.X - center.X;
                double dy = actor.GroundPosition.Y - center.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance <= radius + actor.FootprintRadius + GeometryEpsilon)
                    hits.Add(new CombatAreaHit { ActorId = actor.ActorId, CenterDistance = distance });
            }

            hits.Sort((left, right) =>
            {
                int distanceOrder = left.CenterDistance.CompareTo(right.CenterDistance);
                return distanceOrder != 0
                    ? distanceOrder
                    : string.CompareOrdinal(left.ActorId, right.ActorId);
            });
            return hits;
        }
    }
}
