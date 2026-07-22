// DropSystem.cs — Drop-berekeningen (vertaling van src/core/dropSystem.ts).
// Bevat zowel het kans-gebaseerde model (life-skill nodes) als het
// OSRS-stijl gewogen tabelmodel (monsters). Pure functies; randomness is injected.

using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class DropSystem
    {
        // ── Constanten (OSRS-model) ───────────────────────────────────────────

        /// <summary>Hoe sterk de dropkans-bonus het lege ("nothing") gewicht verkleint. Klein by design.</summary>
        private const double RareAccessFactor = 0.2;
        /// <summary>Het "nothing"-gewicht wordt nooit verder verkleind dan deze fractie van zijn basiswaarde.</summary>
        private const double NothingFloor = 0.5;

        // ── Kans-gebaseerd model (life-skill nodes) ───────────────────────────

        /// <summary>
        /// Verwachte-waarde drop-rol voor het kans-gebaseerde model (life-skill nodes).
        /// Elke entry wordt onafhankelijk gerold: floor + stochastisch fractioneel restant.
        /// dropMult schaalt elke dropkans (account drop-rate bonus); 1 = geen bonus.
        /// </summary>
        public static List<ItemStack> RollDrops(
            List<DropEntry> drops,
            int actions,
            IRandomSource rng,
            double dropMult = 1.0)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (actions < 0) throw new ArgumentOutOfRangeException(nameof(actions));
            if (dropMult < 0) throw new ArgumentOutOfRangeException(nameof(dropMult));
            var result = new List<ItemStack>();
            foreach (var d in drops ?? throw new ArgumentNullException(nameof(drops)))
            {
                ValidateDropEntry(d);
                double avg = (d.Min + d.Max) / 2.0;
                double expected = actions * d.Chance * dropMult * avg;
                int qty = (int)Math.Floor(expected);
                if (rng.NextDouble() < expected - qty) qty++;
                if (qty > 0)
                    result.Add(new ItemStack { ItemId = d.ItemId, Qty = qty });
            }
            return result;
        }

        // ── OSRS-stijl gewogen tabelmodel (monsters) ──────────────────────────

        /// <summary>
        /// Rol een monster-droptabel voor ÉÉN kill (stochastisch).
        /// Voegt alle 'always'-items toe; doet daarna één gewogen pick per Rolls.
        /// </summary>
        public static List<ItemStack> RollDropTable(
            DropTable table,
            IRandomSource rng,
            double dropBonus = 0.0)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            ValidateDropTable(table);
            var result = new List<ItemStack>();

            foreach (var it in table.Always ?? new List<DropItem>())
                result.Add(new ItemStack { ItemId = it.ItemId, Qty = UniformInt(it.Min, it.Max, rng) });

            if (table.Main != null)
            {
                int rolls = table.Main.Rolls;
                for (int r = 0; r < rolls; r++)
                {
                    WeightedSlot picked = PickWeighted(table.Main, rng, dropBonus);
                    if (picked != null && IsItemSlot(picked))
                        result.Add(new ItemStack { ItemId = picked.ItemId, Qty = UniformInt(picked.Min, picked.Max, rng) });
                }
            }

            foreach (var entry in table.Tertiary ?? new List<DropEntry>())
            {
                double effectiveChance = EffectiveTertiaryChance(entry, dropBonus);
                if (rng.NextDouble() < effectiveChance)
                    result.Add(new ItemStack { ItemId = entry.ItemId, Qty = UniformInt(entry.Min, entry.Max, rng) });
            }

            return MergeStacks(result);
        }

        /// <summary>
        /// Verwachte loot van `kills` rollen op een monster-droptabel (offline).
        /// Berekent de ware gemiddelde waarde per item; floor + stochastisch fractioneel restant
        /// (spiegelt RollDrops). Identieke effectieve gewichten als RollDropTable → pariteit.
        /// </summary>
        public static List<ItemStack> ExpectedDropTable(
            DropTable table,
            int kills,
            IRandomSource rng,
            double dropBonus = 0.0)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (kills < 0) throw new ArgumentOutOfRangeException(nameof(kills));
            ValidateDropTable(table);
            var expected = new Dictionary<string, double>();

            void Add(string itemId, double qty)
            {
                expected.TryGetValue(itemId, out double current);
                expected[itemId] = current + qty;
            }

            foreach (var it in table.Always ?? new List<DropItem>())
                Add(it.ItemId, kills * Avg(it.Min, it.Max));

            if (table.Main != null)
            {
                int rolls = table.Main.Rolls;
                double[] weights = EffectiveWeights(table.Main.Slots, dropBonus);
                double total = 0;
                foreach (var w in weights) total += w;

                if (total > 0)
                {
                    for (int i = 0; i < table.Main.Slots.Count; i++)
                    {
                        WeightedSlot slot = table.Main.Slots[i];
                        if (!IsItemSlot(slot)) continue;
                        double p = weights[i] / total;
                        Add(slot.ItemId, kills * rolls * p * Avg(slot.Min, slot.Max));
                    }
                }
            }

            foreach (var entry in table.Tertiary ?? new List<DropEntry>())
                Add(entry.ItemId, kills * EffectiveTertiaryChance(entry, dropBonus) * Avg(entry.Min, entry.Max));

            var result = new List<ItemStack>();
            foreach (var kvp in expected)
            {
                int qty = (int)Math.Floor(kvp.Value);
                if (rng.NextDouble() < kvp.Value - qty) qty++;
                if (qty > 0)
                    result.Add(new ItemStack { ItemId = kvp.Key, Qty = qty });
            }
            return result;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static int UniformInt(int min, int max, IRandomSource rng)
            => rng.NextIntInclusive(min, max);

        private static void ValidateDropEntry(DropEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId))
                throw new ArgumentException("Drop entries require an item ID.");
            if (entry.Chance < 0.0 || entry.Chance > 1.0)
                throw new ArgumentOutOfRangeException(nameof(entry), "Drop chance must be in [0, 1].");
            CoreValidation.RequireValidRange(entry.Min, entry.Max, nameof(entry));
        }

        private static void ValidateDropTable(DropTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            foreach (var item in table.Always ?? new List<DropItem>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                    throw new ArgumentException("Always drops require an item ID.");
                CoreValidation.RequireValidRange(item.Min, item.Max, nameof(table));
            }

            foreach (var entry in table.Tertiary ?? new List<DropEntry>())
                ValidateDropEntry(entry);

            if (table.Main == null) return;
            if (table.Main.Rolls < 0) throw new ArgumentOutOfRangeException(nameof(table));
            if (table.Main.Slots == null)
                throw new ArgumentException("Weighted tables require a slots collection.", nameof(table));
            foreach (var slot in table.Main.Slots)
            {
                if (slot == null || slot.Weight < 0)
                    throw new ArgumentException("Drop-table weights must be non-negative.", nameof(table));
                if (!slot.Nothing)
                {
                    if (string.IsNullOrWhiteSpace(slot.ItemId))
                        throw new ArgumentException("Item slots require an item ID.", nameof(table));
                    CoreValidation.RequireValidRange(slot.Min, slot.Max, nameof(table));
                }
            }
        }

        private static double EffectiveTertiaryChance(DropEntry entry, double dropBonus)
            => Math.Min(1.0, entry.Chance * (1.0 + dropBonus));

        /// <summary>
        /// Geeft true terug als dit slot een item bevat (i.p.v. een "nothing"-slot).
        /// Vervangt de TS type-guard `isItemSlot(s)`.
        /// </summary>
        private static bool IsItemSlot(WeightedSlot s) => !s.Nothing;

        /// <summary>
        /// Berekent het effectieve gewicht van elk slot gegeven de dropkans-bonus.
        /// Alleen het "nothing"-slot wordt licht verkleind; item-slot-ratio's blijven intact.
        /// </summary>
        private static double[] EffectiveWeights(List<WeightedSlot> slots, double dropBonus)
        {
            double nothingScale = Math.Max(NothingFloor, 1.0 - dropBonus * RareAccessFactor);
            var weights = new double[slots.Count];
            for (int i = 0; i < slots.Count; i++)
                weights[i] = IsItemSlot(slots[i]) ? slots[i].Weight : slots[i].Weight * nothingScale;
            return weights;
        }

        /// <summary>Kiest stochastisch één slot op basis van effectieve gewichten. Null voor leeg resultaat.</summary>
        private static WeightedSlot PickWeighted(WeightedTable table, IRandomSource rng, double dropBonus)
        {
            double[] weights = EffectiveWeights(table.Slots, dropBonus);
            double total = 0;
            foreach (var w in weights) total += w;
            if (total <= 0) return null;

            double r = rng.NextDouble() * total;
            for (int i = 0; i < table.Slots.Count; i++)
            {
                r -= weights[i];
                if (r < 0)
                    return IsItemSlot(table.Slots[i]) ? table.Slots[i] : null;
            }
            return null; // numerieke veiligheid
        }

        /// <summary>Samenvoegen van ItemStacks met hetzelfde itemId.</summary>
        private static List<ItemStack> MergeStacks(List<ItemStack> stacks)
        {
            var byId = new Dictionary<string, int>();
            foreach (var s in stacks)
            {
                byId.TryGetValue(s.ItemId, out int current);
                byId[s.ItemId] = current + s.Qty;
            }
            var result = new List<ItemStack>();
            foreach (var kvp in byId)
                result.Add(new ItemStack { ItemId = kvp.Key, Qty = kvp.Value });
            return result;
        }

        private static double Avg(int min, int max) => (min + max) / 2.0;
    }
}
