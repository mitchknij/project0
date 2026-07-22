using System.IO;
using IdleCloud.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneStructureMigration
{
    private const string SourceScenePath = "Assets/Scenes/Scene A.unity";
    private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
    private const string PersistentScenePath = "Assets/Scenes/PersistentGame.unity";
    private const string FirstMapScenePath = "Assets/Scenes/Maps/FirstMap.unity";

    [MenuItem("Tools/IdleCloud/Create Three-Scene Structure")]
    private static void CreateThreeSceneStructure()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!ValidateInputs()) return;

        Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        GameObject player = FindRoot(sourceScene, "Player");
        Vector3 spawnPosition = player.transform.position;
        Quaternion spawnRotation = player.transform.rotation;

        if (!EditorSceneManager.SaveScene(sourceScene, FirstMapScenePath, true))
        {
            Debug.LogError($"[SceneStructureMigration] Could not copy '{SourceScenePath}' to '{FirstMapScenePath}'.");
            return;
        }

        Scene firstMapScene = EditorSceneManager.OpenScene(FirstMapScenePath, OpenSceneMode.Single);
        Scene persistentScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        foreach (string rootName in new[] { "_GameManager", "Main Camera", "CinemachineCamera", "Player" })
            SceneManager.MoveGameObjectToScene(FindRoot(firstMapScene, rootName), persistentScene);

        if (!EditorSceneManager.SaveScene(persistentScene, PersistentScenePath))
        {
            Debug.LogError($"[SceneStructureMigration] Could not save '{PersistentScenePath}'. FirstMap was copied but no further scenes were changed.");
            return;
        }

        SceneManager.SetActiveScene(firstMapScene);
        ClearCrossSceneReferences(firstMapScene);
        CreateDefaultSpawn(spawnPosition, spawnRotation);
        EditorSceneManager.MarkSceneDirty(firstMapScene);
        if (!EditorSceneManager.SaveScene(firstMapScene))
        {
            Debug.LogError($"[SceneStructureMigration] Could not save '{FirstMapScenePath}'.");
            return;
        }

        Scene bootstrapScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        new GameObject("Bootstrap").AddComponent<SceneLoader>();
        if (!EditorSceneManager.SaveScene(bootstrapScene, BootstrapScenePath))
        {
            Debug.LogError($"[SceneStructureMigration] Could not save '{BootstrapScenePath}'.");
            return;
        }

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(BootstrapScenePath, true),
            new EditorBuildSettingsScene(PersistentScenePath, true),
            new EditorBuildSettingsScene(FirstMapScenePath, true),
        };

        SceneManager.SetActiveScene(firstMapScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SceneStructureMigration] Created Bootstrap, PersistentGame, and Maps/FirstMap. Scene A remains unchanged; FirstMap is open for authoring.");
    }

    private static bool ValidateInputs()
    {
        if (!File.Exists(SourceScenePath))
        {
            Debug.LogError($"[SceneStructureMigration] Source scene not found: '{SourceScenePath}'.");
            return false;
        }

        foreach (string targetPath in new[] { BootstrapScenePath, PersistentScenePath, FirstMapScenePath })
        {
            if (File.Exists(targetPath))
            {
                Debug.LogError($"[SceneStructureMigration] Refusing to overwrite existing scene '{targetPath}'. Move or inspect it manually before retrying.");
                return false;
            }
        }

        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Maps")
            && string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets/Scenes", "Maps")))
        {
            Debug.LogError("[SceneStructureMigration] Could not create Assets/Scenes/Maps.");
            return false;
        }

        Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        foreach (string rootName in new[] { "_GameManager", "Main Camera", "CinemachineCamera", "Player" })
        {
            if (FindRoot(sourceScene, rootName) == null)
            {
                Debug.LogError($"[SceneStructureMigration] Required persistent root '{rootName}' is missing from Scene A. No target scenes were created.");
                return false;
            }
        }

        return true;
    }

    private static GameObject FindRoot(Scene scene, string rootName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == rootName) return root;

        return null;
    }

    private static void ClearCrossSceneReferences(Scene mapScene)
    {
        SceneBootstrap sceneBootstrap = FindInScene<SceneBootstrap>(mapScene);
        if (sceneBootstrap != null)
        {
            var serialized = new SerializedObject(sceneBootstrap);
            serialized.FindProperty("player").objectReferenceValue = null;
            serialized.FindProperty("cameraFollow").objectReferenceValue = null;
            serialized.FindProperty("spawnPointOverride").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        CombatView combatView = FindInScene<CombatView>(mapScene);
        if (combatView != null) ClearReference(combatView, "player");

        GatheringView gatheringView = FindInScene<GatheringView>(mapScene);
        if (gatheringView != null) ClearReference(gatheringView, "player");
    }

    private static void ClearReference(Object component, string propertyName)
    {
        var serialized = new SerializedObject(component);
        serialized.FindProperty(propertyName).objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }

        return null;
    }

    private static void CreateDefaultSpawn(Vector3 position, Quaternion rotation)
    {
        var spawns = new GameObject("Spawns");
        var spawn = new GameObject("default");
        spawn.transform.SetParent(spawns.transform, false);
        spawn.transform.SetPositionAndRotation(position, rotation);
        spawn.AddComponent<MapSpawnPoint>();
    }
}
