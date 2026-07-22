// Inventory.cs — Inventarisbeheer (vertaling van src/core/inventory.ts).
// Pure staatstransformaties: alle functies retourneren nieuwe instanties.

using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class Inventory
    {
        /// <summary>
        /// Voegt een stack toe aan de inventaris van het personage.
        /// Vult bestaande stacks eerst; opent nieuwe slots zolang er ruimte is.
        /// Retourneert een nieuw Character en het aantal dat niet paste (overflow).
        /// </summary>
        public static (Character Character, int Overflow) AddToInventory(
            Character character,
            ItemStack stack,
            ItemDef def)
        {
            if (character == null) throw new System.ArgumentNullException(nameof(character));
            CoreValidation.RequireValidItem(stack, def);
            int remaining = stack.Qty;

            // Werk op een kopie van de inventarislijst
            var inventory = new List<ItemStack>();
            foreach (var s in character.Inventory ?? new List<ItemStack>())
                inventory.Add(new ItemStack { ItemId = s.ItemId, Qty = s.Qty });

            // Vul bestaande stacks voor dit item
            foreach (var slot in inventory)
            {
                if (remaining <= 0) break;
                if (slot.ItemId != stack.ItemId) continue;
                int space = def.StackLimit - slot.Qty;
                if (space <= 0) continue;
                int fill = remaining < space ? remaining : space;
                slot.Qty += fill;
                remaining -= fill;
            }

            // Open nieuwe slots zolang er ruimte en voorraad is
            while (remaining > 0 && inventory.Count < character.MaxInventorySlots)
            {
                int qty = remaining < def.StackLimit ? remaining : def.StackLimit;
                inventory.Add(new ItemStack { ItemId = stack.ItemId, Qty = qty });
                remaining -= qty;
            }

            var updated = character.Clone();
            updated.Inventory = inventory;
            return (updated, remaining);
        }

        /// <summary>
        /// Verwijdert een hoeveelheid van een item uit de inventaris.
        /// Retourneert een nieuw Character en het werkelijk verwijderde aantal
        /// (kan minder zijn dan gevraagd wanneer de voorraad onvoldoende is).
        /// </summary>
        public static (Character Character, int Removed) RemoveFromInventory(
            Character character,
            string itemId,
            int qty)
        {
            if (character == null) throw new System.ArgumentNullException(nameof(character));
            if (string.IsNullOrWhiteSpace(itemId)) throw new System.ArgumentException("Item ID is required.", nameof(itemId));
            CoreValidation.RequirePositiveQuantity(qty, nameof(qty));
            int toRemove = qty;

            var inventory = new List<ItemStack>();
            foreach (var s in character.Inventory ?? new List<ItemStack>())
                inventory.Add(new ItemStack { ItemId = s.ItemId, Qty = s.Qty });

            foreach (var slot in inventory)
            {
                if (toRemove <= 0) break;
                if (slot.ItemId != itemId) continue;
                int take = slot.Qty < toRemove ? slot.Qty : toRemove;
                slot.Qty -= take;
                toRemove -= take;
            }

            // Verwijder lege slots
            inventory.RemoveAll(s => s.Qty <= 0);

            var updated = character.Clone();
            updated.Inventory = inventory;
            return (updated, qty - toRemove);
        }
    }
}
