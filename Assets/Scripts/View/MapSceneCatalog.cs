using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleCloud.View
{
    /// <summary>Authoritative mapping from persistent gameplay map IDs to Unity scene paths.</summary>
    [CreateAssetMenu(menuName = "IdleCloud/World/Map Scene Catalog", fileName = "MapSceneCatalog")]
    public sealed class MapSceneCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Stable MapDef ID, used by saves and travel validation.")]
            public string mapId;
            [Tooltip("Path of a scene present in Build Settings.")]
            public string scenePath;
            [Tooltip("Used when a caller has no specific arrival point.")]
            public string defaultSpawnId = "default";
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public bool TryGet(string mapId, out Entry entry)
        {
            foreach (Entry candidate in entries)
            {
                if (candidate != null && candidate.mapId == mapId &&
                    !string.IsNullOrWhiteSpace(candidate.scenePath))
                {
                    entry = candidate;
                    return true;
                }
            }
            entry = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (Entry entry in entries)
            {
                if (entry == null) continue;
                entry.mapId = entry.mapId?.Trim();
                entry.scenePath = entry.scenePath?.Trim();
                entry.defaultSpawnId = string.IsNullOrWhiteSpace(entry.defaultSpawnId)
                    ? "default"
                    : entry.defaultSpawnId.Trim();
            }
        }
#endif
    }
}
