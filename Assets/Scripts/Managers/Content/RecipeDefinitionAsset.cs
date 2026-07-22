using System.Collections.Generic;
using IdleCloud.Core;
using UnityEngine;

namespace IdleCloud.Managers
{
    [CreateAssetMenu(menuName = "IdleCloud/Content/Recipe Definition", fileName = "RecipeDefinition")]
    public sealed class RecipeDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable recipe ID used by crafting and saved UI state.")]
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private string outputItemId;
        [SerializeField, Min(1)] private int outputQuantity = 1;
        [SerializeField, Min(0)] private int coinCost;
        [SerializeField, Min(1)] private int levelRequirement = 1;
        [SerializeField] private List<ItemStack> inputs = new List<ItemStack>();

        public string StableId => stableId;
        public RecipeDef ToPureDefinition() => new RecipeDef
        {
            Id = stableId, Name = displayName, OutputItemId = outputItemId, OutputQty = outputQuantity,
            CoinCost = coinCost, LevelReq = levelRequirement,
            Inputs = CopyStacks(inputs),
        };

        private static List<ItemStack> CopyStacks(List<ItemStack> source)
        {
            var copy = new List<ItemStack>();
            foreach (ItemStack item in source ?? new List<ItemStack>())
                if (item != null) copy.Add(new ItemStack { ItemId = item.ItemId, Qty = item.Qty });
            return copy;
        }
    }
}
