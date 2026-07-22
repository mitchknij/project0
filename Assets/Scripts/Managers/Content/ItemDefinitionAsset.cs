using System;
using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Content/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinitionAsset : ScriptableObject
    {
        [Serializable]
        private sealed class ItemStatsAuthoring
        {
            [Min(0)] public int Strength;
            [Min(0)] public int Agility;
            [Min(0)] public int Wisdom;
            [Min(0)] public int Luck;

            public CoreStats ToPureDefinition() => new CoreStats
            {
                Strength = Strength, Agility = Agility, Wisdom = Wisdom, Luck = Luck,
            };
        }

        [Header("Identity")]
        [Tooltip("Stable save/content ID. Keep this unchanged when renaming or moving the asset.")]
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private ItemType type;
        [Header("Economy")]
        [SerializeField, Min(1)] private int stackLimit = 1;
        [SerializeField, Min(0)] private int sellValue;
        [Header("Equipment")]
        [SerializeField] private bool equippable;
        [SerializeField] private EquipSlot equipmentSlot;
        [SerializeField] private bool hasLevelRequirement;
        [SerializeField, Min(1)] private int levelRequirement = 1;
        [SerializeField] private ItemStatsAuthoring bonuses = new ItemStatsAuthoring();
        [Header("Presentation")]
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject presentationPrefab;

        public string StableId => stableId;
        public Sprite Icon => icon;
        public GameObject PresentationPrefab => presentationPrefab;

        public ItemDef ToPureDefinition() => new ItemDef
        {
            Id = stableId, Name = displayName, Type = type, StackLimit = stackLimit,
            SellValue = sellValue, Slot = equippable ? equipmentSlot : (EquipSlot?)null,
            Bonuses = bonuses?.ToPureDefinition(),
            LevelReq = equippable && hasLevelRequirement ? levelRequirement : (int?)null,
        };
    }
}
