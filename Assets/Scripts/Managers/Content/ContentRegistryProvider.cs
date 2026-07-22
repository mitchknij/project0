using System;
using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;

namespace IdleCloud.Managers
{
    /// <summary>
    /// Converts Unity authoring assets exactly once at the Manager boundary.
    /// Core and Data only receive detached pure definitions; no ScriptableObject
    /// reference is retained by the runtime dictionaries.
    /// </summary>
    public sealed class ContentRegistryProvider : IRuntimeContentProvider
    {
        private readonly Dictionary<ClassId, ClassDef> _classes;
        private readonly Dictionary<string, ItemDef> _items;
        private readonly Dictionary<string, MonsterDef> _monsters;
        private readonly Dictionary<string, ResourceNodeDef> _nodes;
        private readonly Dictionary<string, RecipeDef> _recipes;
        private readonly Dictionary<string, MapDef> _maps;
        private readonly Dictionary<string, TalentDef> _talents;

        public ContentRegistryProvider(ContentRegistryAsset registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var issues = new List<string>(registry.ValidateAssetReferences());
            if (issues.Count > 0) throw new InvalidOperationException(string.Join(";", issues));

            _classes = new Dictionary<ClassId, ClassDef>();
            if (registry.Skills != null)
            {
                foreach (var pair in new SkillContentProvider(registry.Skills).All)
                    _classes.Add(pair.Key, CopyClass(pair.Value));
            }
            else
            {
                foreach (var pair in ClassesRepo.All)
                    _classes.Add(pair.Key, CopyClass(pair.Value));
            }
            foreach (ClassDefinitionAsset asset in registry.Classes ?? Array.Empty<ClassDefinitionAsset>())
            {
                ClassDef definition = asset.ToPureDefinition();
                _classes[definition.Id] = definition;
            }

            _items = CopyItems(ItemsRepo.All);
            _monsters = CopyMonsters(MonstersRepo.All);
            _nodes = CopyNodes(NodesRepo.All);
            _recipes = CopyRecipes(RecipesRepo.All);
            _maps = CopyMaps(MapsRepo.All);
            _talents = CopyTalents(TalentsRepo.All);

            Apply(registry.Items, _items, asset => asset.ToPureDefinition());
            Apply(registry.Monsters, _monsters, asset => asset.ToPureDefinition());
            Apply(registry.Nodes, _nodes, asset => asset.ToPureDefinition());
            Apply(registry.Recipes, _recipes, asset => asset.ToPureDefinition());
            Apply(registry.Maps, _maps, asset => asset.ToPureDefinition());
            Apply(registry.Talents, _talents, asset => asset.ToPureDefinition());

            ConfigurationVersion = registry.ConfigurationVersion;
        }

        public IReadOnlyDictionary<ClassId, ClassDef> All => _classes;
        public IReadOnlyDictionary<string, ItemDef> Items => _items;
        public IReadOnlyDictionary<string, MonsterDef> Monsters => _monsters;
        public IReadOnlyDictionary<string, ResourceNodeDef> Nodes => _nodes;
        public IReadOnlyDictionary<string, RecipeDef> Recipes => _recipes;
        public IReadOnlyDictionary<string, MapDef> Maps => _maps;
        public IReadOnlyDictionary<string, TalentDef> Talents => _talents;
        public string ConfigurationVersion { get; }

        public ClassDef Get(ClassId classId) => _classes.TryGetValue(classId, out ClassDef value) ? value : null;

        private static void Apply<TAsset, TDefinition>(IReadOnlyList<TAsset> assets, Dictionary<string, TDefinition> destination, Func<TAsset, TDefinition> convert)
            where TAsset : UnityEngine.Object
        {
            foreach (TAsset asset in assets ?? Array.Empty<TAsset>())
            {
                TDefinition definition = convert(asset);
                string id = GetId(definition);
                if (destination.ContainsKey(id)) destination[id] = definition;
                else destination.Add(id, definition);
            }
        }

        private static string GetId<T>(T definition)
        {
            if (definition is ItemDef item) return item.Id;
            if (definition is MonsterDef monster) return monster.Id;
            if (definition is ResourceNodeDef node) return node.Id;
            if (definition is RecipeDef recipe) return recipe.Id;
            if (definition is MapDef map) return map.Id;
            if (definition is TalentDef talent) return talent.Id;
            throw new InvalidOperationException("unsupported_definition_type:" + typeof(T).Name);
        }

        private static Dictionary<string, ItemDef> CopyItems(IReadOnlyDictionary<string, ItemDef> source)
        {
            var result = new Dictionary<string, ItemDef>();
            foreach (var pair in source) result[pair.Key] = CopyItem(pair.Value);
            return result;
        }

        private static Dictionary<string, MonsterDef> CopyMonsters(IReadOnlyDictionary<string, MonsterDef> source)
        {
            var result = new Dictionary<string, MonsterDef>();
            foreach (var pair in source) result[pair.Key] = CopyMonster(pair.Value);
            return result;
        }

        private static Dictionary<string, ResourceNodeDef> CopyNodes(IReadOnlyDictionary<string, ResourceNodeDef> source)
        {
            var result = new Dictionary<string, ResourceNodeDef>();
            foreach (var pair in source) result[pair.Key] = CopyNode(pair.Value);
            return result;
        }

        private static Dictionary<string, RecipeDef> CopyRecipes(IReadOnlyDictionary<string, RecipeDef> source)
        {
            var result = new Dictionary<string, RecipeDef>();
            foreach (var pair in source) result[pair.Key] = CopyRecipe(pair.Value);
            return result;
        }

        private static Dictionary<string, MapDef> CopyMaps(IReadOnlyDictionary<string, MapDef> source)
        {
            var result = new Dictionary<string, MapDef>();
            foreach (var pair in source) result[pair.Key] = CopyMap(pair.Value);
            return result;
        }

        private static Dictionary<string, TalentDef> CopyTalents(IReadOnlyDictionary<string, TalentDef> source)
        {
            var result = new Dictionary<string, TalentDef>();
            foreach (var pair in source) result[pair.Key] = CopyTalent(pair.Value);
            return result;
        }

        private static ItemDef CopyItem(ItemDef source) => source == null ? null : new ItemDef
        {
            Id = source.Id, Name = source.Name, Type = source.Type, StackLimit = source.StackLimit,
            SellValue = source.SellValue, Slot = source.Slot, LevelReq = source.LevelReq,
            Bonuses = source.Bonuses == null ? null : new CoreStats
            {
                Strength = source.Bonuses.Strength, Agility = source.Bonuses.Agility,
                Wisdom = source.Bonuses.Wisdom, Luck = source.Bonuses.Luck,
            },
        };

        private static MonsterDef CopyMonster(MonsterDef source) => source == null ? null : new MonsterDef
        {
            Id = source.Id, Name = source.Name, MapId = source.MapId, Hp = source.Hp, Damage = source.Damage,
            Defense = source.Defense, Accuracy = source.Accuracy, Agility = source.Agility, Xp = source.Xp,
            Coins = source.Coins == null ? null : new CoinRange { Min = source.Coins.Min, Max = source.Coins.Max },
            Drops = CopyDropTable(source.Drops), RespawnMs = source.RespawnMs, Element = source.Element,
            Behavior = source.Behavior, Ranged = source.Ranged, Charge = source.Charge, Cast = source.Cast,
        };

        private static ResourceNodeDef CopyNode(ResourceNodeDef source) => source == null ? null : new ResourceNodeDef
        {
            Id = source.Id, Name = source.Name, MapId = source.MapId, Skill = source.Skill,
            LevelReq = source.LevelReq, BaseTimeMs = source.BaseTimeMs, Xp = source.Xp,
            Drops = CopyDropEntries(source.Drops),
        };

        private static RecipeDef CopyRecipe(RecipeDef source) => source == null ? null : new RecipeDef
        {
            Id = source.Id, Name = source.Name, OutputItemId = source.OutputItemId, OutputQty = source.OutputQty,
            CoinCost = source.CoinCost, LevelReq = source.LevelReq, Inputs = CopyStacks(source.Inputs),
        };

        private static MapDef CopyMap(MapDef source) => source == null ? null : new MapDef
        {
            Id = source.Id, Name = source.Name, RecommendedLevel = source.RecommendedLevel,
            Connections = source.Connections == null ? new List<string>() : new List<string>(source.Connections),
            EncounterDensity = source.EncounterDensity, CombatTravelOverheadMs = source.CombatTravelOverheadMs,
        };

        private static TalentDef CopyTalent(TalentDef source) => source == null ? null : new TalentDef
        {
            Id = source.Id, Name = source.Name, ClassId = source.ClassId, AvailableToAll = source.AvailableToAll,
            Description = source.Description, BonusStat = source.BonusStat, BonusPerPoint = source.BonusPerPoint,
            MaxPoints = source.MaxPoints,
        };

        private static DropTable CopyDropTable(DropTable source) => source == null ? null : new DropTable
        {
            Always = source.Always == null ? new List<DropItem>() : new List<DropItem>(source.Always.ConvertAll(item => item == null ? null : new DropItem { ItemId = item.ItemId, Min = item.Min, Max = item.Max })),
            Tertiary = CopyDropEntries(source.Tertiary),
            Main = source.Main == null ? null : new WeightedTable
            {
                Rolls = source.Main.Rolls,
                Slots = source.Main.Slots == null ? new List<WeightedSlot>() : new List<WeightedSlot>(source.Main.Slots.ConvertAll(slot => slot == null ? null : new WeightedSlot { Weight = slot.Weight, Nothing = slot.Nothing, ItemId = slot.ItemId, Min = slot.Min, Max = slot.Max })),
            },
        };

        private static List<DropEntry> CopyDropEntries(List<DropEntry> source)
        {
            var result = new List<DropEntry>();
            foreach (DropEntry item in source ?? new List<DropEntry>())
                if (item != null) result.Add(new DropEntry { ItemId = item.ItemId, Chance = item.Chance, Min = item.Min, Max = item.Max });
            return result;
        }

        private static List<ItemStack> CopyStacks(List<ItemStack> source)
        {
            var result = new List<ItemStack>();
            foreach (ItemStack item in source ?? new List<ItemStack>())
                if (item != null) result.Add(new ItemStack { ItemId = item.ItemId, Qty = item.Qty });
            return result;
        }

        private static ClassDef CopyClass(ClassDef source)
        {
            if (source == null) return null;
            var result = new ClassDef
            {
                Id = source.Id, Name = source.Name, Description = source.Description, PassiveBonus = source.PassiveBonus,
                BaseStats = source.BaseStats == null ? null : new CoreStats { Strength = source.BaseStats.Strength, Agility = source.BaseStats.Agility, Wisdom = source.BaseStats.Wisdom, Luck = source.BaseStats.Luck },
                StatGrowth = source.StatGrowth == null ? null : new StatGrowthDef { Strength = source.StatGrowth.Strength, Agility = source.StatGrowth.Agility, Wisdom = source.StatGrowth.Wisdom, Luck = source.StatGrowth.Luck },
                Passive = source.Passive,
                Skills = new List<ClassSkillDef>(),
            };
            foreach (ClassSkillDef skill in source.Skills ?? new List<ClassSkillDef>())
                if (skill != null) result.Skills.Add(skill);
            return result;
        }
    }
}
