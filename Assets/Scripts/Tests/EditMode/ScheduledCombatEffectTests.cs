using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class ScheduledCombatEffectTests
    {
        [Test]
        public void ArcaneBolt_DealsNoDamageUntilItsExactScheduledTick()
        {
            ActiveSimInput castInput = CreateInput(0, new ActiveCombatState(), targetAvailable: true);
            castInput.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "arcane_bolt" },
            };

            ActiveSimResult cast = ActiveSim.Tick(castInput, Random());
            ActiveSimResult beforeOne = ActiveSim.Tick(CreateInput(50, cast.State, true), Random());
            ActiveSimResult beforeTwo = ActiveSim.Tick(CreateInput(100, beforeOne.State, true), Random());
            ActiveSimResult impact = ActiveSim.Tick(CreateInput(150, beforeTwo.State, true), Random());

            Assert.That(cast.State.EnemyHp, Is.EqualTo(1000));
            Assert.That(cast.Events.Exists(e => e.Kind == CombatEventKind.CombatEffectScheduled &&
                e.SkillId == "arcane_bolt" && e.DurationTicks == 3), Is.True);
            Assert.That(cast.Events.FindIndex(e => e.Kind == CombatEventKind.SkillCastStarted),
                Is.LessThan(cast.Events.FindIndex(e => e.Kind == CombatEventKind.CombatEffectScheduled)));
            Assert.That(cast.Events.FindIndex(e => e.Kind == CombatEventKind.CombatEffectScheduled),
                Is.LessThan(cast.Events.FindIndex(e => e.Kind == CombatEventKind.CooldownStarted)));
            Assert.That(beforeOne.State.EnemyHp, Is.EqualTo(1000));
            Assert.That(beforeTwo.State.EnemyHp, Is.EqualTo(1000));
            Assert.That(impact.State.EnemyHp, Is.LessThan(1000));
            Assert.That(impact.Events.Exists(e => e.Kind == CombatEventKind.SkillExecuted &&
                e.SkillId == "arcane_bolt"), Is.True);
            Assert.That(impact.State.SkillRuntime.ScheduledEffects, Is.Empty);
        }

        [Test]
        public void ArcaneBolt_CancelsWithoutDamageWhenTargetIsInvalidAtImpact()
        {
            ActiveSimInput castInput = CreateInput(0, new ActiveCombatState(), targetAvailable: true);
            castInput.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "arcane_bolt" },
            };
            ActiveSimResult cast = ActiveSim.Tick(castInput, Random());
            ActiveSimResult beforeOne = ActiveSim.Tick(CreateInput(50, cast.State, true), Random());
            ActiveSimResult beforeTwo = ActiveSim.Tick(CreateInput(100, beforeOne.State, true), Random());

            ActiveSimResult cancelled = ActiveSim.Tick(
                CreateInput(150, beforeTwo.State, targetAvailable: false),
                Random());

            Assert.That(cancelled.State.EnemyHp, Is.EqualTo(1000));
            Assert.That(cancelled.Events.Exists(e => e.Kind == CombatEventKind.CombatEffectCancelled &&
                e.SkillId == "arcane_bolt" && e.Reason == "invalid_target"), Is.True);
            Assert.That(cancelled.Events.Exists(e => e.Kind == CombatEventKind.DamageApplied), Is.False);
        }

        [Test]
        public void DueImpacts_WithIdenticalTicksResolveByScheduledSequence()
        {
            var state = new ActiveCombatState
            {
                TargetId = "slime-01",
                EnemyHp = 1000,
                PlayerNextAttackAt = long.MaxValue,
                EnemyNextAttackAt = long.MaxValue,
                SimulationTick = 2,
            };
            state.SkillRuntime.ScheduledEffects.Add(new ScheduledCombatEffect
            {
                SequenceId = 2,
                ExecuteTick = 3,
                Kind = ScheduledCombatEffectKind.ResolveSkillImpact,
                SourceActorId = "player",
                TargetActorId = "slime-01",
                SkillId = "arcane_bolt",
                ActionSequenceId = 202,
            });
            state.SkillRuntime.ScheduledEffects.Add(new ScheduledCombatEffect
            {
                SequenceId = 1,
                ExecuteTick = 3,
                Kind = ScheduledCombatEffectKind.ResolveSkillImpact,
                SourceActorId = "player",
                TargetActorId = "slime-01",
                SkillId = "arcane_bolt",
                ActionSequenceId = 101,
            });

            ActiveSimResult result = ActiveSim.Tick(CreateInput(150, state, true), Random());
            List<CombatEvent> impacts = result.Events.FindAll(e => e.Kind == CombatEventKind.SkillExecuted);

            Assert.That(impacts, Has.Count.EqualTo(2));
            Assert.That(impacts[0].ActionSequenceId, Is.EqualTo(101));
            Assert.That(impacts[1].ActionSequenceId, Is.EqualTo(202));
        }

        [Test]
        public void EarlierImpactThatDefeatsTargetCancelsLaterPendingImpacts()
        {
            var state = new ActiveCombatState
            {
                TargetId = "slime-01",
                EnemyHp = 1,
                PlayerNextAttackAt = long.MaxValue,
                EnemyNextAttackAt = long.MaxValue,
                SimulationTick = 2,
            };
            state.SkillRuntime.ScheduledEffects.Add(new ScheduledCombatEffect
            {
                SequenceId = 1,
                ExecuteTick = 3,
                Kind = ScheduledCombatEffectKind.ResolveSkillImpact,
                SourceActorId = "player",
                TargetActorId = "slime-01",
                SkillId = "arcane_bolt",
                ActionSequenceId = 101,
            });
            state.SkillRuntime.ScheduledEffects.Add(new ScheduledCombatEffect
            {
                SequenceId = 2,
                ExecuteTick = 4,
                Kind = ScheduledCombatEffectKind.ResolveSkillImpact,
                SourceActorId = "player",
                TargetActorId = "slime-01",
                SkillId = "arcane_bolt",
                ActionSequenceId = 202,
            });
            ActiveSimInput input = CreateInput(150, state, true);
            input.Monster.Hp = 1;

            ActiveSimResult result = ActiveSim.Tick(input, Random());

            Assert.That(result.Events.FindAll(e => e.Kind == CombatEventKind.SkillExecuted), Has.Count.EqualTo(1));
            Assert.That(result.Events.Exists(e => e.Kind == CombatEventKind.CombatEffectCancelled &&
                e.ActionSequenceId == 202 && e.Reason == "target_defeated"), Is.True);
            Assert.That(result.State.SkillRuntime.ScheduledEffects, Is.Empty);
        }

        private static ActiveSimInput CreateInput(
            long timestamp,
            ActiveCombatState state,
            bool targetAvailable)
        {
            return new ActiveSimInput
            {
                Timestamp = timestamp,
                Character = new Character
                {
                    Id = "player",
                    ClassId = ClassId.Mage,
                    Level = 1,
                    Equipment = new Dictionary<EquipSlot, string>(),
                    SkillBar = new List<string> { "arcane_bolt", null, null, null },
                },
                Class = new ClassDef
                {
                    Id = ClassId.Mage,
                    BaseStats = new CoreStats { Wisdom = 10 },
                    StatGrowth = new StatGrowthDef(),
                    Passive = new PassiveSkillDef { Multipliers = new PassiveMultipliers() },
                    Skills = new List<ClassSkillDef>
                    {
                        new ClassSkillDef
                        {
                            Id = "arcane_bolt",
                            Element = Element.Arcane,
                            DamageMultiplier = 1.4,
                            CooldownMs = 4000,
                            Timing = SkillTimingKind.ScheduledImpact,
                            ImpactDelayTicks = 3,
                        },
                    },
                },
                Monster = new MonsterDef
                {
                    Id = "slime",
                    Hp = 1000,
                    Damage = 0,
                    Defense = 0,
                    Accuracy = 1,
                    Agility = 0,
                },
                TargetEntityId = "slime-01",
                ItemDefs = new Dictionary<string, ItemDef>(),
                State = state,
                World = new CombatWorldFacts
                {
                    TargetAvailable = targetAvailable,
                    TargetInRange = targetAvailable,
                    LineOfSight = targetAvailable,
                },
            };
        }

        private static SequenceRandomSource Random()
            => new SequenceRandomSource(new[] { 0.0, 0.99, 0.0, 0.99, 0.0 });
    }
}
