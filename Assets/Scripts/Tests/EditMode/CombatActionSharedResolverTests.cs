using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class CombatActionSharedResolverTests
    {
        [Test]
        public void ResolvePlayerDamage_UsesTheSamePipelineForBasicAttackAndSkill()
        {
            var stats = new CoreStats { Strength = 10 };
            var target = new MonsterDef
            {
                Hp = 100,
                Damage = 0,
                Defense = 0,
                Accuracy = 1,
                Agility = 0,
                Element = Element.Physical,
            };

            CombatActionResolution basic = CombatActionResolver.ResolvePlayerDamage(
                CombatActionResolver.BasicAttack(), stats, target, AccountBonuses.Zero(),
                new PassiveMultipliers(), new SequenceRandomSource(new[] { 0.0, 0.99 }));
            CombatActionResolution skill = CombatActionResolver.ResolvePlayerDamage(
                new CombatActionDefinition
                {
                    Id = "power_strike",
                    Kind = CombatActionKind.Skill,
                    Element = Element.Physical,
                    DamageMultiplier = 2.0,
                },
                stats, target, AccountBonuses.Zero(), new PassiveMultipliers(),
                new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(basic.Hit, Is.True);
            Assert.That(skill.Hit, Is.True);
            Assert.That(skill.Damage, Is.EqualTo(basic.Damage * 2));
        }

        [Test]
        public void FromLegacySkill_ProducesADataOnlyActionDefinition()
        {
            CombatActionDefinition action = CombatActionResolver.FromLegacySkill(new ClassSkillDef
            {
                Id = "legacy",
                Element = Element.Fire,
                DamageMultiplier = 1.8,
            });

            Assert.That(action.Id, Is.EqualTo("legacy"));
            Assert.That(action.Kind, Is.EqualTo(CombatActionKind.Skill));
            Assert.That(action.Element, Is.EqualTo(Element.Fire));
            Assert.That(action.DamageMultiplier, Is.EqualTo(1.8));
        }
    }
}
