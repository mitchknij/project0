using System.Collections.Generic;
using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.View
{
    /// <summary>Snapshots the existing logical ISO actor state into pure Core world facts.</summary>
    public static class CombatSpatialAdapter
    {
        public static CombatSpatialFrame Capture(
            string playerActorId,
            PlayerController player,
            IReadOnlyList<CombatTargetView> targets,
            GridPathfinder pathfinder,
            double playerFootprintRadius)
        {
            var frame = new CombatSpatialFrame { SourceActorId = playerActorId };
            if (player == null) return frame;

            Vector3 playerPosition = player.LogicalPosition;
            frame.Actors.Add(CreateSnapshot(
                playerActorId, "player", playerPosition, pathfinder,
                playerFootprintRadius, CombatFaction.Player, true, true));

            if (targets == null) return frame;
            foreach (CombatTargetView target in targets)
            {
                if (target == null) continue;
                frame.Actors.Add(CreateSnapshot(
                    target.EntityId,
                    target.MonsterId,
                    target.LogicalPosition,
                    pathfinder,
                    target.FootprintRadius,
                    CombatFaction.Hostile,
                    target.IsAvailable,
                    target.IsAvailable));
            }
            return frame;
        }

        private static CombatSpatialSnapshot CreateSnapshot(
            string actorId,
            string definitionId,
            Vector3 logicalPosition,
            GridPathfinder pathfinder,
            double footprintRadius,
            CombatFaction faction,
            bool alive,
            bool targetable)
        {
            int floor = Mathf.RoundToInt(logicalPosition.z);
            if (pathfinder != null && pathfinder.TryGetHeightAt(logicalPosition, out int terrainFloor))
                floor = terrainFloor;

            var tile = new CombatTileCoordinate();
            if (pathfinder != null && pathfinder.TryGetCell(logicalPosition, out Vector2Int cell))
                tile = new CombatTileCoordinate(cell.x, cell.y);

            return new CombatSpatialSnapshot
            {
                ActorId = actorId,
                DefinitionId = definitionId,
                GroundPosition = new CombatPoint2(logicalPosition.x, logicalPosition.y),
                Tile = tile,
                Floor = floor,
                FootprintRadius = footprintRadius,
                Faction = faction,
                Alive = alive,
                Targetable = targetable,
            };
        }
    }
}
