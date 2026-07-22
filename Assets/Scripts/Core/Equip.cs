// Equip.cs — Uitrustingsbeheer (vertaling van src/core/equip.ts).
// Headless, puur, geen framework-imports. Bouwt op Inventory.cs.

using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class Equip
    {
        /// <summary>
        /// Ruist een item uit de inventaris van het personage uit.
        /// Als het slot al bezet is, gaat het vorige item terug naar de inventaris.
        /// Gooit een uitzondering bij: niet-uitrustbaar item, level-eis niet gehaald,
        /// item niet in inventaris of inventaris vol bij terugleggen.
        /// </summary>
        public static Character EquipItem(
            Character character,
            string itemId,
            ItemDef def,
            Dictionary<string, ItemDef> itemDefs)
        {
            if (def.Type != ItemType.Equipment || !def.Slot.HasValue)
                throw new InvalidOperationException($"{itemId} is not equippable");

            int levelReq = def.LevelReq ?? 1;
            if (character.Level < levelReq)
                throw new InvalidOperationException($"Requires level {levelReq}");

            // Check dat het item in de inventaris zit
            bool inInventory = false;
            foreach (var s in character.Inventory ?? new List<ItemStack>())
                if (s.ItemId == itemId && s.Qty > 0) { inInventory = true; break; }

            if (!inInventory)
                throw new InvalidOperationException($"{itemId} is not in inventory");

            // Verwijder 1 uit inventaris
            var (afterRemove, removed) = Inventory.RemoveFromInventory(character, itemId, 1);
            if (removed < 1)
                throw new InvalidOperationException($"{itemId} is not in inventory");

            Character working = afterRemove;
            EquipSlot slot = def.Slot.Value;

            // Als er al iets in het slot zit, leg het terug in de inventaris
            string previousId = null;
            working.Equipment?.TryGetValue(slot, out previousId);

            if (previousId != null)
            {
                if (!itemDefs.TryGetValue(previousId, out ItemDef prevDef))
                    throw new InvalidOperationException(
                        $"Unknown item definition for currently equipped item: {previousId}");

                var (afterReturn, overflow) = Inventory.AddToInventory(
                    working,
                    new ItemStack { ItemId = previousId, Qty = 1 },
                    prevDef);

                if (overflow > 0)
                    throw new InvalidOperationException("Inventory full");

                working = afterReturn;
            }

            // Zet het nieuwe item in het slot
            var newEquipment = working.Equipment != null
                ? new Dictionary<EquipSlot, string>(working.Equipment)
                : new Dictionary<EquipSlot, string>();
            newEquipment[slot] = itemId;

            var result = working.Clone();
            result.Equipment = newEquipment;
            return result;
        }

        /// <summary>
        /// Legt het uitgeruste item in een slot terug in de inventaris.
        /// Gooit een uitzondering als het slot leeg is, de definitie onbekend is,
        /// of de inventaris vol is.
        /// </summary>
        public static Character UnequipItem(
            Character character,
            EquipSlot slot,
            Dictionary<string, ItemDef> itemDefs)
        {
            string itemId = null;
            character.Equipment?.TryGetValue(slot, out itemId);

            if (itemId == null)
                throw new InvalidOperationException($"Nothing equipped in slot: {slot}");

            if (!itemDefs.TryGetValue(itemId, out ItemDef def))
                throw new InvalidOperationException($"Unknown item definition for: {itemId}");

            var (afterReturn, overflow) = Inventory.AddToInventory(
                character,
                new ItemStack { ItemId = itemId, Qty = 1 },
                def);

            if (overflow > 0)
                throw new InvalidOperationException("Inventory full");

            var newEquipment = new Dictionary<EquipSlot, string>(afterReturn.Equipment);
            newEquipment.Remove(slot);

            var result = afterReturn.Clone();
            result.Equipment = newEquipment;
            return result;
        }
    }
}
