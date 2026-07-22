using System;
using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class ProgressionAndOfflineTests
    {
        [Test]
        public void ApplyCharacterXp_AwardsLevelAndFreeStatPoint()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 0);
            long award = Progression.XpToNext(1);

            CharacterXpResult result = CharacterHelper.ApplyCharacterXp(character, award);

            Assert.That(result.NewLevel, Is.EqualTo(2));
            Assert.That(result.LevelsGained, Is.EqualTo(1));
            Assert.That(result.Character.FreeStatPoints, Is.EqualTo(1));
        }

        [Test]
        public void OfflineCombat_UsesCanonicalCharacterXpApplication()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 0);
            character.Efficiency = new EfficiencySnapshot
            {
                Kind = ActivityKind.Fighting,
                TargetId = "slime",
                ActionsPerHour = 10,
                XpPerAction = 100,
                CoinsPerAction = 0,
                SnapshotAt = 0,
                ContentVersion = SnapshotValidation.CurrentContentVersion,
                MapDensity = 1.0,
                SurvivalFactor = 1.0,
            };

            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            account.Characters.Add(character);
            var data = new OfflineDataBundle
            {
                Items = new Dictionary<string, ItemDef>(),
                Monsters = new Dictionary<string, MonsterDef>
                {
                    ["slime"] = new MonsterDef { Drops = new DropTable() },
                },
                Nodes = new Dictionary<string, ResourceNodeDef>(),
                OfflineBalance = new OfflineBalanceConfig { Rate = 0.5, CapMs = 3_600_000, MinimumDurationMs = 0 },
            };

            var (after, report) = Offline.SimulateOffline(
                account,
                3_600_000,
                data,
                new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(report, Is.Not.Null);
            long expectedAward = 500;
            Character expected = CharacterHelper.ApplyCharacterXp(character, expectedAward).Character;
            Assert.That(after.Characters[0].Level, Is.EqualTo(expected.Level));
            Assert.That(after.Characters[0].Xp, Is.EqualTo(expected.Xp));
            Assert.That(after.Characters[0].FreeStatPoints, Is.EqualTo(expected.FreeStatPoints));
            Character expectedCombat = CharacterHelper.ApplySkillXp(expected, SkillId.Combat, expectedAward).Character;
            Assert.That(after.Characters[0].Skills[SkillId.Combat].Xp,
                Is.EqualTo(expectedCombat.Skills[SkillId.Combat].Xp));
        }

        [Test]
        public void OfflineCombat_DepositsKillLootIntoInventoryNotBankAndOverflowsWhenFull()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 0);
            character.MaxInventorySlots = 1;
            character.Efficiency = new EfficiencySnapshot
            {
                Kind = ActivityKind.Fighting,
                TargetId = "slime",
                ActionsPerHour = 10,
                XpPerAction = 0,
                CoinsPerAction = 5,
                SnapshotAt = 0,
                ContentVersion = SnapshotValidation.CurrentContentVersion,
                MapDensity = 1.0,
                SurvivalFactor = 1.0,
            };

            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            account.Characters.Add(character);
            var itemDef = new ItemDef { Id = "slime_goo", Name = "Slime Goo", Type = ItemType.Material, StackLimit = 3, SellValue = 1 };
            var data = new OfflineDataBundle
            {
                Items = new Dictionary<string, ItemDef> { ["slime_goo"] = itemDef },
                Monsters = new Dictionary<string, MonsterDef>
                {
                    ["slime"] = new MonsterDef
                    {
                        Drops = new DropTable
                        {
                            Always = new List<DropItem> { new DropItem { ItemId = "slime_goo", Min = 2, Max = 2 } },
                        },
                    },
                },
                Nodes = new Dictionary<string, ResourceNodeDef>(),
                OfflineBalance = new OfflineBalanceConfig { Rate = 1.0, CapMs = 3_600_000, MinimumDurationMs = 0 },
            };

            var (after, report) = Offline.SimulateOffline(
                account,
                3_600_000,
                data,
                new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(report, Is.Not.Null);
            Character updatedCharacter = after.Characters[0];
            // 10 actions/hour * 1 hour = 10 kills * 2 slime_goo each = 20 rolled; a single 1-slot,
            // StackLimit-3 inventory can only hold 3 -> the rest is overflow, never touching the bank.
            Assert.That(updatedCharacter.Inventory.Exists(s => s.ItemId == "slime_goo" && s.Qty == 3), Is.True);
            Assert.That(after.Bank.Slots, Is.Empty);
            Assert.That(after.Bank.Coins, Is.GreaterThan(0));
            Assert.That(report.Characters[0].LootOverflow.Exists(s => s.ItemId == "slime_goo" && s.Qty == 17), Is.True);
        }

        [Test]
        public void OfflineProgression_RejectsSnapshotFromAnOlderCharacterRevision()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 0);
            character.CharacterRevision = 1;
            character.Efficiency = new EfficiencySnapshot
            {
                Kind = ActivityKind.Fighting,
                TargetId = "slime",
                ActionsPerHour = 10,
                XpPerAction = 100,
                SnapshotAt = 0,
                CharacterRevision = 0,
                ActivityRevision = 0,
                ContentVersion = SnapshotValidation.CurrentContentVersion,
                MapDensity = 1.0,
                SurvivalFactor = 1.0,
            };

            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            account.Characters.Add(character);
            var data = new OfflineDataBundle
            {
                Items = new Dictionary<string, ItemDef>(),
                Monsters = new Dictionary<string, MonsterDef>
                {
                    ["slime"] = new MonsterDef { Drops = new DropTable() },
                },
                Nodes = new Dictionary<string, ResourceNodeDef>(),
            };

            var (after, report) = Offline.SimulateOffline(
                account,
                3_600_000,
                data,
                new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(report, Is.Null);
            Assert.That(after.Characters[0].Xp, Is.EqualTo(character.Xp));
            Assert.That(after.Characters[0].Skills[SkillId.Combat].Xp,
                Is.EqualTo(character.Skills[SkillId.Combat].Xp));
        }
    }
}
