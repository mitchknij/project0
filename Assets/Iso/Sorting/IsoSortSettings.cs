using UnityEngine;

namespace Iso.Sorting
{
    [CreateAssetMenu(menuName = "Iso/Sort Settings")]
    public sealed class IsoSortSettings : ScriptableObject
    {
        [Tooltip("Multiplier applied to the raw sort key before rounding to int.")]
        public float sortScale = 10f;

        [Tooltip("Weight applied to height (parsed Floor N index) in the sort key, so elevation reliably outweighs ground-plane position.")]
        public float elevationSortWeight = 2.5f;

        [Tooltip("Constant integer added to the final sortingOrder.")]
        public int baseSortingOffset = 0;

        [Tooltip("If Unity's rendering convention is reversed for this project, set to true. Do not modify the formula.")]
        public bool invertSortSign = false;

        [Tooltip("Name of the Unity Sorting Layer terrain blocks are placed on.")]
        public string terrainSortingLayerName = "Default";
    }
}
