using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class TransientModifierTests
    {
        [Test]
        public void Compose_UsesFlatThenAdditiveThenMultiplicativeThenOverrideAndClamp()
        {
            var modifiers = new List<TransientCombatModifier>
            {
                Modifier("multiply", CombatModifierOperation.MultiplicativePercent, 0.5, 3),
                Modifier("flat", CombatModifierOperation.FlatAdd, 10.0, 1),
                Modifier("additive", CombatModifierOperation.AdditivePercent, 0.5, 2),
            };

            double result = TransientModifierResolver.Compose(
                10.0, CombatStatProperty.Defense, "player", 2, modifiers, 0.0, 100.0);

            Assert.That(result, Is.EqualTo(45.0));
        }

        [Test]
        public void Tick_GuardAppliesSchedulesAndExpiresAtTheAuthoritativeTick()
        {
            ActiveSimInput input = CreateInput(0, new ActiveCombatState());
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "guard" },
            };
            ActiveSimResult applied = ActiveSim.Tick(input, new SequenceRandomSource(new[] { 0.99 }));
            ActiveSimResult waiting = ActiveSim.Tick(
                CreateInput(50, applied.State), new SequenceRandomSource(new[] { 0.99 }));
            ActiveSimResult expired = ActiveSim.Tick(
                CreateInput(100, waiting.State), new SequenceRandomSource(new[] { 0.99 }));

            Assert.That(applied.State.SkillRuntime.ActiveModifiers, Has.Count.EqualTo(1));
            Assert.That(applied.State.SkillRuntime.ScheduledEffects, Has.Count.EqualTo(1));
            Assert.That(waiting.State.SkillRuntime.ActiveModifiers, Has.Count.EqualTo(1));
            Assert.That(expired.State.SkillRuntime.ActiveModifiers, Is.Empty);
            Assert.That(expired.Events.Exists(item =>
                item.Kind == CombatEventKind.TransientModifierExpired && item.SkillId == "guard"), Is.True);
            Assert.That(applied.State.SkillNextReadyAt["guard"], Is.EqualTo(12000));
        }

        private static TransientCombatModifier Modifier(
            string id,
            CombatModifierOperation operation,
            double magnitude,
            long sequence) => new TransientCombatModifier
        {
            InstanceId = id,
            DefinitionId = id,
            TargetActorId = "player",
            Property = CombatStatProperty.Defense,
            Operation = operation,
            Magnitude = magnitude,
            StartTick = 1,
            EndTick = 10,
            ApplicationSequenceId = sequence,
        };

        private static ActiveSimInput CreateInput(long timestamp, ActiveCombatState state) => new ActiveSimInput
        {
            Timestamp = timestamp,
            Character = new Character
            {
                Id = "player",
                ClassId = ClassId.Beginner,
                Level = 1,
                Equipment = new Dictionary<EquipSlot, string>(),
            },
            Class = new ClassDef
            {
                Id = ClassId.Beginner,
                BaseStats = new CoreStats { Strength = 2 },
                StatGrowth = new StatGrowthDef(),
                Passive = new PassiveSkillDef { Multipliers = new PassiveMultipliers() },
                Skills = new List<ClassSkillDef>
                {
                    new ClassSkillDef
                    {
                        Id = "guard",
                        Targeting = SkillTargetingKind.Self,
                        ModifierProperty = CombatStatProperty.Defense,
                        ModifierOperation = CombatModifierOperation.FlatAdd,
                        ModifierMagnitude = 8.0,
                        ModifierDurationTicks = 2,
                        CooldownMs = 12000,
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
            World = new CombatWorldFacts { TargetAvailable = true, TargetInRange = true, LineOfSight = true },
        };
    }
}
