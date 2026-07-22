using UnityEngine;
using UnityEngine.Tilemaps;
using IdleCloud.Data;

namespace IdleCloud.View
{
    public class SceneBootstrap : MonoBehaviour
    {
        [Header("Map")]
        [Tooltip("Root of the loaded map hierarchy. If left unassigned, falls back to finding a GameObject named 'Grass 1' in the scene.")]
        public Transform mapRoot;

        [Header("Player")]
        [Tooltip("Player instance to position and wire up to the pathfinder/camera on scene start.")]
        public PlayerController player;

        [Header("Camera")]
        [Tooltip("Camera-follow component whose target is set to the player on scene start.")]
        public CameraFollow cameraFollow;

        [Header("Scene Wiring (optional)")]
        [Tooltip("Explicit player spawn point. If assigned, used instead of searching mapRoot for a child named 'Spawn'.")]
        [SerializeField] private Transform spawnPointOverride;
        [Tooltip("Explicit pathfinder service. If omitted, the scene fallback discovers or creates one once.")]
        [SerializeField] private GridPathfinder pathfinderOverride;
        [Tooltip("Explicit combat view to receive the pathfinder reference.")]
        [SerializeField] private CombatView combatViewOverride;
        [Tooltip("Curated enemy references for this scene. Empty uses the existing scene-wide fallback.")]
        [SerializeField] private EnemyController[] enemies = new EnemyController[0];
        [Tooltip("Allow legacy discovery for older scenes that have not been curated yet.")]
        [SerializeField] private bool allowLegacyDiscovery = true;

        private GridPathfinder _pathfinder;

        void Start()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>();

            if (mapRoot == null && allowLegacyDiscovery)
            {
                var found = GameObject.Find("Grass 1");
                if (found != null) mapRoot = found.transform;
            }

            // Pathfinder initialisatie
            _pathfinder = pathfinderOverride;
            if (_pathfinder == null && allowLegacyDiscovery) _pathfinder = FindFirstObjectByType<GridPathfinder>();
            if (_pathfinder == null && allowLegacyDiscovery)
            {
                var grid = mapRoot != null ? mapRoot.GetComponentInChildren<Grid>() : FindFirstObjectByType<Grid>();
                if (grid != null) _pathfinder = grid.gameObject.AddComponent<GridPathfinder>();
            }

            // Koppel pathfinder aan player
            if (player != null && _pathfinder != null)
                player.pathfinder = _pathfinder;

            // Koppel pathfinder aan alle enemies in de scene. GridPathfinder pas hierboven
            // aangemaakt/gevonden is, dus een enemy's eigen FindFirstObjectByType-fallback in
            // Start() kan null teruggeven als zijn Start() vóór deze runt — hier wordt de
            // referentie altijd deterministisch gezet, ongeacht Start()-volgorde.
            EnemyController[] sceneEnemies = enemies != null && enemies.Length > 0
                ? enemies
                : (allowLegacyDiscovery ? FindObjectsByType<EnemyController>(FindObjectsSortMode.None) : new EnemyController[0]);
            for (int index = 0; index < sceneEnemies.Length; index++)
            {
                var enemy = sceneEnemies[index];
                if (_pathfinder != null) enemy.pathfinder = _pathfinder;
            }

            var combatView = combatViewOverride;
            if (combatView == null && allowLegacyDiscovery) combatView = FindFirstObjectByType<CombatView>();
            if (_pathfinder != null && combatView != null && combatView.pathfinder == null)
                combatView.pathfinder = _pathfinder;

            // Player sorting (SortingGroup + PlayerSortController, reusing the terrain formula)
            // is wired in-editor on the player prefab/GameObject — not added here, since
            // PlayerSortController lives in Assembly-CSharp and can't be referenced from
            // IdleCloud.View.

            // Camera en Player setup
            ConfigureCamera();
            // A split map owns an explicit spawn marker and SceneLoader places the player after
            // map initialization. Keep the legacy fallback for Scene A, which has no marker.
            if ((allowLegacyDiscovery && FindFirstObjectByType<MapSpawnPoint>() == null) || spawnPointOverride != null)
                PositionPlayer();

            if (cameraFollow != null && player != null)
                cameraFollow.target = player.transform;
        }

        void ConfigureCamera()
        {
            Camera cam = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : Camera.main;
            if (cam == null) return;

            // Custom Axis (0,1,0) = standard Unity Y-sort: depth within a floor is decided by
            // world-Y, matching the project's GraphicsSettings default. Set explicitly here so
            // Play mode can't silently fall back to TransparencySortMode.Default.
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = new Vector3(0f, 1f, 0f);
        }

        void PositionPlayer()
        {
            if (player == null) return;

            if (spawnPointOverride != null)
            {
                player.transform.position = spawnPointOverride.position;
                return;
            }

            if (mapRoot == null || !allowLegacyDiscovery) return;
            var spawnGroup = mapRoot.Find("Spawn");
            if (spawnGroup == null || spawnGroup.childCount == 0) return;

            player.transform.position = spawnGroup.GetChild(0).position;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!allowLegacyDiscovery && mapRoot == null)
                Debug.LogError("[SceneBootstrap] Map root is required when legacy discovery is disabled.", this);
            if (!allowLegacyDiscovery && _pathfinder == null && pathfinderOverride == null)
                Debug.LogError("[SceneBootstrap] Pathfinding service is required when legacy discovery is disabled.", this);
        }
#endif

    }
}
