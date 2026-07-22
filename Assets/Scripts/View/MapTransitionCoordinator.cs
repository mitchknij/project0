using System;
using IdleCloud.Managers;
using UnityEngine;

namespace IdleCloud.View
{
    /// <summary>
    /// Single gateway for adjacent-map and waypoint scene transitions. It keeps the
    /// data-level MapId and the visible Unity map in lockstep.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapTransitionCoordinator : MonoBehaviour
    {
        [SerializeField] private SceneLoader sceneLoader;
        [SerializeField] private MapSceneCatalog mapCatalog;

        public static MapTransitionCoordinator Instance { get; private set; }
        public bool IsTransitioning { get; private set; }
        public event Action<string> MapTransitionCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[MapTransitionCoordinator] Only one coordinator may exist.", this);
                enabled = false;
                return;
            }
            Instance = this;
            if (sceneLoader == null) sceneLoader = GetComponent<SceneLoader>();
            if (sceneLoader == null) sceneLoader = FindFirstObjectByType<SceneLoader>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool RequestTravel(string destinationMapId, string destinationSpawnId = null)
            => Request(destinationMapId, destinationSpawnId, warp: false);

        public bool RequestWaypointWarp(string destinationMapId, string destinationSpawnId = null)
            => Request(destinationMapId, destinationSpawnId, warp: true);

        private bool Request(string destinationMapId, string destinationSpawnId, bool warp)
        {
            if (IsTransitioning || sceneLoader == null || mapCatalog == null ||
                string.IsNullOrWhiteSpace(destinationMapId))
                return false;
            if (!mapCatalog.TryGet(destinationMapId, out MapSceneCatalog.Entry destination))
            {
                Debug.LogError($"[MapTransitionCoordinator] No scene is registered for map '{destinationMapId}'.", this);
                return false;
            }

            GameManager manager = GameManager.Instance;
            bool permitted = warp
                ? manager != null && manager.CanWarpToWaypoint(destinationMapId)
                : manager != null && manager.CanTravelTo(destinationMapId);
            if (!permitted)
            {
                Debug.LogWarning($"[MapTransitionCoordinator] Travel to '{destinationMapId}' is not permitted.", this);
                return false;
            }

            IsTransitioning = true;
            string spawnId = string.IsNullOrWhiteSpace(destinationSpawnId)
                ? destination.defaultSpawnId
                : destinationSpawnId;
            sceneLoader.LoadMap(
                destination.scenePath,
                spawnId,
                scene => ValidateDestinationScene(scene, destinationMapId),
                loaded => CompleteTransition(loaded, destinationMapId, warp));
            return true;
        }

        private bool ValidateDestinationScene(UnityEngine.SceneManagement.Scene scene, string destinationMapId)
        {
            WorldMapContext context = null;
            foreach (WorldMapContext candidate in FindObjectsByType<WorldMapContext>(FindObjectsSortMode.None))
            {
                if (candidate != null && candidate.gameObject.scene == scene)
                {
                    context = candidate;
                    break;
                }
            }
            if (context == null || context.MapId != destinationMapId)
            {
                Debug.LogError($"[MapTransitionCoordinator] Scene '{scene.path}' has no matching WorldMapContext for '{destinationMapId}'.", this);
                return false;
            }

            foreach (string issue in context.ValidateConfiguration())
            {
                Debug.LogError($"[MapTransitionCoordinator] Scene '{scene.path}' is not ready: {issue}", context);
                return false;
            }
            return true;
        }

        private void CompleteTransition(bool loaded, string destinationMapId, bool warp)
        {
            IsTransitioning = false;
            if (!loaded) return;

            GameManager manager = GameManager.Instance;
            bool committed = warp
                ? manager != null && manager.TryWarpToWaypoint(destinationMapId)
                : manager != null && manager.TryTravelTo(destinationMapId);
            if (!committed)
            {
                Debug.LogError($"[MapTransitionCoordinator] Scene loaded but map state could not move to '{destinationMapId}'.", this);
                return;
            }
            MapTransitionCompleted?.Invoke(destinationMapId);
        }
    }
}
