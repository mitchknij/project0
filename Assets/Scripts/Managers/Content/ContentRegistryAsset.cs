using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Content/Content Registry", fileName = "ContentRegistry")]
    public sealed class ContentRegistryAsset : ScriptableObject
    {
        [Header("Versioning")]
        [Tooltip("Increment when content IDs, definitions, or references change in a way that invalidates snapshots.")]
        [SerializeField] private string contentVersion = "content-v1";
        [Tooltip("Increment when balance or policy settings change. Active efficiency snapshots are invalidated.")]
        [SerializeField] private string balanceVersion = "balance-v1";
        [Header("Focused registries")]
        [SerializeField] private SkillContentRegistryAsset skills;
        [SerializeField] private List<ClassDefinitionAsset> classes = new List<ClassDefinitionAsset>();
        [SerializeField] private List<ItemDefinitionAsset> items = new List<ItemDefinitionAsset>();
        [SerializeField] private List<MonsterDefinitionAsset> monsters = new List<MonsterDefinitionAsset>();
        [SerializeField] private List<ResourceNodeDefinitionAsset> nodes = new List<ResourceNodeDefinitionAsset>();
        [SerializeField] private List<RecipeDefinitionAsset> recipes = new List<RecipeDefinitionAsset>();
        [SerializeField] private List<MapGameplayDefinitionAsset> maps = new List<MapGameplayDefinitionAsset>();
        [SerializeField] private List<TalentDefinitionAsset> talents = new List<TalentDefinitionAsset>();

        public string ContentVersion => contentVersion;
        public string BalanceVersion => balanceVersion;
        public string ConfigurationVersion => (contentVersion ?? string.Empty) + "+" + (balanceVersion ?? string.Empty);
        public SkillContentRegistryAsset Skills => skills;
        public IReadOnlyList<ClassDefinitionAsset> Classes => classes;
        public IReadOnlyList<ItemDefinitionAsset> Items => items;
        public IReadOnlyList<MonsterDefinitionAsset> Monsters => monsters;
        public IReadOnlyList<ResourceNodeDefinitionAsset> Nodes => nodes;
        public IReadOnlyList<RecipeDefinitionAsset> Recipes => recipes;
        public IReadOnlyList<MapGameplayDefinitionAsset> Maps => maps;
        public IReadOnlyList<TalentDefinitionAsset> Talents => talents;

        public IEnumerable<string> ValidateAssetReferences()
        {
            if (string.IsNullOrWhiteSpace(contentVersion)) yield return "content_version_missing";
            if (string.IsNullOrWhiteSpace(balanceVersion)) yield return "balance_version_missing";
            foreach (string issue in ValidateIds("item", items, asset => asset?.StableId)) yield return issue;
            foreach (string issue in ValidateIds("monster", monsters, asset => asset?.StableId)) yield return issue;
            foreach (string issue in ValidateIds("node", nodes, asset => asset?.StableId)) yield return issue;
            foreach (string issue in ValidateIds("recipe", recipes, asset => asset?.StableId)) yield return issue;
            foreach (string issue in ValidateIds("map", maps, asset => asset?.StableId)) yield return issue;
            foreach (string issue in ValidateIds("talent", talents, asset => asset?.StableId)) yield return issue;
            var classIds = new HashSet<IdleCloud.Core.ClassId>();
            int classIndex = 0;
            foreach (ClassDefinitionAsset asset in classes ?? new List<ClassDefinitionAsset>())
            {
                if (asset == null) yield return "class_missing_reference:index=" + classIndex;
                else if (!classIds.Add(asset.StableId)) yield return "class_duplicate_id:" + asset.StableId;
                classIndex++;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (string issue in ValidateAssetReferences())
                Debug.LogWarning("[ContentRegistry] " + issue, this);
        }
#endif

        private static IEnumerable<string> ValidateIds<T>(string kind, IReadOnlyList<T> assets, Func<T, string> getId)
            where T : UnityEngine.Object
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int index = 0;
            foreach (T asset in assets ?? Array.Empty<T>())
            {
                if (asset == null) { yield return kind + "_missing_reference:index=" + index; index++; continue; }
                string id = getId(asset);
                if (string.IsNullOrWhiteSpace(id)) yield return kind + "_missing_id:asset=" + asset.name;
                else if (!ids.Add(id)) yield return kind + "_duplicate_id:" + id;
                index++;
            }
        }
    }
}
