using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace IdleCloud.View
{
    /// <summary>
    /// Owns the additive startup sequence and the currently loaded map scene.
    /// It intentionally stays small: map identity is the build-settings scene path and
    /// spawning is delegated to authored <see cref="MapSpawnPoint"/> components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour
    {
        [Header("Startup Scenes")]
        [SerializeField] private string persistentScenePath = "Assets/Scenes/PersistentGame.unity";
        [SerializeField] private string startingMapScenePath = "Assets/Scenes/Maps/Grasslands_1.unity";
        [SerializeField] private string startingSpawnId = "default";

        private Scene _currentMapScene;
        private bool _started;

        public Scene CurrentMapScene => _currentMapScene;
        public bool InitialLoadCompleted { get; private set; }
        public bool IsLoadingMap { get; private set; }

        private void Start()
        {
            if (_started) return;
            _started = true;
            StartCoroutine(LoadInitialScenes());
        }

        public void LoadMap(string mapScenePath, string spawnId = "default", Action<bool> completed = null)
            => LoadMap(mapScenePath, spawnId, validator: null, completed: completed);

        public void LoadMap(
            string mapScenePath,
            string spawnId,
            Func<Scene, bool> validator,
            Action<bool> completed = null)
        {
            if (string.IsNullOrWhiteSpace(mapScenePath))
            {
                Debug.LogError("[SceneLoader] Cannot load a map without a scene path.", this);
                completed?.Invoke(false);
                return;
            }
            if (IsLoadingMap)
            {
                Debug.LogWarning("[SceneLoader] A map transition is already in progress.", this);
                completed?.Invoke(false);
                return;
            }

            StartCoroutine(LoadMapRoutine(mapScenePath, spawnId, validator, completed));
        }

        private IEnumerator LoadInitialScenes()
        {
            try
            {
                bool persistentLoaded = false;
                yield return EnsureSceneLoaded(persistentScenePath, loaded => persistentLoaded = loaded);
                if (persistentLoaded)
                    yield return LoadMapRoutine(startingMapScenePath, startingSpawnId, validator: null);
            }
            finally
            {
                InitialLoadCompleted = true;
            }
        }

        private IEnumerator LoadMapRoutine(
            string mapScenePath,
            string spawnId,
            Func<Scene, bool> validator,
            Action<bool> completed = null)
        {
            IsLoadingMap = true;
            bool succeeded = false;
            try
            {
                if (_currentMapScene.IsValid() && _currentMapScene.isLoaded && _currentMapScene.path == mapScenePath)
                {
                    SceneManager.SetActiveScene(_currentMapScene);
                    succeeded = PlacePlayerAtSpawn(_currentMapScene, spawnId);
                    yield break;
                }

                // Load and validate the destination before unloading the current map. Global 2D
                // lights are special: URP only permits one per sorting layer, so silence the
                // current map's globals during the short additive overlap.
                Scene previous = _currentMapScene;
                List<Behaviour> disabledPreviousGlobalLights = DisableGlobalLights(previous);
                bool destinationLoaded = false;
                yield return EnsureSceneLoaded(mapScenePath, loaded => destinationLoaded = loaded);
                if (!destinationLoaded)
                {
                    RestoreLights(disabledPreviousGlobalLights);
                    yield break;
                }

                Scene destination = SceneManager.GetSceneByPath(mapScenePath);
                if (!destination.IsValid() || !destination.isLoaded)
                {
                    Debug.LogError($"[SceneLoader] Map scene '{mapScenePath}' did not finish loading.", this);
                    RestoreLights(disabledPreviousGlobalLights);
                    yield break;
                }
                if (validator != null && !validator(destination))
                {
                    SceneManager.UnloadSceneAsync(destination);
                    RestoreLights(disabledPreviousGlobalLights);
                    yield break;
                }

                SceneManager.SetActiveScene(destination);
                yield return null;
                if (!PlacePlayerAtSpawn(destination, spawnId))
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    if (destination.IsValid() && destination.isLoaded)
                        SceneManager.UnloadSceneAsync(destination);
                    RestoreLights(disabledPreviousGlobalLights);
                    yield break;
                }

                _currentMapScene = destination;
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(gameObject.scene);
                    AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(previous);
                    if (unloadOperation == null)
                        Debug.LogError($"[SceneLoader] Failed to unload previous map '{previous.path}'.", this);
                    else
                        yield return unloadOperation;
                    SceneManager.SetActiveScene(_currentMapScene);
                }
                succeeded = true;
            }
            finally
            {
                IsLoadingMap = false;
                completed?.Invoke(succeeded);
            }
        }

        private static IEnumerator EnsureSceneLoaded(string scenePath, Action<bool> completed)
        {
            Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                completed?.Invoke(true);
                yield break;
            }

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            if (loadOperation == null)
            {
                Debug.LogError($"[SceneLoader] Unable to start loading scene '{scenePath}'. Check Build Settings.");
                completed?.Invoke(false);
                yield break;
            }

            yield return loadOperation;
            loadedScene = SceneManager.GetSceneByPath(scenePath);
            completed?.Invoke(loadedScene.IsValid() && loadedScene.isLoaded);
        }

        private bool PlacePlayerAtSpawn(Scene mapScene, string spawnId)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("[SceneLoader] Cannot place player because PersistentGame has no PlayerController.", this);
                return false;
            }

            MapSpawnPoint spawn = FindSpawn(mapScene, spawnId);
            if (spawn == null)
            {
                Debug.LogError($"[SceneLoader] Map '{mapScene.path}' has no spawn with ID '{spawnId}'.", this);
                return false;
            }

            ConfigureMapServices(mapScene, player);
            player.TeleportTo(spawn.transform.position, spawn.transform.rotation);
            return true;
        }

        private static void ConfigureMapServices(Scene mapScene, PlayerController player)
        {
            GridPathfinder pathfinder = FindFirstInScene<GridPathfinder>(mapScene);
            if (pathfinder == null)
            {
                Grid grid = FindFirstInScene<Grid>(mapScene);
                if (grid == null)
                {
                    Debug.LogError($"[SceneLoader] Map '{mapScene.path}' has no Grid for pathfinding.");
                    return;
                }
                pathfinder = grid.gameObject.AddComponent<GridPathfinder>();
            }

            player.pathfinder = pathfinder;
            foreach (CombatView combatView in FindAllInScene<CombatView>(mapScene))
                combatView.pathfinder = pathfinder;
            foreach (EnemyController enemy in FindAllInScene<EnemyController>(mapScene))
                enemy.pathfinder = pathfinder;
        }

        private static MapSpawnPoint FindSpawn(Scene mapScene, string spawnId)
        {
            foreach (MapSpawnPoint spawn in FindAllInScene<MapSpawnPoint>(mapScene))
            {
                if (spawn.SpawnId == spawnId)
                    return spawn;
            }

            return null;
        }

        private static T FindFirstInScene<T>(Scene scene) where T : Component
        {
            foreach (T component in FindAllInScene<T>(scene)) return component;
            return null;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            T[] all = FindObjectsByType<T>(FindObjectsSortMode.None);
            var matches = new System.Collections.Generic.List<T>();
            foreach (T component in all)
                if (component != null && component.gameObject.scene == scene) matches.Add(component);
            return matches.ToArray();
        }

        private static List<Behaviour> DisableGlobalLights(Scene scene)
        {
            var disabled = new List<Behaviour>();
            if (!scene.IsValid() || !scene.isLoaded) return disabled;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Behaviour component in root.GetComponentsInChildren<Behaviour>(includeInactive: true))
                {
                    if (component == null || !component.enabled || !IsGlobalLight2D(component)) continue;
                    component.enabled = false;
                    disabled.Add(component);
                }
            }
            return disabled;
        }

        private static void RestoreLights(List<Behaviour> lights)
        {
            foreach (Behaviour light in lights ?? new List<Behaviour>())
                if (light != null) light.enabled = true;
        }

        private static bool IsGlobalLight2D(Behaviour component)
        {
            Type type = component.GetType();
            if (type.FullName != "UnityEngine.Rendering.Universal.Light2D") return false;

            PropertyInfo lightType = type.GetProperty("lightType");
            object value = lightType?.GetValue(component);
            return string.Equals(value?.ToString(), "Global", StringComparison.Ordinal);
        }
    }
}
