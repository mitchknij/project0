// Talents.cs — Talent-allocatie per personage (vertaling van src/core/talents.ts).
// Produceert een AccountBonuses per personage die de store combineert met account-bonussen.
// Pure functies; geen engine-imports.

using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class Talents
    {
        // ── Puntenboekhouding ────────────────────────────────────────────────

        /// <summary>Totale talent-punten die een personage heeft verdiend (1 per level).</summary>
        public static int TalentPointsTotal(int level) => System.Math.Max(0, level);

        /// <summary>Punten die al zijn uitgegeven over alle talenten.</summary>
        public static int SpentTalentPoints(Character character)
        {
            long sum = 0;
            if (character.Talents == null) return 0;
            foreach (int points in character.Talents.Values)
                if (points > 0) sum += points;
            return sum >= int.MaxValue ? int.MaxValue : (int)sum;
        }

        /// <summary>Nog niet uitgegeven punten die beschikbaar zijn om te verdelen.</summary>
        public static int AvailableTalentPoints(Character character)
        {
            if (character == null) return 0;
            return System.Math.Max(0, TalentPointsTotal(character.Level) - SpentTalentPoints(character));
        }

        // ── Beschikbaarheid ──────────────────────────────────────────────────

        /// <summary>Of een talent van toepassing is op de klasse van het personage.</summary>
        public static bool TalentAvailableTo(TalentDef def, Character character)
            => def != null && character != null && (def.AvailableToAll || def.ClassId == character.ClassId);

        // ── Mutaties (immutable stijl — geeft nieuwe Character terug) ────────

        /// <summary>
        /// Verdeelt één punt in een talent. Gooit een uitzondering wanneer er geen punten
        /// beschikbaar zijn, het talent al vol is, of het talent niet beschikbaar is voor
        /// de klasse van het personage.
        /// </summary>
        public static Character AllocateTalent(Character character, TalentDef def)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (!TalentAvailableTo(def, character))
                throw new InvalidOperationException("Talent not available to this class");

            int current = 0;
            character.Talents?.TryGetValue(def.Id, out current);
            current = System.Math.Max(0, current);

            if (current >= def.MaxPoints)
                throw new InvalidOperationException("Talent already maxed");
            if (AvailableTalentPoints(character) <= 0)
                throw new InvalidOperationException("No talent points available");

            Character updated = character.Clone();
            updated.Talents[def.Id] = current + 1;
            return updated;
        }

        /// <summary>Geeft alle verdeelde punten terug (lege talents-dictionary).</summary>
        public static Character ResetTalents(Character character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            Character updated = character.Clone();
            updated.Talents = new Dictionary<string, int>();
            return updated;
        }

        // ── Bonusberekening ──────────────────────────────────────────────────

        /// <summary>
        /// Aggregeert de talent-bonussen van een personage naar de gedeelde bonusshape.
        /// Equivalent aan de TS-functie computeTalentBonuses.
        /// </summary>
        public static AccountBonuses ComputeTalentBonuses(
            Character character,
            Dictionary<string, TalentDef> talentDefs)
        {
            AccountBonuses result = AccountBonuses.Zero();
            if (character.Talents == null) return result;

            foreach (var kvp in character.Talents)
            {
                string talentId = kvp.Key;
                int points = kvp.Value;
                if (points <= 0) continue;
                if (!talentDefs.TryGetValue(talentId, out TalentDef def)) continue;
                result.Add(def.BonusStat, def.BonusPerPoint * points);
            }
            return result;
        }

        // ── Privé helper ─────────────────────────────────────────────────────

        /// <summary>
        /// Maakt een ondiepe kopie van Character met een nieuwe Talents-dictionary,
        /// zodat mutaties de originele instantie niet beïnvloeden.
        /// </summary>
    }
}
