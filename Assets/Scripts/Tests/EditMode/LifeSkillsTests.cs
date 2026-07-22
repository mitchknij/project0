using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class LifeSkillsTests
    {
        [Test]
        public void ActiveGatheringCoordinator_AutoLootStillDepositsToInventoryNotBank()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Beginner, "grass_1", 0);
            account = AccountHelper.AddCharacter(account, character);
            account.AutoLoot = true;

            var coordinator = new ActiveGatheringCoordinator(new SequenceRandomSource(new[] { 0.0 }));
            var startCommands = new List<GatheringCommand>
            {
                new GatheringCommand { Kind = GatheringCommandKind.Start, TargetEntityId = "grass_1.wildflower_patch.01" },
            };
            GatheringWorldFacts world = new GatheringWorldFacts { TargetAvailable = true, TargetInRange = true };

            ActiveGatheringTickResult start = coordinator.Tick(
                account, "player", "grass_1.wildflower_patch.01", "wildflower_patch", world, startCommands, 0, AccountBonuses.Zero());
            ActiveGatheringTickResult completed = coordinator.Tick(
                start.Account, "player", "grass_1.wildflower_patch.01", "wildflower_patch", world, null, 4000, AccountBonuses.Zero());

            Assert.That(completed.Simulation.SuccessfulActions, Is.EqualTo(1));
            Character updatedCharacter = completed.Account.Characters[0];
            Assert.That(updatedCharacter.Inventory.Exists(s => s.ItemId == "wildflower" && s.Qty > 0), Is.True);
            Assert.That(completed.Account.Bank.Slots, Is.Empty);
        }

        [Test]
        public void ActiveGatheringCoordinator_SuccessfulGatherPopulatesSkillLevelFields()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Beginner, "grass_1", 0);
            account = AccountHelper.AddCharacter(account, character);

            var coordinator = new ActiveGatheringCoordinator(new SequenceRandomSource(new[] { 0.0 }));
            var startCommands = new List<GatheringCommand>
            {
                new GatheringCommand { Kind = GatheringCommandKind.Start, TargetEntityId = "grass_1.wildflower_patch.01" },
            };
            GatheringWorldFacts world = new GatheringWorldFacts { TargetAvailable = true, TargetInRange = true };

            ActiveGatheringTickResult start = coordinator.Tick(
                account, "player", "grass_1.wildflower_patch.01", "wildflower_patch", world, startCommands, 0, AccountBonuses.Zero());
            ActiveGatheringTickResult completed = coordinator.Tick(
                start.Account, "player", "grass_1.wildflower_patch.01", "wildflower_patch", world, null, 4000, AccountBonuses.Zero());

            Assert.That(completed.Simulation.SuccessfulActions, Is.EqualTo(1));
            SkillId expectedSkillId = ActivitySkillMapping.ToSkillId(ActivitySkillMapping.ToActivityKind(NodesRepo.Get("wildflower_patch").Skill));
            Assert.That(completed.SkillId, Is.EqualTo(expectedSkillId));
            Assert.That(completed.SkillNewLevel, Is.GreaterThanOrEqualTo(completed.SkillPreviousLevel));
        }

        [Test]
        public void ActiveGatheringCoordinator_SkillLevelCrossingGatherReportsLevelIncrease()
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Beginner, "grass_1", 0);
            ResourceNodeDef node = NodesRepo.Get("wildflower_patch");
            SkillId skillId = ActivitySkillMapping.ToSkillId(ActivitySkillMapping.ToActivityKind(node.Skill));
            character.Skills[skillId] = new SkillProgress
            {
                Level = character.Skills[skillId].Level,
                Xp = Progression.XpToNext(character.Skills[skillId].Level) - 1,
            };
            account = AccountHelper.AddCharacter(account, character);

            var coordinator = new ActiveGatheringCoordinator(new SequenceRandomSource(new[] { 0.0 }));
            var startCommands = new List<GatheringCommand>
            {
                new GatheringCommand { Kind = GatheringCommandKind.Start, TargetEntityId = "grass_1.wildflower_patch.01" },
            };
            GatheringWorldFacts world = new GatheringWorldFacts { TargetAvailable = true, TargetInRange = true };

            ActiveGatheringTickResult start = coordinator.Tick(
                account, "player", "grass_1.wildflower_patch.01", "wildflower_patch", world, startCommands, 0, AccountBonuses.Zero());
            ActiveGatheringTickResult completed = coordinator.Tick(
                start.Account, "player", "grass_1.wildflower_patch.01", "wildflower_patch", world, null, 4000, AccountBonuses.Zero());

            Assert.That(completed.Simulation.SuccessfulActions, Is.EqualTo(1));
            Assert.That(completed.SkillNewLevel, Is.GreaterThan(completed.SkillPreviousLevel));
        }

        [Test]
        public void Tick_ResolvesTimestampBasedGatheringOnce()
        {
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Beginner, "grass_1", 0);
            ResourceNodeDef node = NodesRepo.Get("wildflower_patch");
            var start = Input(0, character, node, null);
            start.Commands = new List<GatheringCommand>
            {
                new GatheringCommand { Kind = GatheringCommandKind.Start, TargetEntityId = "grass_1.wildflower_patch.01" },
            };

            ActiveGatheringResult started = LifeSkills.Tick(start, new SequenceRandomSource(new[] { 0.0 }));
            ActiveGatheringResult completed = LifeSkills.Tick(
                Input(4000, character, node, started.State), new SequenceRandomSource(new[] { 0.0 }));
            ActiveGatheringResult repeated = LifeSkills.Tick(
                Input(4000, character, node, completed.State), new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(started.State.Active, Is.True);
            Assert.That(completed.SuccessfulActions, Is.EqualTo(1));
            Assert.That(completed.SkillXp, Is.EqualTo(node.Xp));
            Assert.That(completed.Loot.Exists(item => item.ItemId == "wildflower"), Is.True);
            Assert.That(repeated.SuccessfulActions, Is.EqualTo(0));
        }

        [Test]
        public void Tick_RequestsMovementWhenTargetIsOutOfRange()
        {
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Beginner, "grass_1", 0);
            ResourceNodeDef node = NodesRepo.Get("copper_vein");
            var active = new ActiveGatheringState
            {
                Active = true,
                TargetEntityId = "grass_1.copper_vein.01",
                NodeId = node.Id,
                LastResolvedAt = 0,
            };
            var input = Input(1000, character, node, active);
            input.World.TargetInRange = false;

            ActiveGatheringResult result = LifeSkills.Tick(input, new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(result.Events.Exists(e => e.Kind == GatheringEventKind.MovementRequested), Is.True);
            Assert.That(result.SuccessfulActions, Is.EqualTo(0));
        }

        [Test]
        public void Tick_ReportsSwingProgressWhileGathering()
        {
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Beginner, "grass_1", 0);
            ResourceNodeDef node = NodesRepo.Get("copper_vein");
            var start = Input(0, character, node, null);
            start.Commands = new List<GatheringCommand>
            {
                new GatheringCommand { Kind = GatheringCommandKind.Start, TargetEntityId = "grass_1.copper_vein.01" },
            };

            ActiveGatheringResult started = LifeSkills.Tick(start, new SequenceRandomSource(new[] { 0.0 }));
            ActiveGatheringResult probe = LifeSkills.Tick(
                Input(10, character, node, started.State), new SequenceRandomSource(new[] { 0.0 }));
            long interval = probe.ActionIntervalMs;

            Assert.That(interval, Is.GreaterThan(10));
            Assert.That(probe.SuccessfulActions, Is.EqualTo(0));
            Assert.That(probe.ActionProgress01, Is.EqualTo(10.0 / interval).Within(1e-9));

            ActiveGatheringResult nearlyDone = LifeSkills.Tick(
                Input(interval - 1, character, node, probe.State), new SequenceRandomSource(new[] { 0.0 }));
            Assert.That(nearlyDone.SuccessfulActions, Is.EqualTo(0));
            Assert.That(nearlyDone.ActionProgress01, Is.EqualTo((interval - 1) / (double)interval).Within(1e-9));
        }

        [Test]
        public void Tick_ResetsProgressAfterCompletedSwing()
        {
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Beginner, "grass_1", 0);
            ResourceNodeDef node = NodesRepo.Get("copper_vein");
            var start = Input(0, character, node, null);
            start.Commands = new List<GatheringCommand>
            {
                new GatheringCommand { Kind = GatheringCommandKind.Start, TargetEntityId = "grass_1.copper_vein.01" },
            };

            ActiveGatheringResult started = LifeSkills.Tick(start, new SequenceRandomSource(new[] { 0.0 }));
            ActiveGatheringResult probe = LifeSkills.Tick(
                Input(10, character, node, started.State), new SequenceRandomSource(new[] { 0.0 }));
            long interval = probe.ActionIntervalMs;

            ActiveGatheringResult completed = LifeSkills.Tick(
                Input(interval, character, node, probe.State), new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(completed.SuccessfulActions, Is.EqualTo(1));
            Assert.That(completed.State.LastResolvedAt, Is.EqualTo(interval));
            Assert.That(completed.ActionProgress01, Is.EqualTo(0.0));
        }

        [Test]
        public void Tick_DoesNotAccrueSwingTimeWhileOutOfRange()
        {
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Beginner, "grass_1", 0);
            ResourceNodeDef node = NodesRepo.Get("copper_vein");
            var active = new ActiveGatheringState
            {
                Active = true,
                TargetEntityId = "grass_1.copper_vein.01",
                NodeId = node.Id,
                LastResolvedAt = 0,
            };
            var walking = Input(1000, character, node, active);
            walking.World.TargetInRange = false;

            ActiveGatheringResult outOfRange = LifeSkills.Tick(walking, new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(outOfRange.State.LastResolvedAt, Is.EqualTo(1000));
            Assert.That(outOfRange.ActionIntervalMs, Is.EqualTo(0));
            Assert.That(outOfRange.ActionProgress01, Is.EqualTo(0.0));

            ActiveGatheringResult arrived = LifeSkills.Tick(
                Input(1500, character, node, outOfRange.State), new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(arrived.ActionIntervalMs, Is.GreaterThan(500));
            Assert.That(arrived.SuccessfulActions, Is.EqualTo(0));
            Assert.That(arrived.ActionProgress01, Is.EqualTo(500.0 / arrived.ActionIntervalMs).Within(1e-9));
        }

        [Test]
        public void Tick_ReportsNoProgressWhenInactive()
        {
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Beginner, "grass_1", 0);
            ResourceNodeDef node = NodesRepo.Get("copper_vein");

            ActiveGatheringResult idle = LifeSkills.Tick(
                Input(1000, character, node, null), new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(idle.ActionIntervalMs, Is.EqualTo(0));
            Assert.That(idle.ActionProgress01, Is.EqualTo(0.0));
        }

        private static ActiveGatheringInput Input(long timestamp, Character character, ResourceNodeDef node, ActiveGatheringState state)
        {
            return new ActiveGatheringInput
            {
                Timestamp = timestamp,
                Character = character,
                Class = ClassesRepo.Get(character.ClassId),
                Node = node,
                TargetEntityId = "grass_1." + node.Id + ".01",
                ItemDefs = ItemsRepo.All,
                Bonuses = AccountBonuses.Zero(),
                State = state,
                World = new GatheringWorldFacts { TargetAvailable = true, TargetInRange = true },
            };
        }
    }
}
