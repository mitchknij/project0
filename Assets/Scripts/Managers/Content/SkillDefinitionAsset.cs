using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Skills/Skill Definition", fileName = "SkillDefinition")]
    public sealed class SkillDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Element element;
        [SerializeField] private SkillMechanic mechanic;
        [SerializeField, Min(0)] private int cooldownMilliseconds;
        [SerializeField] private double damageMultiplier;
        [SerializeField] private int aoeColor;
        [SerializeField] private int priority;
        [SerializeField] private int rangePixels;
        [SerializeField] private int aoeRadiusPixels;
        [SerializeField] private double projectileSpeed;
        [SerializeField] private double projectileRadius;
        [SerializeField] private SkillTargetingKind targeting;
        [SerializeField, Min(1)] private int minimumAutoTargets = 1;
        [SerializeField] private TilePatternDef tilePattern;
        [SerializeField] private bool autoEnabled = true;
        [SerializeField, Min(0)] private double radiusWorldUnits;
        [SerializeField] private CombatStatProperty modifierProperty;
        [SerializeField] private CombatModifierOperation modifierOperation;
        [SerializeField] private double modifierMagnitude;
        [SerializeField, Min(0)] private long modifierDurationTicks;
        [SerializeField] private SkillTimingKind timing;
        [SerializeField, Min(0)] private long impactDelayTicks;
        [SerializeField] private string prerequisiteSkillId;
        [SerializeField] private string branchId = "Class";
        [SerializeField, Min(1)] private int tier = 1;
        [SerializeField, Min(0)] private int skillPointCost = 1;
        [SerializeField] private List<StatusInflict> inflicts = new List<StatusInflict>();
        [SerializeField] private List<ClassId> allowedClasses = new List<ClassId>();

        public string StableId => stableId;
        public IReadOnlyList<ClassId> AllowedClasses => allowedClasses;

        public ClassSkillDef ToPureDefinition() => new ClassSkillDef
        {
            Id = stableId, Name = displayName, Description = description, Element = element,
            Mechanic = mechanic, CooldownMs = cooldownMilliseconds, DamageMultiplier = damageMultiplier,
            AoeColor = aoeColor, Priority = priority, RangePx = rangePixels > 0 ? rangePixels : (int?)null,
            AoeRadiusPx = aoeRadiusPixels > 0 ? aoeRadiusPixels : (int?)null,
            ProjectileSpeed = projectileSpeed > 0 ? projectileSpeed : (double?)null,
            ProjectileRadius = projectileRadius > 0 ? projectileRadius : (double?)null,
            Targeting = targeting, MinimumAutoTargets = minimumAutoTargets,
            TilePattern = IsTilePatternTargeting(targeting) ? CopyTilePattern(tilePattern) : null,
            AutoEnabled = autoEnabled, RadiusWorldUnits = radiusWorldUnits,
            ModifierProperty = modifierProperty, ModifierOperation = modifierOperation,
            ModifierMagnitude = modifierMagnitude, ModifierDurationTicks = modifierDurationTicks,
            Timing = timing, ImpactDelayTicks = impactDelayTicks, Inflicts = CopyStatusInflicts(inflicts),
            PrerequisiteSkillId = prerequisiteSkillId, BranchId = branchId, Tier = tier, SkillPointCost = skillPointCost,
        };

        private static bool IsTilePatternTargeting(SkillTargetingKind value)
            => value == SkillTargetingKind.TilePatternAroundSource ||
               value == SkillTargetingKind.TilePatternAroundTarget;

        internal static TilePatternDef CopyTilePattern(TilePatternDef source) => source == null ? null : new TilePatternDef { PatternKind = source.PatternKind, Size = source.Size, FloorPolicy = source.FloorPolicy, MaxTargets = source.MaxTargets, CustomOffsets = source.CustomOffsets == null ? null : new List<CombatTileCoordinate>(source.CustomOffsets) };

        internal static List<StatusInflict> CopyStatusInflicts(List<StatusInflict> source)
        {
            if (source == null) return null;
            var copy = new List<StatusInflict>(source.Count);
            foreach (StatusInflict status in source)
                if (status != null)
                    copy.Add(new StatusInflict { Kind = status.Kind, DurationMs = status.DurationMs, TickIntervalMs = status.TickIntervalMs, Magnitude = status.Magnitude });
            return copy;
        }
    }
}
