using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    /// <summary>Pure deterministic policies used by active auto-combat.</summary>
    public static class AutoCombatPolicy
    {
        public static CombatCandidate SelectTarget(IReadOnlyList<CombatCandidate> candidates)
        {
            CombatCandidate selected = null;
            if (candidates == null) return null;

            foreach (CombatCandidate candidate in candidates)
            {
                if (candidate == null || !candidate.Available) continue;
                if (selected == null || candidate.Distance < selected.Distance ||
                    (candidate.Distance == selected.Distance &&
                     string.CompareOrdinal(candidate.EntityId, selected.EntityId) < 0))
                    selected = candidate;
            }
            return selected;
        }

        public static ClassSkillDef NextAutoSkill(
            ClassDef classDef,
            Character character,
            Dictionary<string, long> skillNextReadyAt,
            long timestamp,
            CombatWorldFacts world,
            out AutoSkillSelectionDiagnostics diagnostics)
        {
            diagnostics = new AutoSkillSelectionDiagnostics();
            if (classDef?.Skills == null || character?.SkillBar == null)
            {
                diagnostics.FallbackReason = "missing_skill_bar";
                return null;
            }

            int slotCount = Math.Min(Character.SkillBarSlots, character.SkillBar.Count);
            for (int slot = 0; slot < slotCount; slot++)
            {
                string skillId = character.SkillBar[slot];
                var slotDiagnostic = new AutoSkillSlotDiagnostic { SlotIndex = slot, SkillId = skillId };
                diagnostics.Slots.Add(slotDiagnostic);
                if (string.IsNullOrWhiteSpace(skillId))
                {
                    slotDiagnostic.Reason = "empty_slot";
                    continue;
                }

                ClassSkillDef skill = FindSkill(classDef, skillId);
                if (skill == null)
                {
                    slotDiagnostic.Reason = "unknown_skill";
                    continue;
                }
                if (character.UnlockedSkillIds != null && !SkillBuild.IsUnlocked(character, skill.Id))
                {
                    slotDiagnostic.Reason = "skill_locked";
                    continue;
                }
                if (!IsAutomaticallySupported(skill))
                {
                    slotDiagnostic.Reason = "auto_disabled";
                    continue;
                }
                if (skill.DamageMultiplier <= 0.0 && skill.Buff == null && skill.ModifierDurationTicks <= 0)
                {
                    slotDiagnostic.Reason = "unsupported_effect";
                    continue;
                }
                if (skillNextReadyAt != null && skillNextReadyAt.TryGetValue(skill.Id, out long readyAt) && timestamp < readyAt)
                {
                    slotDiagnostic.Reason = "cooldown";
                    continue;
                }
                if (!MeetsTargetCondition(skill, world))
                {
                    slotDiagnostic.Reason = "target_condition";
                    continue;
                }

                slotDiagnostic.Eligible = true;
                slotDiagnostic.Reason = "selected";
                diagnostics.SelectedSlotIndex = slot;
                diagnostics.SelectedSkillId = skill.Id;
                return skill;
            }

            diagnostics.FallbackReason = "no_eligible_slotted_skill";
            return null;
        }

        public static bool IsAutomaticallySupported(ClassSkillDef skill)
            => skill != null && skill.AutoEnabled != false &&
                (skill.DamageMultiplier > 0.0 || skill.Buff != null || skill.ModifierDurationTicks > 0);

        private static bool MeetsTargetCondition(ClassSkillDef skill, CombatWorldFacts world)
        {
            bool tilePattern = skill.Targeting == SkillTargetingKind.TilePatternAroundSource ||
                skill.Targeting == SkillTargetingKind.TilePatternAroundTarget;
            if (tilePattern)
            {
                CombatSpatialFrame tileSpatial = world?.Spatial;
                string tileCenterActorId = skill.Targeting == SkillTargetingKind.TilePatternAroundSource
                    ? tileSpatial?.SourceActorId
                    : world?.PrimaryTargetActorId;
                CombatSpatialSnapshot tileCenter = FindActor(tileSpatial, tileCenterActorId);
                if (tileCenter == null || skill.TilePattern == null) return false;
                List<CombatSpatialSnapshot> actors = TilePatternResolver.ResolveActors(
                    tileCenter.Tile,
                    skill.TilePattern,
                    tileCenter.Floor,
                    CombatFaction.Hostile,
                    tileSpatial.Actors);
                return actors.Count >= Math.Max(1, skill.MinimumAutoTargets);
            }

            bool circle = skill.Targeting == SkillTargetingKind.CircleAroundSource ||
                skill.Targeting == SkillTargetingKind.CircleAroundTarget;
            if (!circle || skill.MinimumAutoTargets <= 1)
                return true;
            CombatSpatialFrame spatial = world?.Spatial;
            string centerActorId = skill.Targeting == SkillTargetingKind.CircleAroundSource
                ? spatial?.SourceActorId
                : world?.PrimaryTargetActorId;
            CombatSpatialSnapshot center = FindActor(spatial, centerActorId);
            if (center == null) return false;
            List<CombatAreaHit> hits = CircleShapeResolver.Resolve(
                center.GroundPosition,
                Math.Max(0.0, skill.RadiusWorldUnits),
                center.Floor,
                CombatFaction.Hostile,
                spatial.Actors);
            return hits.Count >= skill.MinimumAutoTargets;
        }

        private static ClassSkillDef FindSkill(ClassDef classDef, string skillId)
        {
            foreach (ClassSkillDef skill in classDef.Skills)
                if (skill != null && skill.Id == skillId) return skill;
            return null;
        }

        private static CombatSpatialSnapshot FindActor(CombatSpatialFrame frame, string actorId)
        {
            foreach (CombatSpatialSnapshot actor in frame?.Actors ?? new List<CombatSpatialSnapshot>())
                if (actor != null && actor.ActorId == actorId) return actor;
            return null;
        }
    }
}
