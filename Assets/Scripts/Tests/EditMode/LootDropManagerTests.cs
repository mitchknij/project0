using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class LootDropManagerTests
    {
        private static Dictionary<string, ItemDef> Items() => new Dictionary<string, ItemDef>
        {
            ["ore"] = new ItemDef { Id = "ore", Name = "Ore", Type = ItemType.Material, StackLimit = 5, SellValue = 1 },
        };

        private static LootDropConfig Config(long despawnMs = 10000, long vacuumMs = 2000) => new LootDropConfig
        {
            DespawnMs = despawnMs,
            AutoVacuumDelayMs = vacuumMs,
        };

        private static Account NewAccountWithCharacter(out Character character)
        {
            Account account = AccountHelper.CreateAccount("account", "Family", 0);
            character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Warrior, "grass_1", 0);
            return AccountHelper.AddCharacter(account, character);
        }

        [Test]
        public void Spawn_ReturnsDropIdAndFiresLootSpawned()
        {
            var manager = new LootDropManager(Items(), Config());
            LootSpawnedEvent captured = null;
            manager.LootSpawned += e => captured = e;

            string dropId = manager.Spawn(
                "grass_1", new CombatTileCoordinate(1, 1), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 3 } }, 0);

            Assert.That(dropId, Is.Not.Null.And.Not.Empty);
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.DropId, Is.EqualTo(dropId));
            Assert.That(captured.Record.Stacks[0].ItemId, Is.EqualTo("ore"));
            Assert.That(captured.Record.Stacks[0].Qty, Is.EqualTo(3));
        }

        [Test]
        public void ClearMap_RemovesOnlyLootFromTheMapThatWasLeft()
        {
            var manager = new LootDropManager(Items(), Config());
            string grassDrop = manager.Spawn(
                "grass_1", new CombatTileCoordinate(1, 1), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 1 } }, 0);
            string caveDrop = manager.Spawn(
                "rock_1", new CombatTileCoordinate(2, 1), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 1 } }, 0);

            Assert.That(manager.ClearMap("grass_1"), Is.EqualTo(1));
            Account account = NewAccountWithCharacter(out Character character);
            LootPickupResult missingGrassDrop = manager.TryPickup(account, character.Id, grassDrop);
            LootPickupResult remainingCaveDrop = manager.TryPickup(account, character.Id, caveDrop);

            Assert.That(missingGrassDrop.PickedStacks, Is.Empty);
            Assert.That(remainingCaveDrop.PickedStacks, Has.Count.EqualTo(1));
            Assert.That(remainingCaveDrop.PickedStacks[0].Qty, Is.EqualTo(1));
        }

        [Test]
        public void TryPickup_FullPickupRemovesRecordAndFiresLootPickedUpOnce()
        {
            var manager = new LootDropManager(Items(), Config());
            Account account = NewAccountWithCharacter(out Character character);
            string dropId = manager.Spawn(
                "grass_1", new CombatTileCoordinate(1, 1), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 3 } }, 0);

            int firedCount = 0;
            manager.LootPickedUp += _ => firedCount++;

            LootPickupResult result = manager.TryPickup(account, character.Id, dropId);

            Assert.That(result.PickedStacks, Has.Count.EqualTo(1));
            Assert.That(result.PickedStacks[0].Qty, Is.EqualTo(3));
            Assert.That(result.RemainingStacks, Is.Empty);
            Assert.That(firedCount, Is.EqualTo(1));

            int firedAfterRemoval = firedCount;
            LootPickupResult second = manager.TryPickup(result.Account, character.Id, dropId);
            Assert.That(second.PickedStacks, Is.Empty);
            Assert.That(second.RemainingStacks, Is.Empty);
            Assert.That(firedCount, Is.EqualTo(firedAfterRemoval));
        }

        [Test]
        public void TryPickup_VacuumTrueFlagsPickedUpEventAsVacuum()
        {
            var manager = new LootDropManager(Items(), Config());
            Account account = NewAccountWithCharacter(out Character character);
            string dropId = manager.Spawn(
                "grass_1", new CombatTileCoordinate(1, 1), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 3 } }, 0);

            LootPickedUpEvent captured = null;
            manager.LootPickedUp += e => captured = e;

            manager.TryPickup(account, character.Id, dropId, vacuum: true);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.Vacuum, Is.True);
        }

        [Test]
        public void TryPickup_DefaultOverloadFlagsPickedUpEventAsNotVacuum()
        {
            var manager = new LootDropManager(Items(), Config());
            Account account = NewAccountWithCharacter(out Character character);
            string dropId = manager.Spawn(
                "grass_1", new CombatTileCoordinate(1, 1), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 3 } }, 0);

            LootPickedUpEvent captured = null;
            manager.LootPickedUp += e => captured = e;

            manager.TryPickup(account, character.Id, dropId);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.Vacuum, Is.False);
        }

        [Test]
        public void TryPickup_PartialPickupLeavesRemainderInRecordUntilSpaceFrees()
        {
            var manager = new LootDropManager(Items(), Config());
            Account account = NewAccountWithCharacter(out Character character);

            character.MaxInventorySlots = 1;
            character.Inventory = new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 4 } };
            account = AccountHelper.UpdateCharacter(account, character.Id, _ => character);

            string dropId = manager.Spawn(
                "grass_1", new CombatTileCoordinate(1, 1), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 3 } }, 0);

            LootPickupResult first = manager.TryPickup(account, character.Id, dropId);

            Assert.That(first.PickedStacks, Has.Count.EqualTo(1));
            Assert.That(first.PickedStacks[0].Qty, Is.EqualTo(1));
            Assert.That(first.RemainingStacks, Has.Count.EqualTo(1));
            Assert.That(first.RemainingStacks[0].Qty, Is.EqualTo(2));

            Character freed = first.Account.Characters[0];
            freed.Inventory = new List<ItemStack>();
            Account freedAccount = AccountHelper.UpdateCharacter(first.Account, character.Id, _ => freed);

            LootPickupResult second = manager.TryPickup(freedAccount, character.Id, dropId);

            Assert.That(second.PickedStacks, Has.Count.EqualTo(1));
            Assert.That(second.PickedStacks[0].Qty, Is.EqualTo(2));
            Assert.That(second.RemainingStacks, Is.Empty);
        }

        [Test]
        public void TryPickup_NothingPickableFiresNoEventAndLeavesRemainingStacks()
        {
            var manager = new LootDropManager(Items(), Config());
            Account account = NewAccountWithCharacter(out Character character);

            character.MaxInventorySlots = 1;
            character.Inventory = new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 5 } };
            account = AccountHelper.UpdateCharacter(account, character.Id, _ => character);

            string dropId = manager.Spawn(
                "grass_1", new CombatTileCoordinate(1, 1), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 2 } }, 0);

            bool fired = false;
            manager.LootPickedUp += _ => fired = true;

            LootPickupResult result = manager.TryPickup(account, character.Id, dropId);

            Assert.That(fired, Is.False);
            Assert.That(result.PickedStacks, Is.Empty);
            Assert.That(result.RemainingStacks, Is.Not.Empty);
        }

        [Test]
        public void CollectDue_ExpiresRecordPastDespawnAndFiresLootExpired()
        {
            var manager = new LootDropManager(Items(), Config(despawnMs: 1000, vacuumMs: 10000));
            string dropId = manager.Spawn(
                "grass_1", new CombatTileCoordinate(1, 1), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 2 } }, 0);

            LootExpiredEvent captured = null;
            manager.LootExpired += e => captured = e;

            List<string> due = manager.CollectDue(1000, autoLootEnabled: false);

            Assert.That(due, Is.Empty);
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.DropId, Is.EqualTo(dropId));
            Assert.That(captured.Stacks[0].Qty, Is.EqualTo(2));
        }

        [Test]
        public void CollectDue_AutoVacuumIsThrottledUntilTheNextDelayWindow()
        {
            var manager = new LootDropManager(Items(), Config(despawnMs: 100000, vacuumMs: 500));
            manager.Spawn(
                "grass_1", new CombatTileCoordinate(1, 1), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 2 } }, 0);

            List<string> firstDue = manager.CollectDue(500, autoLootEnabled: true);
            Assert.That(firstDue, Has.Count.EqualTo(1));

            List<string> secondDue = manager.CollectDue(500, autoLootEnabled: true);
            Assert.That(secondDue, Is.Empty);

            List<string> thirdDue = manager.CollectDue(1000, autoLootEnabled: true);
            Assert.That(thirdDue, Has.Count.EqualTo(1));
        }

        [Test]
        public void ClearMap_RemovesOnlyThatMapsRecords()
        {
            var manager = new LootDropManager(Items(), Config());
            string dropOnA = manager.Spawn(
                "map_a", new CombatTileCoordinate(0, 0), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 1 } }, 0);
            string dropOnB = manager.Spawn(
                "map_b", new CombatTileCoordinate(0, 0), 0,
                new List<ItemStack> { new ItemStack { ItemId = "ore", Qty = 1 } }, 0);

            int removed = manager.ClearMap("map_a");

            Assert.That(removed, Is.EqualTo(1));
            Account account = NewAccountWithCharacter(out Character character);

            LootPickupResult resultA = manager.TryPickup(account, character.Id, dropOnA);
            Assert.That(resultA.PickedStacks, Is.Empty);
            Assert.That(resultA.RemainingStacks, Is.Empty);

            LootPickupResult resultB = manager.TryPickup(account, character.Id, dropOnB);
            Assert.That(resultB.PickedStacks, Has.Count.EqualTo(1));
        }
    }
}
