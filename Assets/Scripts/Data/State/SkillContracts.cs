using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public enum CombatActionKind
    {
        BasicAttack,
        Skill,
    }

    public enum SkillTargetingKind
    {
        HostileActor,
        Self,
        CircleAroundSource,
        CircleAroundTarget,
        GroundPoint,
        Direction,
        TilePatternAroundSource,
        TilePatternAroundTarget,
    }

    public enum TilePatternKind
    {
        SingleTile,
        Cross,
        SquareRadius,
        CustomOffsets,
    }

    /// <summary>
    /// Floor policies supported by the first tile-pattern release. Other enum
    /// values are intentionally rejected by content validation.
    /// </summary>
    public enum TilePatternFloorPolicy
    {
        SameFloor,
    }

    public enum SkillTimingKind
    {
        Immediate,
        ScheduledImpact,
    }

    public enum SkillEffectKind
    {
        DealDamage,
        Heal,
        ApplyTransientModifier,
        ApplyStatus,
        RemoveEffect,
        ScheduleAreaImpact,
    }

    public enum SkillConditionKind
    {
        TargetExists,
        TargetAlive,
        TargetHostile,
        TargetInRange,
        CooldownReady,
        SourceHpBelow,
        TargetHpBelow,
        MinimumTargetsInShape,
        TargetMissingStatus,
        SourceMissingModifier,
        CorrectFloor,
        HasLineOfSight,
    }

    public enum CombatStatProperty
    {
        Defense,
        Damage,
        AttackSpeed,
        CritChance,
    }

    public enum CombatModifierOperation
    {
        FlatAdd,
        AdditivePercent,
        MultiplicativePercent,
        Override,
    }

    public enum ScheduledCombatEffectKind
    {
        ExpireModifier,
        ResolveSkillImpact,
        TickStatus,
    }

    [Serializable]
    public sealed class TransientCombatModifier
    {
        public string InstanceId;
        public string DefinitionId;
        public string SourceActorId;
        public string TargetActorId;
        public CombatStatProperty Property;
        public CombatModifierOperation Operation;
        public double Magnitude;
        public long StartTick;
        public long EndTick;
        public long ApplicationSequenceId;

        public TransientCombatModifier Clone() => (TransientCombatModifier)MemberwiseClone();
    }

    [Serializable]
    public sealed class ScheduledCombatEffect
    {
        public long SequenceId;
        public long ExecuteTick;
        public ScheduledCombatEffectKind Kind;
        public string ReferenceId;
        public string SourceActorId;
        public string TargetActorId;
        public string SkillId;
        public long CommandSequenceId;
        public long ActionSequenceId;
        public bool HasCapturedAnchor;
        public CombatTileCoordinate CapturedAnchorTile;
        public int CapturedAnchorFloor;

        public ScheduledCombatEffect Clone() => (ScheduledCombatEffect)MemberwiseClone();
    }

    [Serializable]
    public sealed class ActiveCombatStatus
    {
        public string InstanceId;
        public string DefinitionId;
        public string SourceActorId;
        public string TargetActorId;
        public StatusKind Kind;
        public Element Element;
        public double Magnitude;
        public long StartTick;
        public long EndTick;
        public long NextTick;
        public long IntervalTicks;
        public long ApplicationSequenceId;

        public ActiveCombatStatus Clone() => (ActiveCombatStatus)MemberwiseClone();
    }

    [Serializable]
    public sealed class CombatActionDefinition
    {
        public string Id;
        public CombatActionKind Kind;
        public Element Element = Element.Physical;
        public double DamageMultiplier = 1.0;
    }

    [Serializable]
    public sealed class SkillConditionDefinition
    {
        public SkillConditionKind Kind;
        public double Threshold;
        public string ReferenceId;
    }

    [Serializable]
    public sealed class SkillEffectDefinition
    {
        public SkillEffectKind Kind;
        public double Magnitude;
        public Element Element = Element.Physical;
        public string ReferenceId;
        public long DurationTicks;
        public long IntervalTicks;
    }

    [Serializable]
    public sealed class SkillDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public string BranchId;
        public int Tier;
        public List<ClassId> AllowedClasses = new List<ClassId>();
        public SkillTargetingKind Targeting;
        public SkillTimingKind Timing;
        public long CooldownTicks;
        public int Cost;
        public bool ManualEnabled = true;
        public bool AutoEnabled = true;
        public List<SkillConditionDefinition> Conditions = new List<SkillConditionDefinition>();
        public List<SkillEffectDefinition> Effects = new List<SkillEffectDefinition>();
        public string ContentVersion;
    }

    public static class SkillDefinitionValidation
    {
        public static List<string> Validate(SkillDefinition definition)
        {
            var issues = new List<string>();
            if (definition == null)
            {
                issues.Add("skill_definition_missing");
                return issues;
            }

            if (string.IsNullOrWhiteSpace(definition.Id)) issues.Add("skill_id_missing");
            if (string.IsNullOrWhiteSpace(definition.ContentVersion)) issues.Add("skill_content_version_missing");
            if (definition.Tier < 0) issues.Add("skill_tier_negative");
            if (definition.CooldownTicks < 0) issues.Add("skill_cooldown_negative");
            if (definition.Cost < 0) issues.Add("skill_cost_negative");
            if (definition.Effects == null || definition.Effects.Count == 0) issues.Add("skill_effects_missing");

            foreach (SkillEffectDefinition effect in definition.Effects ?? new List<SkillEffectDefinition>())
            {
                if (effect == null)
                {
                    issues.Add("skill_effect_missing");
                    continue;
                }
                if (effect.DurationTicks < 0) issues.Add("skill_effect_duration_negative");
                if (effect.IntervalTicks < 0) issues.Add("skill_effect_interval_negative");
            }
            return issues;
        }
    }

    [Serializable]
    public sealed class SequencedCombatCommand
    {
        public CombatCommand Command;
        public long SimulationTick;
        public long SequenceId;

        public SequencedCombatCommand Clone() => new SequencedCombatCommand
        {
            Command = Command == null ? null : new CombatCommand
            {
                Kind = Command.Kind,
                TargetId = Command.TargetId,
                SkillId = Command.SkillId,
            },
            SimulationTick = SimulationTick,
            SequenceId = SequenceId,
        };
    }

    [Serializable]
    public sealed class CombatSkillRuntimeState
    {
        public Dictionary<string, long> CooldownReadyAt = new Dictionary<string, long>();
        public SequencedCombatCommand QueuedManualCommand;
        public long LastActionSequenceId;
        public long LastScheduledEffectSequenceId;
        public List<ScheduledCombatEffect> ScheduledEffects = new List<ScheduledCombatEffect>();
        public List<TransientCombatModifier> ActiveModifiers = new List<TransientCombatModifier>();
        public List<ActiveCombatStatus> ActiveStatuses = new List<ActiveCombatStatus>();
        public string LastAutoSelectionDiagnostics;
        public AutoSkillSelectionDiagnostics LastAutoSelection;

        public CombatSkillRuntimeState Clone() => new CombatSkillRuntimeState
        {
            CooldownReadyAt = CooldownReadyAt == null
                ? new Dictionary<string, long>()
                : new Dictionary<string, long>(CooldownReadyAt),
            QueuedManualCommand = QueuedManualCommand?.Clone(),
            LastActionSequenceId = LastActionSequenceId,
            LastScheduledEffectSequenceId = LastScheduledEffectSequenceId,
            ScheduledEffects = CloneScheduledEffects(ScheduledEffects),
            ActiveModifiers = CloneModifiers(ActiveModifiers),
            ActiveStatuses = CloneStatuses(ActiveStatuses),
            LastAutoSelectionDiagnostics = LastAutoSelectionDiagnostics,
            LastAutoSelection = LastAutoSelection?.Clone(),
        };

        private static List<ScheduledCombatEffect> CloneScheduledEffects(List<ScheduledCombatEffect> source)
        {
            var result = new List<ScheduledCombatEffect>();
            foreach (ScheduledCombatEffect effect in source ?? new List<ScheduledCombatEffect>())
                if (effect != null) result.Add(effect.Clone());
            return result;
        }

        private static List<TransientCombatModifier> CloneModifiers(List<TransientCombatModifier> source)
        {
            var result = new List<TransientCombatModifier>();
            foreach (TransientCombatModifier modifier in source ?? new List<TransientCombatModifier>())
                if (modifier != null) result.Add(modifier.Clone());
            return result;
        }

        private static List<ActiveCombatStatus> CloneStatuses(List<ActiveCombatStatus> source)
        {
            var result = new List<ActiveCombatStatus>();
            foreach (ActiveCombatStatus status in source ?? new List<ActiveCombatStatus>())
                if (status != null) result.Add(status.Clone());
            return result;
        }
    }

    [Serializable]
    public sealed class AutoSkillSlotDiagnostic
    {
        public int SlotIndex;
        public string SkillId;
        public bool Eligible;
        public string Reason;

        public AutoSkillSlotDiagnostic Clone() => (AutoSkillSlotDiagnostic)MemberwiseClone();
    }

    [Serializable]
    public sealed class AutoSkillSelectionDiagnostics
    {
        public List<AutoSkillSlotDiagnostic> Slots = new List<AutoSkillSlotDiagnostic>();
        public int SelectedSlotIndex = -1;
        public long SelectionSequenceId;
        public string SelectedSkillId;
        public string FallbackReason;

        public AutoSkillSelectionDiagnostics Clone()
        {
            var result = new AutoSkillSelectionDiagnostics
            {
                SelectedSlotIndex = SelectedSlotIndex,
                SelectionSequenceId = SelectionSequenceId,
                SelectedSkillId = SelectedSkillId,
                FallbackReason = FallbackReason,
            };
            foreach (AutoSkillSlotDiagnostic slot in Slots ?? new List<AutoSkillSlotDiagnostic>())
                if (slot != null) result.Slots.Add(slot.Clone());
            return result;
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(SelectedSkillId))
                return $"slot={SelectedSlotIndex + 1};skill={SelectedSkillId}";
            return "basic_attack:" + (FallbackReason ?? "no_eligible_skill");
        }
    }
}
