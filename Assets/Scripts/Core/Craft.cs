// Craft.cs — Craftinglogica (vertaling van src/core/craft.ts).
// Headless, puur, geen framework-imports. Bouwt op BankHelper.

using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class Crafting
    {
        /// <summary>
        /// Controleert of een recept gecraft kan worden zonder bank of personage te muteren.
        /// Valideert ook (via een droogloop) dat het resultaat in de inventaris past
        /// nadat de ingrediënten eruit verbruikt zijn.
        /// Retourneert (Ok=true, Reason=null) bij succes, of (Ok=false, Reason=…) met uitleg.
        /// </summary>
        public static (bool Ok, string Reason) CanCraft(
            Bank bank,
            Character character,
            RecipeDef recipe,
            int characterLevel,
            Dictionary<string, ItemDef> itemDefs)
        {
            if (bank == null) return (false, "Missing bank");
            if (recipe == null) return (false, "Missing recipe");
            if (character == null) return (false, "Missing character");
            if (itemDefs == null) return (false, "Missing item definitions");
            if (recipe.OutputQty <= 0) return (false, "Invalid output quantity");
            if (recipe.CoinCost < 0) return (false, "Invalid coin cost");
            if (characterLevel < recipe.LevelReq)
                return (false, $"Requires level {recipe.LevelReq}");

            if (bank.Coins < recipe.CoinCost)
                return (false, "Not enough coins");

            foreach (var input in recipe.Inputs ?? new List<ItemStack>())
            {
                if (input == null || string.IsNullOrWhiteSpace(input.ItemId) || input.Qty <= 0)
                    return (false, "Invalid recipe input");
                int inventoryHave = 0;
                foreach (ItemStack stack in character.Inventory ?? new List<ItemStack>())
                    if (stack != null && stack.ItemId == input.ItemId) inventoryHave += stack.Qty;
                long have = (long)BankHelper.CountItem(bank, input.ItemId) + inventoryHave;
                if (have < input.Qty)
                    return (false, $"Missing: {input.ItemId} x{input.Qty}");
            }

            if (!itemDefs.TryGetValue(recipe.OutputItemId, out ItemDef outputDef))
                return (false, $"Unknown output item: {recipe.OutputItemId}");
            if (outputDef.StackLimit <= 0)
                return (false, $"Invalid stack limit for output item: {recipe.OutputItemId}");

            // Droogloop: verbruik de inventaris-kant van de ingrediënten op een kopie en
            // controleer of het resultaat daarna past — zo blijft de craft-knop eerlijk.
            Character preview = character.Clone();
            foreach (var input in recipe.Inputs ?? new List<ItemStack>())
            {
                int fromInventory = input.Qty - Math.Min(input.Qty, BankHelper.CountItem(bank, input.ItemId));
                if (fromInventory > 0)
                    preview = Inventory.RemoveFromInventory(preview, input.ItemId, fromInventory).Character;
            }
            var (_, previewOverflow) = Inventory.AddToInventory(
                preview,
                new ItemStack { ItemId = recipe.OutputItemId, Qty = recipe.OutputQty },
                outputDef);
            if (previewOverflow > 0)
                return (false, "Inventory is full");

            return (true, null);
        }

        /// <summary>
        /// Voert het recept uit en retourneert de nieuwe bank- en karakterstatus.
        /// Materialen: bank eerst, dan inventaris; munten alleen uit de bank.
        /// Het resultaat komt in de inventaris van het personage terecht.
        /// Gooit een uitzondering als CanCraft faalt of de inventaris vol is na het craften.
        /// Bank en personage worden alleen gewijzigd als alles slaagt (pure functie via kopieën).
        /// </summary>
        public static (Bank Bank, Character Character) Craft(
            Bank bank,
            Character character,
            RecipeDef recipe,
            int characterLevel,
            Dictionary<string, ItemDef> itemDefs)
        {
            var (ok, reason) = CanCraft(bank, character, recipe, characterLevel, itemDefs);
            if (!ok)
                throw new InvalidOperationException(reason);

            if (!itemDefs.TryGetValue(recipe.OutputItemId, out ItemDef outputDef))
                throw new InvalidOperationException($"Unknown output item: {recipe.OutputItemId}");
            if (outputDef.StackLimit <= 0)
                throw new InvalidOperationException($"Invalid stack limit for output item: {recipe.OutputItemId}");

            // Werk op kopieën — commit pas als alles slaagt
            Bank working = bank;
            Character workingCharacter = character.Clone();

            // Trek de coin-kosten af
            working = BankHelper.AddCoins(working, -recipe.CoinCost);

            // Trek alle ingrediënten af
            foreach (var input in recipe.Inputs ?? new List<ItemStack>())
            {
                int fromBank = Math.Min(input.Qty, BankHelper.CountItem(working, input.ItemId));
                if (fromBank > 0)
                {
                    var (afterBank, _) = BankHelper.WithdrawFromBank(working, input.ItemId, fromBank);
                    working = afterBank;
                }

                int fromInventory = input.Qty - fromBank;
                if (fromInventory > 0)
                {
                    var (afterCharacter, removed) = Inventory.RemoveFromInventory(
                        workingCharacter, input.ItemId, fromInventory);
                    if (removed < fromInventory)
                        throw new InvalidOperationException($"Missing: {input.ItemId} x{input.Qty}");
                    workingCharacter = afterCharacter;
                }
            }

            // Lever het resultaat af in de inventaris — overflow = inventaris vol
            var (finalCharacter, overflow) = Inventory.AddToInventory(
                workingCharacter,
                new ItemStack { ItemId = recipe.OutputItemId, Qty = recipe.OutputQty },
                outputDef);

            if (overflow > 0)
                throw new InvalidOperationException("Inventory is full");

            return (working, finalCharacter);
        }
    }
}
