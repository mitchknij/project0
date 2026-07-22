using System;
using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using UnityEngine;

namespace IdleCloud.View
{
    /// <summary>
    /// Inspector-owned binding between one Unity scene and its persistent map ID.
    /// Targets and nodes are explicitly authored scene references; nothing is spawned.
    /// </summary>
    public sealed class WorldMapContext : MonoBehaviour
    {
        public enum SceneBindingMode
        {
            DiscoverAuthoredComponents,
            ExplicitLists,
        }

        [Header("Map Identity")]
        [Tooltip("Stable MapDef ID represented by this Unity scene.")]
        [SerializeField] private string mapId = "grass_1";

        [Header("Active Efficiency")]
        [Tooltip("Encounter availability multiplier used by combat snapshots for this scene map.")]
        [SerializeField, Min(0.01f)] private float encounterDensity = 1f;
        [Tooltip("Average combat respawn/travel overhead used by snapshots, in milliseconds.")]
        [SerializeField, Min(0f)] private float combatTravelOverheadMs = 1500f;

        [Header("Scene Content")]
        [Tooltip("Discover components placed in this scene, or require the reference lists below to be curated in the Inspector.")]
        [SerializeField] private SceneBindingMode bindingMode = SceneBindingMode.DiscoverAuthoredComponents;
        [Tooltip("Used only in Explicit Lists mode. Drag placed enemy objects here.")]
        [SerializeField] private CombatTargetView[] combatTargets = Array.Empty<CombatTargetView>();
        [Tooltip("Used only in Explicit Lists mode. Drag placed tree, ore, and gathering objects here.")]
        [SerializeField] private GatheringNodeView[] gatheringNodes = Array.Empty<GatheringNodeView>();
        [Tooltip("Optional respawn position used after active-combat death.")]
        [SerializeField] private Transform playerSpawnPoint;

        public string MapId => mapId;
        public float EncounterDensity => encounterDensity;
        public float CombatTravelOverheadMs => combatTravelOverheadMs;
        public SceneBindingMode BindingMode => bindingMode;
        /// <summary>
        /// Explicit death-respawn point when authored; otherwise the map's default arrival marker.
        /// This keeps a newly placed MapSpawnPoint useful without a second inspector reference.
        /// </summary>
        public Transform PlayerSpawnPoint => playerSpawnPoint != null
            ? playerSpawnPoint
            : FindDefaultSpawnPoint();
        public IReadOnlyList<CombatTargetView> CombatTargets => bindingMode == SceneBindingMode.ExplicitLists
            ? combatTargets ?? Array.Empty<CombatTargetView>()
            : FindComponentsInThisScene<CombatTargetView>();
        public IReadOnlyList<GatheringNodeView> GatheringNodes => bindingMode == SceneBindingMode.ExplicitLists
            ? gatheringNodes ?? Array.Empty<GatheringNodeView>()
            : FindComponentsInThisScene<GatheringNodeView>();
        public bool IsKnownMap => RuntimeContent.Maps.ContainsKey(mapId);

        public bool ContainsCombatTarget(CombatTargetView target)
            => Contains(CombatTargets, target);

        public bool ContainsGatheringNode(GatheringNodeView node)
            => Contains(GatheringNodes, node);

        private void Start()
        {
            GameManager.Instance?.ConfigureSceneMapEfficiency(mapId, encounterDensity, combatTravelOverheadMs);
        }

        private static bool Contains<T>(IReadOnlyList<T> values, T value) where T : class
        {
            for (int index = 0; index < values.Count; index++)
                if (values[index] == value) return true;
            return false;
        }

        private T[] FindComponentsInThisScene<T>() where T : Component
        {
            T[] all = FindObjectsByType<T>(FindObjectsSortMode.None);
            var matches = new List<T>();
            foreach (T component in all)
                if (component != null && component.gameObject.scene == gameObject.scene) matches.Add(component);
            return matches.ToArray();
        }

        private Transform FindDefaultSpawnPoint()
        {
            foreach (MapSpawnPoint point in FindComponentsInThisScene<MapSpawnPoint>())
                if (point != null && point.SpawnId == "default") return point.transform;
            return null;
        }

        public IEnumerable<string> ValidateConfiguration()
        {
            if (!IsKnownMap) yield return "unknown_map_id:" + mapId;
            if (encounterDensity <= 0f) yield return "invalid_encounter_density";
            if (combatTravelOverheadMs < 0f) yield return "invalid_combat_travel_overhead";
            foreach (CombatTargetView target in CombatTargets)
            {
                if (target == null) { yield return "combat_target_missing"; continue; }
                RuntimeContent.Monsters.TryGetValue(target.MonsterId, out MonsterDef monster);
                if (monster == null) yield return "combat_target_unknown_monster:" + target.name;
                else if (!MapScope.Includes(monster.MapId, mapId)) yield return "combat_target_wrong_map:" + target.name;
            }
            foreach (GatheringNodeView node in GatheringNodes)
            {
                if (node == null) { yield return "gathering_node_missing"; continue; }
                RuntimeContent.Nodes.TryGetValue(node.NodeId, out ResourceNodeDef definition);
                if (definition == null) yield return "gathering_node_unknown_definition:" + node.name;
                else if (!MapScope.Includes(definition.MapId, mapId)) yield return "gathering_node_wrong_map:" + node.name;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (string issue in ValidateConfiguration())
                Debug.LogWarning($"[WorldMapContext] {issue}", this);
        }
#endif
    }
}
