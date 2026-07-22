using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class SkillDefinitionValidationTests
    {
        [Test]
        public void Validate_AcceptsACompletePureDamageSkill()
        {
            List<string> issues = SkillDefinitionValidation.Validate(new SkillDefinition
            {
                Id = "power_strike",
                Name = "Power Strike",
                BranchId = "power",
                Tier = 1,
                Targeting = SkillTargetingKind.HostileActor,
                Timing = SkillTimingKind.Immediate,
                CooldownTicks = 80,
                ContentVersion = "test-v1",
                Effects = new List<SkillEffectDefinition>
                {
                    new SkillEffectDefinition
                    {
                        Kind = SkillEffectKind.DealDamage,
                        Element = Element.Physical,
                        Magnitude = 1.8,
                    },
                },
            });

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Validate_ReportsMissingIdentityVersionAndEffects()
        {
            List<string> issues = SkillDefinitionValidation.Validate(new SkillDefinition());

            Assert.That(issues, Does.Contain("skill_id_missing"));
            Assert.That(issues, Does.Contain("skill_content_version_missing"));
            Assert.That(issues, Does.Contain("skill_effects_missing"));
        }
    }
}
