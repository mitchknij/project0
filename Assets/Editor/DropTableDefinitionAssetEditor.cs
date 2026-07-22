using IdleCloud.Managers;
using UnityEditor;
using UnityEngine;

namespace IdleCloud.Editor
{
    [CustomEditor(typeof(DropTableDefinitionAsset))]
    internal sealed class DropTableDefinitionAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var table = (DropTableDefinitionAsset)target;
            int totalWeight = 0;
            int nothingWeight = 0;
            double averageQuantity = 0.0;
            int invalid = 0;

            foreach (DropTableDefinitionAsset.WeightedDropAuthoring slot in table.Slots ?? System.Array.Empty<DropTableDefinitionAsset.WeightedDropAuthoring>())
            {
                if (slot == null || slot.weight < 0) { invalid++; continue; }
                totalWeight += slot.weight;
                if (slot.nothing) { nothingWeight += slot.weight; continue; }
                if (slot.minimum < 0 || slot.maximum < slot.minimum || string.IsNullOrWhiteSpace(slot.itemId)) invalid++;
                averageQuantity += slot.weight * ((slot.minimum + slot.maximum) / 2.0);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Computed drop-table preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Main-table weight", totalWeight.ToString());
            EditorGUILayout.LabelField("Nothing probability", totalWeight > 0 ? (nothingWeight / (double)totalWeight).ToString("P2") : "n/a");
            EditorGUILayout.LabelField("Average item quantity / roll", totalWeight > 0 ? (averageQuantity / totalWeight).ToString("0.###") : "n/a");
            EditorGUILayout.LabelField("Expected item quantity / 1,000 rolls", totalWeight > 0 ? (averageQuantity / totalWeight * 1000.0).ToString("0.###") : "n/a");
            if (table.Rolls < 0 || totalWeight <= 0) invalid++;
            if (invalid > 0)
                EditorGUILayout.HelpBox($"{invalid} invalid slot/table value(s). Check weights, item IDs, and min/max quantities.", MessageType.Error);
            else
                EditorGUILayout.HelpBox("Drop table values are structurally valid. Item/map reference validation runs from the Content Registry.", MessageType.Info);
        }
    }
}
