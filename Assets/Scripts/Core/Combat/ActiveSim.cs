using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    /// <summary>
    /// Deterministic, headless resolution for one active-combat tick. World movement
    /// remains outside Core and is returned as a presentation-neutral intent.
    /// </summary>
    public static class ActiveSim
    {
        public static ActiveSimResult Tick(ActiveSimInput input, IRandomSource random)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (input.Character == null) throw new ArgumentNullException(nameof(input.Character));
            if (input.Class == null) throw new ArgumentNullException(nameof(input.Class));
            if (input.Monster == null) throw new ArgumentNullException(nameof(input.Monster));

            var state = input.State?.Clone() ?? new ActiveCombatState();
            var events = new List<CombatEvent>();
            var result = new ActiveSimResult { State = state, Events = events };

            if (input.Timestamp < state.LastUpdatedAt)
            {
                Reject(events, "timestamp_moved_backwards");
                StampEvents(events, state.SimulationTick);
                result.EnemyKilled = state.KillResolved;
                result.PlayerDefeated = state.PlayerDefeated;
                return result;
            }

            if (!IsValidDefinition(input.Monster))
            {
                Reject(events, "invalid_monster_definition");
                StampEvents(events, state.SimulationTick);
                result.EnemyKilled = state.KillResolved;
                result.PlayerDefeated = state.PlayerDefeated;
                return result;
            }
            if (string.IsNullOrEmpty(input.TargetEntityId))
            {
                Reject(events, "missing_target_entity_id");
                StampEvents(events, state.SimulationTick);
                return result;
            }

            state.SimulationTick = checked(state.SimulationTick + 1L);

            var stats = Progression.EffectiveStats(
                input.Character,
                input.Class,
                input.ItemDefs ?? new Dictionary<string, ItemDef>(),
                input.Bonuses ?? AccountBonuses.Zero());
            var passive = input.Class.Passive?.Multipliers;
            int playerMaxHp = CombatMath.MaxHp(
                Math.Max(1, input.Character.Level),
                stats,
                (passive?.Hp ?? 1.0) * (1.0 + (input.Bonuses?.HpPct ?? 0.0)));

            if (state.PlayerHp <= 0 && !state.PlayerDefeated)
                state.PlayerHp = playerMaxHp;
            if (state.EnemyHp <= 0 && !state.KillResolved)
                state.EnemyHp = input.Monster.Hp;
            SynchronizeActorHealth(input, state);
            if (state.SkillRuntime == null)
                state.SkillRuntime = new CombatSkillRuntimeState();

            ApplyOutOfCombatRegen(input, state, playerMaxHp);

            List<SequencedCombatCommand> commands = IngestCommands(input.Commands, state, events);
            bool movementRequested = ProcessCommands(input, state, commands, events);
            ResolveDueScheduledEffects(input, state, stats, passive, random, events);
            ResumeAutoCombatAfterGrace(input, state, events);
            bool actionResolved = false;

            if (!movementRequested && !state.AutoCombatStopped && string.IsNullOrEmpty(state.TargetId) && input.World?.TargetAvailable == true)
            {
                state.TargetId = input.TargetEntityId;
                events.Add(new CombatEvent
                {
                    Kind = CombatEventKind.TargetSelected,
                    ActorId = input.Character.Id,
                    TargetId = input.TargetEntityId,
                });
            }

            if (!movementRequested && !state.PlayerDefeated && !state.KillResolved)
            {
                actionResolved = ResolveManualSkill(input, state, commands, stats, passive, random, events);

                if (!actionResolved && !state.AutoCombatStopped && HasValidTarget(state, input) && CanFight(input.World) &&
                    input.Timestamp >= state.PlayerNextAttackAt)
                {
                    ClassSkillDef skill = null;
                    if (input.Config?.AutoSkillRotation == true)
                    {
                        if (string.IsNullOrEmpty(input.World.PrimaryTargetActorId))
                            input.World.PrimaryTargetActorId = state.TargetId;
                        skill = AutoCombatPolicy.NextAutoSkill(
                            input.Class,
                            input.Character,
                            state.SkillNextReadyAt,
                            input.Timestamp,
                            input.World,
                            out AutoSkillSelectionDiagnostics diagnostics);
                        state.SkillRuntime.LastAutoSelectionDiagnostics = diagnostics.ToString();
                        // Carry the previous id forward: a basic-attack fallback tick must leave it
                        // unchanged, not reset it, so observers only see it move on a real auto cast.
                        diagnostics.SelectionSequenceId =
                            state.SkillRuntime.LastAutoSelection?.SelectionSequenceId ?? 0L;
                        state.SkillRuntime.LastAutoSelection = diagnostics;
                    }
                    if (skill != null)
                    {
                        ExecuteSkillAction(input, state, stats, passive, skill, random, events, 0L);
                        state.SkillRuntime.LastAutoSelection.SelectionSequenceId =
                            state.SkillRuntime.LastActionSequenceId;
                    }
                    else
                        ResolvePlayerAttack(input, state, stats, passive, null, random, events, 0L);
                    if (skill != null)
                        state.SkillNextReadyAt[skill.Id] = input.Timestamp + Math.Max(0L, (long)Math.Ceiling(skill.CooldownMs));
                    actionResolved = true;
                }

                if (HasValidTarget(state, input) && !CanFight(input.World))
                    RequestMovement(events, state.TargetId, "target_out_of_range");
            }

            if (!state.KillResolved && !state.PlayerDefeated && HasValidTarget(state, input))
                ResolveEnemyAttacks(input, state, stats, random, events);

            state.SkillRuntime.QueuedManualCommand = null;

            state.LastUpdatedAt = input.Timestamp;
            StampEvents(events, state.SimulationTick);
            result.EnemyKilled = state.KillResolved;
            result.PlayerDefeated = state.PlayerDefeated;
            result.DefeatedActorIds = DefeatedActorsFrom(events);
            return result;
        }

        private static List<SequencedCombatCommand> IngestCommands(
            List<CombatCommand> commands,
            ActiveCombatState state,
            List<CombatEvent> events)
        {
            var sequenced = new List<SequencedCombatCommand>();
            if (commands == null) return sequenced;

            foreach (CombatCommand command in commands)
            {
                if (command == null)
                {
                    Reject(events, "null_command");
                    continue;
                }

                var accepted = new SequencedCombatCommand
                {
                    Command = new CombatCommand
                    {
                        Kind = command.Kind,
                        TargetId = command.TargetId,
                        SkillId = command.SkillId,
                    },
                    SimulationTick = state.SimulationTick,
                    SequenceId = checked(++state.LastCommandSequenceId),
                };
                sequenced.Add(accepted);
                if (command.Kind == CombatCommandKind.TriggerSkill)
                    state.SkillRuntime.QueuedManualCommand = accepted.Clone();
            }
            return sequenced;
        }

        private static bool ProcessCommands(
            ActiveSimInput input,
            ActiveCombatState state,
            List<SequencedCombatCommand> commands,
            List<CombatEvent> events)
        {
            bool movementRequested = false;

            foreach (SequencedCombatCommand sequenced in commands)
            {
                CombatCommand command = sequenced.Command;

                if (command.Kind == CombatCommandKind.MoveIntent)
                {
                    state.AutoCombatStopped = true;
                    state.AutoCombatStoppedAt = input.Timestamp;
                    movementRequested = true;
                    state.SkillRuntime.QueuedManualCommand = null;
                    RequestMovement(events, command.TargetId, "manual_movement", sequenced.SequenceId);
                    continue;
                }

                if (command.Kind == CombatCommandKind.SelectTarget)
                {
                    if (!input.World.TargetAvailable || command.TargetId != input.TargetEntityId)
                    {
                        Reject(events, "invalid_target", command.TargetId, sequenced.SequenceId);
                        continue;
                    }

                    state.TargetId = command.TargetId;
                    state.AutoCombatStopped = false;
                    events.Add(new CombatEvent
                    {
                        Kind = CombatEventKind.TargetSelected,
                        ActorId = input.Character.Id,
                        TargetId = command.TargetId,
                        CommandSequenceId = sequenced.SequenceId,
                    });
                }
            }

            return movementRequested;
        }

        private static void ResumeAutoCombatAfterGrace(ActiveSimInput input, ActiveCombatState state, List<CombatEvent> events)
        {
            long graceMs = input.Config?.AutoResumeGraceMs ?? 0L;
            if (!state.AutoCombatStopped || graceMs <= 0L || input.Timestamp < state.AutoCombatStoppedAt + graceMs) return;

            state.AutoCombatStopped = false;
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.AutoCombatResumed,
                ActorId = input.Character.Id,
                TargetId = state.TargetId,
            });
        }

        private static void ApplyOutOfCombatRegen(ActiveSimInput input, ActiveCombatState state, int playerMaxHp)
        {
            double regenPctPerSec = input.Config?.OutOfCombatRegenPctPerSec ?? 0.0;
            if (regenPctPerSec <= 0.0 || state.PlayerDefeated || (!state.KillResolved && CanFight(input.World))) return;

            long elapsedMs = input.Timestamp - state.LastUpdatedAt;
            // elapsedMs is milliseconds; the rate is configured per second.
            int heal = (int)Math.Floor(playerMaxHp * regenPctPerSec * elapsedMs / 1000.0);
            if (heal > 0) state.PlayerHp = Math.Min(playerMaxHp, state.PlayerHp + heal);
        }

        private static bool ResolveManualSkill(
            ActiveSimInput input,
            ActiveCombatState state,
            List<SequencedCombatCommand> commands,
            CoreStats stats,
            PassiveMultipliers passive,
            IRandomSource random,
            List<CombatEvent> events)
        {
            foreach (SequencedCombatCommand sequenced in commands)
            {
                CombatCommand command = sequenced.Command;
                if (command.Kind != CombatCommandKind.TriggerSkill) continue;
                ClassSkillDef skill = FindSkill(input.Class, command.SkillId);
                if (skill == null)
                {
                    Reject(events, "unknown_skill", command.SkillId, sequenced.SequenceId);
                    continue;
                }
                if (input.Character.UnlockedSkillIds != null &&
                    !SkillBuild.IsUnlocked(input.Character, skill.Id))
                {
                    Reject(events, "skill_locked", command.SkillId, sequenced.SequenceId);
                    continue;
                }
                bool requiresHostileTarget = skill.Targeting == SkillTargetingKind.HostileActor ||
                    skill.Targeting == SkillTargetingKind.CircleAroundTarget ||
                    skill.Targeting == SkillTargetingKind.TilePatternAroundTarget;
                if (requiresHostileTarget && !HasValidTarget(state, input))
                {
                    Reject(events, "no_valid_target", command.SkillId, sequenced.SequenceId);
                    continue;
                }
                if (requiresHostileTarget && !CanFight(input.World))
                {
                    Reject(events, "target_out_of_range", command.SkillId, sequenced.SequenceId);
                    continue;
                }
                bool appliesModifier = skill.ModifierDurationTicks > 0 || skill.Buff != null;
                if (skill.DamageMultiplier <= 0.0 && !appliesModifier)
                {
                    Reject(events, "unsupported_skill", command.SkillId, sequenced.SequenceId);
                    continue;
                }
                if (state.SkillNextReadyAt.TryGetValue(skill.Id, out long readyAt) && input.Timestamp < readyAt)
                {
                    Reject(events, "skill_on_cooldown", skill.Id, sequenced.SequenceId);
                    continue;
                }

                ExecuteSkillAction(input, state, stats, passive, skill, random, events, sequenced.SequenceId);
                state.SkillNextReadyAt[skill.Id] = input.Timestamp + Math.Max(0L, (long)Math.Ceiling(skill.CooldownMs));
                return true;
            }

            return false;
        }

        private static void ExecuteSkillAction(
            ActiveSimInput input,
            ActiveCombatState state,
            CoreStats stats,
            PassiveMultipliers passive,
            ClassSkillDef skill,
            IRandomSource random,
            List<CombatEvent> events,
            long commandSequenceId)
        {
            if (skill.ModifierDurationTicks > 0 || skill.Buff != null)
            {
                ApplyModifierSkill(input, state, stats, skill, events, commandSequenceId);
                return;
            }
            if (skill.Timing == SkillTimingKind.ScheduledImpact)
            {
                ScheduleSkillImpact(input, state, stats, skill, events, commandSequenceId);
                return;
            }
            ResolvePlayerAttack(input, state, stats, passive, skill, random, events, commandSequenceId);
        }

        private static void ScheduleSkillImpact(
            ActiveSimInput input,
            ActiveCombatState state,
            CoreStats stats,
            ClassSkillDef skill,
            List<CombatEvent> events,
            long commandSequenceId)
        {
            long delayTicks = Math.Max(1L, skill.ImpactDelayTicks);
            long actionSequenceId = checked(++state.SkillRuntime.LastActionSequenceId);
            long scheduledSequenceId = checked(++state.SkillRuntime.LastScheduledEffectSequenceId);
            var effect = new ScheduledCombatEffect
            {
                SequenceId = scheduledSequenceId,
                ExecuteTick = checked(state.SimulationTick + delayTicks),
                Kind = ScheduledCombatEffectKind.ResolveSkillImpact,
                ReferenceId = skill.Id,
                SourceActorId = input.Character.Id,
                TargetActorId = state.TargetId,
                SkillId = skill.Id,
                CommandSequenceId = commandSequenceId,
                ActionSequenceId = actionSequenceId,
            };
            if (IsTilePatternSkill(skill))
            {
                CombatSpatialSnapshot anchor = FindTileAnchor(input, state, skill);
                if (anchor != null)
                {
                    effect.HasCapturedAnchor = true;
                    effect.CapturedAnchorTile = anchor.Tile;
                    effect.CapturedAnchorFloor = anchor.Floor;
                }
            }
            state.SkillRuntime.ScheduledEffects.Add(effect);
            state.PlayerAttacksResolved++;
            state.PlayerNextAttackAt = input.Timestamp + AttackIntervalMs(stats, state, input.Character.Id);

            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.SkillCastStarted,
                ActorId = input.Character.Id,
                TargetId = state.TargetId,
                SkillId = skill.Id,
                CommandSequenceId = commandSequenceId,
                ActionSequenceId = actionSequenceId,
                DurationTicks = delayTicks,
            });
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.CombatEffectScheduled,
                ActorId = input.Character.Id,
                TargetId = state.TargetId,
                SkillId = skill.Id,
                CommandSequenceId = commandSequenceId,
                ActionSequenceId = actionSequenceId,
                DurationTicks = delayTicks,
            });
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.CooldownStarted,
                ActorId = input.Character.Id,
                TargetId = state.TargetId,
                SkillId = skill.Id,
                CommandSequenceId = commandSequenceId,
                ActionSequenceId = actionSequenceId,
            });
        }

        private static void ApplyModifierSkill(
            ActiveSimInput input,
            ActiveCombatState state,
            CoreStats stats,
            ClassSkillDef skill,
            List<CombatEvent> events,
            long commandSequenceId)
        {
            long actionSequenceId = checked(++state.SkillRuntime.LastActionSequenceId);
            string instanceId = $"modifier:{actionSequenceId}:{skill.Id}";
            ResolveModifierDefinition(
                skill,
                out CombatStatProperty property,
                out CombatModifierOperation operation,
                out double magnitude,
                out long durationTicks);
            state.SkillRuntime.ActiveModifiers.RemoveAll(item =>
                item != null && item.DefinitionId == skill.Id && item.TargetActorId == input.Character.Id);
            var modifier = new TransientCombatModifier
            {
                InstanceId = instanceId,
                DefinitionId = skill.Id,
                SourceActorId = input.Character.Id,
                TargetActorId = input.Character.Id,
                Property = property,
                Operation = operation,
                Magnitude = magnitude,
                StartTick = state.SimulationTick,
                EndTick = checked(state.SimulationTick + durationTicks),
                ApplicationSequenceId = actionSequenceId,
            };
            state.SkillRuntime.ActiveModifiers.Add(modifier);
            long scheduledSequenceId = checked(++state.SkillRuntime.LastScheduledEffectSequenceId);
            state.SkillRuntime.ScheduledEffects.Add(new ScheduledCombatEffect
            {
                SequenceId = scheduledSequenceId,
                ExecuteTick = modifier.EndTick,
                Kind = ScheduledCombatEffectKind.ExpireModifier,
                ReferenceId = instanceId,
            });
            state.PlayerNextAttackAt = input.Timestamp + AttackIntervalMs(stats, state, input.Character.Id);

            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.SkillExecuted,
                ActorId = input.Character.Id,
                TargetId = input.Character.Id,
                SkillId = skill.Id,
                Hit = true,
                CommandSequenceId = commandSequenceId,
                ActionSequenceId = actionSequenceId,
            });
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.TransientModifierApplied,
                ActorId = input.Character.Id,
                TargetId = input.Character.Id,
                SkillId = skill.Id,
                Amount = (int)Math.Round(magnitude),
                CommandSequenceId = commandSequenceId,
                ActionSequenceId = actionSequenceId,
            });
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.CooldownStarted,
                ActorId = input.Character.Id,
                SkillId = skill.Id,
                CommandSequenceId = commandSequenceId,
                ActionSequenceId = actionSequenceId,
            });
        }

        private static void ResolveModifierDefinition(
            ClassSkillDef skill,
            out CombatStatProperty property,
            out CombatModifierOperation operation,
            out double magnitude,
            out long durationTicks)
        {
            if (skill.ModifierDurationTicks > 0)
            {
                property = skill.ModifierProperty;
                operation = skill.ModifierOperation;
                magnitude = skill.ModifierMagnitude;
                durationTicks = skill.ModifierDurationTicks;
                return;
            }

            SelfBuff buff = skill.Buff;
            property = buff.Stat switch
            {
                BuffStat.Haste => CombatStatProperty.AttackSpeed,
                BuffStat.Defense => CombatStatProperty.Defense,
                _ => CombatStatProperty.Damage,
            };
            operation = buff.Stat == BuffStat.Defense
                ? CombatModifierOperation.FlatAdd
                : CombatModifierOperation.AdditivePercent;
            magnitude = buff.Magnitude;
            durationTicks = CombatTimeContract.DurationToTicks(Math.Max(0, buff.DurationMs));
        }

        private static void ResolveDueScheduledEffects(
            ActiveSimInput input,
            ActiveCombatState state,
            CoreStats stats,
            PassiveMultipliers passive,
            IRandomSource random,
            List<CombatEvent> events)
        {
            List<ScheduledCombatEffect> scheduled = state.SkillRuntime.ScheduledEffects;
            scheduled.Sort((left, right) =>
            {
                int tickOrder = left.ExecuteTick.CompareTo(right.ExecuteTick);
                return tickOrder != 0 ? tickOrder : left.SequenceId.CompareTo(right.SequenceId);
            });
            while (scheduled.Count > 0 && scheduled[0].ExecuteTick <= state.SimulationTick)
            {
                ScheduledCombatEffect effect = scheduled[0];
                scheduled.RemoveAt(0);
                if (effect.Kind == ScheduledCombatEffectKind.ExpireModifier)
                {
                    TransientCombatModifier modifier = state.SkillRuntime.ActiveModifiers.Find(
                        item => item != null && item.InstanceId == effect.ReferenceId);
                    if (modifier == null) continue;
                    state.SkillRuntime.ActiveModifiers.Remove(modifier);
                    events.Add(new CombatEvent
                    {
                        Kind = CombatEventKind.TransientModifierExpired,
                        ActorId = modifier.SourceActorId,
                        TargetId = modifier.TargetActorId,
                        SkillId = modifier.DefinitionId,
                    });
                    continue;
                }
                if (effect.Kind == ScheduledCombatEffectKind.ResolveSkillImpact)
                {
                    ResolveScheduledSkillImpact(input, state, stats, passive, random, events, effect);
                    continue;
                }
                if (effect.Kind == ScheduledCombatEffectKind.TickStatus)
                    ResolveStatusTick(input, state, stats, events, effect);
            }
        }

        private static void ResolveStatusTick(
            ActiveSimInput input,
            ActiveCombatState state,
            CoreStats stats,
            List<CombatEvent> events,
            ScheduledCombatEffect effect)
        {
            ActiveCombatStatus status = state.SkillRuntime.ActiveStatuses.Find(
                item => item != null && item.InstanceId == effect.ReferenceId);
            if (status == null) return;
            if (!IsScheduledTargetValid(input, state, status.TargetActorId))
            {
                ExpireStatus(state, events, status, "invalid_target");
                return;
            }

            int rawDamage = CombatMath.StatusTickDamage(status.Magnitude, CombatMath.BaseDamage(stats));
            int mitigated = CombatMath.Mitigate(rawDamage, input.Monster.Defense);
            int damage = Math.Max(1, (int)Math.Floor(mitigated * Elements.ElementMultiplier(
                status.Element, input.Monster.Element ?? Element.Physical)));
            int hp = Math.Max(0, GetActorHp(state, status.TargetActorId, input.Monster.Hp) - damage);
            state.EnemyHpByActorId[status.TargetActorId] = hp;
            if (status.TargetActorId == input.TargetEntityId) state.EnemyHp = hp;

            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.StatusTicked,
                ActorId = status.SourceActorId,
                TargetId = status.TargetActorId,
                SkillId = status.DefinitionId,
                Amount = damage,
                ActionSequenceId = status.ApplicationSequenceId,
            });
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.DamageApplied,
                ActorId = status.SourceActorId,
                TargetId = status.TargetActorId,
                SkillId = status.DefinitionId,
                Amount = damage,
                ActionSequenceId = status.ApplicationSequenceId,
            });
            if (hp == 0)
            {
                ResolveEnemyDeath(input, state, events, status.TargetActorId, status.ApplicationSequenceId);
                return;
            }

            status.NextTick = checked(status.NextTick + status.IntervalTicks);
            if (status.NextTick <= status.EndTick)
            {
                long sequenceId = checked(++state.SkillRuntime.LastScheduledEffectSequenceId);
                state.SkillRuntime.ScheduledEffects.Add(new ScheduledCombatEffect
                {
                    SequenceId = sequenceId,
                    ExecuteTick = status.NextTick,
                    Kind = ScheduledCombatEffectKind.TickStatus,
                    ReferenceId = status.InstanceId,
                    SourceActorId = status.SourceActorId,
                    TargetActorId = status.TargetActorId,
                    SkillId = status.DefinitionId,
                    ActionSequenceId = status.ApplicationSequenceId,
                });
            }
            else
            {
                ExpireStatus(state, events, status, "duration_complete");
            }
        }

        private static void ExpireStatus(
            ActiveCombatState state,
            List<CombatEvent> events,
            ActiveCombatStatus status,
            string reason)
        {
            state.SkillRuntime.ActiveStatuses.Remove(status);
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.StatusExpired,
                ActorId = status.SourceActorId,
                TargetId = status.TargetActorId,
                SkillId = status.DefinitionId,
                Reason = reason,
                ActionSequenceId = status.ApplicationSequenceId,
            });
        }

        private static void ResolveScheduledSkillImpact(
            ActiveSimInput input,
            ActiveCombatState state,
            CoreStats stats,
            PassiveMultipliers passive,
            IRandomSource random,
            List<CombatEvent> events,
            ScheduledCombatEffect effect)
        {
            ClassSkillDef skill = FindSkill(input.Class, effect.SkillId);
            bool tilePattern = IsTilePatternSkill(skill);
            bool sourceAnchoredTile = skill?.Targeting == SkillTargetingKind.TilePatternAroundSource;
            if (skill == null || state.PlayerDefeated || effect.SourceActorId != input.Character.Id ||
                (!sourceAnchoredTile && !IsScheduledTargetValid(input, state, effect.TargetActorId)) ||
                (tilePattern && !effect.HasCapturedAnchor))
            {
                events.Add(new CombatEvent
                {
                    Kind = CombatEventKind.CombatEffectCancelled,
                    ActorId = effect.SourceActorId,
                    TargetId = effect.TargetActorId,
                    SkillId = effect.SkillId,
                    Reason = tilePattern && !effect.HasCapturedAnchor ? "invalid_anchor" : "invalid_target",
                    CommandSequenceId = effect.CommandSequenceId,
                    ActionSequenceId = effect.ActionSequenceId,
                });
                return;
            }

            if (tilePattern)
            {
                ResolveScheduledTilePatternImpact(input, state, stats, passive, random, events, effect, skill);
                return;
            }

            CombatActionDefinition action = CombatActionResolver.FromLegacySkill(skill);
            action.DamageMultiplier *= TransientModifierResolver.Compose(
                1.0,
                CombatStatProperty.Damage,
                input.Character.Id,
                state.SimulationTick,
                state.SkillRuntime.ActiveModifiers,
                0.0,
                100.0);
            CombatActionResolution resolution = CombatActionResolver.ResolvePlayerDamage(
                action, stats, input.Monster, input.Bonuses, passive, random);
            int hp = GetActorHp(state, effect.TargetActorId, input.Monster.Hp);
            if (resolution.Damage > 0)
            {
                hp = Math.Max(0, hp - resolution.Damage);
                state.EnemyHpByActorId[effect.TargetActorId] = hp;
            }
            if (effect.TargetActorId == input.TargetEntityId)
                state.EnemyHp = hp;

            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.SkillExecuted,
                ActorId = effect.SourceActorId,
                TargetId = effect.TargetActorId,
                SkillId = effect.SkillId,
                Amount = resolution.Damage,
                Hit = resolution.Hit,
                Critical = resolution.Critical,
                CommandSequenceId = effect.CommandSequenceId,
                ActionSequenceId = effect.ActionSequenceId,
            });
            if (resolution.Damage > 0)
                events.Add(new CombatEvent
                {
                    Kind = CombatEventKind.DamageApplied,
                    ActorId = effect.SourceActorId,
                    TargetId = effect.TargetActorId,
                    SkillId = effect.SkillId,
                    Amount = resolution.Damage,
                    Critical = resolution.Critical,
                    CommandSequenceId = effect.CommandSequenceId,
                    ActionSequenceId = effect.ActionSequenceId,
                });
            if (hp == 0)
                ResolveEnemyDeath(input, state, events, effect.TargetActorId, effect.ActionSequenceId);
        }

        private static void ResolveScheduledTilePatternImpact(
            ActiveSimInput input,
            ActiveCombatState state,
            CoreStats stats,
            PassiveMultipliers passive,
            IRandomSource random,
            List<CombatEvent> events,
            ScheduledCombatEffect effect,
            ClassSkillDef skill)
        {
            TilePatternResolution tileResolution = ResolveTilePattern(
                input,
                state,
                skill,
                effect.CapturedAnchorTile,
                effect.CapturedAnchorFloor);
            CombatActionDefinition action = CombatActionResolver.FromLegacySkill(skill);
            action.DamageMultiplier *= TransientModifierResolver.Compose(
                1.0,
                CombatStatProperty.Damage,
                input.Character.Id,
                state.SimulationTick,
                state.SkillRuntime.ActiveModifiers,
                0.0,
                100.0);

            var resolvedTargets = new List<ResolvedTargetDamage>();
            int totalDamage = 0;
            foreach (string targetId in tileResolution.ActorIds)
            {
                if (state.DefeatedActorIds.Contains(targetId)) continue;
                CombatActionResolution resolution = CombatActionResolver.ResolvePlayerDamage(
                    action, stats, input.Monster, input.Bonuses, passive, random);
                int hp = GetActorHp(state, targetId, input.Monster.Hp);
                if (resolution.Damage > 0)
                {
                    hp = Math.Max(0, hp - resolution.Damage);
                    state.EnemyHpByActorId[targetId] = hp;
                    totalDamage += resolution.Damage;
                }
                resolvedTargets.Add(new ResolvedTargetDamage
                {
                    ActorId = targetId,
                    Resolution = resolution,
                    RemainingHp = hp,
                });
            }

            if (state.EnemyHpByActorId.TryGetValue(input.TargetEntityId, out int currentTargetHp))
                state.EnemyHp = currentTargetHp;

            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.SkillExecuted,
                ActorId = effect.SourceActorId,
                TargetId = effect.TargetActorId,
                SkillId = effect.SkillId,
                Amount = totalDamage,
                Hit = resolvedTargets.Exists(item => item.Resolution.Hit),
                Critical = resolvedTargets.Exists(item => item.Resolution.Critical),
                CommandSequenceId = effect.CommandSequenceId,
                ActionSequenceId = effect.ActionSequenceId,
            });
            if (tileResolution.Tiles.Count > 0)
                events.Add(CreateTileAreaResolvedEvent(
                    input,
                    state,
                    skill,
                    tileResolution,
                    effect.CommandSequenceId,
                    effect.ActionSequenceId));

            foreach (ResolvedTargetDamage resolved in resolvedTargets)
            {
                if (resolved.Resolution.Damage > 0)
                    events.Add(new CombatEvent
                    {
                        Kind = CombatEventKind.DamageApplied,
                        ActorId = effect.SourceActorId,
                        TargetId = resolved.ActorId,
                        SkillId = effect.SkillId,
                        Amount = resolved.Resolution.Damage,
                        Critical = resolved.Resolution.Critical,
                        CommandSequenceId = effect.CommandSequenceId,
                        ActionSequenceId = effect.ActionSequenceId,
                    });
                if (resolved.Resolution.Hit && resolved.RemainingHp > 0)
                    ApplyStatusesForSkill(input, state, skill, resolved.ActorId, effect.ActionSequenceId, events);
                if (resolved.RemainingHp == 0)
                    ResolveEnemyDeath(input, state, events, resolved.ActorId, effect.ActionSequenceId);
            }
        }

        private static bool IsScheduledTargetValid(
            ActiveSimInput input,
            ActiveCombatState state,
            string targetActorId)
        {
            if (string.IsNullOrEmpty(targetActorId) || state.DefeatedActorIds.Contains(targetActorId)) return false;
            CombatSpatialFrame frame = input.World?.Spatial;
            if (frame != null)
            {
                CombatSpatialSnapshot actor = FindSpatialActor(frame, targetActorId);
                return actor != null && actor.Alive && actor.Faction == CombatFaction.Hostile;
            }
            return targetActorId == input.TargetEntityId && input.World?.TargetAvailable == true;
        }

        private static void ResolvePlayerAttack(
            ActiveSimInput input,
            ActiveCombatState state,
            CoreStats stats,
            PassiveMultipliers passive,
            ClassSkillDef skill,
            IRandomSource random,
            List<CombatEvent> events,
            long commandSequenceId)
        {
            CombatActionDefinition action = skill == null
                ? CombatActionResolver.BasicAttack()
                : CombatActionResolver.FromLegacySkill(skill);
            action.DamageMultiplier *= TransientModifierResolver.Compose(
                1.0,
                CombatStatProperty.Damage,
                input.Character.Id,
                state.SimulationTick,
                state.SkillRuntime.ActiveModifiers,
                0.0,
                100.0);
            long actionSequenceId = checked(++state.SkillRuntime.LastActionSequenceId);
            TilePatternResolution tileResolution;
            List<string> targetIds = ResolveActionTargetIds(input, state, skill, out tileResolution);
            var resolvedTargets = new List<ResolvedTargetDamage>();
            int totalDamage = 0;

            foreach (string targetId in targetIds)
            {
                if (state.DefeatedActorIds.Contains(targetId)) continue;
                CombatActionResolution resolution = CombatActionResolver.ResolvePlayerDamage(
                    action, stats, input.Monster, input.Bonuses, passive, random);
                int hp = GetActorHp(state, targetId, input.Monster.Hp);
                if (resolution.Damage > 0)
                {
                    hp = Math.Max(0, hp - resolution.Damage);
                    state.EnemyHpByActorId[targetId] = hp;
                    totalDamage += resolution.Damage;
                }
                resolvedTargets.Add(new ResolvedTargetDamage
                {
                    ActorId = targetId,
                    Resolution = resolution,
                    RemainingHp = hp,
                });
            }

            if (state.EnemyHpByActorId.TryGetValue(input.TargetEntityId, out int currentTargetHp))
                state.EnemyHp = currentTargetHp;

            events.Add(new CombatEvent
            {
                Kind = skill == null ? CombatEventKind.AttackResolved : CombatEventKind.SkillExecuted,
                ActorId = input.Character.Id,
                TargetId = input.TargetEntityId,
                SkillId = skill?.Id,
                Amount = totalDamage,
                Hit = resolvedTargets.Exists(item => item.Resolution.Hit),
                Critical = resolvedTargets.Exists(item => item.Resolution.Critical),
                CommandSequenceId = commandSequenceId,
                ActionSequenceId = actionSequenceId,
            });
            if (tileResolution != null && tileResolution.Tiles.Count > 0)
                events.Add(CreateTileAreaResolvedEvent(
                    input,
                    state,
                    skill,
                    tileResolution,
                    commandSequenceId,
                    actionSequenceId));
            else if (skill?.Targeting == SkillTargetingKind.CircleAroundSource ||
                skill?.Targeting == SkillTargetingKind.CircleAroundTarget)
            {
                string centerActorId = skill.Targeting == SkillTargetingKind.CircleAroundSource
                    ? input.World?.Spatial?.SourceActorId
                    : state.TargetId;
                CombatSpatialSnapshot centerActor = FindSpatialActor(input.World?.Spatial, centerActorId);
                if (centerActor != null)
                    events.Add(new CombatEvent
                    {
                        Kind = CombatEventKind.AreaResolved,
                        ActorId = input.Character.Id,
                        SkillId = skill.Id,
                        Amount = resolvedTargets.Count,
                        PositionX = centerActor.GroundPosition.X,
                        PositionY = centerActor.GroundPosition.Y,
                        Radius = Math.Max(0.0, skill.RadiusWorldUnits),
                        CommandSequenceId = commandSequenceId,
                        ActionSequenceId = actionSequenceId,
                    });
            }
            state.PlayerAttacksResolved++;

            foreach (ResolvedTargetDamage resolved in resolvedTargets)
            {
                if (resolved.Resolution.Damage > 0)
                    events.Add(new CombatEvent
                    {
                        Kind = CombatEventKind.DamageApplied,
                        ActorId = input.Character.Id,
                        TargetId = resolved.ActorId,
                        SkillId = skill?.Id,
                        Amount = resolved.Resolution.Damage,
                        Critical = resolved.Resolution.Critical,
                        CommandSequenceId = commandSequenceId,
                        ActionSequenceId = actionSequenceId,
                    });
                if (skill != null && resolved.Resolution.Hit && resolved.RemainingHp > 0)
                    ApplyStatusesForSkill(input, state, skill, resolved.ActorId, actionSequenceId, events);
                if (resolved.RemainingHp == 0)
                    ResolveEnemyDeath(input, state, events, resolved.ActorId, actionSequenceId);
            }

            state.PlayerNextAttackAt = input.Timestamp + AttackIntervalMs(stats, state, input.Character.Id);
        }

        private static void ApplyStatusesForSkill(
            ActiveSimInput input,
            ActiveCombatState state,
            ClassSkillDef skill,
            string targetActorId,
            long actionSequenceId,
            List<CombatEvent> events)
        {
            foreach (StatusInflict inflict in skill.Inflicts ?? new List<StatusInflict>())
            {
                if (inflict == null || inflict.DurationMs <= 0 || inflict.TickIntervalMs <= 0) continue;
                long durationTicks = CombatTimeContract.DurationToTicks(inflict.DurationMs);
                long intervalTicks = CombatTimeContract.DurationToTicks(inflict.TickIntervalMs);
                ActiveCombatStatus existing = state.SkillRuntime.ActiveStatuses.Find(item =>
                    item != null && item.SourceActorId == input.Character.Id &&
                    item.TargetActorId == targetActorId && item.Kind == inflict.Kind);
                if (existing != null)
                {
                    if (existing.DefinitionId != skill.Id)
                        events.Add(new CombatEvent
                        {
                            Kind = CombatEventKind.StatusExpired,
                            ActorId = existing.SourceActorId,
                            TargetId = existing.TargetActorId,
                            SkillId = existing.DefinitionId,
                            Reason = "refreshed_by_other_skill",
                            ActionSequenceId = existing.ApplicationSequenceId,
                        });
                    existing.DefinitionId = skill.Id;
                    existing.Element = skill.Element;
                    existing.Magnitude = inflict.Magnitude;
                    existing.EndTick = checked(state.SimulationTick + durationTicks);
                    events.Add(new CombatEvent
                    {
                        Kind = CombatEventKind.StatusApplied,
                        ActorId = input.Character.Id,
                        TargetId = targetActorId,
                        SkillId = skill.Id,
                        Reason = "refreshed",
                        ActionSequenceId = actionSequenceId,
                    });
                    continue;
                }

                long scheduledSequenceId = checked(++state.SkillRuntime.LastScheduledEffectSequenceId);
                var status = new ActiveCombatStatus
                {
                    InstanceId = $"status:{actionSequenceId}:{targetActorId}:{inflict.Kind}",
                    DefinitionId = skill.Id,
                    SourceActorId = input.Character.Id,
                    TargetActorId = targetActorId,
                    Kind = inflict.Kind,
                    Element = skill.Element,
                    Magnitude = inflict.Magnitude,
                    StartTick = state.SimulationTick,
                    EndTick = checked(state.SimulationTick + durationTicks),
                    NextTick = checked(state.SimulationTick + intervalTicks),
                    IntervalTicks = intervalTicks,
                    ApplicationSequenceId = actionSequenceId,
                };
                state.SkillRuntime.ActiveStatuses.Add(status);
                state.SkillRuntime.ScheduledEffects.Add(new ScheduledCombatEffect
                {
                    SequenceId = scheduledSequenceId,
                    ExecuteTick = status.NextTick,
                    Kind = ScheduledCombatEffectKind.TickStatus,
                    ReferenceId = status.InstanceId,
                    SourceActorId = status.SourceActorId,
                    TargetActorId = status.TargetActorId,
                    SkillId = status.DefinitionId,
                    ActionSequenceId = actionSequenceId,
                });
                events.Add(new CombatEvent
                {
                    Kind = CombatEventKind.StatusApplied,
                    ActorId = input.Character.Id,
                    TargetId = targetActorId,
                    SkillId = skill.Id,
                    Reason = "applied",
                    ActionSequenceId = actionSequenceId,
                });
            }
        }

        private sealed class ResolvedTargetDamage
        {
            public string ActorId;
            public CombatActionResolution Resolution;
            public int RemainingHp;
        }

        private sealed class TilePatternResolution
        {
            public int Floor;
            public List<CombatTileCoordinate> Tiles = new List<CombatTileCoordinate>();
            public List<string> ActorIds = new List<string>();
        }

        private static List<string> ResolveActionTargetIds(
            ActiveSimInput input,
            ActiveCombatState state,
            ClassSkillDef skill,
            out TilePatternResolution tileResolution)
        {
            tileResolution = null;
            if (IsTilePatternSkill(skill))
            {
                tileResolution = ResolveTilePattern(input, state, skill);
                return tileResolution.ActorIds;
            }

            bool sourceCircle = skill?.Targeting == SkillTargetingKind.CircleAroundSource;
            bool targetCircle = skill?.Targeting == SkillTargetingKind.CircleAroundTarget;
            if (!sourceCircle && !targetCircle)
                return new List<string> { input.TargetEntityId };

            CombatSpatialFrame frame = input.World?.Spatial;
            CombatSpatialSnapshot centerActor = FindSpatialActor(
                frame, sourceCircle ? frame?.SourceActorId : state.TargetId);
            if (centerActor == null) return new List<string>();
            List<CombatAreaHit> hits = CircleShapeResolver.Resolve(
                centerActor.GroundPosition,
                Math.Max(0.0, skill.RadiusWorldUnits),
                centerActor.Floor,
                CombatFaction.Hostile,
                frame.Actors);
            var targets = new List<string>();
            foreach (CombatAreaHit hit in hits)
                if (!state.DefeatedActorIds.Contains(hit.ActorId)) targets.Add(hit.ActorId);
            return targets;
        }

        private static TilePatternResolution ResolveTilePattern(
            ActiveSimInput input,
            ActiveCombatState state,
            ClassSkillDef skill)
        {
            CombatSpatialSnapshot anchor = FindTileAnchor(input, state, skill);
            if (anchor == null)
                return new TilePatternResolution();
            return ResolveTilePattern(input, state, skill, anchor.Tile, anchor.Floor);
        }

        private static TilePatternResolution ResolveTilePattern(
            ActiveSimInput input,
            ActiveCombatState state,
            ClassSkillDef skill,
            CombatTileCoordinate anchor,
            int floor)
        {
            var resolution = new TilePatternResolution { Floor = floor };
            if (skill?.TilePattern == null) return resolution;

            resolution.Tiles = TilePatternResolver.ResolveTiles(anchor, skill.TilePattern);
            foreach (CombatSpatialSnapshot actor in TilePatternResolver.ResolveActors(
                         resolution.Tiles,
                         floor,
                         CombatFaction.Hostile,
                         input.World?.Spatial?.Actors,
                         skill.TilePattern.MaxTargets))
            {
                if (actor != null && !state.DefeatedActorIds.Contains(actor.ActorId))
                    resolution.ActorIds.Add(actor.ActorId);
            }
            return resolution;
        }

        private static CombatSpatialSnapshot FindTileAnchor(
            ActiveSimInput input,
            ActiveCombatState state,
            ClassSkillDef skill)
        {
            CombatSpatialFrame frame = input.World?.Spatial;
            string actorId = skill?.Targeting == SkillTargetingKind.TilePatternAroundSource
                ? frame?.SourceActorId ?? input.Character.Id
                : state.TargetId;
            return FindSpatialActor(frame, actorId);
        }

        private static CombatEvent CreateTileAreaResolvedEvent(
            ActiveSimInput input,
            ActiveCombatState state,
            ClassSkillDef skill,
            TilePatternResolution resolution,
            long commandSequenceId,
            long actionSequenceId)
            => new CombatEvent
            {
                Kind = CombatEventKind.TileAreaResolved,
                ActorId = input.Character.Id,
                TargetId = state.TargetId,
                SkillId = skill?.Id,
                Floor = resolution.Floor,
                AffectedTiles = new List<CombatTileCoordinate>(resolution.Tiles),
                ResolvedActorIds = new List<string>(resolution.ActorIds),
                CommandSequenceId = commandSequenceId,
                ActionSequenceId = actionSequenceId,
            };

        private static bool IsTilePatternSkill(ClassSkillDef skill)
            => skill?.Targeting == SkillTargetingKind.TilePatternAroundSource ||
               skill?.Targeting == SkillTargetingKind.TilePatternAroundTarget;

        private static CombatSpatialSnapshot FindSpatialActor(CombatSpatialFrame frame, string actorId)
        {
            foreach (CombatSpatialSnapshot actor in frame?.Actors ?? new List<CombatSpatialSnapshot>())
                if (actor != null && actor.ActorId == actorId) return actor;
            return null;
        }

        private static int GetActorHp(ActiveCombatState state, string actorId, int defaultHp)
        {
            if (state.EnemyHpByActorId.TryGetValue(actorId, out int hp)) return hp;
            state.EnemyHpByActorId[actorId] = defaultHp;
            return defaultHp;
        }

        private static void SynchronizeActorHealth(ActiveSimInput input, ActiveCombatState state)
        {
            if (state.EnemyHpByActorId == null)
                state.EnemyHpByActorId = new Dictionary<string, int>();
            if (state.DefeatedActorIds == null)
                state.DefeatedActorIds = new List<string>();

            CombatSpatialFrame frame = input.World?.Spatial;
            if (frame == null)
            {
                if (!state.EnemyHpByActorId.ContainsKey(input.TargetEntityId))
                    state.EnemyHpByActorId[input.TargetEntityId] = state.EnemyHp;
                return;
            }

            foreach (CombatSpatialSnapshot actor in frame.Actors ?? new List<CombatSpatialSnapshot>())
            {
                if (actor == null || actor.Faction != CombatFaction.Hostile || !actor.Alive) continue;
                if (!state.EnemyHpByActorId.ContainsKey(actor.ActorId))
                {
                    state.DefeatedActorIds.Remove(actor.ActorId);
                    state.EnemyHpByActorId[actor.ActorId] = input.Monster.Hp;
                }
            }
            if (state.EnemyHpByActorId.TryGetValue(input.TargetEntityId, out int targetHp))
                state.EnemyHp = targetHp;
        }

        private static void ResolveEnemyAttacks(
            ActiveSimInput input,
            ActiveCombatState state,
            CoreStats stats,
            IRandomSource random,
            List<CombatEvent> events)
        {
            List<HostileAttackerFacts> attackers = input.World?.HostileAttackers;
            if (attackers == null || attackers.Count == 0)
            {
                if (CanFight(input.World) && input.Timestamp >= state.EnemyNextAttackAt)
                    ResolveEnemyAttack(input, state, stats, input.Monster, input.TargetEntityId, random, events);
                return;
            }

            state.EnemyNextAttackAtByActorId ??= new Dictionary<string, long>();
            foreach (HostileAttackerFacts attacker in attackers)
            {
                if (attacker == null || !attacker.CanAttack || string.IsNullOrWhiteSpace(attacker.ActorId) ||
                    string.IsNullOrWhiteSpace(attacker.MonsterId) ||
                    input.MonsterDefs == null || !input.MonsterDefs.TryGetValue(attacker.MonsterId, out MonsterDef monster))
                    continue;
                long nextAttackAt = state.EnemyNextAttackAtByActorId.TryGetValue(attacker.ActorId, out long value) ? value : 0L;
                if (input.Timestamp < nextAttackAt) continue;
                ResolveEnemyAttack(input, state, stats, monster, attacker.ActorId, random, events);
                state.EnemyNextAttackAtByActorId[attacker.ActorId] = input.Timestamp + 1000L;
            }
        }

        private static void ResolveEnemyAttack(
            ActiveSimInput input,
            ActiveCombatState state,
            CoreStats stats,
            MonsterDef monster,
            string attackerId,
            IRandomSource random,
            List<CombatEvent> events)
        {
            bool hit = random.NextDouble() < CombatMath.MonsterHitChance(monster, stats);
            int persistentDefense = input.Class.Passive?.Multipliers?.DefenseFlat ?? 0;
            int defense = (int)Math.Floor(TransientModifierResolver.Compose(
                persistentDefense,
                CombatStatProperty.Defense,
                input.Character.Id,
                state.SimulationTick,
                state.SkillRuntime.ActiveModifiers,
                0.0,
                int.MaxValue));
            int damage = hit && monster.Damage > 0
                ? CombatMath.Mitigate(monster.Damage, defense)
                : 0;
            if (damage > 0) state.PlayerHp = Math.Max(0, state.PlayerHp - damage);
            long actionSequenceId = checked(++state.SkillRuntime.LastActionSequenceId);

            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.AttackResolved,
                ActorId = attackerId,
                TargetId = input.Character.Id,
                Amount = damage,
                Hit = hit,
                ActionSequenceId = actionSequenceId,
            });
            state.EnemyAttacksResolved++;
            if (damage > 0)
                events.Add(new CombatEvent
                {
                    Kind = CombatEventKind.DamageApplied,
                    ActorId = attackerId,
                    TargetId = input.Character.Id,
                    Amount = damage,
                    ActionSequenceId = actionSequenceId,
                });

            state.EnemyNextAttackAt = input.Timestamp + 1000L;
            if (state.PlayerHp == 0)
            {
                state.PlayerDefeated = true;
                events.Add(new CombatEvent
                {
                    Kind = CombatEventKind.ActorDefeated,
                    ActorId = input.Character.Id,
                    TargetId = attackerId,
                    ActionSequenceId = actionSequenceId,
                });
            }
        }

        private static void ResolveEnemyDeath(
            ActiveSimInput input,
            ActiveCombatState state,
            List<CombatEvent> events,
            string defeatedActorId,
            long actionSequenceId)
        {
            if (state.DefeatedActorIds.Contains(defeatedActorId)) return;
            state.DefeatedActorIds.Add(defeatedActorId);
            state.KillResolved = true;
            state.EnemiesKilled++;
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.ActorDefeated,
                ActorId = defeatedActorId,
                TargetId = input.Character.Id,
                ActionSequenceId = actionSequenceId,
            });
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.EnemyKilled,
                ActorId = input.Character.Id,
                TargetId = defeatedActorId,
                ActionSequenceId = actionSequenceId,
            });
            CancelPendingImpactsForTarget(input, state, events, defeatedActorId);
        }

        private static void CancelPendingImpactsForTarget(
            ActiveSimInput input,
            ActiveCombatState state,
            List<CombatEvent> events,
            string defeatedActorId)
        {
            var removedStatusIds = new HashSet<string>();
            for (int index = 0; index < state.SkillRuntime.ActiveStatuses.Count;)
            {
                ActiveCombatStatus status = state.SkillRuntime.ActiveStatuses[index];
                if (status == null || status.TargetActorId != defeatedActorId)
                {
                    index++;
                    continue;
                }
                state.SkillRuntime.ActiveStatuses.RemoveAt(index);
                removedStatusIds.Add(status.InstanceId);
                events.Add(new CombatEvent
                {
                    Kind = CombatEventKind.StatusExpired,
                    ActorId = status.SourceActorId,
                    TargetId = status.TargetActorId,
                    SkillId = status.DefinitionId,
                    Reason = "target_defeated",
                    ActionSequenceId = status.ApplicationSequenceId,
                });
            }

            List<ScheduledCombatEffect> pending = state.SkillRuntime.ScheduledEffects;
            pending.Sort((left, right) => left.SequenceId.CompareTo(right.SequenceId));
            for (int index = 0; index < pending.Count;)
            {
                ScheduledCombatEffect effect = pending[index];
                bool statusTickRemoved = effect.Kind == ScheduledCombatEffectKind.TickStatus &&
                    removedStatusIds.Contains(effect.ReferenceId);
                // Source-anchored tile impacts depend only on their captured anchor, not the
                // incidental target id they were scheduled with — they survive the target's death.
                bool impactCancelled = effect.Kind == ScheduledCombatEffectKind.ResolveSkillImpact &&
                    effect.TargetActorId == defeatedActorId &&
                    FindSkill(input.Class, effect.SkillId)?.Targeting !=
                        SkillTargetingKind.TilePatternAroundSource;
                if (!statusTickRemoved && !impactCancelled)
                {
                    index++;
                    continue;
                }
                pending.RemoveAt(index);
                if (!impactCancelled) continue;
                events.Add(new CombatEvent
                {
                    Kind = CombatEventKind.CombatEffectCancelled,
                    ActorId = effect.SourceActorId,
                    TargetId = effect.TargetActorId,
                    SkillId = effect.SkillId,
                    Reason = "target_defeated",
                    CommandSequenceId = effect.CommandSequenceId,
                    ActionSequenceId = effect.ActionSequenceId,
                });
            }
        }

        private static List<string> DefeatedActorsFrom(List<CombatEvent> events)
        {
            var actorIds = new List<string>();
            foreach (CombatEvent combatEvent in events)
                if (combatEvent.Kind == CombatEventKind.EnemyKilled &&
                    !string.IsNullOrEmpty(combatEvent.TargetId) &&
                    !actorIds.Contains(combatEvent.TargetId))
                    actorIds.Add(combatEvent.TargetId);
            return actorIds;
        }

        private static bool HasValidTarget(ActiveCombatState state, ActiveSimInput input)
            => input.World != null && input.World.TargetAvailable && state.TargetId == input.TargetEntityId &&
               (state.DefeatedActorIds == null || !state.DefeatedActorIds.Contains(input.TargetEntityId));

        private static bool CanFight(CombatWorldFacts world)
            => world != null && world.TargetInRange && world.LineOfSight;

        private static ClassSkillDef FindSkill(ClassDef classDef, string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || classDef.Skills == null) return null;
            foreach (ClassSkillDef skill in classDef.Skills)
                if (skill != null && skill.Id == skillId) return skill;
            return null;
        }

        private static long AttackIntervalMs(CoreStats stats, ActiveCombatState state, string actorId)
        {
            double attacksPerSecond = TransientModifierResolver.Compose(
                CombatMath.AttacksPerSecond(stats),
                CombatStatProperty.AttackSpeed,
                actorId,
                state.SimulationTick,
                state.SkillRuntime.ActiveModifiers,
                0.1,
                20.0);
            return Math.Max(1L, (long)Math.Ceiling(1000.0 / attacksPerSecond));
        }

        private static bool IsValidDefinition(MonsterDef monster)
            => monster.Hp > 0 && monster.Damage >= 0 && monster.Defense >= 0 && monster.Accuracy > 0 && monster.Agility >= 0;

        private static void RequestMovement(
            List<CombatEvent> events,
            string targetId,
            string reason,
            long commandSequenceId = 0L)
        {
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.MovementRequested,
                TargetId = targetId,
                Reason = reason,
                CommandSequenceId = commandSequenceId,
            });
        }

        private static void Reject(
            List<CombatEvent> events,
            string reason,
            string targetId = null,
            long commandSequenceId = 0L)
        {
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.CommandRejected,
                TargetId = targetId,
                Reason = reason,
                CommandSequenceId = commandSequenceId,
            });
        }

        private static void StampEvents(List<CombatEvent> events, long simulationTick)
        {
            foreach (CombatEvent combatEvent in events)
                combatEvent.SimulationTick = simulationTick;
        }
    }
}
