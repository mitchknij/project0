using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class ActiveCombatCoordinatorTests
    {
        [Test]
        public void Tick_EnemyKilledCommitsRewardsOnlyForTheKillEvent()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Warrior, "grass_1", 0);
            character.Level = 10;
            account = AccountHelper.AddCharacter(account, character);
            account.AutoLoot = true;

            var coordinator = new ActiveCombatCoordinator(new SequenceRandomSource(new[] { 0.0, 0.99 }));
            var commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "grass_1.slime.01" },
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "ground_slam" },
            };

            ActiveCombatTickResult first = coordinator.Tick(
                account, "player", "grass_1.slime.01", "slime", InRangeWorld(), commands, 0, AccountBonuses.Zero());
            ActiveCombatTickResult second = coordinator.Tick(
                first.Account, "player", "grass_1.slime.01", "slime", InRangeWorld(), null, 1000, AccountBonuses.Zero());

            Assert.That(first.Reward, Is.Not.Null);
            Assert.That(first.Reward.CharacterXp, Is.GreaterThan(0));
            Assert.That(first.Account.Bank.Coins, Is.GreaterThan(0));
            Assert.That(first.Reward.KillLoot, Has.Count.EqualTo(1));
            Assert.That(first.Reward.KillLoot[0].ActorEntityId, Is.EqualTo("grass_1.slime.01"));
            Assert.That(first.Account.Characters[0].Inventory, Is.Empty);
            Assert.That(first.Account.Bank.Slots, Is.Empty);
            Assert.That(second.Reward, Is.Null);
            Assert.That(second.Simulation.Events.Exists(e => e.Kind == CombatEventKind.EnemyKilled), Is.False);
        }

        [Test]
        public void Tick_RejectsTargetFromAnotherMapWithoutMutatingAccount()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Warrior, "thornhaven", 0);
            account = AccountHelper.AddCharacter(account, character);
            var coordinator = new ActiveCombatCoordinator(new SequenceRandomSource(new[] { 0.0 }));

            ActiveCombatTickResult result = coordinator.Tick(
                account, "player", "grass_1.slime.01", "slime", InRangeWorld(), null, 0, AccountBonuses.Zero());

            Assert.That(result.RejectionReason, Is.EqualTo("target_not_on_character_map"));
            Assert.That(result.Account, Is.Null);
        }

        [Test]
        public void PrepareNextEncounter_KeepsPlayerStateAndResetsEnemyState()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Warrior, "grass_1", 0);
            account = AccountHelper.AddCharacter(account, character);
            var coordinator = new ActiveCombatCoordinator(new SequenceRandomSource(new[] { 0.0, 0.99 }));

            ActiveCombatTickResult first = coordinator.Tick(
                account, "player", "grass_1.slime.01", "slime", InRangeWorld(),
                new List<CombatCommand> { new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "grass_1.slime.01" } },
                0, AccountBonuses.Zero());
            coordinator.PrepareNextEncounter("player");
            ActiveCombatTickResult next = coordinator.Tick(
                first.Account, "player", "grass_1.slime.02", "slime", OutOfRangeWorld(), null, 1000, AccountBonuses.Zero());

            Assert.That(next.Simulation.State.PlayerHp, Is.EqualTo(first.Simulation.State.PlayerHp));
            Assert.That(next.Simulation.State.KillResolved, Is.False);
            Assert.That(next.Simulation.State.EnemyHp, Is.LessThanOrEqualTo(MonstersRepo.Get("slime").Hp));
        }

        [Test]
        public void Tick_ForwardsConfigForAutoSkillRotation()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Beginner, "grass_1", 0);
            account = AccountHelper.AddCharacter(account, character);
            var coordinator = new ActiveCombatCoordinator(new SequenceRandomSource(new[] { 0.0, 0.99 }))
            {
                Config = new ActiveCombatConfig { AutoSkillRotation = true },
            };

            ActiveCombatTickResult result = coordinator.Tick(
                account, "player", "grass_1.slime.01", "slime", InRangeWorld(), null, 0, AccountBonuses.Zero());

            Assert.That(result.Simulation.Events.Exists(e => e.Kind == CombatEventKind.SkillExecuted), Is.True);
        }

        [Test]
        public void Tick_EnemyKilledPopulatesKillLootCoinsSummingToRewardCoins()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Warrior, "grass_1", 0);
            character.Level = 10;
            account = AccountHelper.AddCharacter(account, character);
            account.AutoLoot = true;

            var coordinator = new ActiveCombatCoordinator(new SequenceRandomSource(new[] { 0.0, 0.99 }));
            var commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "grass_1.slime.01" },
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "ground_slam" },
            };

            ActiveCombatTickResult first = coordinator.Tick(
                account, "player", "grass_1.slime.01", "slime", InRangeWorld(), commands, 0, AccountBonuses.Zero());

            Assert.That(first.Reward, Is.Not.Null);
            Assert.That(first.Reward.KillLoot, Has.Count.EqualTo(1));
            int killLootCoinsSum = 0;
            foreach (KillLootRecord record in first.Reward.KillLoot)
                killLootCoinsSum += record.Coins;
            Assert.That(killLootCoinsSum, Is.EqualTo(first.Reward.Coins));
        }

        [Test]
        public void Tick_EnemyKilledCrossingLevelThresholdReportsLevelTransitions()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Warrior, "grass_1", 0);
            character.Level = 10;
            character.Xp = Progression.XpToNext(character.Level) - 1;
            character.Skills[SkillId.Combat] = new SkillProgress
            {
                Level = 10,
                Xp = Progression.XpToNext(10) - 1,
            };
            account = AccountHelper.AddCharacter(account, character);
            account.AutoLoot = true;

            var coordinator = new ActiveCombatCoordinator(new SequenceRandomSource(new[] { 0.0, 0.99 }));
            var commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "grass_1.slime.01" },
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "ground_slam" },
            };

            ActiveCombatTickResult first = coordinator.Tick(
                account, "player", "grass_1.slime.01", "slime", InRangeWorld(), commands, 0, AccountBonuses.Zero());

            Assert.That(first.Reward, Is.Not.Null);
            Assert.That(first.Reward.CharacterNewLevel, Is.GreaterThan(first.Reward.CharacterPreviousLevel));
            Assert.That(first.Reward.CombatSkillNewLevel, Is.GreaterThan(first.Reward.CombatSkillPreviousLevel));
        }

        [Test]
        public void Tick_EnemyKilledWithoutCrossingLevelThresholdKeepsSameLevel()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Warrior, "grass_1", 0);
            character.Level = 10;
            account = AccountHelper.AddCharacter(account, character);
            account.AutoLoot = true;

            var coordinator = new ActiveCombatCoordinator(new SequenceRandomSource(new[] { 0.0, 0.99 }));
            var commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "grass_1.slime.01" },
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "ground_slam" },
            };

            ActiveCombatTickResult first = coordinator.Tick(
                account, "player", "grass_1.slime.01", "slime", InRangeWorld(), commands, 0, AccountBonuses.Zero());

            Assert.That(first.Reward, Is.Not.Null);
            Assert.That(first.Reward.CharacterNewLevel, Is.EqualTo(first.Reward.CharacterPreviousLevel));
        }

        private static CombatWorldFacts InRangeWorld() => new CombatWorldFacts
        {
            TargetAvailable = true,
            TargetInRange = true,
            LineOfSight = true,
        };

        private static CombatWorldFacts OutOfRangeWorld() => new CombatWorldFacts
        {
            TargetAvailable = true,
            TargetInRange = false,
            LineOfSight = true,
        };
    }
}
