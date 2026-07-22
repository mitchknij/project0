using System;
using System.Collections.Generic;
using IdleCloud.Core;

namespace IdleCloud.Data
{
    public interface IClassContentProvider
    {
        IReadOnlyDictionary<ClassId, ClassDef> All { get; }
        ClassDef Get(ClassId classId);
    }

    /// <summary>
    /// Runtime content boundary. Implementations may be backed by Unity assets,
    /// generated data, or the legacy repositories, but the exposed values are
    /// always pure Data/Core definitions.
    /// </summary>
    public interface IRuntimeContentProvider : IClassContentProvider
    {
        IReadOnlyDictionary<string, ItemDef> Items { get; }
        IReadOnlyDictionary<string, MonsterDef> Monsters { get; }
        IReadOnlyDictionary<string, ResourceNodeDef> Nodes { get; }
        IReadOnlyDictionary<string, RecipeDef> Recipes { get; }
        IReadOnlyDictionary<string, MapDef> Maps { get; }
        IReadOnlyDictionary<string, TalentDef> Talents { get; }
        string ConfigurationVersion { get; }
    }

    public static class RuntimeContent
    {
        private static IRuntimeContentProvider _provider = new LegacyContentProvider();
        public static IReadOnlyDictionary<ClassId, ClassDef> All => _provider.All;
        public static IReadOnlyDictionary<string, ItemDef> Items => _provider.Items;
        public static IReadOnlyDictionary<string, MonsterDef> Monsters => _provider.Monsters;
        public static IReadOnlyDictionary<string, ResourceNodeDef> Nodes => _provider.Nodes;
        public static IReadOnlyDictionary<string, RecipeDef> Recipes => _provider.Recipes;
        public static IReadOnlyDictionary<string, MapDef> Maps => _provider.Maps;
        public static IReadOnlyDictionary<string, TalentDef> Talents => _provider.Talents;
        public static string ConfigurationVersion => _provider.ConfigurationVersion;

        public static ClassDef Get(ClassId classId) => _provider.Get(classId);

        public static void Configure(IClassContentProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _provider = provider as IRuntimeContentProvider ?? new LegacyClassAdapter(provider);
        }

        public static void UseLegacyContentForTests()
        {
            _provider = new LegacyContentProvider();
        }

        private sealed class LegacyContentProvider : IRuntimeContentProvider
        {
            public IReadOnlyDictionary<ClassId, ClassDef> All => ClassesRepo.All;
            public IReadOnlyDictionary<string, ItemDef> Items => ItemsRepo.All;
            public IReadOnlyDictionary<string, MonsterDef> Monsters => MonstersRepo.All;
            public IReadOnlyDictionary<string, ResourceNodeDef> Nodes => NodesRepo.All;
            public IReadOnlyDictionary<string, RecipeDef> Recipes => RecipesRepo.All;
            public IReadOnlyDictionary<string, MapDef> Maps => MapsRepo.All;
            public IReadOnlyDictionary<string, TalentDef> Talents => TalentsRepo.All;
            public string ConfigurationVersion => "legacy-code-v1";

            public ClassDef Get(ClassId classId)
                => ClassesRepo.All.TryGetValue(classId, out ClassDef definition) ? definition : null;
        }

        private sealed class LegacyClassAdapter : IRuntimeContentProvider
        {
            private readonly IClassContentProvider _classes;

            public LegacyClassAdapter(IClassContentProvider classes) => _classes = classes;
            public IReadOnlyDictionary<ClassId, ClassDef> All => _classes.All;
            public IReadOnlyDictionary<string, ItemDef> Items => ItemsRepo.All;
            public IReadOnlyDictionary<string, MonsterDef> Monsters => MonstersRepo.All;
            public IReadOnlyDictionary<string, ResourceNodeDef> Nodes => NodesRepo.All;
            public IReadOnlyDictionary<string, RecipeDef> Recipes => RecipesRepo.All;
            public IReadOnlyDictionary<string, MapDef> Maps => MapsRepo.All;
            public IReadOnlyDictionary<string, TalentDef> Talents => TalentsRepo.All;
            public string ConfigurationVersion => "legacy-code-v1";
            public ClassDef Get(ClassId classId) => _classes.Get(classId);
        }
    }
}
