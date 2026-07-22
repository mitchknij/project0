using System.Collections.Generic;
using IdleCloud.Core;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class AutoCombatPolicyTests
    {
        [Test]
        public void SelectTarget_ReturnsNearestAvailableCandidate()
        {
            CombatCandidate result = AutoCombatPolicy.SelectTarget(new List<CombatCandidate>
            {
                new CombatCandidate { EntityId = "far", Available = true, Distance = 5.0 },
                new CombatCandidate { EntityId = "near", Available = true, Distance = 1.0 },
                new CombatCandidate { EntityId = "unavailable", Available = false, Distance = 0.0 },
            });

            Assert.That(result.EntityId, Is.EqualTo("near"));
        }

        [Test]
        public void SelectTarget_BreaksDistanceTiesByOrdinalEntityId()
        {
            CombatCandidate result = AutoCombatPolicy.SelectTarget(new List<CombatCandidate>
            {
                new CombatCandidate { EntityId = "slime-b", Available = true, Distance = 1.0 },
                new CombatCandidate { EntityId = "slime-a", Available = true, Distance = 1.0 },
            });

            Assert.That(result.EntityId, Is.EqualTo("slime-a"));
        }

        [Test]
        public void SelectTarget_SkipsUnavailableAndReturnsNullWithoutCandidates()
        {
            Assert.That(AutoCombatPolicy.SelectTarget(new List<CombatCandidate>
            {
                null,
                new CombatCandidate { EntityId = "slime", Available = false, Distance = 1.0 },
            }), Is.Null);
        }

        [Test]
        public void NextAutoSkill_UsesFirstEligiblePlayerAuthoredSlot()
        {
            ClassSkillDef result = AutoCombatPolicy.NextAutoSkill(
                ClassWithSkills(),
                CharacterWithBar("slot-two", "slot-one"),
                new Dictionary<string, long>(),
                0,
                InRangeWorld(),
                out AutoSkillSelectionDiagnostics diagnostics);

            Assert.That(result.Id, Is.EqualTo("slot-two"));
            Assert.That(diagnostics.SelectedSlotIndex, Is.EqualTo(0));
        }

        [Test]
        public void NextAutoSkill_SelectsLegacyBuffAfterEarlierSkillsAreCoolingDown()
        {
            var readyAt = new Dictionary<string, long>
            {
                ["slot-one"] = 1,
                ["slot-two"] = 1,
            };

            ClassSkillDef result = AutoCombatPolicy.NextAutoSkill(
                ClassWithSkills(), CharacterWithBar("buff", "slot-one", "slot-two"),
                readyAt, 0, InRangeWorld(), out _);

            Assert.That(result.Id, Is.EqualTo("buff"));
        }

        [Test]
        public void NextAutoSkill_SkipsExplicitManualOnlyDamageSkill()
        {
            ClassDef classDef = ClassWithSkills();
            classDef.Skills.Insert(0, new ClassSkillDef
            {
                Id = "manual_only",
                DamageMultiplier = 50.0,
                AutoEnabled = false,
            });

            ClassSkillDef result = AutoCombatPolicy.NextAutoSkill(
                classDef,
                CharacterWithBar("manual_only", "slot-one"),
                new Dictionary<string, long>(),
                0,
                InRangeWorld(),
                out AutoSkillSelectionDiagnostics diagnostics);

            Assert.That(result.Id, Is.EqualTo("slot-one"));
            Assert.That(diagnostics.Slots[0].Reason, Is.EqualTo("auto_disabled"));
        }

        [Test]
        public void NextAutoSkill_SkipsTileSkillBelowMinimumTargetsAndSelectsWhenMet()
        {
            ClassDef classDef = TileSkillClass();
            var world = TileWorld(withDiagonal: false);

            ClassSkillDef skipped = AutoCombatPolicy.NextAutoSkill(
                classDef, CharacterWithBar("ground_smash", "power_strike"),
                new Dictionary<string, long>(), 0, world, out AutoSkillSelectionDiagnostics skippedDiagnostics);

            Assert.That(skipped.Id, Is.EqualTo("power_strike"));
            Assert.That(skippedDiagnostics.Slots[0].Reason, Is.EqualTo("target_condition"));

            var worldWithTwo = TileWorld(withDiagonal: true);
            ClassSkillDef selected = AutoCombatPolicy.NextAutoSkill(
                classDef, CharacterWithBar("ground_smash", "power_strike"),
                new Dictionary<string, long>(), 0, worldWithTwo, out AutoSkillSelectionDiagnostics selectedDiagnostics);

            Assert.That(selected.Id, Is.EqualTo("ground_smash"));
            Assert.That(selectedDiagnostics.SelectedSlotIndex, Is.EqualTo(0));
        }

        [Test]
        public void NextAutoSkill_TilePatternAroundTargetIsIneligibleWithoutACurrentTarget()
        {
            ClassDef classDef = TileSkillClass();
            var world = new CombatWorldFacts
            {
                TargetAvailable = true,
                TargetInRange = true,
                LineOfSight = true,
                Spatial = new CombatSpatialFrame { SourceActorId = "player" },
                PrimaryTargetActorId = null,
            };
            world.Spatial.Actors.Add(new CombatSpatialSnapshot
            {
                ActorId = "player",
                Tile = new CombatTileCoordinate(0, 0),
                Faction = CombatFaction.Player,
            });

            ClassSkillDef result = AutoCombatPolicy.NextAutoSkill(
                classDef, CharacterWithBar("arcane_detonation", "power_strike"),
                new Dictionary<string, long>(), 0, world, out AutoSkillSelectionDiagnostics diagnostics);

            Assert.That(result.Id, Is.EqualTo("power_strike"));
            Assert.That(diagnostics.Slots[0].Reason, Is.EqualTo("target_condition"));
        }

        [Test]
        public void NextAutoSkill_FallsBackToBasicAttackWhenSlotsAreAllIneligibleTileSkills()
        {
            ClassDef classDef = new ClassDef
            {
                Skills = new List<ClassSkillDef>
                {
                    new ClassSkillDef
                    {
                        Id = "ground_smash",
                        DamageMultiplier = 1.0,
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
                },
            };
            var world = TileWorld(withDiagonal: false);

            ClassSkillDef result = AutoCombatPolicy.NextAutoSkill(
                classDef, CharacterWithBar("ground_smash", null, null, null),
                new Dictionary<string, long>(), 0, world, out AutoSkillSelectionDiagnostics diagnostics);

            Assert.That(result, Is.Null);
            Assert.That(diagnostics.FallbackReason, Is.EqualTo("no_eligible_slotted_skill"));
        }

        [Test]
        public void NextAutoSkill_SelectsSkillInLastOfEightSlots()
        {
            ClassSkillDef result = AutoCombatPolicy.NextAutoSkill(
                ClassWithSkills(),
                CharacterWithBar(null, null, null, null, null, null, null, "slot-one"),
                new Dictionary<string, long>(),
                0,
                InRangeWorld(),
                out AutoSkillSelectionDiagnostics diagnostics);

            Assert.That(result.Id, Is.EqualTo("slot-one"));
            Assert.That(diagnostics.SelectedSlotIndex, Is.EqualTo(7));
        }

        [Test]
        public void NextAutoSkill_AcrossWidenedBarStillPrefersLowestEligibleSlotIndex()
        {
            ClassSkillDef result = AutoCombatPolicy.NextAutoSkill(
                ClassWithSkills(),
                CharacterWithBar(null, null, "slot-one", null, null, null, "slot-two", null),
                new Dictionary<string, long>(),
                0,
                InRangeWorld(),
                out AutoSkillSelectionDiagnostics diagnostics);

            Assert.That(result.Id, Is.EqualTo("slot-one"));
            Assert.That(diagnostics.SelectedSlotIndex, Is.EqualTo(2));
        }

        [Test]
        public void AutoSkillSelectionDiagnostics_ClonePreservesSelectionSequenceId()
        {
            var original = new AutoSkillSelectionDiagnostics { SelectionSequenceId = 42 };

            AutoSkillSelectionDiagnostics clone = original.Clone();

            Assert.That(clone.SelectionSequenceId, Is.EqualTo(42));
        }

        private static ClassDef TileSkillClass() => new ClassDef
        {
            Skills = new List<ClassSkillDef>
            {
                new ClassSkillDef
                {
                    Id = "ground_smash",
                    DamageMultiplier = 1.0,
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
                    DamageMultiplier = 1.0,
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
                new ClassSkillDef { Id = "power_strike", DamageMultiplier = 1.0, AutoEnabled = true },
            },
        };

        private static CombatWorldFacts TileWorld(bool withDiagonal)
        {
            var spatial = new CombatSpatialFrame { SourceActorId = "player" };
            spatial.Actors.Add(new CombatSpatialSnapshot
            {
                ActorId = "player",
                Tile = new CombatTileCoordinate(0, 0),
                Faction = CombatFaction.Player,
                Alive = true,
                Targetable = true,
            });
            spatial.Actors.Add(new CombatSpatialSnapshot
            {
                ActorId = "north",
                Tile = new CombatTileCoordinate(0, 1),
                Faction = CombatFaction.Hostile,
                Alive = true,
                Targetable = true,
            });
            if (withDiagonal)
                spatial.Actors.Add(new CombatSpatialSnapshot
                {
                    ActorId = "east",
                    Tile = new CombatTileCoordinate(1, 0),
                    Faction = CombatFaction.Hostile,
                    Alive = true,
                    Targetable = true,
                });

            return new CombatWorldFacts
            {
                TargetAvailable = true,
                TargetInRange = true,
                LineOfSight = true,
                Spatial = spatial,
            };
        }

        private static ClassDef ClassWithSkills() => new ClassDef
        {
            Skills = new List<ClassSkillDef>
            {
                new ClassSkillDef
                {
                    Id = "buff",
                    Buff = new SelfBuff { Stat = BuffStat.Damage, Magnitude = 0.25, DurationMs = 3000 },
                },
                new ClassSkillDef { Id = "slot-one", DamageMultiplier = 1.0, AutoEnabled = true },
                new ClassSkillDef { Id = "slot-two", DamageMultiplier = 1.0, AutoEnabled = true },
            },
        };

        private static Character CharacterWithBar(params string[] skillIds) => new Character
        {
            SkillBar = new List<string>(skillIds),
        };

        private static CombatWorldFacts InRangeWorld() => new CombatWorldFacts
        {
            TargetAvailable = true,
            TargetInRange = true,
            LineOfSight = true,
        };
    }
}
