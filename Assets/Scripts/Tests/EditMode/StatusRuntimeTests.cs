using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class StatusRuntimeTests
    {
        [Test]
        public void FlameBurst_AppliesBurnToOrderedTargetsAndTicksAtConfiguredInterval()
        {
            ActiveSimInput castInput = CreateInput(0, new ActiveCombatState());
            castInput.Commands = CastFlameBurst();

            ActiveSimResult cast = ActiveSim.Tick(castInput, Random());
            ActiveSimResult beforeTick = ActiveSim.Tick(CreateInput(50, cast.State), Random());
            ActiveSimResult tick = ActiveSim.Tick(CreateInput(100, beforeTick.State), Random());

            Assert.That(cast.Events.FindAll(e => e.Kind == CombatEventKind.StatusApplied), Has.Count.EqualTo(2));
            Assert.That(cast.State.SkillRuntime.ActiveStatuses, Has.Count.EqualTo(2));
            Assert.That(beforeTick.Events.Exists(e => e.Kind == CombatEventKind.StatusTicked), Is.False);
            List<CombatEvent> ticks = tick.Events.FindAll(e => e.Kind == CombatEventKind.StatusTicked);
            Assert.That(ticks, Has.Count.EqualTo(2));
            Assert.That(ticks[0].TargetId, Is.EqualTo("slime-a"));
            Assert.That(ticks[1].TargetId, Is.EqualTo("slime-b"));
        }

        [Test]
        public void FlameBurst_RefreshesExistingBurnDurationWithoutStackingOrExtraTickSchedules()
        {
            ActiveSimInput firstInput = CreateInput(0, new ActiveCombatState());
            firstInput.Commands = CastFlameBurst();
            ActiveSimResult first = ActiveSim.Tick(firstInput, Random());
            first.State.SkillNextReadyAt.Clear();
            first.State.PlayerNextAttackAt = 0;

            ActiveSimInput refreshInput = CreateInput(50, first.State);
            refreshInput.Commands = CastFlameBurst();
            ActiveSimResult refreshed = ActiveSim.Tick(refreshInput, Random());

            Assert.That(refreshed.State.SkillRuntime.ActiveStatuses, Has.Count.EqualTo(2));
            Assert.That(refreshed.State.SkillRuntime.ActiveStatuses.TrueForAll(s => s.EndTick == 8), Is.True);
            Assert.That(refreshed.State.SkillRuntime.ScheduledEffects.FindAll(
                e => e.Kind == ScheduledCombatEffectKind.TickStatus), Has.Count.EqualTo(2));
            Assert.That(refreshed.Events.FindAll(e => e.Kind == CombatEventKind.StatusApplied &&
                e.Reason == "refreshed"), Has.Count.EqualTo(2));
        }

        private static List<CombatCommand> CastFlameBurst() => new List<CombatCommand>
        {
            new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "flame_burst" },
        };

        private static ActiveSimInput CreateInput(long timestamp, ActiveCombatState state)
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
                    SkillBar = new List<string> { "flame_burst", null, null, null },
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
                            Id = "flame_burst",
                            Element = Element.Fire,
                            DamageMultiplier = 1.0,
                            CooldownMs = 8000,
                            Targeting = SkillTargetingKind.CircleAroundTarget,
                            RadiusWorldUnits = 1.0,
                            Inflicts = new List<StatusInflict>
                            {
                                new StatusInflict
                                {
                                    Kind = StatusKind.Burn,
                                    DurationMs = 300,
                                    TickIntervalMs = 100,
                                    Magnitude = 0.2,
                                },
                            },
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
                TargetEntityId = "slime-a",
                ItemDefs = new Dictionary<string, ItemDef>(),
                State = state,
                World = new CombatWorldFacts
                {
                    TargetAvailable = true,
                    TargetInRange = true,
                    LineOfSight = true,
                    PrimaryTargetActorId = "slime-a",
                    Spatial = new CombatSpatialFrame
                    {
                        SourceActorId = "player",
                        Actors = new List<CombatSpatialSnapshot>
                        {
                            Actor("player", 0, 0, 0.2, CombatFaction.Player),
                            Actor("slime-a", 1, 0, 0.2, CombatFaction.Hostile),
                            Actor("slime-b", 1.5, 0, 0.2, CombatFaction.Hostile),
                            Actor("outside", 3, 0, 0.2, CombatFaction.Hostile),
                        },
                    },
                },
            };
        }

        private static CombatSpatialSnapshot Actor(
            string id,
            double x,
            double y,
            double radius,
            CombatFaction faction) => new CombatSpatialSnapshot
        {
            ActorId = id,
            GroundPosition = new CombatPoint2(x, y),
            Floor = 0,
            FootprintRadius = radius,
            Faction = faction,
            Alive = true,
            Targetable = true,
        };

        private static SequenceRandomSource Random() => new SequenceRandomSource(new[]
        {
            0.0, 0.99,
            0.0, 0.99,
            0.0, 0.99,
        });
    }
}
