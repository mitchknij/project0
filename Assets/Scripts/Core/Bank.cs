// Bank.cs — Bankbeheer (vertaling van src/core/bank.ts).
// Pure staatstransformaties: alle functies retourneren nieuwe instanties.
// Klasse heet BankHelper omdat 'Bank' al een dataklasse is in GameTypes.cs.

using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class BankHelper
    {
        /// <summary>
        /// Stort een stack in de bank. Vult bestaande stacks eerst; opent
        /// nieuwe slots zolang er ruimte is. Retourneert nieuwe Bank en overflow.
        /// </summary>
        public static (Bank Bank, int Overflow) DepositToBank(
            Bank bank,
            ItemStack stack,
            ItemDef def)
        {
            if (bank == null) throw new System.ArgumentNullException(nameof(bank));
            CoreValidation.RequireValidItem(stack, def);
            if (bank.MaxSlots < 0) throw new System.ArgumentOutOfRangeException(nameof(bank));
            int remaining = stack.Qty;

            var slots = new List<ItemStack>();
            foreach (var s in bank.Slots ?? new List<ItemStack>())
                slots.Add(new ItemStack { ItemId = s.ItemId, Qty = s.Qty });

            // Vul bestaande stacks
            foreach (var slot in slots)
            {
                if (remaining <= 0) break;
                if (slot.ItemId != stack.ItemId) continue;
                int space = def.StackLimit - slot.Qty;
                if (space <= 0) continue;
                int fill = remaining < space ? remaining : space;
                slot.Qty += fill;
                remaining -= fill;
            }

            // Open nieuwe slots
            while (remaining > 0 && slots.Count < bank.MaxSlots)
            {
                int qty = remaining < def.StackLimit ? remaining : def.StackLimit;
                slots.Add(new ItemStack { ItemId = stack.ItemId, Qty = qty });
                remaining -= qty;
            }

            var updated = bank.Clone();
            updated.Slots = slots;
            return (updated, remaining);
        }

        /// <summary>
        /// Neemt een hoeveelheid van een item op uit de bank.
        /// Retourneert nieuwe Bank en werkelijk verwijderd aantal.
        /// </summary>
        public static (Bank Bank, int Removed) WithdrawFromBank(
            Bank bank,
            string itemId,
            int qty)
        {
            if (bank == null) throw new System.ArgumentNullException(nameof(bank));
            if (string.IsNullOrWhiteSpace(itemId)) throw new System.ArgumentException("Item ID is required.", nameof(itemId));
            CoreValidation.RequirePositiveQuantity(qty, nameof(qty));
            int toRemove = qty;

            var slots = new List<ItemStack>();
            foreach (var s in bank.Slots ?? new List<ItemStack>())
                slots.Add(new ItemStack { ItemId = s.ItemId, Qty = s.Qty });

            foreach (var slot in slots)
            {
                if (toRemove <= 0) break;
                if (slot.ItemId != itemId) continue;
                int take = slot.Qty < toRemove ? slot.Qty : toRemove;
                slot.Qty -= take;
                toRemove -= take;
            }

            slots.RemoveAll(s => s.Qty <= 0);

            var updated = bank.Clone();
            updated.Slots = slots;
            return (updated, qty - toRemove);
        }

        /// <summary>
        /// Voegt coins toe (of trekt af). Resultaat is nooit negatief.
        /// </summary>
        public static Bank AddCoins(Bank bank, int amount)
        {
            if (bank == null) throw new System.ArgumentNullException(nameof(bank));
            var updated = bank.Clone();
            long coins = (long)bank.Coins + amount;
            updated.Coins = (int)System.Math.Max(0, System.Math.Min(int.MaxValue, coins));
            return updated;
        }

        /// <summary>Telt het totale aantal van een item in alle bankslots.</summary>
        public static int CountItem(Bank bank, string itemId)
        {
            int total = 0;
            if (bank.Slots == null) return 0;
            foreach (var s in bank.Slots)
                if (s.ItemId == itemId) total += s.Qty;
            return total;
        }
    }
}
