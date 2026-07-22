using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class EarthbreakerTests
    {
        [Test]
        public void Tick_EarthbreakerDamagesSpatialTargetsInDistanceThenIdOrder()
        {
            ActiveSimInput input = CreateInput();
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "slime-b" },
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "earthbreaker" },
            };

            ActiveSimResult result = ActiveSim.Tick(
                input, new SequenceRandomSource(new[] { 0.0, 0.99, 0.0, 0.99, 0.0, 0.99 }));
            List<CombatEvent> damage = result.Events.FindAll(item => item.Kind == CombatEventKind.DamageApplied);

            CollectionAssert.AreEqual(
                new[] { "slime-center", "slime-a", "slime-b" },
                damage.ConvertAll(item => item.TargetId));
            Assert.That(result.Events.Exists(item =>
                item.Kind == CombatEventKind.AreaResolved && item.Amount == 3), Is.True);
            Assert.That(result.State.SkillNextReadyAt["earthbreaker"], Is.EqualTo(10000));
        }

        [Test]
        public void AutoSelector_SkipsEarthbreakerBelowMinimumTargetsAndUsesNextSlot()
        {
            ActiveSimInput input = CreateInput();
            input.Character.SkillBar = new List<string> { "earthbreaker", "power_strike", null, null };
            input.Class.Skills.Add(new ClassSkillDef
            {
                Id = "power_strike",
                DamageMultiplier = 1.8,
                CooldownMs = 4000,
                AutoEnabled = true,
            });
            input.Config = new ActiveCombatConfig { AutoSkillRotation = true };
            input.World.Spatial.Actors.RemoveAll(actor => actor.ActorId != "player" && actor.ActorId != "slime-b");

            ActiveSimResult result = ActiveSim.Tick(
                input, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(result.Events.Exists(item =>
                item.Kind == CombatEventKind.SkillExecuted && item.SkillId == "power_strike"), Is.True);
            Assert.That(result.State.SkillRuntime.LastAutoSelectionDiagnostics,
                Is.EqualTo("slot=2;skill=power_strike"));
            Assert.That(result.State.SkillRuntime.LastAutoSelection.Slots[0].Reason,
                Is.EqualTo("target_condition"));
            Assert.That(result.State.SkillRuntime.LastAutoSelection.Slots[1].Eligible, Is.True);
        }

        [Test]
        public void Tick_LethalEarthbreakerEmitsOneKillPerActorAndNeverDuplicatesActors()
        {
            ActiveSimInput input = CreateInput();
            input.Monster.Hp = 1;
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "slime-b" },
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "earthbreaker" },
            };

            ActiveSimResult result = ActiveSim.Tick(
                input, new SequenceRandomSource(new[] { 0.0, 0.99, 0.0, 0.99, 0.0, 0.99 }));

            Assert.That(result.Events.FindAll(item => item.Kind == CombatEventKind.EnemyKilled), Has.Count.EqualTo(3));
            CollectionAssert.AreEquivalent(
                new[] { "slime-center", "slime-a", "slime-b" }, result.DefeatedActorIds);
            Assert.That(result.State.DefeatedActorIds, Is.Unique);
        }

        [Test]
        public void Tick_ManualSelfCenteredEarthbreakerDoesNotRequireAPrimaryTarget()
        {
            ActiveSimInput input = CreateInput();
            input.World.TargetAvailable = false;
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "earthbreaker" },
            };

            ActiveSimResult result = ActiveSim.Tick(
                input, new SequenceRandomSource(new[] { 0.0, 0.99, 0.0, 0.99, 0.0, 0.99 }));

            Assert.That(result.Events.Exists(item =>
                item.Kind == CombatEventKind.SkillExecuted && item.SkillId == "earthbreaker"), Is.True);
            Assert.That(result.Events.Exists(item => item.Kind == CombatEventKind.CommandRejected), Is.False);
        }

        private static ActiveSimInput CreateInput()
        {
            var spatial = new CombatSpatialFrame { SourceActorId = "player" };
            spatial.Actors.Add(Actor("player", 0.0, 0.0, CombatFaction.Player));
            spatial.Actors.Add(Actor("slime-b", 1.0, 0.0, CombatFaction.Hostile));
            spatial.Actors.Add(Actor("slime-a", 1.0, 0.0, CombatFaction.Hostile));
            spatial.Actors.Add(Actor("slime-center", 0.2, 0.0, CombatFaction.Hostile));

            return new ActiveSimInput
            {
                Timestamp = 0,
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
                            Id = "earthbreaker",
                            DamageMultiplier = 1.7,
                            CooldownMs = 10000,
                            AutoEnabled = true,
                            Targeting = SkillTargetingKind.CircleAroundSource,
                            MinimumAutoTargets = 2,
                            RadiusWorldUnits = 1.0,
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
                TargetEntityId = "slime-b",
                ItemDefs = new Dictionary<string, ItemDef>(),
                State = new ActiveCombatState(),
                World = new CombatWorldFacts
                {
                    TargetAvailable = true,
                    TargetInRange = true,
                    LineOfSight = true,
                    Spatial = spatial,
                },
            };
        }

        private static CombatSpatialSnapshot Actor(
            string id,
            double x,
            double y,
            CombatFaction faction) => new CombatSpatialSnapshot
        {
            ActorId = id,
            DefinitionId = faction == CombatFaction.Player ? "player" : "slime",
            GroundPosition = new CombatPoint2(x, y),
            Floor = 0,
            FootprintRadius = 0.2,
            Faction = faction,
            Alive = true,
            Targetable = true,
        };
    }
}
