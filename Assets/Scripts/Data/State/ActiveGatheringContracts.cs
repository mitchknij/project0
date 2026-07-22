using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public enum GatheringCommandKind { Start, Stop, MoveIntent }
    public enum GatheringEventKind { GatheringStarted, GatheringStopped, MovementRequested, AttemptResolved, ResourceGathered, CommandRejected }

    [Serializable]
    public class GatheringCommand
    {
        public GatheringCommandKind Kind;
        public string TargetEntityId;
    }

    [Serializable]
    public class ActiveGatheringState
    {
        public string TargetEntityId;
        public string NodeId;
        public long LastResolvedAt;
        public bool Active;

        public ActiveGatheringState Clone() => new ActiveGatheringState
        {
            TargetEntityId = TargetEntityId,
            NodeId = NodeId,
            LastResolvedAt = LastResolvedAt,
            Active = Active,
        };
    }

    [Serializable]
    public class GatheringWorldFacts
    {
        public bool TargetAvailable;
        public bool TargetInRange;
    }

    [Serializable]
    public class ActiveGatheringInput
    {
        public long Timestamp;
        public Character Character;
        public ClassDef Class;
        public ResourceNodeDef Node;
        public string TargetEntityId;
        public Dictionary<string, ItemDef> ItemDefs;
        public AccountBonuses Bonuses;
        public ActiveGatheringState State;
        public GatheringWorldFacts World;
        public List<GatheringCommand> Commands;
    }

    [Serializable]
    public class GatheringEvent
    {
        public GatheringEventKind Kind;
        public string TargetEntityId;
        public int Amount;
        public bool Success;
        public string Reason;
    }

    [Serializable]
    public class ActiveGatheringResult
    {
        public ActiveGatheringState State;
        public List<GatheringEvent> Events;
        public int SuccessfulActions;
        public long SkillXp;
        public List<ItemStack> Loot;
        public long ActionIntervalMs;
        public double ActionProgress01;
    }
}
