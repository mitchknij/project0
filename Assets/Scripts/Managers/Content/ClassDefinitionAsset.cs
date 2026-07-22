using System.Collections.Generic;
using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Content/Class Definition", fileName = "ClassDefinition")]
    public sealed class ClassDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable class enum value. Display names are presentation only.")]
        [SerializeField] private ClassId stableId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string passiveSummary;
        [Header("Stats")]
        [SerializeField] private CoreStats baseStats = new CoreStats();
        [SerializeField] private StatGrowthDef statGrowth = new StatGrowthDef();
        [SerializeField] private PassiveSkillDef passive;
        [Header("Skills")]
        [SerializeField] private List<SkillDefinitionAsset> skills = new List<SkillDefinitionAsset>();

        public ClassId StableId => stableId;
        public ClassDef ToPureDefinition()
        {
            var result = new ClassDef
            {
                Id = stableId, Name = displayName, Description = description, PassiveBonus = passiveSummary,
                BaseStats = baseStats == null ? null : new CoreStats
                {
                    Strength = baseStats.Strength, Agility = baseStats.Agility,
                    Wisdom = baseStats.Wisdom, Luck = baseStats.Luck,
                },
                StatGrowth = statGrowth == null ? null : new StatGrowthDef
                {
                    Strength = statGrowth.Strength, Agility = statGrowth.Agility,
                    Wisdom = statGrowth.Wisdom, Luck = statGrowth.Luck,
                },
                Passive = passive,
                Skills = new List<ClassSkillDef>(),
            };
            foreach (SkillDefinitionAsset skill in skills ?? new List<SkillDefinitionAsset>())
                if (skill != null) result.Skills.Add(skill.ToPureDefinition());
            return result;
        }
    }
}
