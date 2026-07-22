using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public enum CombatFaction
    {
        Player,
        Hostile,
        Neutral,
    }

    [Serializable]
    public struct CombatPoint2
    {
        public double X;
        public double Y;

        public CombatPoint2(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    [Serializable]
    public struct CombatTileCoordinate
    {
        public int X;
        public int Y;

        public CombatTileCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [Serializable]
    public sealed class CombatSpatialSnapshot
    {
        public string ActorId;
        public string DefinitionId;
        public CombatPoint2 GroundPosition;
        public CombatTileCoordinate Tile;
        public int Floor;
        public double FootprintRadius;
        public CombatFaction Faction;
        public bool Alive = true;
        public bool Targetable = true;
    }

    [Serializable]
    public sealed class CombatSpatialFrame
    {
        public string SourceActorId;
        public List<CombatSpatialSnapshot> Actors = new List<CombatSpatialSnapshot>();
    }

    [Serializable]
    public sealed class CombatAreaHit
    {
        public string ActorId;
        public double CenterDistance;
    }
}
