using System.Collections.Generic;
using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Content/Drop Table Definition", fileName = "DropTableDefinition")]
    public sealed class DropTableDefinitionAsset : ScriptableObject
    {
        [System.Serializable]
        public sealed class AlwaysDropAuthoring
        {
            public string itemId;
            [Min(0)] public int minimum = 1;
            [Min(0)] public int maximum = 1;

            internal DropItem ToPureDefinition() => new DropItem
            {
                ItemId = itemId, Min = minimum, Max = maximum,
            };
        }

        [System.Serializable]
        public sealed class WeightedDropAuthoring
        {
            [Min(0)] public int weight = 1;
            public bool nothing;
            public string itemId;
            [Min(0)] public int minimum = 1;
            [Min(0)] public int maximum = 1;

            internal WeightedSlot ToPureDefinition() => new WeightedSlot
            {
                Weight = weight, Nothing = nothing, ItemId = itemId,
                Min = minimum, Max = maximum,
            };
        }

        [System.Serializable]
        public sealed class TertiaryDropAuthoring
        {
            [Range(0f, 1f)] public double chance;
            public string itemId;
            [Min(0)] public int minimum = 1;
            [Min(0)] public int maximum = 1;

            internal DropEntry ToPureDefinition() => new DropEntry
            {
                Chance = chance, ItemId = itemId, Min = minimum, Max = maximum,
            };
        }

        [Header("Stable identity")]
        [Tooltip("Stable ID used by tools and diagnostics. It is not used as a save-state reference.")]
        [SerializeField] private string stableId;
        [Header("Always drops")]
        [SerializeField] private List<AlwaysDropAuthoring> always = new List<AlwaysDropAuthoring>();
        [Header("Main weighted table")]
        [SerializeField, Min(0)] private int rolls = 1;
        [SerializeField] private List<WeightedDropAuthoring> slots = new List<WeightedDropAuthoring>();
        [Header("Tertiary independent drops")]
        [SerializeField] private List<TertiaryDropAuthoring> tertiary = new List<TertiaryDropAuthoring>();

        public string StableId => stableId;
        public int Rolls => rolls;
        public IReadOnlyList<AlwaysDropAuthoring> Always => always;
        public IReadOnlyList<WeightedDropAuthoring> Slots => slots;
        public IReadOnlyList<TertiaryDropAuthoring> Tertiary => tertiary;

        public DropTable ToPureDefinition()
        {
            var result = new DropTable
            {
                Always = new List<DropItem>(),
                Main = new WeightedTable { Rolls = rolls, Slots = new List<WeightedSlot>() },
                Tertiary = new List<DropEntry>(),
            };
            foreach (AlwaysDropAuthoring entry in always ?? new List<AlwaysDropAuthoring>())
                if (entry != null) result.Always.Add(entry.ToPureDefinition());
            foreach (WeightedDropAuthoring entry in slots ?? new List<WeightedDropAuthoring>())
                if (entry != null) result.Main.Slots.Add(entry.ToPureDefinition());
            foreach (TertiaryDropAuthoring entry in tertiary ?? new List<TertiaryDropAuthoring>())
                if (entry != null) result.Tertiary.Add(entry.ToPureDefinition());
            return result;
        }
    }
}
