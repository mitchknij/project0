using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class TileTargetingSkillTests
    {
        [Test]
        public void GroundSmash_HitsAnchorAndCardinalNeighboursButNotDiagonalOrOutsideTiles()
        {
            ActiveSimInput input = CreateInput();
            input.World.TargetAvailable = false;
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "ground_smash" },
            };

            ActiveSimResult result = ActiveSim.Tick(
                input, new SequenceRandomSource(new[] { 0.0, 0.99, 0.0, 0.99 }));
            List<CombatEvent> damage = result.Events.FindAll(item => item.Kind == CombatEventKind.DamageApplied);

            CollectionAssert.AreEqual(new[] { "north" }, damage.ConvertAll(item => item.TargetId));
        }

        [Test]
        public void GroundSmash_LethalHitProducesSingleRewardTransactionPerActor()
        {
            ActiveSimInput input = CreateInput();
            input.Monster.Hp = 1;
            input.World.TargetAvailable = false;
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "ground_smash" },
            };

            ActiveSimResult result = ActiveSim.Tick(
                input, new SequenceRandomSource(new[] { 0.0, 0.99, 0.0, 0.99 }));

            Assert.That(result.Events.FindAll(item => item.Kind == CombatEventKind.EnemyKilled), Has.Count.EqualTo(1));
            CollectionAssert.AreEquivalent(new[] { "north" }, result.DefeatedActorIds);
            Assert.That(result.State.DefeatedActorIds, Is.Unique);
        }

        [Test]
        public void ArcaneDetonation_AnchorsOnCurrentTargetTile()
        {
            ActiveSimInput input = CreateInput();
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.SelectTarget, TargetId = "diagonal" },
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "arcane_detonation" },
            };

            ActiveSimResult result = ActiveSim.Tick(
                input, new SequenceRandomSource(new[] { 0.0, 0.99, 0.0, 0.99, 0.0, 0.99 }));
            List<CombatEvent> damage = result.Events.FindAll(item => item.Kind == CombatEventKind.DamageApplied);

            // 3x3 (Chebyshev 1) around "diagonal" (1,1) covers (0,1), (1,1) AND (0,2);
            // the player at (0,0) is also inside the pattern but filtered by faction.
            CollectionAssert.AreEquivalent(
                new[] { "north", "diagonal", "outside" }, damage.ConvertAll(item => item.TargetId));
        }

        [Test]
        public void TileAreaResolved_ContainsOrderedTilesFloorAndResolvedActorIds()
        {
            ActiveSimInput input = CreateInput();
            input.World.TargetAvailable = false;
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "ground_smash" },
            };

            ActiveSimResult result = ActiveSim.Tick(
                input, new SequenceRandomSource(new[] { 0.0, 0.99, 0.0, 0.99 }));
            CombatEvent tileEvent = result.Events.Find(item => item.Kind == CombatEventKind.TileAreaResolved);

            Assert.That(tileEvent, Is.Not.Null);
            Assert.That(tileEvent.Floor, Is.EqualTo(0));
            CollectionAssert.AreEqual(
                new[]
                {
                    new CombatTileCoordinate(0, 0),
                    new CombatTileCoordinate(0, 1),
                    new CombatTileCoordinate(1, 0),
                    new CombatTileCoordinate(0, -1),
                    new CombatTileCoordinate(-1, 0),
                },
                tileEvent.AffectedTiles);
            CollectionAssert.AreEqual(new[] { "north" }, tileEvent.ResolvedActorIds);
        }

        [Test]
        public void TileAreaResolved_IsNotEmittedWhenSpatialFrameIsMissing()
        {
            ActiveSimInput input = CreateInput();
            input.World.TargetAvailable = false;
            input.World.Spatial = null;
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "ground_smash" },
            };

            ActiveSimResult result = ActiveSim.Tick(
                input, new SequenceRandomSource(new[] { 0.0, 0.99, 0.0, 0.99 }));

            Assert.That(result.Events.Exists(item => item.Kind == CombatEventKind.TileAreaResolved), Is.False);
            Assert.That(result.Events.Exists(item => item.Kind == CombatEventKind.DamageApplied), Is.False);
        }

        [Test]
        public void ScheduledTilePatternSkill_ResolvesFromCapturedAnchorAtImpactTick()
        {
            ActiveSimInput castInput = CreateInput();
            castInput.World.TargetAvailable = false;
            castInput.Class.Skills.Add(new ClassSkillDef
            {
                Id = "delayed_smash",
                DamageMultiplier = 1.5,
                CooldownMs = 4000,
                Targeting = SkillTargetingKind.TilePatternAroundSource,
                Timing = SkillTimingKind.ScheduledImpact,
                ImpactDelayTicks = 1,
                TilePattern = new TilePatternDef
                {
                    PatternKind = TilePatternKind.Cross,
                    Size = 1,
                    FloorPolicy = TilePatternFloorPolicy.SameFloor,
                },
            });
            castInput.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "delayed_smash" },
            };

            ActiveSimResult cast = ActiveSim.Tick(
                castInput, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            ActiveSimInput impactInput = CreateInput();
            impactInput.World.TargetAvailable = false;
            impactInput.Timestamp = 50;
            impactInput.Class = castInput.Class;
            impactInput.State = cast.State;

            ActiveSimResult impact = ActiveSim.Tick(
                impactInput, new SequenceRandomSource(new[] { 0.0, 0.99 }));

            Assert.That(cast.Events.Exists(item => item.Kind == CombatEventKind.CombatEffectScheduled), Is.True);
            List<CombatEvent> damage = impact.Events.FindAll(item => item.Kind == CombatEventKind.DamageApplied);
            CollectionAssert.AreEqual(new[] { "north" }, damage.ConvertAll(item => item.TargetId));
            Assert.That(impact.Events.Exists(item => item.Kind == CombatEventKind.TileAreaResolved), Is.True);
        }

        [Test]
        public void TargetDeath_CancelsTargetAnchoredButNotSourceAnchoredPendingTileImpacts()
        {
            ActiveSimInput input = CreateInput();
            input.Monster.Hp = 1;
            input.World.TargetAvailable = false;
            input.Class.Skills.Add(new ClassSkillDef
            {
                Id = "delayed_smash",
                DamageMultiplier = 1.5,
                CooldownMs = 4000,
                Targeting = SkillTargetingKind.TilePatternAroundSource,
                Timing = SkillTimingKind.ScheduledImpact,
                ImpactDelayTicks = 1,
                TilePattern = new TilePatternDef
                {
                    PatternKind = TilePatternKind.Cross,
                    Size = 1,
                    FloorPolicy = TilePatternFloorPolicy.SameFloor,
                },
            });
            input.State.SkillRuntime.ScheduledEffects.Add(new ScheduledCombatEffect
            {
                SequenceId = 1,
                ExecuteTick = 1000,
                Kind = ScheduledCombatEffectKind.ResolveSkillImpact,
                SourceActorId = "player",
                TargetActorId = "north",
                SkillId = "delayed_smash",
                HasCapturedAnchor = true,
                CapturedAnchorTile = new CombatTileCoordinate(0, 0),
                CapturedAnchorFloor = 0,
            });
            input.State.SkillRuntime.ScheduledEffects.Add(new ScheduledCombatEffect
            {
                SequenceId = 2,
                ExecuteTick = 1000,
                Kind = ScheduledCombatEffectKind.ResolveSkillImpact,
                SourceActorId = "player",
                TargetActorId = "north",
                SkillId = "arcane_detonation",
            });
            input.Commands = new List<CombatCommand>
            {
                new CombatCommand { Kind = CombatCommandKind.TriggerSkill, SkillId = "ground_smash" },
            };

            ActiveSimResult result = ActiveSim.Tick(
                input, new SequenceRandomSource(new[] { 0.0, 0.99, 0.0, 0.99 }));

            Assert.That(result.Events.Exists(item =>
                item.Kind == CombatEventKind.EnemyKilled && item.TargetId == "north"), Is.True);
            List<ScheduledCombatEffect> pending = result.State.SkillRuntime.ScheduledEffects;
            Assert.That(pending.Exists(item => item.SkillId == "delayed_smash"), Is.True,
                "source-anchored pending impact must survive its incidental target's death");
            Assert.That(pending.Exists(item => item.SkillId == "arcane_detonation"), Is.False,
                "target-anchored pending impact must cancel when its target dies");
            Assert.That(result.Events.Exists(item =>
                item.Kind == CombatEventKind.CombatEffectCancelled && item.SkillId == "arcane_detonation"), Is.True);
        }

        private static ActiveSimInput CreateInput()
        {
            var spatial = new CombatSpatialFrame { SourceActorId = "player" };
            spatial.Actors.Add(Actor("player", new CombatTileCoordinate(0, 0), CombatFaction.Player));
            spatial.Actors.Add(Actor("north", new CombatTileCoordinate(0, 1), CombatFaction.Hostile));
            spatial.Actors.Add(Actor("diagonal", new CombatTileCoordinate(1, 1), CombatFaction.Hostile));
            spatial.Actors.Add(Actor("outside", new CombatTileCoordinate(0, 2), CombatFaction.Hostile));

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
                            Id = "ground_smash",
                            DamageMultiplier = 2.2,
                            CooldownMs = 8000,
                            AutoEnabled = true,
                            Targeting = SkillTargetingKind.TilePatternAroundSource,
                            MinimumAutoTargets = 2,
                            TilePattern = new TilePatternDef
                            {
                                PatternKind = TilePatternKind.Cross,
                                Size = 1,
                                FloorPolicy = TilePatternFloorPolicy.SameFloor,
                            },
                        },
                        new ClassSkillDef
                        {
                            Id = "arcane_detonation",
                            DamageMultiplier = 2.4,
                            CooldownMs = 10000,
                            AutoEnabled = true,
                            Targeting = SkillTargetingKind.TilePatternAroundTarget,
                            MinimumAutoTargets = 1,
                            TilePattern = new TilePatternDef
                            {
                                PatternKind = TilePatternKind.SquareRadius,
                                Size = 1,
                                FloorPolicy = TilePatternFloorPolicy.SameFloor,
                            },
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
                TargetEntityId = "north",
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
            CombatTileCoordinate tile,
            CombatFaction faction) => new CombatSpatialSnapshot
        {
            ActorId = id,
            DefinitionId = faction == CombatFaction.Player ? "player" : "slime",
            Tile = tile,
            Floor = 0,
            FootprintRadius = 0.2,
            Faction = faction,
            Alive = true,
            Targetable = true,
        };
    }
}
