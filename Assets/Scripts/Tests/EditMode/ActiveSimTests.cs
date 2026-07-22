using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class ActiveSimTests
    {
        [Test]
        public void Tick_AutoSelectsAndResolvesABasicAttackDeterministically()
        {
            ActiveSimResult result = ActiveSim.Tick(
                CreateInput(0, new ActiveCombatState()),
                new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(result.State.TargetId, Is.EqualTo("slime-01"));
            Assert.That(result.State.EnemyHp, Is.LessThan(20));
            Assert.That(result.Events.Exists(e => e.Kind == CombatEventKind.TargetSelected), Is.True);
            Assert.That(result.Events.Exists(e => e.Kind == CombatEventKind.AttackResolved && e.ActorId == "player"), Is.True);
        }

        [Test]
        public void Tick_ManualSkillTakesPriorityAndStartsItsCooldown()
        {
            var input = CreateInput(0, new ActiveCombatState());
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "slime-01" },
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "heavy_strike" },
            };

            ActiveSimResult result = ActiveSim.Tick(input, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(result.Events.Exists(e => e.Kind == CombatEventKind.SkillExecuted && e.SkillId == "heavy_strike"), Is.True);
            Assert.That(result.Events.Exists(e => e.Kind == CombatEventKind.AttackResolved && e.ActorId == "player"), Is.False);
            Assert.That(result.State.SkillNextReadyAt["heavy_strike"], Is.EqualTo(3000));
        }

        [Test]
        public void Tick_ManualMovementStopsAutoCombatAndRequestsMovement()
        {
            var input = CreateInput(0, new ActiveCombatState { TargetId = "slime-01" });
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.MoveIntent, TargetId = "walk_destination" },
            };

            ActiveSimResult result = ActiveSim.Tick(input, new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(result.State.AutoCombatStopped, Is.True);
            Assert.That(result.State.EnemyHp, Is.EqualTo(20));
            Assert.That(result.Events.Exists(e => e.Kind == CombatEventKind.MovementRequested && e.Reason == "manual_movement"), Is.True);
        }

        [Test]
        public void Tick_ResolvesEnemyKillOnlyOnce()
        {
            ActiveSimInput input = CreateInput(0, new ActiveCombatState());
            input.Monster.Hp = 1;

            ActiveSimResult first = ActiveSim.Tick(input, new SequenceRandomSource(new[] { 0.0, 0.99 }));
            ActiveSimResult second = ActiveSim.Tick(
                CreateInput(1000, first.State),
                new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(first.EnemyKilled, Is.True);
            Assert.That(first.Events.FindAll(e => e.Kind == CombatEventKind.EnemyKilled), Has.Count.EqualTo(1));
            Assert.That(second.Events.FindAll(e => e.Kind == CombatEventKind.EnemyKilled), Is.Empty);
        }

        [Test]
        public void Tick_EmitsPlayerDefeatWhenEnemyReducesHpToZero()
        {
            var input = CreateInput(0, new ActiveCombatState
            {
                TargetId = "slime-01",
                PlayerHp = 1,
                EnemyHp = 20,
                PlayerNextAttackAt = long.MaxValue,
            });
            input.Monster.Damage = 10;
            input.Monster.Agility = 10;

            ActiveSimResult result = ActiveSim.Tick(input, new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(result.PlayerDefeated, Is.True);
            Assert.That(result.Events.Exists(e => e.Kind == CombatEventKind.ActorDefeated && e.ActorId == "player"), Is.True);
        }

        [Test]
        public void Tick_AutoRotationUsesFirstEligibleSkillBarSlot()
        {
            ActiveSimInput input = CreateInput(0, new ActiveCombatState());
            input.Config = new ActiveCombatConfig { AutoSkillRotation = true };
            input.Class.Skills = DamageSkills();
            input.Character.SkillBar = new List<string> { "priority_three", "priority_two", null, null };

            ActiveSimResult result = ActiveSim.Tick(input, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(result.Events.Exists(e => e.Kind == CombatEventKind.SkillExecuted && e.SkillId == "priority_three"), Is.True);
        }

        [Test]
        public void Tick_AutoRotationAdvancesThroughSlottedAttacksAndBuffsByPriority()
        {
            ActiveSimInput firstInput = CreatePriorityRotationInput(0, new ActiveCombatState());

            ActiveSimResult first = ActiveSim.Tick(firstInput, new SequenceRandomSource(new[] { 0.0, 0.99 }));
            ActiveSimResult second = ActiveSim.Tick(
                CreatePriorityRotationInput(1000, first.State),
                new SequenceRandomSource(new[] { 0.0, 0.99 }));
            ActiveSimResult third = ActiveSim.Tick(
                CreatePriorityRotationInput(2000, second.State),
                new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(first.Events.Exists(e => e.Kind == CombatEventKind.SkillExecuted && e.SkillId == "slot_one"), Is.True);
            Assert.That(second.Events.Exists(e => e.Kind == CombatEventKind.SkillExecuted && e.SkillId == "slot_two_buff"), Is.True);
            Assert.That(second.Events.Exists(e => e.Kind == CombatEventKind.TransientModifierApplied && e.SkillId == "slot_two_buff"), Is.True);
            Assert.That(third.Events.Exists(e => e.Kind == CombatEventKind.SkillExecuted && e.SkillId == "slot_three"), Is.True);
        }

        [Test]
        public void Tick_AutoRotationRepeatedSameSlotProducesDistinctSelectionSequenceIds()
        {
            ActiveSimInput firstInput = CreateInput(0, new ActiveCombatState());
            firstInput.Config = new ActiveCombatConfig { AutoSkillRotation = true };
            firstInput.Monster.Hp = 1000;
            firstInput.Character.SkillBar = new List<string> { "repeatable", null, null, null };
            firstInput.Class.Skills = new List<ClassSkillDef>
            {
                new ClassSkillDef { Id = "repeatable", DamageMultiplier = 1.0, AutoEnabled = true, CooldownMs = 0 },
            };

            ActiveSimResult first = ActiveSim.Tick(firstInput, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            ActiveSimInput secondInput = CreateInput(5000, first.State);
            secondInput.Config = new ActiveCombatConfig { AutoSkillRotation = true };
            secondInput.Monster.Hp = 1000;
            secondInput.Character.SkillBar = new List<string> { "repeatable", null, null, null };
            secondInput.Class.Skills = new List<ClassSkillDef>
            {
                new ClassSkillDef { Id = "repeatable", DamageMultiplier = 1.0, AutoEnabled = true, CooldownMs = 0 },
            };

            ActiveSimResult second = ActiveSim.Tick(secondInput, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(first.Events.Exists(e => e.Kind == CombatEventKind.SkillExecuted && e.SkillId == "repeatable"), Is.True);
            Assert.That(second.Events.Exists(e => e.Kind == CombatEventKind.SkillExecuted && e.SkillId == "repeatable"), Is.True);
            long firstSequenceId = first.State.SkillRuntime.LastAutoSelection.SelectionSequenceId;
            long secondSequenceId = second.State.SkillRuntime.LastAutoSelection.SelectionSequenceId;
            Assert.That(firstSequenceId, Is.GreaterThan(0),
                "must be stamped after ExecuteSkillAction increments the counter; stamping before it would record 0 and lag the UI pulse one cast behind");
            Assert.That(secondSequenceId, Is.Not.EqualTo(firstSequenceId));
        }

        [Test]
        public void Tick_AutoRotationFallsBackToBasicAttackWhenAllSkillsAreCoolingDown()
        {
            ActiveSimInput input = CreateInput(0, new ActiveCombatState
            {
                SkillNextReadyAt = new Dictionary<string, long> { ["priority_two"] = 1, ["priority_three"] = 1 },
            });
            input.Config = new ActiveCombatConfig { AutoSkillRotation = true };
            input.Class.Skills = DamageSkills();
            input.Character.SkillBar = new List<string> { "priority_two", "priority_three", null, null };

            ActiveSimResult result = ActiveSim.Tick(input, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(result.Events.Exists(e => e.Kind == CombatEventKind.AttackResolved && e.ActorId == "player"), Is.True);
        }

        [Test]
        public void Tick_BasicAttackFallbackPreservesTheAutoSelectionSequenceId()
        {
            ActiveSimInput firstInput = CreateInput(0, new ActiveCombatState());
            firstInput.Config = new ActiveCombatConfig { AutoSkillRotation = true };
            firstInput.Monster.Hp = 1000;
            firstInput.Character.SkillBar = new List<string> { "long_cooldown", null, null, null };
            firstInput.Class.Skills = new List<ClassSkillDef>
            {
                new ClassSkillDef { Id = "long_cooldown", DamageMultiplier = 1.0, AutoEnabled = true, CooldownMs = 100000 },
            };

            ActiveSimResult cast = ActiveSim.Tick(firstInput, new SequenceRandomSource(new[] { 0.0, 0.99 }));
            long castSequenceId = cast.State.SkillRuntime.LastAutoSelection.SelectionSequenceId;

            ActiveSimInput secondInput = CreateInput(5000, cast.State);
            secondInput.Config = new ActiveCombatConfig { AutoSkillRotation = true };
            secondInput.Monster.Hp = 1000;
            secondInput.Character.SkillBar = new List<string> { "long_cooldown", null, null, null };
            secondInput.Class.Skills = new List<ClassSkillDef>
            {
                new ClassSkillDef { Id = "long_cooldown", DamageMultiplier = 1.0, AutoEnabled = true, CooldownMs = 100000 },
            };

            ActiveSimResult fallback = ActiveSim.Tick(secondInput, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(castSequenceId, Is.GreaterThan(0));
            Assert.That(fallback.Events.Exists(e => e.Kind == CombatEventKind.AttackResolved && e.ActorId == "player"), Is.True,
                "second tick must fall back to a basic attack while the skill is still cooling down");
            Assert.That(fallback.State.SkillRuntime.LastAutoSelection.SelectionSequenceId, Is.EqualTo(castSequenceId),
                "a basic-attack tick must leave the id unchanged rather than resetting it to 0");
        }

        [Test]
        public void Tick_AutoRotationSkipsSkillsWithoutAnImplementedEffect()
        {
            ActiveSimInput input = CreateInput(0, new ActiveCombatState());
            input.Config = new ActiveCombatConfig { AutoSkillRotation = true };
            input.Class.Skills = new List<ClassSkillDef>
            {
                new ClassSkillDef { Id = "buff", DamageMultiplier = 0.0, AutoEnabled = false },
                new ClassSkillDef { Id = "damage", DamageMultiplier = 2.0, AutoEnabled = true },
            };
            input.Character.SkillBar = new List<string> { "buff", "damage", null, null };

            ActiveSimResult result = ActiveSim.Tick(input, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(result.Events.Exists(e => e.Kind == CombatEventKind.SkillExecuted && e.SkillId == "damage"), Is.True);
        }

        [Test]
        public void Tick_AutoResumeGraceElapsesFromTheMoveTimestamp()
        {
            var state = new ActiveCombatState { TargetId = "slime-01", AutoCombatStopped = true, AutoCombatStoppedAt = 0 };
            ActiveSimInput beforeGrace = CreateInput(99, state);
            beforeGrace.Config = new ActiveCombatConfig { AutoResumeGraceMs = 100 };
            ActiveSimResult first = ActiveSim.Tick(beforeGrace, new SequenceRandomSource(new[] { 0.0 }));
            ActiveSimInput atGrace = CreateInput(100, first.State);
            atGrace.Config = new ActiveCombatConfig { AutoResumeGraceMs = 100 };

            ActiveSimResult second = ActiveSim.Tick(atGrace, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(first.State.AutoCombatStopped, Is.True);
            Assert.That(second.State.AutoCombatStopped, Is.False);
            Assert.That(second.Events.Exists(e => e.Kind == CombatEventKind.AutoCombatResumed), Is.True);
        }

        [Test]
        public void Tick_OutOfCombatRegenUsesOnlyElapsedTimestamps()
        {
            ActiveSimInput input = CreateInput(1000, new ActiveCombatState { PlayerHp = 10, LastUpdatedAt = 0 });
            input.World.TargetInRange = false;
            input.Config = new ActiveCombatConfig { OutOfCombatRegenPctPerSec = 0.1 };

            ActiveSimResult result = ActiveSim.Tick(input, new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(result.State.PlayerHp, Is.GreaterThan(10));
        }

        private static List<ClassSkillDef> DamageSkills() => new List<ClassSkillDef>
        {
            new ClassSkillDef { Id = "priority_three", DamageMultiplier = 2.0, AutoEnabled = true, CooldownMs = 3000 },
            new ClassSkillDef { Id = "priority_two", DamageMultiplier = 2.0, AutoEnabled = true, CooldownMs = 3000 },
        };

        private static ActiveSimInput CreatePriorityRotationInput(long timestamp, ActiveCombatState state)
        {
            ActiveSimInput input = CreateInput(timestamp, state);
            input.Config = new ActiveCombatConfig { AutoSkillRotation = true };
            input.Monster.Hp = 1000;
            input.Character.SkillBar = new List<string>
            {
                "slot_one",
                "slot_two_buff",
                "slot_three",
                null,
            };
            input.Class.Skills = new List<ClassSkillDef>
            {
                new ClassSkillDef { Id = "slot_one", DamageMultiplier = 1.0, CooldownMs = 5000 },
                new ClassSkillDef
                {
                    Id = "slot_two_buff",
                    CooldownMs = 5000,
                    Buff = new SelfBuff { Stat = BuffStat.Damage, Magnitude = 0.25, DurationMs = 3000 },
                },
                new ClassSkillDef { Id = "slot_three", DamageMultiplier = 1.0, CooldownMs = 5000 },
            };
            return input;
        }

        private static ActiveSimInput CreateInput(long timestamp, ActiveCombatState state)
        {
            var classDef = new ClassDef
            {
                Id = ClassId.Warrior,
                BaseStats = new CoreStats { Strength = 10, Agility = 0, Wisdom = 0, Luck = 0 },
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
            };

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
                Class = classDef,
                TargetEntityId = "slime-01",
                ItemDefs = new Dictionary<string, ItemDef>(),
                Monster = new MonsterDef
                {
                    Id = "slime",
                    Hp = 20,
                    Damage = 0,
                    Defense = 0,
                    Accuracy = 1,
                    Agility = 0,
                },
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
