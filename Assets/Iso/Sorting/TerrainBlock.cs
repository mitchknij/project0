using UnityEngine;

namespace Iso.Sorting
{
    public readonly struct TerrainBlock
    {
        public readonly int groundX;  // Tilemap cell x
        public readonly int groundY;  // Tilemap cell y
        public readonly int height;   // Floor index parsed from Tilemap name
        public readonly Sprite sprite;
        public readonly Vector3 worldPosition;

        public TerrainBlock(int groundX, int groundY, int height, Sprite sprite, Vector3 worldPosition)
        {
            this.groundX = groundX;
            this.groundY = groundY;
            this.height = height;
            this.sprite = sprite;
            this.worldPosition = worldPosition;
        }
    }
}
