using System.Collections.Generic;
using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Content/Resource Node Definition", fileName = "ResourceNodeDefinition")]
    public sealed class ResourceNodeDefinitionAsset : ScriptableObject
    {
        [System.Serializable]
        private sealed class ResourceDropAuthoring
        {
            public string itemId;
            [Range(0f, 1f)] public double chance = 1.0;
            [Min(0)] public int minimum = 1;
            [Min(0)] public int maximum = 1;

            internal DropEntry ToPureDefinition() => new DropEntry
            {
                ItemId = itemId, Chance = chance, Min = minimum, Max = maximum,
            };
        }

        [Header("Identity")]
        [Tooltip("Stable resource-node ID. Moving or renaming this asset must not change it.")]
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private string mapId;
        [SerializeField] private HarvestSkill skill;
        [Header("Gathering balance")]
        [SerializeField, Min(1)] private int levelRequirement = 1;
        [Tooltip("Milliseconds per attempt.")]
        [SerializeField, Min(1)] private int baseTimeMilliseconds = 1000;
        [SerializeField, Min(0)] private int experience;
        [SerializeField] private List<ResourceDropAuthoring> drops = new List<ResourceDropAuthoring>();
        [Header("Presentation")]
        [SerializeField] private GameObject visualPrefab;
        [SerializeField, Min(0f)] private float interactionRange = 1f;
        [SerializeField, Min(0)] private int respawnMilliseconds;

        public string StableId => stableId;
        public GameObject VisualPrefab => visualPrefab;
        public float InteractionRange => interactionRange;
        public int RespawnMilliseconds => respawnMilliseconds;

        public ResourceNodeDef ToPureDefinition()
        {
            var result = new ResourceNodeDef
            {
                Id = stableId, Name = displayName, MapId = mapId, Skill = skill,
                LevelReq = levelRequirement, BaseTimeMs = baseTimeMilliseconds, Xp = experience,
                Drops = new List<DropEntry>(),
            };
            foreach (ResourceDropAuthoring drop in drops ?? new List<ResourceDropAuthoring>())
                if (drop != null) result.Drops.Add(drop.ToPureDefinition());
            return result;
        }
    }
}
