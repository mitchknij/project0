using UnityEngine;

namespace Iso.Sorting
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TerrainBlockVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        public int GroundX { get; private set; }
        public int GroundY { get; private set; }
        public int Height { get; private set; }
        public int AppliedSortingOrder { get; private set; }
        public float AppliedSortKey { get; private set; }

        private void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Configure(TerrainBlock block, IsoSortSettings settings)
        {
            GroundX = block.groundX;
            GroundY = block.groundY;
            Height = block.height;

            transform.position = block.worldPosition;

            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = block.sprite;
            spriteRenderer.sortingLayerName = settings.terrainSortingLayerName;

            AppliedSortKey = IsoTerrainSortCalculator.CalculateSortKey(block.groundX, block.groundY, block.height, settings);
            AppliedSortingOrder = IsoTerrainSortCalculator.CalculateSortingOrder(block.groundX, block.groundY, block.height, settings);
            spriteRenderer.sortingOrder = AppliedSortingOrder;
        }
    }
}
