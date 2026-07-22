using System;
using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using NUnit.Framework;

namespace IdleCloud.Tests
{
    public class DropAndActivityTests
    {
        [Test]
        public void RollDrops_IsDeterministicForSequenceSource()
        {
            var drops = new List<DropEntry>
            {
                new DropEntry { ItemId = "herb", Chance = 0.5, Min = 1, Max = 1 },
            };

            List<ItemStack> result = DropSystem.RollDrops(
                drops, 1, new SequenceRandomSource(new[] { 0.49 }));

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Qty, Is.EqualTo(1));
        }

        [Test]
        public void AssignActivity_SupportsGatheringWithCanonicalSkillMapping()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 0);
            var data = new ActivityDataBundle
            {
                Classes = ClassesRepo.All,
                Items = ItemsRepo.All,
                Monsters = MonstersRepo.All,
                Nodes = NodesRepo.All,
                Maps = MapsRepo.All,
            };

            Character after = Activity.AssignActivity(
                character, ActivityKind.Gathering, "wildflower_patch", data, 1000);

            Assert.That(after.Activity.Kind, Is.EqualTo(ActivityKind.Gathering));
            Assert.That(after.Efficiency.ActionsPerHour, Is.GreaterThan(0));
        }

        [Test]
        public void CombatSnapshot_UsesAuthoredMapDensityAndTravel()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 0);
            var standard = new ActivityDataBundle
            {
                Classes = ClassesRepo.All, Items = ItemsRepo.All,
                Nodes = NodesRepo.All, Maps = MapsRepo.All,
                Monsters = new Dictionary<string, MonsterDef>
                {
                    { "training", new MonsterDef { Id = "training", MapId = "grass_1", Hp = 12, Damage = 0, Defense = 1, Accuracy = 10, Agility = 8, Xp = 1, Coins = new CoinRange() } },
                },
            };
            var sparse = new ActivityDataBundle
            {
                Classes = ClassesRepo.All, Items = ItemsRepo.All, Nodes = NodesRepo.All,
                Maps = new Dictionary<string, MapDef>
                {
                    { "grass_1", new MapDef { Id = "grass_1", EncounterDensity = 0.5, CombatTravelOverheadMs = 3000.0 } },
                },
                Monsters = new Dictionary<string, MonsterDef>
                {
                    { "training", new MonsterDef { Id = "training", MapId = "grass_1", Hp = 12, Damage = 0, Defense = 1, Accuracy = 10, Agility = 8, Xp = 1, Coins = new CoinRange() } },
                },
            };

            EfficiencySnapshot normal = Activity.ComputeEfficiencySnapshot(character, ActivityKind.Fighting, "training", standard, 0);
            EfficiencySnapshot configured = Activity.ComputeEfficiencySnapshot(character, ActivityKind.Fighting, "training", sparse, 0);

            Assert.That(configured.MapDensity, Is.EqualTo(0.5));
            Assert.That(configured.TravelOverheadMs, Is.EqualTo(3000.0));
            Assert.That(configured.ActionsPerHour, Is.LessThan(normal.ActionsPerHour * 0.5));
            Assert.That(configured.DebugBreakdown, Does.Contain("mapDensity=0.5"));
        }

        [Test]
        public void CombatSnapshot_StopsWhenTheCharacterCannotSurviveOneEncounter()
        {
            Character character = CharacterHelper.CreateCharacter("character", "Tester", ClassId.Beginner, "grass_1", 0);
            var data = new ActivityDataBundle
            {
                Classes = ClassesRepo.All,
                Items = ItemsRepo.All,
                Nodes = NodesRepo.All,
                Maps = MapsRepo.All,
                Monsters = new Dictionary<string, MonsterDef>
                {
                    { "lethal", new MonsterDef { Id = "lethal", MapId = "grass_1", Hp = 100, Damage = 100, Defense = 0, Accuracy = 1, Agility = 100, Xp = 1, Coins = new CoinRange() } },
                },
            };

            EfficiencySnapshot snapshot = Activity.ComputeEfficiencySnapshot(character, ActivityKind.Fighting, "lethal", data, 0);

            Assert.That(snapshot.SurvivalFactor, Is.EqualTo(0.0));
            Assert.That(snapshot.ActionsPerHour, Is.EqualTo(0.0));
        }

        [Test]
        public void MalformedDropRange_IsRejected()
        {
            var table = new DropTable
            {
                Always = new List<DropItem> { new DropItem { ItemId = "ore", Min = 2, Max = 1 } },
            };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DropSystem.RollDropTable(table, new SequenceRandomSource(new[] { 0.0 })));
        }

        [Test]
        public void RollDropTable_RollsEachTertiaryEntryIndependently()
        {
            var table = new DropTable
            {
                Tertiary = new List<DropEntry>
                {
                    new DropEntry { ItemId = "slime_goo", Chance = 1.0, Min = 2, Max = 4 },
                    new DropEntry { ItemId = "wolf_pelt", Chance = 0.0, Min = 1, Max = 1 },
                },
            };

            // Sequence: Chance=1.0 entry -> chance-check draw, qty draw; Chance=0.0 entry -> chance-check draw only.
            List<ItemStack> result = DropSystem.RollDropTable(
                table, new SequenceRandomSource(new[] { 0.0, 0.99, 0.5 }));

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].ItemId, Is.EqualTo("slime_goo"));
            Assert.That(result[0].Qty, Is.InRange(2, 4));
        }

        [Test]
        public void ExpectedDropTable_IncludesTertiaryContribution()
        {
            var table = new DropTable
            {
                Tertiary = new List<DropEntry>
                {
                    new DropEntry { ItemId = "slime_goo", Chance = 0.5, Min = 1, Max = 3 },
                },
            };

            // kills=10, chance=0.5, avg(1,3)=2 -> expected 10 * 0.5 * 2 = 10.0 exactly (no fractional remainder).
            List<ItemStack> result = DropSystem.ExpectedDropTable(
                table, 10, new SequenceRandomSource(new[] { 0.0 }));

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].ItemId, Is.EqualTo("slime_goo"));
            Assert.That(result[0].Qty, Is.EqualTo(10));
        }

        [Test]
        public void ExpectedDropTable_TertiaryAppliesFractionalRemainderStochastically()
        {
            var table = new DropTable
            {
                Tertiary = new List<DropEntry>
                {
                    // kills=1, chance=0.5, avg(1,1)=1 -> expected 0.5 -> floor 0, fractional remainder 0.5.
                    new DropEntry { ItemId = "slime_goo", Chance = 0.5, Min = 1, Max = 1 },
                },
            };

            List<ItemStack> below = DropSystem.ExpectedDropTable(
                table, 1, new SequenceRandomSource(new[] { 0.49 }));
            List<ItemStack> above = DropSystem.ExpectedDropTable(
                table, 1, new SequenceRandomSource(new[] { 0.51 }));

            Assert.That(below, Has.Count.EqualTo(1));
            Assert.That(below[0].Qty, Is.EqualTo(1));
            Assert.That(above, Is.Empty);
        }

        [Test]
        public void RollDropTable_RejectsTertiaryEntryMissingItemId()
        {
            var table = new DropTable
            {
                Tertiary = new List<DropEntry> { new DropEntry { ItemId = null, Chance = 0.5, Min = 1, Max = 1 } },
            };

            Assert.Throws<ArgumentException>(() =>
                DropSystem.RollDropTable(table, new SequenceRandomSource(new[] { 0.0 })));
        }

        [Test]
        public void RollDropTable_RejectsTertiaryEntryWithChanceOutsideUnitRange()
        {
            var table = new DropTable
            {
                Tertiary = new List<DropEntry> { new DropEntry { ItemId = "slime_goo", Chance = 1.5, Min = 1, Max = 1 } },
            };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DropSystem.RollDropTable(table, new SequenceRandomSource(new[] { 0.0 })));
        }

        [Test]
        public void RollDropTable_RejectsTertiaryEntryWithMaxLessThanMin()
        {
            var table = new DropTable
            {
                Tertiary = new List<DropEntry> { new DropEntry { ItemId = "slime_goo", Chance = 0.5, Min = 3, Max = 1 } },
            };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DropSystem.RollDropTable(table, new SequenceRandomSource(new[] { 0.0 })));
        }
    }
}
