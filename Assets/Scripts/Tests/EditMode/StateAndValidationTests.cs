using System;
using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class StateAndValidationTests
    {
        [Test]
        public void AccountClone_DoesNotShareNestedMutableState()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 0);
            character.Inventory.Add(new ItemStack { ItemId = "ore", Qty = 1 });
            account.Characters.Add(character);

            Account clone = account.Clone();
            clone.Characters[0].Inventory[0].Qty = 99;
            clone.Characters[0].Skills[SkillId.Mining].Level = 20;
            clone.Bank.Slots.Add(new ItemStack { ItemId = "ore", Qty = 5 });

            Assert.That(account.Characters[0].Inventory[0].Qty, Is.EqualTo(1));
            Assert.That(account.Characters[0].Skills[SkillId.Mining].Level, Is.EqualTo(1));
            Assert.That(account.Bank.Slots, Is.Empty);
        }

        [Test]
        public void InventoryAndBank_RejectInvalidQuantitiesAndStackLimits()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 0);
            var item = new ItemDef { Id = "ore", StackLimit = 0 };

            Assert.Throws<ArgumentOutOfRangeException>(() => Inventory.AddToInventory(
                character, new ItemStack { ItemId = "ore", Qty = 1 }, item));
            Assert.Throws<ArgumentOutOfRangeException>(() => BankHelper.WithdrawFromBank(
                new Bank { MaxSlots = 1, Slots = new List<ItemStack>() }, "ore", 0));
        }

        [Test]
        public void Talents_NormalizeMalformedNegativePointsAndReturnDeepStateCopies()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 0);
            character.Level = 2;
            character.Talents["starter"] = -5;
            var talent = new TalentDef
            {
                Id = "starter",
                AvailableToAll = true,
                MaxPoints = 3,
            };

            Character allocated = Talents.AllocateTalent(character, talent);
            Character reset = Talents.ResetTalents(allocated);
            reset.Skills[SkillId.Mining].Level = 20;

            Assert.That(Talents.AvailableTalentPoints(character), Is.EqualTo(2));
            Assert.That(allocated.Talents["starter"], Is.EqualTo(1));
            Assert.That(allocated.Skills[SkillId.Mining].Level, Is.EqualTo(1));
            Assert.That(character.Talents["starter"], Is.EqualTo(-5));
        }

        [Test]
        public void StateInvariantValidator_ReportsMalformedPersistedStateWithoutMutatingIt()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            account.Bank.Coins = -1;
            Character character = CharacterHelper.CreateCharacter("", "Tester", ClassId.Beginner, "grass_1", 0);
            character.Level = 0;
            character.Inventory.Add(new ItemStack { ItemId = "ore", Qty = 0 });
            account.Characters.Add(character);

            var issues = StateInvariantValidator.Validate(account);

            Assert.That(issues, Does.Contain("bank_coins_negative"));
            Assert.That(issues, Does.Contain("character_id_missing"));
            Assert.That(issues, Has.Some.StartsWith("character_level_invalid:"));
            Assert.That(issues, Has.Some.StartsWith("item_stack_invalid:inventory:"));
            Assert.That(account.Bank.Coins, Is.EqualTo(-1));
        }

        [Test]
        public void StateInvariantValidator_ReportsSkillBarIssues()
        {
            Account nullBarAccount = AccountHelper.CreateAccount("account", "Family", 0);
            Character nullBarCharacter = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Warrior, "grass_1", 0);
            nullBarCharacter.SkillBar = null;
            nullBarAccount.Characters.Add(nullBarCharacter);

            List<string> nullBarIssues = StateInvariantValidator.Validate(nullBarAccount);
            Assert.That(nullBarIssues, Has.Some.StartsWith("skill_bar_missing:"));

            Account wrongLengthAccount = AccountHelper.CreateAccount("account", "Family", 0);
            Character wrongLengthCharacter = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Warrior, "grass_1", 0);
            wrongLengthCharacter.SkillBar = new List<string> { "ground_slam" };
            wrongLengthAccount.Characters.Add(wrongLengthCharacter);

            List<string> wrongLengthIssues = StateInvariantValidator.Validate(wrongLengthAccount);
            Assert.That(wrongLengthIssues, Has.Some.StartsWith("skill_bar_length_invalid:"));

            Account foreignSkillAccount = AccountHelper.CreateAccount("account", "Family", 0);
            Character foreignSkillCharacter = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Warrior, "grass_1", 0);
            foreignSkillCharacter.SkillBar = CharacterHelper.CreateDefaultSkillBar(ClassId.Warrior);
            foreignSkillCharacter.SkillBar[3] = "not_a_real_skill";
            foreignSkillAccount.Characters.Add(foreignSkillCharacter);

            List<string> foreignSkillIssues = StateInvariantValidator.Validate(foreignSkillAccount);
            Assert.That(foreignSkillIssues, Has.Some.StartsWith("skill_bar_skill_invalid:"));

            Account duplicateAccount = AccountHelper.CreateAccount("account", "Family", 0);
            Character duplicateCharacter = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Warrior, "grass_1", 0);
            duplicateCharacter.SkillBar = CharacterHelper.CreateDefaultSkillBar(ClassId.Warrior);
            duplicateCharacter.SkillBar[3] = duplicateCharacter.SkillBar[0];
            duplicateAccount.Characters.Add(duplicateCharacter);

            List<string> duplicateIssues = StateInvariantValidator.Validate(duplicateAccount);
            Assert.That(duplicateIssues, Has.Some.StartsWith("skill_bar_duplicate:"));
        }

        [Test]
        public void CreateCharacter_ProducesAPrioritySortedSkillBarWithNoValidationIssues()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Warrior, "grass_1", 0);
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            account.Characters.Add(character);

            List<string> issues = StateInvariantValidator.Validate(account);

            Assert.That(issues, Has.None.Matches<string>(issue => issue.StartsWith("skill_bar_")));
            Assert.That(character.SkillBar, Has.Count.EqualTo(Character.SkillBarSlots));
            Assert.That(character.SkillBar[0], Is.EqualTo("battle_roar"));
            Assert.That(character.SkillBar[1], Is.EqualTo("ground_slam"));
            Assert.That(character.SkillBar[2], Is.EqualTo("cleaving_throw"));
            Assert.That(character.SkillBar[3], Is.Null);
        }

        [Test]
        public void StartingMap_ContainsTheStarterCombatAndGatheringDefinitions()
        {
            Assert.That(MonstersRepo.Get("slime").MapId, Is.EqualTo(MapsRepo.StartingMapId));
            Assert.That(NodesRepo.Get("oak_tree").MapId, Is.EqualTo(MapsRepo.StartingMapId));
            Assert.That(NodesRepo.Get("copper_vein").MapId, Is.EqualTo(MapsRepo.StartingMapId));
            Assert.That(NodesRepo.Get("wildflower_patch").MapId, Is.EqualTo(MapsRepo.StartingMapId));
        }
    }
}
