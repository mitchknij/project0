using System;
using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;

namespace IdleCloud.Managers
{
    /// <summary>
    /// Validates the existing stable-ID registries at the Manager boundary. Core
    /// receives only already-validated pure definitions and never sees Unity assets.
    /// </summary>
    public static class ContentValidator
    {
        public static List<string> Validate()
        {
            var issues = new List<string>();
            foreach (var pair in RuntimeContent.Maps)
            {
                MapDef map = pair.Value;
                if (map == null || map.Id != pair.Key) issues.Add("map_id_mismatch:" + pair.Key);
                if (map != null && (map.EncounterDensity <= 0 || map.CombatTravelOverheadMs < 0))
                    issues.Add("map_encounter_invalid:" + pair.Key);
                foreach (string connection in map?.Connections ?? new List<string>())
                    if (!RuntimeContent.Maps.ContainsKey(connection)) issues.Add("map_connection_missing:" + pair.Key + ":" + connection);
            }

            foreach (var pair in RuntimeContent.Items)
                if (pair.Value == null || pair.Value.Id != pair.Key || pair.Value.StackLimit <= 0)
                    issues.Add("item_invalid:" + pair.Key);

            foreach (var pair in RuntimeContent.Monsters)
            {
                MonsterDef monster = pair.Value;
                if (monster == null || monster.Id != pair.Key ||
                    (monster.MapId != MapScope.AnyMap && !RuntimeContent.Maps.ContainsKey(monster.MapId)) ||
                    monster.Hp <= 0 || monster.Damage < 0 || monster.Coins == null ||
                    monster.Coins.Min < 0 || monster.Coins.Max < monster.Coins.Min)
                    issues.Add("monster_invalid:" + pair.Key);
                ValidateDropTable("monster_drop:" + pair.Key, monster?.Drops, issues);
            }

            foreach (var pair in RuntimeContent.Nodes)
            {
                ResourceNodeDef node = pair.Value;
                if (node == null || node.Id != pair.Key ||
                    (node.MapId != MapScope.AnyMap && !RuntimeContent.Maps.ContainsKey(node.MapId)) ||
                    node.LevelReq < 1 || node.BaseTimeMs <= 0 || node.Xp < 0)
                    issues.Add("node_invalid:" + pair.Key);
                foreach (DropEntry drop in node?.Drops ?? new List<DropEntry>())
                    if (drop == null || string.IsNullOrWhiteSpace(drop.ItemId) || !RuntimeContent.Items.ContainsKey(drop.ItemId) ||
                        drop.Chance < 0 || drop.Chance > 1 || drop.Min < 0 || drop.Max < drop.Min)
                        issues.Add("node_drop_invalid:" + pair.Key);
            }

            foreach (var pair in RuntimeContent.All)
            {
                ClassDef classDef = pair.Value;
                if (classDef == null || classDef.Id != pair.Key)
                {
                    issues.Add("class_invalid:" + pair.Key);
                    continue;
                }
                var skillIds = new HashSet<string>();
                foreach (ClassSkillDef skill in classDef.Skills ?? new List<ClassSkillDef>())
                {
                    string prefix = "skill_invalid:" + pair.Key + ":" + (skill?.Id ?? "missing");
                    if (skill == null || string.IsNullOrWhiteSpace(skill.Id) || !skillIds.Add(skill.Id) ||
                        skill.CooldownMs < 0 || !IsWholeCombatTick(skill.CooldownMs) ||
                        skill.SkillPointCost < 0 || skill.Tier < 0)
                    {
                        issues.Add(prefix);
                        continue;
                    }
                    bool circle = skill.Targeting == SkillTargetingKind.CircleAroundSource ||
                        skill.Targeting == SkillTargetingKind.CircleAroundTarget;
                    if (circle && (skill.RadiusWorldUnits <= 0 || skill.MinimumAutoTargets < 1))
                        issues.Add(prefix + ":circle");
                    bool tilePattern = skill.Targeting == SkillTargetingKind.TilePatternAroundSource ||
                        skill.Targeting == SkillTargetingKind.TilePatternAroundTarget;
                    if (tilePattern && skill.TilePattern == null)
                        issues.Add(prefix + ":tile_pattern_missing");
                    if (!tilePattern && skill.TilePattern != null)
                        issues.Add(prefix + ":tile_pattern_unexpected");
                    if (skill.TilePattern != null)
                        ValidateTilePattern(prefix, skill.TilePattern, issues);
                    if (skill.Timing == SkillTimingKind.ScheduledImpact && skill.ImpactDelayTicks <= 0)
                        issues.Add(prefix + ":schedule");
                    if (skill.ModifierDurationTicks < 0)
                        issues.Add(prefix + ":modifier");
                    if (!string.IsNullOrWhiteSpace(skill.PrerequisiteSkillId) &&
                        FindSkill(classDef, skill.PrerequisiteSkillId) == null)
                        issues.Add(prefix + ":prerequisite");
                    foreach (StatusInflict status in skill.Inflicts ?? new List<StatusInflict>())
                        if (status == null || status.DurationMs <= 0 || status.TickIntervalMs <= 0 ||
                            !IsWholeCombatTick(status.DurationMs) || !IsWholeCombatTick(status.TickIntervalMs))
                            issues.Add(prefix + ":status");
                }
            }

            foreach (var pair in RuntimeContent.Recipes)
            {
                RecipeDef recipe = pair.Value;
                if (recipe == null || recipe.Id != pair.Key || string.IsNullOrWhiteSpace(recipe.OutputItemId) ||
                    !RuntimeContent.Items.ContainsKey(recipe.OutputItemId) || recipe.OutputQty <= 0 ||
                    recipe.CoinCost < 0 || recipe.LevelReq < 1)
                    issues.Add("recipe_invalid:" + pair.Key);
                foreach (ItemStack input in recipe?.Inputs ?? new List<ItemStack>())
                    if (input == null || input.Qty <= 0 || !RuntimeContent.Items.ContainsKey(input.ItemId))
                        issues.Add("recipe_input_invalid:" + pair.Key);
            }

            OfflineBalanceConfig offline = OfflineBalanceRepo.Default;
            if (offline == null || offline.Rate < 0 || offline.Rate > 1 ||
                offline.CapMs <= 0 || offline.MinimumDurationMs < 0 ||
                offline.MinimumDurationMs > offline.CapMs)
                issues.Add("offline_balance_invalid");
            return issues;
        }

        private static void ValidateTilePattern(string prefix, TilePatternDef pattern, List<string> issues)
        {
            if (!Enum.IsDefined(typeof(TilePatternKind), pattern.PatternKind))
                issues.Add(prefix + ":tile_pattern_kind");
            if (pattern.MaxTargets < 0)
                issues.Add(prefix + ":tile_pattern_max_targets");
            if (pattern.FloorPolicy != TilePatternFloorPolicy.SameFloor)
                issues.Add(prefix + ":tile_pattern_floor_policy");

            switch (pattern.PatternKind)
            {
                case TilePatternKind.SingleTile:
                    break;
                case TilePatternKind.Cross:
                case TilePatternKind.SquareRadius:
                    if (pattern.Size < 1 || pattern.Size > TilePatternDef.MaxSafeOffsetMagnitude)
                        issues.Add(prefix + ":tile_pattern_size");
                    break;
                case TilePatternKind.CustomOffsets:
                    if (pattern.CustomOffsets == null || pattern.CustomOffsets.Count == 0)
                    {
                        issues.Add(prefix + ":tile_pattern_offsets_missing");
                        break;
                    }

                    var offsets = new HashSet<CombatTileCoordinate>();
                    foreach (CombatTileCoordinate offset in pattern.CustomOffsets)
                    {
                        if (Math.Abs((long)offset.X) > TilePatternDef.MaxSafeOffsetMagnitude ||
                            Math.Abs((long)offset.Y) > TilePatternDef.MaxSafeOffsetMagnitude)
                            issues.Add(prefix + ":tile_pattern_offset_range");
                        if (!offsets.Add(offset))
                            issues.Add(prefix + ":tile_pattern_duplicate_offset");
                    }
                    break;
            }
        }

        private static void ValidateDropTable(string prefix, DropTable table, List<string> issues)
        {
            if (table == null) return;
            foreach (DropItem item in table.Always ?? new List<DropItem>())
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || !RuntimeContent.Items.ContainsKey(item.ItemId) ||
                    item.Min < 0 || item.Max < item.Min)
                    issues.Add(prefix + ":always");

            foreach (DropEntry entry in table.Tertiary ?? new List<DropEntry>())
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId) || !RuntimeContent.Items.ContainsKey(entry.ItemId) ||
                    entry.Chance < 0 || entry.Chance > 1 || entry.Min < 0 || entry.Max < entry.Min)
                    issues.Add(prefix + ":tertiary");

            if (table.Main == null) return;
            if (table.Main.Rolls < 0) issues.Add(prefix + ":rolls");
            int totalWeight = 0;
            foreach (WeightedSlot slot in table.Main.Slots ?? new List<WeightedSlot>())
            {
                if (slot == null || slot.Weight < 0) { issues.Add(prefix + ":weight"); continue; }
                totalWeight += slot.Weight;
                if (!slot.Nothing && (string.IsNullOrWhiteSpace(slot.ItemId) || !RuntimeContent.Items.ContainsKey(slot.ItemId) ||
                    slot.Min < 0 || slot.Max < slot.Min))
                    issues.Add(prefix + ":item");
                if (slot.Nothing && !string.IsNullOrWhiteSpace(slot.ItemId)) issues.Add(prefix + ":nothing_item");
            }
            if (totalWeight <= 0 && table.Main.Rolls > 0) issues.Add(prefix + ":total_weight");
        }

        private static bool IsWholeCombatTick(double milliseconds)
        {
            if (milliseconds < 0) return false;
            double ticks = milliseconds / CombatTimeContract.DefaultStepMilliseconds;
            return Math.Abs(ticks - Math.Round(ticks)) < 0.000001;
        }

        private static ClassSkillDef FindSkill(ClassDef classDef, string skillId)
        {
            foreach (ClassSkillDef skill in classDef?.Skills ?? new List<ClassSkillDef>())
                if (skill != null && skill.Id == skillId) return skill;
            return null;
        }
    }
}
