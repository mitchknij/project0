// Activity.cs — Activiteit-toewijzing en efficiëntie-snapshot (vertaling van src/core/activity.ts).
// Puur, geen MonoBehaviour, immutability via Clone(). Bouwt op Progression, CombatMath, Elements, Bonuses.

using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class Activity
    {
        /// <summary>
        /// Berekent de efficiëntie-snapshot voor het huidige personage, activiteitstype en doel.
        /// Spiegelt computeEfficiencySnapshot() uit activity.ts exact (incl. gathering → 0-snapshot).
        /// </summary>
        public static EfficiencySnapshot ComputeEfficiencySnapshot(
            Character character,
            ActivityKind kind,
            string targetId,
            ActivityDataBundle data,
            long now,
            AccountBonuses bonuses = null)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (character.Activity != null && now < character.Activity.StartedAt)
                throw new ArgumentOutOfRangeException(nameof(now), "Time cannot move backwards.");
            if (bonuses == null) bonuses = AccountBonuses.Zero();

            data.Classes.TryGetValue(character.ClassId, out ClassDef cls);
            CoreStats stats = Progression.EffectiveStats(character, cls, data.Items, bonuses);

            PassiveMultipliers mult = cls?.Passive?.Multipliers;
            double xpMult = (mult?.Xp ?? 1.0) * (1.0 + bonuses.XpPct);

            if (kind == ActivityKind.Fighting && targetId != null)
            {
                if (!data.Monsters.TryGetValue(targetId, out MonsterDef monster))
                    return ZeroSnapshot(kind, targetId, now);

                MapDef map = null;
                if (data.Maps != null) data.Maps.TryGetValue(character.MapId, out map);
                double mapDensity = map != null && map.EncounterDensity > 0 ? map.EncounterDensity : 1.0;
                double travelOverheadMs = map != null && map.CombatTravelOverheadMs >= 0
                    ? map.CombatTravelOverheadMs
                    : CombatMath.DefaultMoveOverheadMs;
                SkillRotationApproximation rotation = SkillSnapshotApproximation.Evaluate(
                    character, cls, stats, monster, mapDensity);
                double basicCombatMs = CombatMath.TimeToKillMs(stats, monster, 0.0);
                double combatTimeMs = basicCombatMs / Math.Max(0.1, rotation.DamageRateMultiplier);
                double killTimeMs = combatTimeMs + travelOverheadMs;
                int playerMaxHp = CombatMath.MaxHp(character.Level, stats,
                    (mult?.Hp ?? 1.0) * (1.0 + bonuses.HpPct));
                int playerDefense = mult?.DefenseFlat ?? 0;
                double survivalFactor = CombatMath.EncounterSurvivalFactor(
                    playerMaxHp, playerDefense, stats, monster, combatTimeMs);
                double actionsPerHour  = (3_600_000.0 / killTimeMs)
                    * mapDensity * (1.0 + bonuses.CombatPct) * survivalFactor;
                double xpPerAction     = monster.Xp * xpMult;
                double coinsPerAction  = (monster.Coins.Min + monster.Coins.Max) / 2.0;

                return new EfficiencySnapshot
                {
                    Kind           = kind,
                    TargetId       = targetId,
                    ActionsPerHour = actionsPerHour,
                    XpPerAction    = xpPerAction,
                    CoinsPerAction = coinsPerAction,
                    SnapshotAt     = now,
                    ContentVersion = SnapshotValidation.CurrentContentVersion,
                    MapDensity = mapDensity,
                    TravelOverheadMs = travelOverheadMs,
                    SurvivalFactor = survivalFactor,
                    DebugBreakdown = $"formula=combat;mapDensity={mapDensity:0.###};travelMs={travelOverheadMs:0};survival={survivalFactor:0.###};skillRate={rotation.DamageRateMultiplier:0.###};{rotation.DebugBreakdown}",
                    SkillDiagnostics = rotation.Diagnostics,
                };
            }

            if (ActivitySkillMapping.IsHarvest(kind) && targetId != null)
            {
                if (!data.Nodes.TryGetValue(targetId, out ResourceNodeDef node))
                    return ZeroSnapshot(kind, targetId, now);

                SkillId skillId    = ActivitySkillMapping.ToSkillId(kind);
                int skillLevel     = character.Skills[skillId].Level;

                double passiveEff  = ActivitySkillMapping.PassiveEfficiency(mult, kind);
                double cardEff     = 1.0 + ActivitySkillMapping.EfficiencyBonus(bonuses, kind);

                double actionsPerHour = CombatMath.HarvestsPerHour(stats, skillLevel, node) * passiveEff * cardEff;
                double xpPerAction    = node.Xp * xpMult;

                return new EfficiencySnapshot
                {
                    Kind           = kind,
                    TargetId       = targetId,
                    ActionsPerHour = actionsPerHour,
                    XpPerAction    = xpPerAction,
                    CoinsPerAction = 0,
                    SnapshotAt     = now,
                    ContentVersion = SnapshotValidation.CurrentContentVersion,
                    MapDensity = 1.0,
                    TravelOverheadMs = 0.0,
                    SurvivalFactor = 1.0,
                    DebugBreakdown = "formula=harvest",
                };
            }

            // Idle of onbekend (incl. gathering — spiegelt TS-gedrag exact)
            return ZeroSnapshot(ActivityKind.Idle, null, now);
        }

        /// <summary>
        /// Wijst een activiteit toe aan het personage; valideert monster/node op de huidige map.
        /// Gooit bij ongeldige doelen; geeft null-efficiency terug bij idle.
        /// Spiegelt assignActivity() uit activity.ts exact.
        /// </summary>
        public static Character AssignActivity(
            Character character,
            ActivityKind kind,
            string targetId,
            ActivityDataBundle data,
            long now,
            AccountBonuses bonuses = null)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (character.Activity != null && now < character.Activity.StartedAt)
                throw new ArgumentOutOfRangeException(nameof(now), "Time cannot move backwards.");
            if (bonuses == null) bonuses = AccountBonuses.Zero();

            if (kind == ActivityKind.Idle)
            {
                var idle = character.Clone();
                idle.Activity   = new ActivityState { Kind = ActivityKind.Idle, StartedAt = now };
                idle.Efficiency = null;
                return idle;
            }

            if (targetId == null)
                throw new InvalidOperationException($"targetId required for activity kind \"{kind}\"");

            if (kind == ActivityKind.Fighting)
            {
                if (!data.Monsters.TryGetValue(targetId, out MonsterDef monster))
                    throw new InvalidOperationException($"Monster \"{targetId}\" not found");
                if (!MapScope.Includes(monster.MapId, character.MapId))
                    throw new InvalidOperationException(
                        $"Monster \"{targetId}\" is not on map \"{character.MapId}\"");
            }
            else if (ActivitySkillMapping.IsHarvest(kind))
            {
                if (!data.Nodes.TryGetValue(targetId, out ResourceNodeDef node))
                    throw new InvalidOperationException($"Node \"{targetId}\" not found");

                // Converteer ActivityKind naar HarvestSkill voor vergelijking
                HarvestSkill expectedSkill = ActivitySkillMapping.ToHarvestSkill(kind);
                if (node.Skill != expectedSkill)
                    throw new InvalidOperationException(
                        $"Node \"{targetId}\" requires {node.Skill}, not {kind}");
                if (!MapScope.Includes(node.MapId, character.MapId))
                    throw new InvalidOperationException(
                        $"Node \"{targetId}\" is not on map \"{character.MapId}\"");

                SkillId skillId    = ActivitySkillMapping.ToSkillId(kind);
                int skillLevel     = character.Skills[skillId].Level;
                if (skillLevel < node.LevelReq)
                    throw new InvalidOperationException(
                        $"Skill level {skillLevel} is below the requirement of {node.LevelReq}" +
                        $" for node \"{targetId}\"");
            }

            var efficiency = ComputeEfficiencySnapshot(character, kind, targetId, data, now, bonuses);
            var result     = character.Clone();
            result.Activity   = new ActivityState { Kind = kind, TargetId = targetId, StartedAt = now };
            result.Efficiency = efficiency;
            return result;
        }

        /// <summary>
        /// Laat het personage reizen naar een andere map; stopt lopende activiteit.
        /// Gooit wanneer de doel-map niet bestaat of niet verbonden is.
        /// Spiegelt travel() uit activity.ts exact.
        /// </summary>
        public static Character Travel(
            Character character,
            string mapId,
            Dictionary<string, MapDef> maps,
            long now)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (maps == null) throw new ArgumentNullException(nameof(maps));
            if (character.Activity != null && now < character.Activity.StartedAt)
                throw new ArgumentOutOfRangeException(nameof(now), "Time cannot move backwards.");
            if (!maps.ContainsKey(mapId))
                throw new InvalidOperationException($"Map \"{mapId}\" does not exist");

            if (!maps.TryGetValue(character.MapId, out MapDef currentMap) ||
                currentMap.Connections == null ||
                !currentMap.Connections.Contains(mapId))
                throw new InvalidOperationException(
                    $"Map \"{mapId}\" is not connected to current map \"{character.MapId}\"");

            var result      = character.Clone();
            result.MapId    = mapId;
            result.Activity  = new ActivityState { Kind = ActivityKind.Idle, StartedAt = now };
            result.Efficiency = null;
            return result;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static EfficiencySnapshot ZeroSnapshot(ActivityKind kind, string targetId, long now)
            => new EfficiencySnapshot
            {
                Kind           = kind,
                TargetId       = targetId,
                ActionsPerHour = 0,
                XpPerAction    = 0,
                CoinsPerAction = 0,
                SnapshotAt     = now,
                ContentVersion = SnapshotValidation.CurrentContentVersion,
                MapDensity = 1.0,
                TravelOverheadMs = 0.0,
                SurvivalFactor = 1.0,
                DebugBreakdown = "formula=inactive",
            };
    }

    public sealed class SkillRotationApproximation
    {
        public double DamageRateMultiplier = 1.0;
        public List<SkillSnapshotDiagnostic> Diagnostics = new List<SkillSnapshotDiagnostic>();
        public string DebugBreakdown;
    }

    public static class SkillSnapshotApproximation
    {
        private const double HorizonSeconds = 60.0;

        public static SkillRotationApproximation Evaluate(
            Character character,
            ClassDef classDef,
            CoreStats stats,
            MonsterDef monster,
            double mapDensity)
        {
            var result = new SkillRotationApproximation();
            if (character?.SkillBar == null || classDef?.Skills == null || stats == null || monster == null)
            {
                result.DebugBreakdown = "skills=missing_inputs";
                return result;
            }

            var skillsBySlot = new List<ClassSkillDef>();
            var casts = new List<int>();
            var contributions = new List<double>();
            int slotCount = Math.Min(Character.SkillBarSlots, character.SkillBar.Count);
            for (int slot = 0; slot < slotCount; slot++)
            {
                string skillId = character.SkillBar[slot];
                ClassSkillDef skill = FindSkill(classDef, skillId);
                var diagnostic = new SkillSnapshotDiagnostic
                {
                    SlotIndex = slot,
                    SkillId = skillId,
                    ExpectedTargetsPerCast = 1.0,
                    AssumptionSource = "slot_order_60s_steady_state",
                };
                if (string.IsNullOrWhiteSpace(skillId)) diagnostic.Reason = "empty_slot";
                else if (skill == null) diagnostic.Reason = "unknown_skill";
                else if (character.UnlockedSkillIds != null && !SkillBuild.IsUnlocked(character, skillId))
                    diagnostic.Reason = "skill_locked";
                else if (!AutoCombatPolicy.IsAutomaticallySupported(skill)) diagnostic.Reason = "auto_disabled";
                else if (!OfflineEffectSupported(skill)) diagnostic.Reason = "unsupported_offline_effect";
                else
                {
                    string assumptionSource;
                    diagnostic.ExpectedTargetsPerCast = ExpectedTargets(skill, mapDensity, out assumptionSource);
                    diagnostic.AssumptionSource = assumptionSource;
                    if (diagnostic.ExpectedTargetsPerCast < Math.Max(1, skill.MinimumAutoTargets))
                        diagnostic.Reason = "minimum_targets_not_met";
                    else
                    {
                        diagnostic.Included = true;
                        diagnostic.Reason = "included_slot_priority";
                    }
                }
                result.Diagnostics.Add(diagnostic);
                skillsBySlot.Add(diagnostic.Included ? skill : null);
                casts.Add(0);
                contributions.Add(0.0);
            }

            var readyAt = new double[slotCount];
            double baseAttacksPerSecond = CombatMath.AttacksPerSecond(stats);
            double physicalElement = Math.Max(0.0001, Elements.ElementMultiplier(
                Element.Physical, monster.Element ?? Element.Physical));
            double damageBuff = 0.0;
            double damageBuffUntil = 0.0;
            double hasteBuff = 0.0;
            double hasteBuffUntil = 0.0;
            double totalDamageEquivalent = 0.0;

            for (double time = 0.0; time < HorizonSeconds;)
            {
                if (time >= damageBuffUntil) damageBuff = 0.0;
                if (time >= hasteBuffUntil) hasteBuff = 0.0;
                int selectedSlot = -1;
                for (int slot = 0; slot < slotCount; slot++)
                    if (skillsBySlot[slot] != null && time + 0.000001 >= readyAt[slot])
                    {
                        selectedSlot = slot;
                        break;
                    }

                if (selectedSlot < 0)
                {
                    totalDamageEquivalent += 1.0 + damageBuff;
                }
                else
                {
                    ClassSkillDef skill = skillsBySlot[selectedSlot];
                    casts[selectedSlot]++;
                    readyAt[selectedSlot] = time + Math.Max(0.0, skill.CooldownMs / 1000.0);
                    if (skill.Buff != null)
                    {
                        double until = time + Math.Max(0.0, skill.Buff.DurationMs / 1000.0);
                        if (skill.Buff.Stat == BuffStat.Damage)
                        {
                            damageBuff = Math.Max(damageBuff, skill.Buff.Magnitude);
                            damageBuffUntil = Math.Max(damageBuffUntil, until);
                        }
                        else if (skill.Buff.Stat == BuffStat.Haste)
                        {
                            hasteBuff = Math.Max(hasteBuff, skill.Buff.Magnitude);
                            hasteBuffUntil = Math.Max(hasteBuffUntil, until);
                        }
                    }
                    else
                    {
                        double targets = result.Diagnostics[selectedSlot].ExpectedTargetsPerCast;
                        double equivalent = skill.DamageMultiplier * targets;
                        foreach (StatusInflict status in skill.Inflicts ?? new List<StatusInflict>())
                            if (status != null && status.DurationMs > 0 && status.TickIntervalMs > 0)
                                equivalent += status.Magnitude * Math.Floor(
                                    (double)status.DurationMs / status.TickIntervalMs) * targets;
                        equivalent *= Elements.ElementMultiplier(
                            skill.Element, monster.Element ?? Element.Physical) / physicalElement;
                        equivalent *= 1.0 + damageBuff;
                        totalDamageEquivalent += equivalent;
                        contributions[selectedSlot] += equivalent;
                    }
                }

                double attacksPerSecond = Math.Max(0.1, baseAttacksPerSecond * (1.0 + hasteBuff));
                time += 1.0 / attacksPerSecond;
            }

            result.DamageRateMultiplier = Math.Max(
                0.1,
                (totalDamageEquivalent / HorizonSeconds) / Math.Max(0.1, baseAttacksPerSecond));
            var debug = new List<string>();
            for (int slot = 0; slot < result.Diagnostics.Count; slot++)
            {
                SkillSnapshotDiagnostic diagnostic = result.Diagnostics[slot];
                diagnostic.ExpectedCastsPerHour = casts[slot] * (3600.0 / HorizonSeconds);
                diagnostic.ExpectedDamageContribution = contributions[slot] * (3600.0 / HorizonSeconds);
                debug.Add($"slot{slot + 1}:{diagnostic.SkillId ?? "empty"}={diagnostic.Reason},casts={diagnostic.ExpectedCastsPerHour:0.#}");
            }
            result.DebugBreakdown = string.Join("|", debug);
            return result;
        }

        private static bool OfflineEffectSupported(ClassSkillDef skill)
            => skill.DamageMultiplier > 0.0 || (skill.Buff != null &&
                (skill.Buff.Stat == BuffStat.Damage || skill.Buff.Stat == BuffStat.Haste));

        private static double ExpectedTargets(
            ClassSkillDef skill,
            double mapDensity,
            out string assumptionSource)
        {
            bool tilePattern = skill.Targeting == SkillTargetingKind.TilePatternAroundSource ||
                skill.Targeting == SkillTargetingKind.TilePatternAroundTarget;
            if (tilePattern)
            {
                if (skill.TilePattern == null)
                {
                    assumptionSource = "tile_pattern_missing";
                    return 0.0;
                }

                int affectedTileCount = TilePatternResolver.ResolveTiles(
                    new CombatTileCoordinate(0, 0),
                    skill.TilePattern).Count;
                double densityExpected = 1.0 + Math.Floor(Math.Max(0.0, mapDensity - 1.0));
                double expectedTargets = Math.Min(affectedTileCount, densityExpected);
                if (skill.TilePattern.MaxTargets > 0)
                    expectedTargets = Math.Min(expectedTargets, skill.TilePattern.MaxTargets);
                assumptionSource =
                    $"tile_pattern={skill.TilePattern.PatternKind};affected_tiles={affectedTileCount};" +
                    $"density_expected={densityExpected:0.#};max_targets={skill.TilePattern.MaxTargets}";
                return expectedTargets;
            }

            bool area = skill.Targeting == SkillTargetingKind.CircleAroundSource ||
                skill.Targeting == SkillTargetingKind.CircleAroundTarget;
            assumptionSource = "slot_order_60s_steady_state";
            return area ? 1.0 + Math.Floor(Math.Max(0.0, mapDensity - 1.0)) : 1.0;
        }

        private static ClassSkillDef FindSkill(ClassDef classDef, string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return null;
            foreach (ClassSkillDef skill in classDef.Skills)
                if (skill != null && skill.Id == skillId) return skill;
            return null;
        }
    }
}
