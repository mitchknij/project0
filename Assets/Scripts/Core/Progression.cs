// Progression.cs — Progressie-wiskunde (vertaling van src/core/progression.ts).
// Enkelvoudige bron van waarheid voor alle schaalcurves.
// Alleen pure functies; geen toestand, geen bijwerkingen.

using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class Progression
    {
        private static ProgressionBalanceConfig _configuration = new ProgressionBalanceConfig();

        public static ProgressionBalanceConfig CurrentConfiguration => _configuration;

        public static void Configure(ProgressionBalanceConfig configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            _configuration = configuration;
        }

        /// <summary>
        /// XP benodigd om te stijgen VAN `level` naar `level + 1`.
        /// Vloeiende super-lineaire curve: floor(50 * level^1.8 + 50).
        /// </summary>
        public static int XpToNext(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            return (int)Math.Floor(_configuration.XpCoefficient * Math.Pow(level, _configuration.XpExponent) +
                _configuration.XpBase);
        }

        /// <summary>
        /// Totale XP vanaf level 1 om `level` te bereiken (som van XpToNext voor 1..level-1).
        /// Geeft long terug om overflow bij hoge levels te voorkomen.
        /// </summary>
        public static long TotalXpForLevel(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            long sum = 0;
            for (int l = 1; l < level; l++)
                sum += XpToNext(l);
            return sum;
        }

        /// <summary>Basis-stats voor een klasse op een gegeven level (geen uitrusting).</summary>
        public static CoreStats BaseStatsAtLevel(ClassDef cls, int level)
        {
            // StatGrowth is een StatGrowthDef met double-waarden (bv. 0.4 per niveau).
            // floor(base + growth * (level - 1)) spiegelt de TS-berekening exact.
            int Grow(int baseVal, double perLevel)
                => (int)Math.Floor(baseVal + perLevel * (level - 1));

            var g = cls.StatGrowth ?? new StatGrowthDef();
            return new CoreStats
            {
                Strength = Grow(cls.BaseStats.Strength, g.Strength),
                Agility  = Grow(cls.BaseStats.Agility,  g.Agility),
                Wisdom   = Grow(cls.BaseStats.Wisdom,   g.Wisdom),
                Luck     = Grow(cls.BaseStats.Luck,     g.Luck),
            };
        }

        /// <summary>
        /// Effectieve stats = klasse-basis op level + vlakke bonussen van uitgeruste items +
        /// klasse-passieve multipliers + account-brede vlakke bonussen (kaarten/zegels/talenten).
        /// `account` is optioneel: geef null door wanneer meta-bonussen niet relevant zijn.
        /// </summary>
        public static CoreStats EffectiveStats(
            Character character,
            ClassDef cls,
            Dictionary<string, ItemDef> itemDefs,
            AccountBonuses account = null)
        {
            CoreStats stats = BaseStatsAtLevel(cls, character.Level);

            // Vlakke bonussen van uitgerust itemId
            if (character.Equipment != null)
            {
                foreach (string itemId in character.Equipment.Values)
                {
                    if (itemId == null) continue;
                    if (!itemDefs.TryGetValue(itemId, out ItemDef def)) continue;
                    CoreStats bonuses = def?.Bonuses;
                    if (bonuses == null) continue;
                    stats.Strength += bonuses.Strength;
                    stats.Agility  += bonuses.Agility;
                    stats.Wisdom   += bonuses.Wisdom;
                    stats.Luck     += bonuses.Luck;
                }
            }

            // Handmatig verdeelde statpunten (1 per level-up, door speler verdeeld)
            CoreStats alloc = character.AllocatedStats;
            if (alloc != null)
            {
                stats.Strength += alloc.Strength;
                stats.Agility  += alloc.Agility;
                stats.Wisdom   += alloc.Wisdom;
                stats.Luck     += alloc.Luck;
            }

            // Account-brede vlakke bonussen
            if (account != null)
            {
                stats.Strength += account.Strength;
                stats.Agility  += account.Agility;
                stats.Wisdom   += account.Wisdom;
                stats.Luck     += account.Luck;
            }

            // Klasse-passieve luck-multiplier
            double? luckMult = cls.Passive?.Multipliers?.Luck;
            if (luckMult.HasValue)
                stats.Luck = (int)Math.Floor(stats.Luck * luckMult.Value);

            return stats;
        }
    }
}
