// TalentsRepo.cs — Statische talent-definities (vertaling van src/data/talents.ts).
// 1:1 met de TypeScript-bron; ClassId=null betekent 'all' (universeel talent).

using System.Collections.Generic;
using IdleCloud.Core;

namespace IdleCloud.Data
{
    public static class TalentsRepo
    {
        private static readonly List<TalentDef> List = new List<TalentDef>
        {
            // ── Warrior ──────────────────────────────────────────────────────────
            T("t_war_brawn",  "Brawn",     ClassId.Warrior, "Raw muscle.",            BonusStat.Strength,    2,      10),
            T("t_war_tough",  "Toughness", ClassId.Warrior, "Thicker hide.",          BonusStat.HpPct,       0.02,   10),
            T("t_war_frenzy", "Frenzy",    ClassId.Warrior, "Faster, harder blows.",  BonusStat.CombatPct,   0.03,   10),
            T("t_war_quarry", "Quarryman", ClassId.Warrior, "Mine faster.",           BonusStat.MiningPct,   0.03,   10),

            // ── Archer / Ranger ───────────────────────────────────────────────────
            T("t_arc_precision", "Precision", ClassId.Archer, "Steadier aim.",    BonusStat.Agility,     2,      10),
            T("t_arc_eagle",     "Eagle Eye", ClassId.Archer, "Lethal shots.",    BonusStat.CombatPct,   0.03,   10),
            T("t_arc_timber",    "Timber",    ClassId.Archer, "Chop faster.",     BonusStat.ChoppingPct, 0.03,   10),
            T("t_arc_evade",     "Evasion",   ClassId.Archer, "Harder to hit.",   BonusStat.HpPct,       0.02,   10),

            // ── Mage / Arcanist ───────────────────────────────────────────────────
            T("t_mag_intellect", "Intellect",   ClassId.Mage, "Deeper knowledge.", BonusStat.Wisdom,    2,      10),
            T("t_mag_insight",   "Insight",     ClassId.Mage, "Learn quicker.",    BonusStat.XpPct,     0.025,  10),
            T("t_mag_channel",   "Channeling",  ClassId.Mage, "Stronger spells.",  BonusStat.CombatPct, 0.035,  10),
            T("t_mag_ward",      "Arcane Ward", ClassId.Mage, "Magical protection.", BonusStat.HpPct,   0.02,   10),

            // ── Beginner / Journeyman ─────────────────────────────────────────────
            T("t_beg_vigor", "Vigor",     ClassId.Beginner, "Hardier body.",    BonusStat.HpPct,    0.02,  10),
            T("t_beg_apt",   "Apt Pupil", ClassId.Beginner, "Quick study.",     BonusStat.XpPct,    0.025, 10),
            T("t_beg_handy", "Handywork", ClassId.Beginner, "Jack of trades.",  BonusStat.Strength, 1,     10),
            T("t_beg_swift", "Swiftness", ClassId.Beginner, "Light on feet.",   BonusStat.Agility,  1,     10),

            // ── Universeel (classId=null) ─────────────────────────────────────────
            TAll("t_all_endurance", "Endurance", "Lasting stamina.", BonusStat.HpPct, 0.015, 10),
            TAll("t_all_scholar",   "Scholar",   "Broad learning.",  BonusStat.XpPct, 0.015, 10),
        };

        /// <summary>Alle talents geïndexeerd op id.</summary>
        public static readonly Dictionary<string, TalentDef> All;

        static TalentsRepo()
        {
            All = new Dictionary<string, TalentDef>(List.Count);
            foreach (var d in List)
                All[d.Id] = d;
        }

        /// <summary>Retourneert de TalentDef voor het gegeven id, of null wanneer niet gevonden.</summary>
        public static TalentDef Get(string id) => All.TryGetValue(id, out TalentDef def) ? def : null;

        /// <summary>
        /// Alle talents beschikbaar voor een klasse (eigen boom + universele talents).
        /// Spiegelt talentsForClass() uit talents.ts exact.
        /// </summary>
        public static List<TalentDef> ForClass(ClassId classId)
        {
            var result = new List<TalentDef>();
            foreach (var d in List)
                if (d.ClassId == classId || d.ClassId == null)
                    result.Add(d);
            return result;
        }

        // ── Fabriekshulpers ───────────────────────────────────────────────────────

        private static TalentDef T(
            string id, string name, ClassId classId,
            string description, BonusStat bonusStat,
            double bonusPerPoint, int maxPoints)
            => new TalentDef
            {
                Id           = id,
                Name         = name,
                ClassId      = classId,
                Description  = description,
                BonusStat    = bonusStat,
                BonusPerPoint = bonusPerPoint,
                MaxPoints    = maxPoints,
            };

        private static TalentDef TAll(
            string id, string name,
            string description, BonusStat bonusStat,
            double bonusPerPoint, int maxPoints)
            => new TalentDef
            {
                Id           = id,
                Name         = name,
                ClassId      = null, // 'all' — universeel
                Description  = description,
                BonusStat    = bonusStat,
                BonusPerPoint = bonusPerPoint,
                MaxPoints    = maxPoints,
            };
    }
}
