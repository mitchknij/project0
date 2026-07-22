using System;
using System.Collections.Generic;
using IdleCloud.Data;
using IdleCloud.View;
using UnityEditor;
using UnityEngine;

namespace IdleCloud.EditorTools
{
    [CustomEditor(typeof(GatheringNodeView))]
    public sealed class GatheringNodeViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("entityId"));
            WorldAuthoringMenu.DrawIdPopup(serializedObject.FindProperty("nodeId"), "Node Definition", WorldAuthoringMenu.NodeIds());
            WorldAuthoringMenu.DrawRemaining(serializedObject, "entityId", "nodeId");
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(CombatTargetView))]
    public sealed class CombatTargetViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("entityId"));
            WorldAuthoringMenu.DrawIdPopup(serializedObject.FindProperty("monsterId"), "Monster Definition", WorldAuthoringMenu.MonsterIds());
            WorldAuthoringMenu.DrawRemaining(serializedObject, "entityId", "monsterId");
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(WorldMapContext))]
    public sealed class WorldMapContextEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            WorldAuthoringMenu.DrawIdPopup(serializedObject.FindProperty("mapId"), "Map Definition", WorldAuthoringMenu.MapIds());
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active Efficiency", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("encounterDensity"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("combatTravelOverheadMs"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playerSpawnPoint"));
            SerializedProperty bindingMode = serializedObject.FindProperty("bindingMode");
            EditorGUILayout.PropertyField(bindingMode);

            bool explicitLists = bindingMode.enumValueIndex == (int)WorldMapContext.SceneBindingMode.ExplicitLists;
            using (new EditorGUI.DisabledScope(!explicitLists))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("combatTargets"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gatheringNodes"), true);
            }
            if (!explicitLists)
                EditorGUILayout.HelpBox("Placed CombatTargetView and GatheringNodeView components are discovered automatically.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
            foreach (string issue in ((WorldMapContext)target).ValidateConfiguration())
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
        }
    }

    internal static class WorldAuthoringMenu
    {
        [MenuItem("IdleCloud/World/Add Gathering Node To Selection", true)]
        [MenuItem("IdleCloud/World/Add Combat Target To Selection", true)]
        private static bool CanAddToSelection() => Selection.activeGameObject != null;

        [MenuItem("IdleCloud/World/Add Gathering Node To Selection")]
        private static void AddGatheringNode()
        {
            var target = Selection.activeGameObject;
            if (target == null) return;
            if (target.GetComponent<GatheringNodeView>() == null)
                Undo.AddComponent<GatheringNodeView>(target);
            Selection.activeGameObject = target;
        }

        [MenuItem("IdleCloud/World/Add Combat Target To Selection")]
        private static void AddCombatTarget()
        {
            var target = Selection.activeGameObject;
            if (target == null) return;
            if (target.GetComponent<CombatTargetView>() == null)
                Undo.AddComponent<CombatTargetView>(target);
            Selection.activeGameObject = target;
        }

        internal static void DrawIdPopup(SerializedProperty property, string label, string[] ids)
        {
            int selected = Array.IndexOf(ids, property.stringValue);
            var labels = new string[ids.Length + 1];
            labels[0] = "<Unassigned>";
            Array.Copy(ids, 0, labels, 1, ids.Length);
            int next = EditorGUILayout.Popup(label, selected + 1, labels);
            property.stringValue = next == 0 ? string.Empty : ids[next - 1];
            if (selected < 0 && !string.IsNullOrWhiteSpace(property.stringValue))
                EditorGUILayout.HelpBox("The saved ID is not present in the current content registry.", MessageType.Warning);
        }

        internal static void DrawRemaining(SerializedObject serializedObject, params string[] excluded)
        {
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script" || Array.IndexOf(excluded, property.name) >= 0) continue;
                EditorGUILayout.PropertyField(property, true);
            }
        }

        internal static string[] NodeIds() => SortedIds(RuntimeContent.Nodes.Keys);
        internal static string[] MonsterIds() => SortedIds(RuntimeContent.Monsters.Keys);
        internal static string[] MapIds() => SortedIds(RuntimeContent.Maps.Keys);

        private static string[] SortedIds(IEnumerable<string> ids)
        {
            var result = new List<string>(ids);
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }
    }
}
