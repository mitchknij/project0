using IdleCloud.Managers;
using UnityEditor;
using UnityEngine;

namespace IdleCloud.Editor
{
    [CustomEditor(typeof(RuntimeDebugView))]
    internal sealed class RuntimeDebugViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var debug = (RuntimeDebugView)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Authoritative runtime state", EditorStyles.boldLabel);
            Field("Account ID", debug.AccountId);
            Field("Character ID", debug.CharacterId);
            Field("Account revision", debug.AccountRevision.ToString());
            Field("Content/balance version", debug.ConfigurationVersion);
            Field("Activity", debug.Activity);
            Field("Snapshot validity", debug.SnapshotValidity);
            Field("Snapshot version", debug.EfficiencyVersion);
            Field("Snapshot breakdown", debug.SnapshotBreakdown);
            IdleCloud.Core.CoreStats stats = debug.EffectiveStats;
            Field("Effective stats", stats == null ? "<none>" : $"STR {stats.Strength} / AGI {stats.Agility} / WIS {stats.Wisdom} / LUK {stats.Luck}");
            Field("Bank", $"{debug.BankSlots}/{debug.BankCapacity} slots");
            Field("Inventory", $"{debug.InventorySlots}/{debug.InventoryCapacity} slots");
            Field("Last reward transaction", debug.LastRewardTransactionId);
            Repaint();
        }

        private static void Field(string label, string value)
            => EditorGUILayout.LabelField(label, string.IsNullOrEmpty(value) ? "<none>" : value);
    }
}
