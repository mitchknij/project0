using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class PowerStrikeTests
    {
        [Test]
        public void Tick_PowerStrikeUsesSharedDamageStartsFourSecondCooldownAndBasicResumes()
        {
            ActiveSimInput firstInput = CreateInput(0, new ActiveCombatState());
            firstInput.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "slime-01" },
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "power_strike" },
            };
            ActiveSimResult first = ActiveSim.Tick(
                firstInput, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            ActiveSimResult second = ActiveSim.Tick(
                CreateInput(1000, first.State), new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(first.Events.Exists(item =>
                item.Kind == CombatEventKind.SkillExecuted && item.SkillId == "power_strike"), Is.True);
            Assert.That(first.State.SkillNextReadyAt["power_strike"], Is.EqualTo(4000));
            Assert.That(second.Events.Exists(item =>
                item.Kind == CombatEventKind.AttackResolved && item.ActorId == "player"), Is.True);
        }

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
                BaseStats = new CoreStats { Strength = 10 },
                StatGrowth = new StatGrowthDef(),
                Passive = new PassiveSkillDef { Multipliers = new PassiveMultipliers() },
                Skills = new List<ClassSkillDef>
                {
                    new ClassSkillDef
                    {
                        Id = "power_strike",
                        Element = Element.Physical,
                        DamageMultiplier = 1.8,
                        CooldownMs = 4000,
                    },
                },
            },
            Monster = new MonsterDef
            {
                Id = "slime",
                Hp = 100,
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
