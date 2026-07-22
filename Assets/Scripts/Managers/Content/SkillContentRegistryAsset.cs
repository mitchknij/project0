using System;
using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Skills/Skill Content Registry", fileName = "SkillContentRegistry")]
    public sealed class SkillContentRegistryAsset : ScriptableObject
    {
        [Header("Versioning")]
        [Tooltip("Changing skill definitions invalidates active efficiency snapshots.")]
        [SerializeField] private string contentVersion = "skills-v1";
        [SerializeField] private List<SkillDefinitionAsset> skills = new List<SkillDefinitionAsset>();
        public IReadOnlyList<SkillDefinitionAsset> Skills => skills;
        public string ContentVersion => contentVersion;
    }

    public sealed class SkillContentProvider : IRuntimeContentProvider
    {
        private static readonly HashSet<string> ProductionSkillIds = new HashSet<string>
        {
            "ground_smash", "arcane_detonation", "power_strike", "earthbreaker",
            "guard", "arcane_bolt", "flame_burst",
        };
        private readonly Dictionary<ClassId, ClassDef> _classes = new Dictionary<ClassId, ClassDef>();
        public IReadOnlyDictionary<ClassId, ClassDef> All => _classes;
        public IReadOnlyDictionary<string, ItemDef> Items => ItemsRepo.All;
        public IReadOnlyDictionary<string, MonsterDef> Monsters => MonstersRepo.All;
        public IReadOnlyDictionary<string, ResourceNodeDef> Nodes => NodesRepo.All;
        public IReadOnlyDictionary<string, RecipeDef> Recipes => RecipesRepo.All;
        public IReadOnlyDictionary<string, MapDef> Maps => MapsRepo.All;
        public IReadOnlyDictionary<string, TalentDef> Talents => TalentsRepo.All;
        public string ConfigurationVersion { get; }

        public SkillContentProvider(SkillContentRegistryAsset registry)
        {
            ConfigurationVersion = registry == null || string.IsNullOrWhiteSpace(registry.ContentVersion)
                ? "skills-v1"
                : registry.ContentVersion;
            var converted = ConvertProductionSkills(registry);
            foreach (var pair in ClassesRepo.All)
                _classes.Add(pair.Key, CopyClass(pair.Value, converted));
        }

        public ClassDef Get(ClassId classId) => _classes.TryGetValue(classId, out ClassDef definition) ? definition : null;

        private static Dictionary<string, SkillDefinitionAsset> ConvertProductionSkills(SkillContentRegistryAsset registry)
        {
            var converted = new Dictionary<string, SkillDefinitionAsset>();
            int index = 0;
            foreach (SkillDefinitionAsset asset in registry?.Skills ?? Array.Empty<SkillDefinitionAsset>())
            {
                if (asset == null)
                    throw new InvalidOperationException("skill_asset_missing_reference:index=" + index);
                if (string.IsNullOrWhiteSpace(asset.StableId))
                    throw new InvalidOperationException("skill_asset_missing_id:asset=" + asset.name + ":index=" + index);
                if (!ProductionSkillIds.Contains(asset.StableId))
                    throw new InvalidOperationException("skill_asset_not_production:" + asset.StableId);
                if (!converted.TryAdd(asset.StableId, asset))
                    throw new InvalidOperationException("skill_asset_duplicate_id:" + asset.StableId);
                index++;
            }
            foreach (string skillId in ProductionSkillIds)
                if (!converted.ContainsKey(skillId))
                    throw new InvalidOperationException("production_skill_asset_missing:" + skillId);
            return converted;
        }

        private static ClassDef CopyClass(ClassDef source, Dictionary<string, SkillDefinitionAsset> converted)
        {
            var copy = new ClassDef { Id = source.Id, Name = source.Name, Description = source.Description, PassiveBonus = source.PassiveBonus, BaseStats = CopyStats(source.BaseStats), StatGrowth = CopyGrowth(source.StatGrowth), Passive = CopyPassive(source.Passive), Skills = new List<ClassSkillDef>() };
            foreach (ClassSkillDef legacy in source.Skills ?? new List<ClassSkillDef>())
            {
                if (legacy == null) continue;
                if (ProductionSkillIds.Contains(legacy.Id))
                    throw new InvalidOperationException("production_skill_in_legacy_class:" + source.Id + ":" + legacy.Id);
                copy.Skills.Add(CopySkill(legacy));
            }
            foreach (SkillDefinitionAsset asset in converted.Values)
                if (IsAvailableTo(asset, source.Id)) copy.Skills.Add(CopySkill(asset.ToPureDefinition()));
            return copy;
        }

        private static bool IsAvailableTo(SkillDefinitionAsset asset, ClassId classId)
        {
            IReadOnlyList<ClassId> allowed = asset.AllowedClasses;
            if (allowed == null || allowed.Count == 0) return false;
            foreach (ClassId allowedClass in allowed)
                if (allowedClass == classId) return true;
            return false;
        }

        private static CoreStats CopyStats(CoreStats source) => source == null ? null : new CoreStats { Strength = source.Strength, Agility = source.Agility, Wisdom = source.Wisdom, Luck = source.Luck };
        private static StatGrowthDef CopyGrowth(StatGrowthDef source) => source == null ? null : new StatGrowthDef { Strength = source.Strength, Agility = source.Agility, Wisdom = source.Wisdom, Luck = source.Luck };
        private static PassiveSkillDef CopyPassive(PassiveSkillDef source) => source == null ? null : new PassiveSkillDef
        {
            Name = source.Name, Description = source.Description,
            Multipliers = source.Multipliers == null ? null : new PassiveMultipliers
            {
                Hp = source.Multipliers.Hp, Luck = source.Multipliers.Luck, Xp = source.Multipliers.Xp,
                MiningEff = source.Multipliers.MiningEff, ChoppingEff = source.Multipliers.ChoppingEff,
                GatheringEff = source.Multipliers.GatheringEff, DamagePct = source.Multipliers.DamagePct,
                CritPct = source.Multipliers.CritPct, DefenseFlat = source.Multipliers.DefenseFlat,
                Lifesteal = source.Multipliers.Lifesteal,
            },
        };

        private static ClassSkillDef CopySkill(ClassSkillDef source)
        {
            return new ClassSkillDef { Id = source.Id, Name = source.Name, Description = source.Description, Element = source.Element, DamageMultiplier = source.DamageMultiplier, AoeColor = source.AoeColor, Priority = source.Priority, CooldownMs = source.CooldownMs, Mechanic = source.Mechanic, RangePx = source.RangePx, AoeRadiusPx = source.AoeRadiusPx, ProjectileSpeed = source.ProjectileSpeed, ProjectileRadius = source.ProjectileRadius, Inflicts = SkillDefinitionAsset.CopyStatusInflicts(source.Inflicts), Buff = source.Buff == null ? null : new SelfBuff { Stat = source.Buff.Stat, Magnitude = source.Buff.Magnitude, DurationMs = source.Buff.DurationMs }, Targeting = source.Targeting, TilePattern = SkillDefinitionAsset.CopyTilePattern(source.TilePattern), AutoEnabled = source.AutoEnabled, MinimumAutoTargets = source.MinimumAutoTargets, RadiusWorldUnits = source.RadiusWorldUnits, ModifierProperty = source.ModifierProperty, ModifierOperation = source.ModifierOperation, ModifierMagnitude = source.ModifierMagnitude, ModifierDurationTicks = source.ModifierDurationTicks, Timing = source.Timing, ImpactDelayTicks = source.ImpactDelayTicks, BranchId = source.BranchId, Tier = source.Tier, SkillPointCost = source.SkillPointCost, PrerequisiteSkillId = source.PrerequisiteSkillId };
        }
    }
}
