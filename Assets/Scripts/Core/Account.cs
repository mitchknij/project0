// Account.cs — Account-beheer (vertaling van src/core/account.ts).
// Puur, geen MonoBehaviour, immutability via Clone()-methoden.
// Bouwt op Inventory.cs (Inventory.*) en Bank.cs (BankHelper.*).

using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class AccountHelper
    {
        /// <summary>
        /// Maakt een nieuw leeg account aan.
        /// Spiegelt createAccount() uit account.ts exact.
        /// </summary>
        public static Account CreateAccount(string id, string familyName, long now)
        {
            return new Account
            {
                Id                 = id,
                Name               = familyName,
                CreatedAt          = now,
                Characters         = new List<Character>(),
                MaxCharacters      = 4,
                Bank               = new Bank { Coins = 0, Slots = new List<ItemStack>(), MaxSlots = 48 },
                LastSeenAt         = now,
                UnlockedWaypoints  = new List<string>(),
                AutoLoot           = false,
            };
        }

        /// <summary>
        /// Voegt een personage toe aan het account.
        /// Gooit als de limiet bereikt is of de naam (case-insensitief) al in gebruik is.
        /// </summary>
        public static Account AddCharacter(Account account, Character character)
        {
            if (account.Characters.Count >= account.MaxCharacters)
                throw new InvalidOperationException("Character limit reached");

            string nameLower = character.Name.ToLowerInvariant();
            foreach (var c in account.Characters)
            {
                if (c.Name.ToLowerInvariant() == nameLower)
                    throw new InvalidOperationException(
                        $"Character name \"{character.Name}\" is already taken");
            }

            // Maak een nieuwe lijst zodat de originele onaangetast blijft
            var newChars = new List<Character>(account.Characters) { character };
            var result = CloneAccount(account);
            result.Characters = newChars;
            return result;
        }

        /// <summary>
        /// Past een personage in het account aan via een updater-functie.
        /// Personages zonder overeenkomend id worden ongewijzigd gelaten.
        /// </summary>
        public static Account UpdateCharacter(
            Account account,
            string characterId,
            Func<Character, Character> updater)
        {
            var newChars = new List<Character>(account.Characters.Count);
            foreach (var c in account.Characters)
                newChars.Add(c.Id == characterId ? updater(c) : c);

            var result = CloneAccount(account);
            result.Characters = newChars;
            return result;
        }

        /// <summary>
        /// Verplaatst een item van de inventaris van een personage naar de gedeelde bank.
        /// Overflow (bank vol) gaat terug naar de inventaris; qty=0 → geen verandering.
        /// </summary>
        public static Account DepositItemToBank(
            Account account,
            string characterId,
            string itemId,
            int qty,
            ItemDef def)
        {
            Character character = FindCharacter(account, characterId);
            if (character == null) return account;

            var (charAfterRemove, removed) = Inventory.RemoveFromInventory(character, itemId, qty);
            if (removed == 0) return account;

            var (newBank, overflow) = BankHelper.DepositToBank(
                account.Bank,
                new ItemStack { ItemId = itemId, Qty = removed },
                def);

            Character finalChar = charAfterRemove;
            if (overflow > 0)
            {
                var (charWithOverflow, _) = Inventory.AddToInventory(
                    charAfterRemove,
                    new ItemStack { ItemId = itemId, Qty = overflow },
                    def);
                finalChar = charWithOverflow;
            }

            var result = CloneAccount(account);
            result.Bank = newBank;
            result.Characters = ReplaceCharacter(account.Characters, characterId, finalChar);
            return result;
        }

        /// <summary>
        /// Verplaatst een item van de gedeelde bank naar de inventaris van een personage.
        /// Overflow (inventaris vol) gaat terug naar de bank; removed=0 → geen verandering.
        /// </summary>
        public static Account WithdrawItemFromBank(
            Account account,
            string characterId,
            string itemId,
            int qty,
            ItemDef def)
        {
            Character character = FindCharacter(account, characterId);
            if (character == null) return account;

            var (bankAfterWithdraw, removed) = BankHelper.WithdrawFromBank(account.Bank, itemId, qty);
            if (removed == 0) return account;

            var (newChar, overflow) = Inventory.AddToInventory(
                character,
                new ItemStack { ItemId = itemId, Qty = removed },
                def);

            Bank finalBank = bankAfterWithdraw;
            if (overflow > 0)
            {
                var (bankWithOverflow, _) = BankHelper.DepositToBank(
                    bankAfterWithdraw,
                    new ItemStack { ItemId = itemId, Qty = overflow },
                    def);
                finalBank = bankWithOverflow;
            }

            var result = CloneAccount(account);
            result.Bank = finalBank;
            result.Characters = ReplaceCharacter(account.Characters, characterId, newChar);
            return result;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static Character FindCharacter(Account account, string id)
        {
            foreach (var c in account.Characters)
                if (c.Id == id) return c;
            return null;
        }

        private static List<Character> ReplaceCharacter(
            List<Character> chars,
            string id,
            Character replacement)
        {
            var list = new List<Character>(chars.Count);
            foreach (var c in chars)
                list.Add(c.Id == id ? replacement : c);
            return list;
        }

        /// <summary>
        /// Ondiepe kopie van Account (verse Characters-list, Bank-kopie, UnlockedWaypoints-kopie).
        /// </summary>
        private static Account CloneAccount(Account account) => account.Clone();
    }
}
