using System.Collections.Generic;
using IdleCloud.Data;

namespace IdleCloud.Core
{
    /// <summary>
    /// Pure validation for persisted state. Managers decide whether diagnostics are
    /// surfaced in development builds; gameplay code never depends on this checker.
    /// </summary>
    public static class StateInvariantValidator
    {
        public static List<string> Validate(Account account)
        {
            var issues = new List<string>();
            if (account == null)
            {
                issues.Add("account_missing");
                return issues;
            }
            if (account.Bank == null) issues.Add("bank_missing");
            else
            {
                if (account.Bank.Coins < 0) issues.Add("bank_coins_negative");
                if (account.Bank.MaxSlots < 0) issues.Add("bank_max_slots_negative");
                ValidateStacks(account.Bank.Slots, "bank", issues);
            }

            var characterIds = new HashSet<string>();
            foreach (Character character in account.Characters ?? new List<Character>())
            {
                if (character == null)
                {
                    issues.Add("character_missing");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(character.Id)) issues.Add("character_id_missing");
                else if (!characterIds.Add(character.Id)) issues.Add("character_id_duplicate:" + character.Id);
                if (character.Level < 1) issues.Add("character_level_invalid:" + character.Id);
                if (character.Xp < 0) issues.Add("character_xp_negative:" + character.Id);
                if (character.MaxInventorySlots < 0) issues.Add("inventory_max_slots_negative:" + character.Id);
                ValidateStacks(character.Inventory, "inventory:" + character.Id, issues);
                ValidateSkillBar(character, issues);
                ValidateSkillBuild(character, issues);

                foreach (var skill in character.Skills ?? new Dictionary<SkillId, SkillProgress>())
                    if (skill.Value == null || skill.Value.Level < 1 || skill.Value.Xp < 0)
                        issues.Add("skill_invalid:" + character.Id + ":" + skill.Key);

                foreach (var talent in character.Talents ?? new Dictionary<string, int>())
                    if (talent.Value < 0) issues.Add("talent_negative:" + character.Id + ":" + talent.Key);
            }
            return issues;
        }

        private static void ValidateStacks(List<ItemStack> stacks, string owner, List<string> issues)
        {
            foreach (ItemStack stack in stacks ?? new List<ItemStack>())
            {
                if (stack == null || string.IsNullOrWhiteSpace(stack.ItemId) || stack.Qty <= 0)
                    issues.Add("item_stack_invalid:" + owner);
            }
        }

        private static void ValidateSkillBar(Character character, List<string> issues)
        {
            if (character.SkillBar == null)
            {
                issues.Add("skill_bar_missing:" + character.Id);
                return;
            }
            if (character.SkillBar.Count != Character.SkillBarSlots)
                issues.Add("skill_bar_length_invalid:" + character.Id);

            var classSkillIds = new HashSet<string>();
            ClassDef classDef = RuntimeContent.Get(character.ClassId);
            foreach (ClassSkillDef skill in classDef?.Skills ?? new List<ClassSkillDef>())
                if (skill != null && !string.IsNullOrWhiteSpace(skill.Id))
                    classSkillIds.Add(skill.Id);

            var assignedSkillIds = new HashSet<string>();
            foreach (string skillId in character.SkillBar)
            {
                if (skillId == null) continue;
                if (!classSkillIds.Contains(skillId))
                    issues.Add("skill_bar_skill_invalid:" + character.Id + ":" + skillId);
                if (!assignedSkillIds.Add(skillId))
                    issues.Add("skill_bar_duplicate:" + character.Id + ":" + skillId);
            }
        }

        private static void ValidateSkillBuild(Character character, List<string> issues)
        {
            if (character.SkillStateSchemaVersion != SkillBuild.CurrentSchemaVersion)
                issues.Add("skill_state_schema_invalid:" + character.Id);
            if (character.AvailableSkillPoints < 0 || character.SpentSkillPoints < 0)
                issues.Add("skill_points_negative:" + character.Id);
            var unlocked = new HashSet<string>();
            foreach (string skillId in character.UnlockedSkillIds ?? new List<string>())
                if (string.IsNullOrWhiteSpace(skillId) || !unlocked.Add(skillId))
                    issues.Add("skill_unlock_invalid:" + character.Id);
            foreach (string skillId in character.SkillBar ?? new List<string>())
                if (skillId != null && !unlocked.Contains(skillId))
                    issues.Add("skill_bar_locked:" + character.Id + ":" + skillId);
        }
    }
}
