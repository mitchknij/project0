// Character.cs — Personage-beheer (vertaling van src/core/character.ts).
// Puur, geen MonoBehaviour, immutability via Character.Clone().
// Bouwt op Progression.cs (XpToNext).

using System;
using System.Collections.Generic;
using IdleCloud.Data;

namespace IdleCloud.Core
{
    public sealed class CharacterXpResult
    {
        public Character Character;
        public long XpAwarded;
        public int PreviousLevel;
        public int NewLevel;
        public int LevelsGained;
        public int FreeStatPointsGained;
    }

    public sealed class SkillXpResult
    {
        public Character Character;
        public SkillId SkillId;
        public long XpAwarded;
        public int PreviousLevel;
        public int NewLevel;
        public int LevelsGained;
    }

    public static class CharacterHelper
    {
        /// <summary>Alle vaardigheidstypes; volgorde is canoniek (spiegelt ALL_SKILLS in TS).</summary>
        public static readonly SkillId[] AllSkills =
        {
            SkillId.Combat,
            SkillId.Mining,
            SkillId.Chopping,
            SkillId.Gathering,
        };

        /// <summary>
        /// Maakt een nieuw personage aan op level 1 met lege inventaris en geen activiteit.
        /// Spiegelt createCharacter() uit character.ts exact.
        /// </summary>
        public static Character CreateCharacter(
            string id,
            string name,
            ClassId classId,
            string mapId,
            long now)
        {
            var skills = new Dictionary<SkillId, SkillProgress>();
            foreach (var s in AllSkills)
                skills[s] = new SkillProgress { Level = 1, Xp = 0 };
            List<string> defaultSkillBar = CreateDefaultSkillBar(classId);

            return new Character
            {
                Id                = id,
                Name              = name,
                ClassId           = classId,
                Level             = 1,
                Xp                = 0,
                Skills            = skills,
                Equipment         = new Dictionary<EquipSlot, string>(),
                Inventory         = new List<ItemStack>(),
                MaxInventorySlots = 16,
                MapId             = mapId,
                Activity          = new ActivityState { Kind = ActivityKind.Idle, StartedAt = now },
                Talents           = new Dictionary<string, int>(),
                FreeStatPoints    = null,
                AllocatedStats    = null,
                SkillBar          = defaultSkillBar,
                UnlockedSkillIds  = SkillBuild.DefaultUnlocked(defaultSkillBar),
                AvailableSkillPoints = SkillBuild.PrototypeStartingPoints,
                SpentSkillPoints = 0,
                SkillStateSchemaVersion = SkillBuild.CurrentSchemaVersion,
            };
        }

        /// <summary>
        /// Creates the default manual bar from the class skills in ascending priority order.
        /// Empty slots are retained so the result always has the data contract's fixed length.
        /// </summary>
        public static List<string> CreateDefaultSkillBar(ClassId classId)
        {
            var orderedSkills = new List<ClassSkillDef>();
            ClassDef classDef = RuntimeContent.Get(classId);
            foreach (ClassSkillDef skill in classDef?.Skills ?? new List<ClassSkillDef>())
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.Id)) continue;
                int insertAt = 0;
                while (insertAt < orderedSkills.Count && orderedSkills[insertAt].Priority <= skill.Priority)
                    insertAt++;
                orderedSkills.Insert(insertAt, skill);
            }

            int seededSlots = Math.Min(orderedSkills.Count, Character.DefaultSeededSkillBarSlots);
            var skillBar = new List<string>(Character.SkillBarSlots);
            for (int slot = 0; slot < Character.SkillBarSlots; slot++)
                skillBar.Add(slot < seededSlots ? orderedSkills[slot].Id : null);
            return skillBar;
        }

        /// <summary>
        /// Kent XP toe en verhoogt het level zo vaak als nodig.
        /// Elke levelup legt één gratis stat-punt bij.
        /// Spiegelt gainXp() uit character.ts exact.
        /// </summary>
        public static Character GainXp(Character character, long amount)
        {
            return ApplyCharacterXp(character, amount).Character;
        }

        public static CharacterXpResult ApplyCharacterXp(Character character, long amount)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            int level = character.Level;
            long xp   = checked(character.Xp + amount);

            while (xp >= Progression.XpToNext(level))
            {
                xp -= Progression.XpToNext(level);
                level++;
            }

            int levelsGained   = level - character.Level;
            int freeStatPoints = (character.FreeStatPoints ?? 0) + levelsGained;

            var result         = character.Clone();
            result.Level       = level;
            result.Xp          = xp;
            result.FreeStatPoints = freeStatPoints;
            return new CharacterXpResult
            {
                Character = result,
                XpAwarded = amount,
                PreviousLevel = character.Level,
                NewLevel = level,
                LevelsGained = levelsGained,
                FreeStatPointsGained = levelsGained,
            };
        }

        /// <summary>
        /// Geeft één gratis stat-punt uit in de gekozen core-stat.
        /// Geen effect wanneer er geen punten beschikbaar zijn.
        /// Spiegelt allocateStatPoint() uit character.ts exact.
        /// </summary>
        public static Character AllocateStatPoint(Character character, string stat)
        {
            if ((character.FreeStatPoints ?? 0) <= 0) return character;

            var allocated = character.AllocatedStats ?? new CoreStats();
            int current   = GetStatValue(allocated, stat);

            var newAllocated = CloneCoreStats(allocated);
            SetStatValue(newAllocated, stat, current + 1);

            var result          = character.Clone();
            result.FreeStatPoints = (character.FreeStatPoints ?? 0) - 1;
            result.AllocatedStats = newAllocated;
            return result;
        }

        /// <summary>
        /// Kent XP toe aan een vaardigheid en verhoogt het vaardigheidslevel zo vaak als nodig.
        /// Spiegelt gainSkillXp() uit character.ts exact.
        /// </summary>
        public static Character GainSkillXp(Character character, SkillId skillId, long amount)
        {
            return ApplySkillXp(character, skillId, amount).Character;
        }

        public static SkillXpResult ApplySkillXp(Character character, SkillId skillId, long amount)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (character.Skills == null || !character.Skills.TryGetValue(skillId, out SkillProgress skill))
                throw new ArgumentException($"Missing skill state for {skillId}.", nameof(skillId));

            int level  = skill.Level;
            long xp    = checked(skill.Xp + amount);

            while (xp >= Progression.XpToNext(level))
            {
                xp -= Progression.XpToNext(level);
                level++;
            }

            var newSkills      = new Dictionary<SkillId, SkillProgress>(character.Skills);
            newSkills[skillId] = new SkillProgress { Level = level, Xp = xp };

            var result    = character.Clone();
            result.Skills = newSkills;
            return new SkillXpResult
            {
                Character = result,
                SkillId = skillId,
                XpAwarded = amount,
                PreviousLevel = skill.Level,
                NewLevel = level,
                LevelsGained = level - skill.Level,
            };
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static int GetStatValue(CoreStats s, string stat)
        {
            switch (stat)
            {
                case "strength": return s.Strength;
                case "agility":  return s.Agility;
                case "wisdom":   return s.Wisdom;
                case "luck":     return s.Luck;
                default: throw new ArgumentException($"Unknown stat: {stat}");
            }
        }

        private static void SetStatValue(CoreStats s, string stat, int value)
        {
            switch (stat)
            {
                case "strength": s.Strength = value; break;
                case "agility":  s.Agility  = value; break;
                case "wisdom":   s.Wisdom   = value; break;
                case "luck":     s.Luck     = value; break;
                default: throw new ArgumentException($"Unknown stat: {stat}");
            }
        }

        private static CoreStats CloneCoreStats(CoreStats s) => new CoreStats
        {
            Strength = s.Strength,
            Agility  = s.Agility,
            Wisdom   = s.Wisdom,
            Luck     = s.Luck,
        };
    }

    public static class SkillBuild
    {
        public const int CurrentSchemaVersion = 1;
        public const int PrototypeStartingPoints = 3;

        public static List<string> DefaultUnlocked(List<string> skillBar)
        {
            var result = new List<string>();
            foreach (string skillId in skillBar ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(skillId) && !result.Contains(skillId)) result.Add(skillId);
            return result;
        }

        public static bool IsUnlocked(Character character, string skillId)
            => character?.UnlockedSkillIds != null && character.UnlockedSkillIds.Contains(skillId);

        public static bool CanUnlock(Character character, ClassSkillDef skill)
        {
            if (character == null || skill == null || string.IsNullOrWhiteSpace(skill.Id) || IsUnlocked(character, skill.Id))
                return false;
            int cost = Math.Max(0, skill.SkillPointCost);
            if (character.AvailableSkillPoints < cost) return false;
            return string.IsNullOrWhiteSpace(skill.PrerequisiteSkillId) ||
                IsUnlocked(character, skill.PrerequisiteSkillId);
        }

        public static Character Unlock(Character character, ClassSkillDef skill)
        {
            if (!CanUnlock(character, skill)) throw new InvalidOperationException("skill_not_unlockable");
            Character result = character.Clone();
            result.UnlockedSkillIds ??= new List<string>();
            result.UnlockedSkillIds.Add(skill.Id);
            int cost = Math.Max(0, skill.SkillPointCost);
            result.AvailableSkillPoints -= cost;
            result.SpentSkillPoints += cost;
            result.SkillStateSchemaVersion = CurrentSchemaVersion;
            return result;
        }

        public static Character DevelopmentRespec(Character character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            Character result = character.Clone();
            List<string> defaults = CharacterHelper.CreateDefaultSkillBar(character.ClassId);
            result.UnlockedSkillIds = DefaultUnlocked(defaults);
            result.AvailableSkillPoints = checked(result.AvailableSkillPoints + result.SpentSkillPoints);
            result.SpentSkillPoints = 0;
            result.SkillStateSchemaVersion = CurrentSchemaVersion;
            result.SkillBar = new List<string>(defaults);
            return result;
        }
    }
}
