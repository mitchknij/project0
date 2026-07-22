using System;

namespace IdleCloud.Core
{
    /// <summary>Shared map-membership rule for authored content. "*" is reusable placeholder content available on every map.</summary>
    public static class MapScope
    {
        public const string AnyMap = "*";

        public static bool Includes(string definitionMapId, string mapId)
            => definitionMapId == AnyMap || definitionMapId == mapId;
    }

    internal static class CoreValidation
    {
        public static void RequirePositiveQuantity(int quantity, string paramName)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(paramName, "Quantity must be positive.");
        }

        public static void RequireValidItem(ItemStack stack, ItemDef definition)
        {
            if (stack == null) throw new ArgumentNullException(nameof(stack));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(stack.ItemId)) throw new ArgumentException("Item ID is required.", nameof(stack));
            if (stack.ItemId != definition.Id) throw new ArgumentException("Stack and definition item IDs differ.", nameof(definition));
            RequirePositiveQuantity(stack.Qty, nameof(stack));
            if (definition.StackLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(definition), "Stack limit must be positive.");
        }

        public static void RequireValidRange(int min, int max, string name)
        {
            if (min < 0 || max < min)
                throw new ArgumentOutOfRangeException(name, "Range must be non-negative and ordered.");
        }
    }
}
