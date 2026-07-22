using System;

namespace IdleCloud.Core
{
    /// <summary>Canonical conversion between persistent activity and harvest-skill IDs.</summary>
    public static class ActivitySkillMapping
    {
        public static bool IsHarvest(ActivityKind kind)
            => kind == ActivityKind.Mining || kind == ActivityKind.Chopping || kind == ActivityKind.Gathering;

        public static SkillId ToSkillId(ActivityKind kind) => kind switch
        {
            ActivityKind.Mining => SkillId.Mining,
            ActivityKind.Chopping => SkillId.Chopping,
            ActivityKind.Gathering => SkillId.Gathering,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a harvest activity."),
        };

        public static HarvestSkill ToHarvestSkill(ActivityKind kind) => kind switch
        {
            ActivityKind.Mining => HarvestSkill.Mining,
            ActivityKind.Chopping => HarvestSkill.Chopping,
            ActivityKind.Gathering => HarvestSkill.Gathering,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a harvest activity."),
        };

        public static ActivityKind ToActivityKind(HarvestSkill skill) => skill switch
        {
            HarvestSkill.Mining => ActivityKind.Mining,
            HarvestSkill.Chopping => ActivityKind.Chopping,
            HarvestSkill.Gathering => ActivityKind.Gathering,
            _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, "Unknown harvest skill."),
        };

        public static double EfficiencyBonus(AccountBonuses bonuses, ActivityKind kind) => kind switch
        {
            ActivityKind.Mining => bonuses.MiningPct,
            ActivityKind.Chopping => bonuses.ChoppingPct,
            ActivityKind.Gathering => bonuses.GatheringPct,
            _ => 0.0,
        };

        public static double PassiveEfficiency(PassiveMultipliers multipliers, ActivityKind kind) => kind switch
        {
            ActivityKind.Mining => multipliers?.MiningEff ?? 1.0,
            ActivityKind.Chopping => multipliers?.ChoppingEff ?? 1.0,
            ActivityKind.Gathering => multipliers?.GatheringEff ?? 1.0,
            _ => 1.0,
        };
    }
}
