using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class CraftingTests
    {
        // recipe_copper_pickaxe: 6x copper_ore + 4x oak_log + 30 coins -> 1x copper_pickaxe (Lv.1)

        [Test]
        public void Craft_UsesInventoryMaterialsWhenBankHasNone()
        {
            RecipeDef recipe = RecipesRepo.Get("recipe_copper_pickaxe");
            Bank bank = MakeBank(30);
            Character character = MakeCharacter(
                new ItemStack { ItemId = "copper_ore", Qty = 9 },
                new ItemStack { ItemId = "oak_log", Qty = 4 });

            var (ok, reason) = Crafting.CanCraft(bank, character, recipe, character.Level, ItemsRepo.All);
            Assert.That(ok, Is.True, reason);

            var crafted = Crafting.Craft(bank, character, recipe, character.Level, ItemsRepo.All);

            Assert.That(CountItem(crafted.Character.Inventory, "copper_pickaxe"), Is.EqualTo(1));
            Assert.That(CountItem(crafted.Bank.Slots, "copper_pickaxe"), Is.EqualTo(0));
            Assert.That(crafted.Bank.Coins, Is.EqualTo(0));
            Assert.That(CountItem(crafted.Character.Inventory, "copper_ore"), Is.EqualTo(3));
            Assert.That(CountItem(crafted.Character.Inventory, "oak_log"), Is.EqualTo(0));
        }

        [Test]
        public void Craft_ConsumesBankMaterialsBeforeInventory()
        {
            RecipeDef recipe = RecipesRepo.Get("recipe_copper_pickaxe");
            Bank bank = MakeBank(30,
                new ItemStack { ItemId = "copper_ore", Qty = 2 },
                new ItemStack { ItemId = "oak_log", Qty = 4 });
            Character character = MakeCharacter(
                new ItemStack { ItemId = "copper_ore", Qty = 5 });

            var crafted = Crafting.Craft(bank, character, recipe, character.Level, ItemsRepo.All);

            Assert.That(CountItem(crafted.Bank.Slots, "copper_ore"), Is.EqualTo(0));
            Assert.That(CountItem(crafted.Bank.Slots, "oak_log"), Is.EqualTo(0));
            Assert.That(CountItem(crafted.Character.Inventory, "copper_ore"), Is.EqualTo(1));
        }

        [Test]
        public void CanCraft_StillRequiresBankCoins()
        {
            RecipeDef recipe = RecipesRepo.Get("recipe_copper_pickaxe");
            Bank bank = MakeBank(0);
            Character character = MakeCharacter(
                new ItemStack { ItemId = "copper_ore", Qty = 9 },
                new ItemStack { ItemId = "oak_log", Qty = 9 });

            var (ok, reason) = Crafting.CanCraft(bank, character, recipe, character.Level, ItemsRepo.All);

            Assert.That(ok, Is.False);
            Assert.That(reason, Is.EqualTo("Not enough coins"));
        }

        [Test]
        public void CanCraft_RejectsWhenOutputDoesNotFitInventory()
        {
            RecipeDef recipe = RecipesRepo.Get("recipe_copper_pickaxe");
            Bank bank = MakeBank(30,
                new ItemStack { ItemId = "copper_ore", Qty = 6 },
                new ItemStack { ItemId = "oak_log", Qty = 4 });
            // Inventory completely full of unrelated full stacks: no slot frees up
            // because every ingredient comes from the bank.
            var filler = new ItemStack[16];
            for (int slot = 0; slot < filler.Length; slot++)
                filler[slot] = new ItemStack { ItemId = "wildflower", Qty = 999 };
            Character character = MakeCharacter(filler);

            var (ok, reason) = Crafting.CanCraft(bank, character, recipe, character.Level, ItemsRepo.All);

            Assert.That(ok, Is.False);
            Assert.That(reason, Is.EqualTo("Inventory is full"));
        }

        [Test]
        public void Craft_DoesNotMutateInputCharacter()
        {
            RecipeDef recipe = RecipesRepo.Get("recipe_copper_pickaxe");
            Bank bank = MakeBank(30);
            Character character = MakeCharacter(
                new ItemStack { ItemId = "copper_ore", Qty = 6 },
                new ItemStack { ItemId = "oak_log", Qty = 4 });

            Crafting.Craft(bank, character, recipe, character.Level, ItemsRepo.All);

            Assert.That(CountItem(character.Inventory, "copper_ore"), Is.EqualTo(6));
            Assert.That(CountItem(character.Inventory, "oak_log"), Is.EqualTo(4));
        }

        private static Bank MakeBank(int coins, params ItemStack[] slots)
            => new Bank { Coins = coins, Slots = new List<ItemStack>(slots), MaxSlots = 48 };

        private static Character MakeCharacter(params ItemStack[] inventory)
        {
            Character character = CharacterHelper.CreateCharacter("player", "Player", ClassId.Beginner, "grass_1", 0);
            character.Inventory = new List<ItemStack>(inventory);
            return character;
        }

        private static int CountItem(List<ItemStack> stacks, string itemId)
        {
            int total = 0;
            foreach (ItemStack stack in stacks ?? new List<ItemStack>())
                if (stack != null && stack.ItemId == itemId) total += stack.Qty;
            return total;
        }
    }
}
