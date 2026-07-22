using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class CombatTimeContract
    {
        public const long DefaultStepMilliseconds = 50L;

        public static long DurationToTicks(long durationMilliseconds, long stepMilliseconds = DefaultStepMilliseconds)
        {
            if (durationMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));
            if (stepMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(stepMilliseconds));
            if (durationMilliseconds == 0) return 0;
            return checked((durationMilliseconds + stepMilliseconds - 1) / stepMilliseconds);
        }

        public static long TicksToMilliseconds(long ticks, long stepMilliseconds = DefaultStepMilliseconds)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            if (stepMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(stepMilliseconds));
            return checked(ticks * stepMilliseconds);
        }

        public static long QuantizeDurationMilliseconds(
            long durationMilliseconds,
            long stepMilliseconds = DefaultStepMilliseconds)
            => TicksToMilliseconds(DurationToTicks(durationMilliseconds, stepMilliseconds), stepMilliseconds);
    }

    public enum CombatCommandKind
    {
        SelectTarget,
        TriggerSkill,
        MoveIntent,
    }

    public enum CombatEventKind
    {
        TargetSelected,
        MovementRequested,
        AttackResolved,
        SkillCastStarted,
        SkillExecuted,
        CombatEffectScheduled,
        CombatEffectCancelled,
        AreaResolved,
        // Spatial resolution only; damage and status outcomes use their own events.
        TileAreaResolved,
        TransientModifierApplied,
        TransientModifierExpired,
        StatusApplied,
        StatusTicked,
        StatusExpired,
        CooldownStarted,
        DamageApplied,
        ActorDefeated,
        EnemyKilled,
        AutoCombatResumed,
        CommandRejected,
    }

    [Serializable]
    public class CombatCommand
    {
        public CombatCommandKind Kind;
        public string TargetId;
        public string SkillId;
    }

    [Serializable]
    public class CombatWorldFacts
    {
        public bool TargetAvailable;
        public bool TargetInRange;
        public bool LineOfSight;
        public double Distance;
        public string PrimaryTargetActorId;
        public CombatSpatialFrame Spatial;
        public List<HostileAttackerFacts> HostileAttackers = new List<HostileAttackerFacts>();
    }

    [Serializable]
    public class HostileAttackerFacts
    {
        public string ActorId;
        public string MonsterId;
        public bool CanAttack;
    }

    [Serializable]
    public class ActiveCombatState
    {
        public string TargetId;
        public int PlayerHp;
        public int EnemyHp;
        public Dictionary<string, int> EnemyHpByActorId = new Dictionary<string, int>();
        public List<string> DefeatedActorIds = new List<string>();
        public long PlayerNextAttackAt;
        public long EnemyNextAttackAt;
        public Dictionary<string, long> EnemyNextAttackAtByActorId = new Dictionary<string, long>();
        public long LastUpdatedAt;
        public bool KillResolved;
        public bool PlayerDefeated;
        public bool AutoCombatStopped;
        public long AutoCombatStoppedAt;
        public long PlayerAttacksResolved;
        public long EnemyAttacksResolved;
        public long EnemiesKilled;
        public long SimulationTick;
        public long LastCommandSequenceId;
        public CombatSkillRuntimeState SkillRuntime = new CombatSkillRuntimeState();

        // Compatibility surface for the current UI and tests. Encounter cooldown ownership
        // lives in SkillRuntime; this alias can be removed after all consumers migrate.
        public Dictionary<string, long> SkillNextReadyAt
        {
            get
            {
                if (SkillRuntime == null) SkillRuntime = new CombatSkillRuntimeState();
                if (SkillRuntime.CooldownReadyAt == null)
                    SkillRuntime.CooldownReadyAt = new Dictionary<string, long>();
                return SkillRuntime.CooldownReadyAt;
            }
            set
            {
                if (SkillRuntime == null) SkillRuntime = new CombatSkillRuntimeState();
                SkillRuntime.CooldownReadyAt = value ?? new Dictionary<string, long>();
            }
        }

        public ActiveCombatState Clone()
        {
            return new ActiveCombatState
            {
                TargetId = TargetId,
                PlayerHp = PlayerHp,
                EnemyHp = EnemyHp,
                EnemyHpByActorId = EnemyHpByActorId == null
                    ? new Dictionary<string, int>()
                    : new Dictionary<string, int>(EnemyHpByActorId),
                DefeatedActorIds = DefeatedActorIds == null
                    ? new List<string>()
                    : new List<string>(DefeatedActorIds),
                PlayerNextAttackAt = PlayerNextAttackAt,
                EnemyNextAttackAt = EnemyNextAttackAt,
                EnemyNextAttackAtByActorId = EnemyNextAttackAtByActorId == null
                    ? new Dictionary<string, long>()
                    : new Dictionary<string, long>(EnemyNextAttackAtByActorId),
                LastUpdatedAt = LastUpdatedAt,
                KillResolved = KillResolved,
                PlayerDefeated = PlayerDefeated,
                AutoCombatStopped = AutoCombatStopped,
                AutoCombatStoppedAt = AutoCombatStoppedAt,
                PlayerAttacksResolved = PlayerAttacksResolved,
                EnemyAttacksResolved = EnemyAttacksResolved,
                EnemiesKilled = EnemiesKilled,
                SimulationTick = SimulationTick,
                LastCommandSequenceId = LastCommandSequenceId,
                SkillRuntime = SkillRuntime?.Clone() ?? new CombatSkillRuntimeState(),
            };
        }
    }

    [Serializable]
    public class ActiveCombatConfig
    {
        public long AutoResumeGraceMs;
        public bool AutoSkillRotation;
        public double OutOfCombatRegenPctPerSec;
    }

    [Serializable]
    public class CombatCandidate
    {
        public string EntityId;
        public string MonsterId;
        public bool Available;
        public double Distance;
    }

    [Serializable]
    public class ActiveSimInput
    {
        public long Timestamp;
        public Character Character;
        public ClassDef Class;
        public MonsterDef Monster;
        public string TargetEntityId;
        public Dictionary<string, ItemDef> ItemDefs;
        public Dictionary<string, MonsterDef> MonsterDefs;
        public ActiveCombatState State;
        public CombatWorldFacts World;
        public List<CombatCommand> Commands;
        public AccountBonuses Bonuses;
        public ActiveCombatConfig Config;
    }

    [Serializable]
    public class CombatEvent
    {
        public CombatEventKind Kind;
        public string ActorId;
        public string TargetId;
        public string SkillId;
        public int Amount;
        public bool Hit;
        public bool Critical;
        public string Reason;
        public long SimulationTick;
        public long CommandSequenceId;
        public long ActionSequenceId;
        public double PositionX;
        public double PositionY;
        public double Radius;
        public long DurationTicks;
        public List<CombatTileCoordinate> AffectedTiles;
        public int Floor;
        public List<string> ResolvedActorIds;
    }

    [Serializable]
    public class ActiveSimResult
    {
        public ActiveCombatState State;
        public List<CombatEvent> Events;
        public bool EnemyKilled;
        public bool PlayerDefeated;
        public List<string> DefeatedActorIds = new List<string>();
    }
}
