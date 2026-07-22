using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using NUnit.Framework;
using UnityEngine;

namespace IdleCloud.Tests
{
    public class SkillUnlockSlotTests
    {
        [SetUp]
        public void SetUp()
        {
            SkillContentRegistryAsset registry = Resources.Load<SkillContentRegistryAsset>("SkillContentRegistry");
            Assert.That(registry, Is.Not.Null, "The production skill registry must be available to EditMode tests.");
            RuntimeContent.Configure(new SkillContentProvider(registry));
        }

        [TearDown]
        public void TearDown() => RuntimeContent.UseLegacyContentForTests();

        [Test]
        public void Unlock_ConsumesPointsAndEnforcesArcaneBranchPrerequisite()
        {
            Character character = CharacterHelper.CreateCharacter(
                "character", "Tester", ClassId.Beginner, "grass_1", 0);
            ClassDef classDef = RuntimeContent.Get(character.ClassId);
            ClassSkillDef bolt = Find(classDef, "arcane_bolt");
            ClassSkillDef burst = Find(classDef, "flame_burst");

            Assert.That(SkillBuild.CanUnlock(character, burst), Is.False);
            Character afterBolt = SkillBuild.Unlock(character, bolt);
            Character afterBurst = SkillBuild.Unlock(afterBolt, burst);

            Assert.That(SkillBuild.IsUnlocked(afterBurst, "arcane_bolt"), Is.True);
            Assert.That(SkillBuild.IsUnlocked(afterBurst, "flame_burst"), Is.True);
            Assert.That(afterBurst.AvailableSkillPoints, Is.EqualTo(SkillBuild.PrototypeStartingPoints - 2));
            Assert.That(afterBurst.SpentSkillPoints, Is.EqualTo(2));
            Assert.That(character.UnlockedSkillIds, Does.Not.Contain("arcane_bolt"),
                "unlocking must not mutate the source character");
        }

        [Test]
        public void DevelopmentRespec_RefundsPointsAndRestoresValidDefaultSlots()
        {
            Character character = CharacterHelper.CreateCharacter(
                "character", "Tester", ClassId.Beginner, "grass_1", 0);
            character = SkillBuild.Unlock(character, Find(RuntimeContent.Get(character.ClassId), "arcane_bolt"));
            character.SkillBar = new List<string> { "arcane_bolt", null, null, null };

            Character reset = SkillBuild.DevelopmentRespec(character);

            Assert.That(reset.AvailableSkillPoints, Is.EqualTo(SkillBuild.PrototypeStartingPoints));
            Assert.That(reset.SpentSkillPoints, Is.Zero);
            Assert.That(reset.UnlockedSkillIds, Does.Not.Contain("arcane_bolt"));
            CollectionAssert.AreEqual(CharacterHelper.CreateDefaultSkillBar(ClassId.Beginner), reset.SkillBar);
            Assert.That(StateInvariantValidator.Validate(AccountWith(reset)), Is.Empty);
        }

        private static ClassSkillDef Find(ClassDef classDef, string skillId)
            => classDef.Skills.Find(skill => skill.Id == skillId);

        private static Account AccountWith(Character character)
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            account.Characters.Add(character);
            return account;
        }
    }
}
