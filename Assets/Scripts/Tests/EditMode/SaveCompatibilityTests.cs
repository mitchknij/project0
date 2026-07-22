using System;
using System.Collections.Generic;
using System.IO;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using NUnit.Framework;
using UnityEngine;

namespace IdleCloud.Tests
{
    public class SaveCompatibilityTests
    {
        [Test]
        public void CurrentV4_FileRoundTrip_RestoresPersistedStateExactly()
        {
            string directory = Path.Combine(Path.GetTempPath(), "IdleCloud.SaveCompatibilityTests", Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "current-v4.json");
            Directory.CreateDirectory(directory);

            try
            {
                Character character = CharacterHelper.CreateCharacter(
                    "character-v4", "Tester", ClassId.Warrior, MapsRepo.StartingMapId, 456);
                character.Level = 7;
                character.Xp = 321;
                character.CharacterRevision = 12;
                character.ActivityRevision = 13;
                character.Inventory.Add(new ItemStack { ItemId = "round_trip_item", Qty = 3 });
                character.Equipment[EquipSlot.Weapon] = "round_trip_weapon";
                character.Talents["round_trip_talent"] = 2;
                character.FreeStatPoints = 4;
                character.AllocatedStats = new CoreStats { Strength = 3, Agility = 2, Wisdom = 1, Luck = 5 };

                Account account = AccountHelper.CreateAccount("account-v4", "Family", 123);
                account.Characters.Add(character);
                account.Bank.Coins = 987;
                account.Bank.Slots.Add(new ItemStack { ItemId = "round_trip_bank_item", Qty = 9 });
                account.UnlockedWaypoints.Add("round_trip_waypoint");
                account.LastSeenAt = 789;
                account.AutoLoot = true;
                account.AutoCombatDisabled = true;
                SaveData current = SaveManager.MigrateLoadedData(new SaveData
                {
                    SaveSchemaVersion = SaveManager.CurrentSaveSchemaVersion,
                    Account = account,
                });
                var worldKills = new Dictionary<string, int> { ["slime"] = 14, ["wolf"] = 2 };
                var expected = new SaveData
                {
                    SaveSchemaVersion = SaveManager.CurrentSaveSchemaVersion,
                    ContentVersion = SnapshotValidation.CurrentContentVersion,
                    Account = current.Account,
                    SelectedCharacterId = character.Id,
                    WorldKills = worldKills,
                    SessionRevision = 42,
                };

                Assert.That(SaveManager.SaveToPath(path, current.Account, character.Id, worldKills, 42), Is.True);
                SaveData loaded = SaveManager.LoadFromPath(path);

                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.SaveSchemaVersion, Is.EqualTo(SaveManager.CurrentSaveSchemaVersion));
                Assert.That(loaded.ContentVersion, Is.EqualTo(SnapshotValidation.CurrentContentVersion));
                Assert.That(loaded.SelectedCharacterId, Is.EqualTo(character.Id));
                CollectionAssert.AreEquivalent(worldKills, loaded.WorldKills);
                Assert.That(loaded.SessionRevision, Is.EqualTo(42));
                Assert.That(SaveManager.SerializeData(loaded), Is.EqualTo(SaveManager.SerializeData(expected)));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void LoadFromPath_MissingAndCorruptFiles_ReturnsNull()
        {
            string directory = Path.Combine(Path.GetTempPath(), "IdleCloud.SaveCompatibilityTests", Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "save.json");
            Directory.CreateDirectory(directory);

            try
            {
                Assert.That(SaveManager.LoadFromPath(path), Is.Null);
                File.WriteAllText(path, "{not valid json");
                Assert.That(SaveManager.LoadFromPath(path), Is.Null);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void LegacyAccountFixture_DeserializesAfterDataAssemblyMove()
        {
            string path = Path.Combine("Assets", "Scripts", "Tests", "EditMode", "Fixtures", "idlecloud-save-v1.json");
            Account account = JsonUtility.FromJson<Account>(File.ReadAllText(path));

            Assert.That(account.Id, Is.EqualTo("legacy-account"));
            Assert.That(account.Bank.Coins, Is.EqualTo(42));
            Assert.That(account.Characters, Is.Empty);
        }

        [Test]
        public void Migration_V1Envelope_UpgradesSnapshotDefaultsToCurrentSchema()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 100);
            character.Efficiency = new EfficiencySnapshot
            {
                Kind = ActivityKind.Fighting,
                TargetId = "slime",
                ActionsPerHour = 1,
                XpPerAction = 1,
            };
            Account account = AccountHelper.CreateAccount("account", "Family", 100);
            account.Characters.Add(character);

            SaveData migrated = SaveManager.MigrateLoadedData(new SaveData
            {
                SaveSchemaVersion = 1,
                Account = account,
            });

            Assert.That(migrated.SaveSchemaVersion, Is.EqualTo(SaveManager.CurrentSaveSchemaVersion));
            Assert.That(migrated.ContentVersion, Is.EqualTo(SnapshotValidation.CurrentContentVersion));
            Assert.That(migrated.Account.Characters[0].Efficiency.ContentVersion,
                Is.EqualTo(SnapshotValidation.CurrentContentVersion));
            Assert.That(migrated.Account.Characters[0].Efficiency.MapDensity, Is.EqualTo(1.0));
            Assert.That(migrated.Account.Characters[0].Efficiency.SurvivalFactor, Is.EqualTo(1.0));
        }

        [Test]
        public void Migration_PreservesCurrentUnsurvivableSnapshot()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 100);
            character.Efficiency = new EfficiencySnapshot
            {
                Kind = ActivityKind.Fighting,
                TargetId = "slime",
                ActionsPerHour = 0,
                XpPerAction = 1,
                ContentVersion = SnapshotValidation.CurrentContentVersion,
                MapDensity = 1.0,
                SurvivalFactor = 0.0,
            };
            Account account = AccountHelper.CreateAccount("account", "Family", 100);
            account.Characters.Add(character);

            SaveData migrated = SaveManager.MigrateLoadedData(new SaveData { Account = account });

            Assert.That(migrated.Account.Characters[0].Efficiency.SurvivalFactor, Is.EqualTo(0.0));
        }

        [Test]
        public void Migration_V2SaveWithoutSkillBar_SeedsPrioritySortedClassSkills()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Warrior, "grass_1", 0);
            character.SkillBar = null;
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            account.Characters.Add(character);

            SaveData migrated = SaveManager.MigrateLoadedData(new SaveData { Account = account, SaveSchemaVersion = 2 });

            List<string> bar = migrated.Account.Characters[0].SkillBar;
            Assert.That(bar, Has.Count.EqualTo(Character.SkillBarSlots));
            Assert.That(bar[0], Is.EqualTo("battle_roar"));
            Assert.That(bar[1], Is.EqualTo("ground_slam"));
            Assert.That(bar[2], Is.EqualTo("cleaving_throw"));
            Assert.That(bar[3], Is.Null);
        }

        [Test]
        public void Migration_V3MalformedSkillBar_NormalizesLengthForeignIdAndDuplicates()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Warrior, "grass_1", 0);
            character.SkillBar = new List<string> { "ground_slam", "not_a_real_skill", "ground_slam" };
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            account.Characters.Add(character);

            SaveData migrated = SaveManager.MigrateLoadedData(new SaveData { Account = account, SaveSchemaVersion = 3 });

            List<string> bar = migrated.Account.Characters[0].SkillBar;
            Assert.That(bar, Has.Count.EqualTo(Character.SkillBarSlots));
            Assert.That(bar[0], Is.EqualTo("ground_slam"));
            Assert.That(bar[1], Is.Null, "foreign id should be dropped");
            Assert.That(bar[2], Is.Null, "second occurrence of a duplicate should be dropped");
            for (int slot = 3; slot < Character.SkillBarSlots; slot++)
                Assert.That(bar[slot], Is.Null);
        }

        [Test]
        public void Migration_IsIdempotentAcrossRepeatedRuns()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Warrior, "grass_1", 0);
            character.SkillBar = new List<string> { "ground_slam", "not_a_real_skill", "ground_slam" };
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            account.Characters.Add(character);

            SaveData first = SaveManager.MigrateLoadedData(new SaveData { Account = account, SaveSchemaVersion = 3 });
            string firstSnapshot = SaveManager.SerializeData(first);
            SaveData second = SaveManager.MigrateLoadedData(first);

            Assert.That(SaveManager.SerializeData(second), Is.EqualTo(firstSnapshot));
        }

        [Test]
        public void Migration_LegacyCharacterSeedsSkillBuildFromPreservedSlots()
        {
            Character character = CharacterHelper.CreateCharacter(
                "character", "Tester", ClassId.Beginner, "grass_1", 0);
            character.UnlockedSkillIds = null;
            character.AvailableSkillPoints = 0;
            character.SpentSkillPoints = 0;
            character.SkillStateSchemaVersion = 0;
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            account.Characters.Add(character);

            SaveData migrated = SaveManager.MigrateLoadedData(new SaveData
            {
                Account = account,
                SaveSchemaVersion = 3,
            });
            Character result = migrated.Account.Characters[0];

            Assert.That(result.SkillStateSchemaVersion, Is.EqualTo(SkillBuild.CurrentSchemaVersion));
            Assert.That(result.AvailableSkillPoints, Is.EqualTo(SkillBuild.PrototypeStartingPoints));
            foreach (string skillId in result.SkillBar)
                if (skillId != null) Assert.That(result.UnlockedSkillIds, Does.Contain(skillId));
        }
    }
}
