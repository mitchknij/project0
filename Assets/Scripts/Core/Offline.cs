// Offline.cs — Offline-progressie-engine (vertaling van src/core/offline.ts).
// Puur, geen MonoBehaviour, immutability via Clone(). Bouwt op BankHelper, Progression, DropSystem, Bonuses.

using System;
using System.Collections.Generic;
using IdleCloud.Data;

namespace IdleCloud.Core
{
    public static class Offline
    {
        // ── Constanten (spiegelen TS-bron exact) ─────────────────────────────

        // ── Publieke API ──────────────────────────────────────────────────────

        /// <summary>
        /// Runt de offline-formule voor alle personages en stort loot/coins in de gedeelde bank.
        /// Puur: geeft een nieuw Account + een OfflineReport terug (null als er te weinig tijd is
        /// verstreken of geen enkel personage iets deed).
        /// Spiegelt simulateOffline() uit offline.ts exact.
        /// </summary>
        public static (Account Account, OfflineReport Report) SimulateOffline(
            Account account,
            long now,
            OfflineDataBundle data,
            IRandomSource rng,
            AccountBonuses bonuses = null)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (account.Bank == null) throw new ArgumentException("Account bank is required.", nameof(account));
            if (bonuses == null) bonuses = AccountBonuses.Zero();
            if (now < account.LastSeenAt) throw new ArgumentOutOfRangeException(nameof(now), "Time cannot move backwards.");

            OfflineBalanceConfig balance = data.OfflineBalance ?? OfflineBalanceRepo.Default;
            if (balance == null || balance.Rate < 0 || balance.Rate > 1 ||
                balance.CapMs <= 0 || balance.MinimumDurationMs < 0 ||
                balance.MinimumDurationMs > balance.CapMs)
                throw new ArgumentException("Offline balance is invalid.", nameof(data));

            long elapsedMs = now - account.LastSeenAt;
            if (elapsedMs < balance.MinimumDurationMs)
            {
                var quickClone      = CloneAccount(account);
                quickClone.LastSeenAt = now;
                return (quickClone, null);
            }

            long cappedMs = System.Math.Min(elapsedMs, balance.CapMs);
            double hours  = cappedMs / 3_600_000.0;

            Bank bank                          = account.Bank.Clone();
            var characters                     = new List<Character>();
            var reports                        = new List<OfflineCharacterReport>();
            double dropMult                    = 1.0 + bonuses.DropPct;

            foreach (var c in account.Characters ?? new List<Character>())
            {
                var (simChar, report) = SimulateCharacter(c, hours, data, rng, dropMult, balance.Rate);

                // Loot gaat eerst naar de inventaris van het personage
                var lootOverflow = new List<ItemStack>();
                var updatedChar = simChar.Clone();
                foreach (var stack in report.Loot)
                {
                    if (!data.Items.TryGetValue(stack.ItemId, out ItemDef def)) continue;
                    var (newCharacter, overflow) = Inventory.AddToInventory(updatedChar, stack, def);
                    updatedChar = newCharacter;
                    if (overflow > 0)
                        lootOverflow.Add(new ItemStack { ItemId = stack.ItemId, Qty = overflow });
                }
                if (report.CoinsGained > 0)
                    bank = BankHelper.AddCoins(bank, report.CoinsGained);

                // Herstel startedAt zodat de volgende heartbeat correct werkt
                updatedChar.Activity   = new ActivityState
                {
                    Kind      = simChar.Activity?.Kind ?? ActivityKind.Idle,
                    TargetId  = simChar.Activity?.TargetId,
                    StartedAt = now,
                };
                characters.Add(updatedChar);

                report.LootOverflow = lootOverflow;
                reports.Add(report);
            }

            bool didAnything = false;
            foreach (var r in reports)
                if (r.Actions > 0) { didAnything = true; break; }

            var resultAccount            = CloneAccount(account);
            resultAccount.Characters     = characters;
            resultAccount.Bank           = bank;
            resultAccount.LastSeenAt     = now;

            OfflineReport offlineReport  = didAnything
                ? new OfflineReport { ElapsedMs = elapsedMs, CappedMs = cappedMs, Characters = reports }
                : null;

            return (resultAccount, offlineReport);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Verwerkt één personage voor de offline-periode en retourneert het bijgewerkte personage
        /// + het per-personage-rapport. Spiegelt simulateCharacter() uit offline.ts exact.
        /// </summary>
        private static (Character Character, OfflineCharacterReport Report) SimulateCharacter(
            Character character,
            double hours,
            OfflineDataBundle data,
            IRandomSource rng,
            double dropMult,
            double offlineRate)
        {
            var emptyReport = new OfflineCharacterReport
            {
                CharacterId   = character.Id,
                CharacterName = character.Name,
                Kind          = character.Activity?.Kind ?? ActivityKind.Idle,
                TargetId      = character.Activity?.TargetId,
                Actions       = 0,
                XpGained      = 0,
                LevelsGained  = 0,
                CoinsGained   = 0,
                Loot          = new List<ItemStack>(),
                LootOverflow  = new List<ItemStack>(),
            };

            EfficiencySnapshot snap = character.Efficiency;
            if (!SnapshotValidation.IsUsable(snap, character) || snap.Kind == ActivityKind.Idle || snap.ActionsPerHour <= 0)
                return (character, emptyReport);

            double rawActions = snap.ActionsPerHour * offlineRate * hours;
            int actions = rawActions >= int.MaxValue
                ? int.MaxValue
                : (int)System.Math.Floor(rawActions);
            if (actions <= 0) return (character, emptyReport);

            long xpGained     = (long)System.Math.Floor(actions * snap.XpPerAction);
            int  coinsGained  = (int)System.Math.Floor(actions * snap.CoinsPerAction);

            // Combat → gewogen OSRS-tabel; life-skill nodes → kans-gebaseerd model
            List<ItemStack> loot;
            if (snap.Kind == ActivityKind.Fighting)
            {
                DropTable table = null;
                if (snap.TargetId != null && data.Monsters.TryGetValue(snap.TargetId, out MonsterDef mon))
                    table = mon.Drops;
                loot = table != null
                    ? DropSystem.ExpectedDropTable(table, actions, rng, dropMult - 1.0)
                    : new List<ItemStack>();
            }
            else
            {
                List<DropEntry> nodeDrops = new List<DropEntry>();
                if (snap.TargetId != null && data.Nodes.TryGetValue(snap.TargetId, out ResourceNodeDef node))
                    nodeDrops = node.Drops ?? new List<DropEntry>();
                loot = DropSystem.RollDrops(nodeDrops, actions, rng, dropMult);
            }

            Character next = character.Clone();
            int levelsGained;

            if (snap.Kind == ActivityKind.Fighting)
            {
                // Gevechts-XP gaat naar zowel het personage-level als de combat-vaardigheid
                var charResult = CharacterHelper.ApplyCharacterXp(next, xpGained);
                next = charResult.Character;
                levelsGained = charResult.LevelsGained;
                next = CharacterHelper.ApplySkillXp(next, SkillId.Combat, xpGained).Character;
            }
            else
            {
                SkillId skillId = ActivitySkillMapping.ToSkillId(snap.Kind);
                var skillResult = CharacterHelper.ApplySkillXp(next, skillId, xpGained);
                next = skillResult.Character;
                levelsGained = skillResult.LevelsGained;
            }

            var report = new OfflineCharacterReport
            {
                CharacterId   = emptyReport.CharacterId,
                CharacterName = emptyReport.CharacterName,
                Kind          = emptyReport.Kind,
                TargetId      = emptyReport.TargetId,
                Actions       = actions,
                XpGained      = xpGained,
                LevelsGained  = levelsGained,
                CoinsGained   = coinsGained,
                Loot          = loot,
                LootOverflow  = new List<ItemStack>(), // wordt ingevuld door simulateOffline
            };

            return (next, report);
        }

        /// <summary>Ondiepe kopie van Account voor immutable updates.</summary>
        private static Account CloneAccount(Account account) => account.Clone();
    }
}
