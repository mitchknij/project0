using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class LifeSkills
    {
        public static ActiveGatheringResult Tick(ActiveGatheringInput input, IRandomSource random)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (input.Character == null || input.Class == null || input.Node == null)
                throw new ArgumentException("Character, class, and node are required.", nameof(input));

            var state = input.State?.Clone() ?? new ActiveGatheringState();
            var result = new ActiveGatheringResult { State = state, Events = new List<GatheringEvent>(), Loot = new List<ItemStack>() };
            if (input.Timestamp < state.LastResolvedAt) return Reject(result, "timestamp_moved_backwards");
            if (string.IsNullOrWhiteSpace(input.TargetEntityId)) return Reject(result, "missing_target_entity_id");
            if (!MapScope.Includes(input.Node.MapId, input.Character.MapId)) return Reject(result, "node_not_on_character_map");

            if (ProcessCommands(input, state, result)) return result;
            if (!state.Active) return result;
            if (state.TargetEntityId != input.TargetEntityId || state.NodeId != input.Node.Id)
                return Reject(result, "active_target_mismatch");
            if (!input.World?.TargetAvailable ?? true) return Reject(result, "target_unavailable");
            if (!input.World.TargetInRange)
            {
                state.LastResolvedAt = input.Timestamp;
                result.Events.Add(new GatheringEvent { Kind = GatheringEventKind.MovementRequested, TargetEntityId = state.TargetEntityId });
                return result;
            }

            SkillId skillId = ActivitySkillMapping.ToSkillId(ActivitySkillMapping.ToActivityKind(input.Node.Skill));
            if (input.Character.Skills == null || !input.Character.Skills.TryGetValue(skillId, out SkillProgress skill) || skill.Level < input.Node.LevelReq)
                return Reject(result, "skill_requirement_not_met");

            CoreStats stats = Progression.EffectiveStats(input.Character, input.Class, input.ItemDefs ?? new Dictionary<string, ItemDef>(), input.Bonuses);
            ActivityKind kind = ActivitySkillMapping.ToActivityKind(input.Node.Skill);
            double speed = ActivitySkillMapping.PassiveEfficiency(input.Class.Passive?.Multipliers, kind) *
                (1.0 + ActivitySkillMapping.EfficiencyBonus(input.Bonuses ?? AccountBonuses.Zero(), kind));
            long interval = Math.Max(1L, (long)Math.Ceiling(CombatMath.HarvestTimeMs(stats, input.Node) / speed));
            long actions = (input.Timestamp - state.LastResolvedAt) / interval;
            if (actions > 0) state.LastResolvedAt += actions * interval;
            result.ActionIntervalMs = interval;
            result.ActionProgress01 = Math.Max(0d, Math.Min(1d,
                (input.Timestamp - state.LastResolvedAt) / (double)interval));
            if (actions <= 0) return result;

            double successChance = CombatMath.HarvestSuccessChance(skill.Level, input.Node);
            for (long action = 0; action < actions; action++)
            {
                bool success = random.NextDouble() < successChance;
                result.Events.Add(new GatheringEvent { Kind = GatheringEventKind.AttemptResolved, TargetEntityId = state.TargetEntityId, Success = success });
                if (!success) continue;
                result.SuccessfulActions++;
                result.SkillXp += input.Node.Xp;
                MergeLoot(result.Loot, DropSystem.RollDrops(input.Node.Drops, 1, random));
                result.Events.Add(new GatheringEvent { Kind = GatheringEventKind.ResourceGathered, TargetEntityId = state.TargetEntityId, Amount = 1, Success = true });
            }
            return result;
        }

        private static bool ProcessCommands(ActiveGatheringInput input, ActiveGatheringState state, ActiveGatheringResult result)
        {
            foreach (GatheringCommand command in input.Commands ?? new List<GatheringCommand>())
            {
                if (command == null) { Reject(result, "null_command"); continue; }
                if (command.Kind == GatheringCommandKind.Stop || command.Kind == GatheringCommandKind.MoveIntent)
                {
                    state.Active = false;
                    result.Events.Add(new GatheringEvent { Kind = GatheringEventKind.GatheringStopped, TargetEntityId = state.TargetEntityId });
                    return true;
                }
                if (command.Kind == GatheringCommandKind.Start)
                {
                    if (command.TargetEntityId != input.TargetEntityId) { Reject(result, "invalid_target"); continue; }
                    state.Active = true;
                    state.TargetEntityId = input.TargetEntityId;
                    state.NodeId = input.Node.Id;
                    state.LastResolvedAt = input.Timestamp;
                    result.Events.Add(new GatheringEvent { Kind = GatheringEventKind.GatheringStarted, TargetEntityId = state.TargetEntityId });
                    return true;
                }
            }
            return false;
        }

        private static ActiveGatheringResult Reject(ActiveGatheringResult result, string reason)
        {
            result.Events.Add(new GatheringEvent { Kind = GatheringEventKind.CommandRejected, Reason = reason });
            return result;
        }

        private static void MergeLoot(List<ItemStack> destination, List<ItemStack> source)
        {
            foreach (ItemStack stack in source)
            {
                ItemStack existing = destination.Find(item => item.ItemId == stack.ItemId);
                if (existing == null) destination.Add(new ItemStack { ItemId = stack.ItemId, Qty = stack.Qty });
                else existing.Qty += stack.Qty;
            }
        }
    }
}
