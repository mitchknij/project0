using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class CombatTimeContractTests
    {
        [Test]
        public void QuantizeDuration_RoundsUpToTheExistingFiftyMillisecondBoundary()
        {
            Assert.That(CombatTimeContract.DurationToTicks(953), Is.EqualTo(20));
            Assert.That(CombatTimeContract.QuantizeDurationMilliseconds(953), Is.EqualTo(1000));
            Assert.That(CombatTimeContract.QuantizeDurationMilliseconds(4000), Is.EqualTo(4000));
        }

        [Test]
        public void Tick_AssignsMonotonicSimulationAndCommandSequencesInAuthoredOrder()
        {
            ActiveSimInput input = CreateInput(0, new ActiveCombatState());
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "slime-01" },
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "heavy_strike" },
            };

            ActiveSimResult result = ActiveSim.Tick(input, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            CombatEvent selected = result.Events.Find(item => item.Kind == CombatEventKind.TargetSelected);
            CombatEvent skill = result.Events.Find(item => item.Kind == CombatEventKind.SkillExecuted);
            Assert.That(result.State.SimulationTick, Is.EqualTo(1));
            Assert.That(result.State.LastCommandSequenceId, Is.EqualTo(2));
            Assert.That(selected.CommandSequenceId, Is.EqualTo(1));
            Assert.That(skill.CommandSequenceId, Is.EqualTo(2));
            Assert.That(result.Events.TrueForAll(item => item.SimulationTick == 1), Is.True);
        }

        [Test]
        public void Tick_BackwardTimestampDoesNotAdvanceSimulationTick()
        {
            ActiveSimResult first = ActiveSim.Tick(
                CreateInput(100, new ActiveCombatState()),
                new SequenceRandomSource(new[] { 0.0, 0.99 }));
            ActiveSimResult rejected = ActiveSim.Tick(
                CreateInput(99, first.State),
                new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(rejected.State.SimulationTick, Is.EqualTo(first.State.SimulationTick));
            Assert.That(rejected.Events[0].Reason, Is.EqualTo("timestamp_moved_backwards"));
            Assert.That(rejected.Events[0].SimulationTick, Is.EqualTo(first.State.SimulationTick));
        }

        [Test]
        public void Tick_LethalManualSkillPreservesDocumentedEventOrderAndActionSequence()
        {
            ActiveSimInput input = CreateInput(0, new ActiveCombatState());
            input.Monster.Hp = 1;
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "slime-01" },
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "heavy_strike" },
            };

            ActiveSimResult result = ActiveSim.Tick(input, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            CollectionAssert.AreEqual(
                new[]
                {
                    CombatEventKind.TargetSelected,
                    CombatEventKind.SkillExecuted,
                    CombatEventKind.DamageApplied,
                    CombatEventKind.ActorDefeated,
                    CombatEventKind.EnemyKilled,
                },
                result.Events.ConvertAll(item => item.Kind));
            Assert.That(result.Events.FindAll(item => item.ActionSequenceId == 1), Has.Count.EqualTo(4));
        }

        private static ActiveSimInput CreateInput(long timestamp, ActiveCombatState state)
        {
            return new ActiveSimInput
            {
                Timestamp = timestamp,
                Character = new Character
                {
                    Id = "player",
                    ClassId = ClassId.Warrior,
                    Level = 1,
                    Equipment = new Dictionary<EquipSlot, string>(),
                },
                Class = new ClassDef
                {
                    Id = ClassId.Warrior,
                    BaseStats = new CoreStats { Strength = 10 },
                    StatGrowth = new StatGrowthDef(),
                    Passive = new PassiveSkillDef { Multipliers = new PassiveMultipliers() },
                    Skills = new List<ClassSkillDef>
                    {
                        new ClassSkillDef
                        {
                            Id = "heavy_strike",
                            Element = Element.Physical,
                            DamageMultiplier = 2.0,
                            CooldownMs = 3000,
                        },
                    },
                },
                Monster = new MonsterDef
                {
                    Id = "slime",
                    Hp = 20,
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
                    TargetAvailable = true,
                    TargetInRange = true,
                    LineOfSight = true,
                },
            };
        }
    }
}
