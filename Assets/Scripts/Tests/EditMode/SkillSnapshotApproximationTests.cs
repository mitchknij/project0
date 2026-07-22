using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class SkillSnapshotApproximationTests
    {
        [Test]
        public void Evaluation_UsesSlotPriorityWithoutHiddenDamageRanking()
        {
            var first = new ClassSkillDef
            {
                Id = "first",
                DamageMultiplier = 1.0,
                CooldownMs = 0,
            };
            var strongerSecond = new ClassSkillDef
            {
                Id = "stronger_second",
                DamageMultiplier = 100.0,
                CooldownMs = 0,
            };
            var classDef = new ClassDef { Skills = new List<ClassSkillDef> { first, strongerSecond } };
            var character = new Character
            {
                SkillBar = new List<string> { "first", "stronger_second", null, null },
                UnlockedSkillIds = new List<string> { "first", "stronger_second" },
            };

            SkillRotationApproximation result = SkillSnapshotApproximation.Evaluate(
                character,
                classDef,
                new CoreStats(),
                Monster(),
                1.0);

            Assert.That(result.Diagnostics[0].ExpectedCastsPerHour, Is.GreaterThan(0));
            Assert.That(result.Diagnostics[1].ExpectedCastsPerHour, Is.Zero,
                "a permanently eligible first slot must starve later slots regardless of damage");
        }

        [Test]
        public void Evaluation_ExplainsManualOnlyAndMinimumTargetExclusions()
        {
            var classDef = new ClassDef
            {
                Skills = new List<ClassSkillDef>
                {
                    new ClassSkillDef
                    {
                        Id = "guard",
                        ModifierDurationTicks = 10,
                        AutoEnabled = false,
                    },
                    new ClassSkillDef
                    {
                        Id = "earthbreaker",
                        DamageMultiplier = 1.7,
                        Targeting = SkillTargetingKind.CircleAroundSource,
                        MinimumAutoTargets = 2,
                    },
                },
            };
            var character = new Character
            {
                SkillBar = new List<string> { "guard", "earthbreaker", null, null },
                UnlockedSkillIds = new List<string> { "guard", "earthbreaker" },
            };

            SkillRotationApproximation result = SkillSnapshotApproximation.Evaluate(
                character, classDef, new CoreStats(), Monster(), 1.0);

            Assert.That(result.Diagnostics[0].Reason, Is.EqualTo("auto_disabled"));
            Assert.That(result.Diagnostics[1].Reason, Is.EqualTo("minimum_targets_not_met"));
            Assert.That(result.DebugBreakdown, Does.Contain("slot1:guard=auto_disabled"));
        }

        [Test]
        public void Evaluation_GroundSmashExpectedTargetsAreDensityBoundedAndAssumptionSourceRecordsCrossPattern()
        {
            var groundSmash = new ClassSkillDef
            {
                Id = "ground_smash",
                DamageMultiplier = 2.2,
                Targeting = SkillTargetingKind.TilePatternAroundSource,
                MinimumAutoTargets = 2,
                TilePattern = new TilePatternDef
                {
                    PatternKind = TilePatternKind.Cross,
                    Size = 1,
                    FloorPolicy = TilePatternFloorPolicy.SameFloor,
                },
            };
            var classDef = new ClassDef { Skills = new List<ClassSkillDef> { groundSmash } };
            var character = new Character
            {
                SkillBar = new List<string> { "ground_smash", null, null, null },
                UnlockedSkillIds = new List<string> { "ground_smash" },
            };

            SkillRotationApproximation result = SkillSnapshotApproximation.Evaluate(
                character, classDef, new CoreStats(), Monster(), mapDensity: 6.0);

            SkillSnapshotDiagnostic diagnostic = result.Diagnostics[0];
            Assert.That(diagnostic.ExpectedTargetsPerCast, Is.EqualTo(5.0),
                "affected-tile-count upper bound (5 for a Cross size 1) must cap the density estimate");
            Assert.That(diagnostic.AssumptionSource, Does.Contain("tile_pattern=Cross"));
            Assert.That(diagnostic.Included, Is.True);
        }

        [Test]
        public void Evaluation_ExcludesTileSkillWhenLowDensityFallsBelowMinimumAutoTargets()
        {
            var groundSmash = new ClassSkillDef
            {
                Id = "ground_smash",
                DamageMultiplier = 2.2,
                Targeting = SkillTargetingKind.TilePatternAroundSource,
                MinimumAutoTargets = 2,
                TilePattern = new TilePatternDef
                {
                    PatternKind = TilePatternKind.Cross,
                    Size = 1,
                    FloorPolicy = TilePatternFloorPolicy.SameFloor,
                },
            };
            var classDef = new ClassDef { Skills = new List<ClassSkillDef> { groundSmash } };
            var character = new Character
            {
                SkillBar = new List<string> { "ground_smash", null, null, null },
                UnlockedSkillIds = new List<string> { "ground_smash" },
            };

            SkillRotationApproximation result = SkillSnapshotApproximation.Evaluate(
                character, classDef, new CoreStats(), Monster(), mapDensity: 1.0);

            Assert.That(result.Diagnostics[0].Reason, Is.EqualTo("minimum_targets_not_met"));
        }

        [Test]
        public void Evaluation_RespectsTilePatternMaxTargetsCap()
        {
            var arcaneDetonation = new ClassSkillDef
            {
                Id = "arcane_detonation",
                DamageMultiplier = 2.4,
                Targeting = SkillTargetingKind.TilePatternAroundTarget,
                MinimumAutoTargets = 1,
                TilePattern = new TilePatternDef
                {
                    PatternKind = TilePatternKind.SquareRadius,
                    Size = 1,
                    FloorPolicy = TilePatternFloorPolicy.SameFloor,
                    MaxTargets = 2,
                },
            };
            var classDef = new ClassDef { Skills = new List<ClassSkillDef> { arcaneDetonation } };
            var character = new Character
            {
                SkillBar = new List<string> { "arcane_detonation", null, null, null },
                UnlockedSkillIds = new List<string> { "arcane_detonation" },
            };

            SkillRotationApproximation result = SkillSnapshotApproximation.Evaluate(
                character, classDef, new CoreStats(), Monster(), mapDensity: 20.0);

            Assert.That(result.Diagnostics[0].ExpectedTargetsPerCast, Is.EqualTo(2.0));
        }

        private static MonsterDef Monster() => new MonsterDef
        {
            Hp = 100,
            Defense = 0,
            Accuracy = 1,
            Agility = 0,
            Element = Element.Physical,
        };
    }
}
