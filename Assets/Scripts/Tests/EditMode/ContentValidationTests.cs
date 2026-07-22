using System.Collections.Generic;
using System.Reflection;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using NUnit.Framework;
using UnityEngine;

namespace IdleCloud.Tests
{
    public class ContentValidationTests
    {
        [SetUp]
        public void ResetRuntimeContent()
        {
            RuntimeContent.UseLegacyContentForTests();
            SnapshotValidation.ConfigureContentVersion("code-v3");
        }

        [TearDown]
        public void RestoreCoreConfiguration()
        {
            RuntimeContent.UseLegacyContentForTests();
            SnapshotValidation.ConfigureContentVersion("code-v3");
            CombatMath.Configure(new CombatBalanceConfig());
            Progression.Configure(new ProgressionBalanceConfig());
        }

        [Test]
        public void CurrentContentRegistries_AreValid()
        {
            Assert.That(ContentValidator.Validate(), Is.Empty);
        }

        [Test]
        public void ProductionSkillAssets_CopyTilePatternsOnlyForTileTargeting()
        {
            SkillContentRegistryAsset registry = Resources.Load<SkillContentRegistryAsset>("SkillContentRegistry");
            Assert.That(registry, Is.Not.Null);

            for (int index = 0; index < registry.Skills.Count; index++)
            {
                SkillDefinitionAsset asset = registry.Skills[index];
                Assert.That(asset, Is.Not.Null, "Registry entry " + index + " is missing.");
                ClassSkillDef skill = asset.ToPureDefinition();
                bool tileTargeting = skill.Targeting == SkillTargetingKind.TilePatternAroundSource ||
                    skill.Targeting == SkillTargetingKind.TilePatternAroundTarget;
                Assert.That(skill.TilePattern != null, Is.EqualTo(tileTargeting), asset.StableId);
            }
        }

        [Test]
        public void ContentRegistry_ConvertsToPureRuntimeDefinitionsAndCarriesVersion()
        {
            ContentRegistryAsset registry = Resources.Load<ContentRegistryAsset>("ContentRegistry");
            Assert.That(registry, Is.Not.Null);

            var provider = new ContentRegistryProvider(registry);
            Assert.That(provider.Get(ClassId.Beginner), Is.Not.Null);
            Assert.That(provider.Items, Is.Not.Null);
            Assert.That(provider.Maps, Is.Not.Empty);
            Assert.That(provider.ConfigurationVersion, Is.EqualTo("content-v1+balance-v1"));
        }

        [Test]
        public void OfflineConfig_ConvertsMinutesAndHoursToMilliseconds()
        {
            OfflineProgressionConfigAsset config = Resources.Load<OfflineProgressionConfigAsset>("OfflineProgressionConfig");
            Assert.That(config, Is.Not.Null);
            OfflineBalanceConfig pure = config.ToPureDefinition();
            Assert.That(pure.Rate, Is.EqualTo(0.4).Within(0.0001));
            Assert.That(pure.CapMs, Is.EqualTo(24L * 60 * 60 * 1000));
            Assert.That(pure.MinimumDurationMs, Is.EqualTo(60L * 1000));
        }

        [Test]
        public void SnapshotVersionChangeInvalidatesExistingSnapshot()
        {
            var snapshot = new EfficiencySnapshot
            {
                ContentVersion = "content-a",
                ActionsPerHour = 1,
                XpPerAction = 1,
                CoinsPerAction = 0,
                MapDensity = 1,
                SurvivalFactor = 1,
            };
            SnapshotValidation.ConfigureContentVersion("content-a");
            Assert.That(SnapshotValidation.IsUsable(snapshot), Is.True);
            SnapshotValidation.ConfigureContentVersion("content-b");
            Assert.That(SnapshotValidation.IsUsable(snapshot), Is.False);
            SnapshotValidation.ConfigureContentVersion("code-v3");
        }

        [Test]
        public void CoreFormula_UsesSuppliedBalanceConfiguration()
        {
            CombatMath.Configure(new CombatBalanceConfig
            {
                MaxHpBase = 10,
                MaxHpPerLevel = 0,
                MaxHpPerStrength = 0,
            });
            Assert.That(CombatMath.MaxHp(20, new CoreStats { Strength = 99 }), Is.EqualTo(10));
            CombatMath.Configure(new CombatBalanceConfig());
        }

        [Test]
        public void ValidateTilePattern_AcceptsAWellFormedSameFloorCross()
        {
            List<string> issues = ValidateTilePattern(new TilePatternDef
            {
                PatternKind = TilePatternKind.Cross,
                Size = 1,
                FloorPolicy = TilePatternFloorPolicy.SameFloor,
            });

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void ValidateTilePattern_RejectsCrossOrSquareRadiusWithZeroSize()
        {
            Assert.That(ValidateTilePattern(new TilePatternDef
            {
                PatternKind = TilePatternKind.Cross,
                Size = 0,
            }), Has.Some.Contains("tile_pattern_size"));

            Assert.That(ValidateTilePattern(new TilePatternDef
            {
                PatternKind = TilePatternKind.SquareRadius,
                Size = 0,
            }), Has.Some.Contains("tile_pattern_size"));
        }

        [Test]
        public void ValidateTilePattern_RejectsEmptyCustomOffsets()
        {
            Assert.That(ValidateTilePattern(new TilePatternDef
            {
                PatternKind = TilePatternKind.CustomOffsets,
                CustomOffsets = new List<CombatTileCoordinate>(),
            }), Has.Some.Contains("tile_pattern_offsets_missing"));
        }

        [Test]
        public void ValidateTilePattern_RejectsDuplicateCustomOffsets()
        {
            Assert.That(ValidateTilePattern(new TilePatternDef
            {
                PatternKind = TilePatternKind.CustomOffsets,
                CustomOffsets = new List<CombatTileCoordinate>
                {
                    new CombatTileCoordinate(1, 1),
                    new CombatTileCoordinate(1, 1),
                },
            }), Has.Some.Contains("tile_pattern_duplicate_offset"));
        }

        [Test]
        public void ValidateTilePattern_RejectsCustomOffsetsBeyondTheSafeMagnitude()
        {
            Assert.That(ValidateTilePattern(new TilePatternDef
            {
                PatternKind = TilePatternKind.CustomOffsets,
                CustomOffsets = new List<CombatTileCoordinate>
                {
                    new CombatTileCoordinate(TilePatternDef.MaxSafeOffsetMagnitude + 1, 0),
                },
            }), Has.Some.Contains("tile_pattern_offset_range"));
        }

        [Test]
        public void ValidateDropTable_FlagsTertiaryEntryWithUnknownItemId()
        {
            var table = new DropTable
            {
                Tertiary = new List<DropEntry>
                {
                    new DropEntry { ItemId = "totally_unknown_item", Chance = 0.5, Min = 1, Max = 1 },
                },
            };

            List<string> issues = ValidateDropTable("test_prefix", table);

            Assert.That(issues, Has.Some.Contains("test_prefix:tertiary"));
        }

        private static List<string> ValidateDropTable(string prefix, DropTable table)
        {
            var issues = new List<string>();
            MethodInfo method = typeof(ContentValidator).GetMethod(
                "ValidateDropTable", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "ContentValidator.ValidateDropTable must exist");
            method.Invoke(null, new object[] { prefix, table, issues });
            return issues;
        }

        /// <summary>
        /// ContentValidator.Validate() only inspects the static repo registries, so tile-pattern
        /// authoring rules are exercised directly through the private ValidateTilePattern helper.
        /// </summary>
        private static List<string> ValidateTilePattern(TilePatternDef pattern)
        {
            var issues = new List<string>();
            MethodInfo method = typeof(ContentValidator).GetMethod(
                "ValidateTilePattern", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "ContentValidator.ValidateTilePattern must exist");
            method.Invoke(null, new object[] { "test_prefix", pattern, issues });
            return issues;
        }
    }
}
