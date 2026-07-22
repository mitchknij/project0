using System.Collections.Generic;
using IdleCloud.Data;
using IdleCloud.Managers;
using UnityEditor;
using UnityEngine;

namespace IdleCloud.Editor
{
    [CustomEditor(typeof(ContentRegistryAsset))]
    internal sealed class ContentRegistryAssetEditor : UnityEditor.Editor
    {
        private readonly List<string> _issues = new List<string>();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Validate Content Registry")) Validate();
            foreach (string issue in _issues)
                EditorGUILayout.HelpBox(issue, MessageType.Error);
        }

        private void Validate()
        {
            _issues.Clear();
            var registry = (ContentRegistryAsset)target;
            _issues.AddRange(registry.ValidateAssetReferences());
            if (_issues.Count == 0)
            {
                try
                {
                    RuntimeContent.Configure(new ContentRegistryProvider(registry));
                    _issues.AddRange(ContentValidator.Validate());
                }
                catch (System.Exception exception)
                {
                    _issues.Add(exception.Message);
                }
                finally
                {
                    RuntimeContent.UseLegacyContentForTests();
                }
            }
            if (_issues.Count == 0) _issues.Add("Content registry is valid.");
            Repaint();
        }
    }
}
