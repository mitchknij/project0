#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Iso.Sorting
{
    [CustomEditor(typeof(TerrainBlockVisual))]
    public sealed class IsoTerrainSortDebugGizmos : Editor
    {
        private void OnSceneGUI()
        {
            TerrainBlockVisual v = (TerrainBlockVisual)target;
            Handles.Label(
                v.transform.position,
                $"({v.GroundX},{v.GroundY},{v.Height})\nS={v.AppliedSortKey}\norder={v.AppliedSortingOrder}");
        }
    }
}
#endif
