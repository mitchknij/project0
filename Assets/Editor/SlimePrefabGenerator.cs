// SlimePrefabGenerator.cs — Editor tool that builds/migrates Assets/Prefabs/Enemies/Slime.prefab
// (SpriteRenderer + SpriteSheetAnimator + EnemyController + SortingGroup + EnemySortController)
// wired to the already-sliced Slime Idle/Slime walking sprite sheets, so slimes can be hand-placed
// as scene objects instead of runtime-spawned by SceneBootstrap. Menu: Tools > IdleCloud > Create
// Slime Prefab.
//
// EnemyController/EnemySortController are generic mob-behavior components (not Slime-specific) —
// this generator just builds one particular prefab from one particular texture pair; a future mob
// gets its own thin generator (or hand-built prefab) reusing the same two components.
//
// Safety: never edits source textures; if Slime.prefab doesn't exist yet, creates it fresh; if it
// already exists, migrates it in place (removes the legacy ElevationSorter, adds SortingGroup +
// EnemySortController if missing, wires empty slots only) — never deletes or recreates the asset,
// never touches already hand-tuned idleFrames/walkFrames/wander values.

using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using IdleCloud.View;
using Iso.Sorting;

public static class SlimePrefabGenerator
{
    private const string IdleTexturePath = "Assets/Art/Mobs/Slime Idle.png";
    private const string WalkTexturePath = "Assets/Art/Mobs/Slime walking.png";
    private const string TargetPrefabPath = "Assets/Prefabs/Enemies/Slime.prefab";
    private const string IsoSortSettingsPath = "Assets/Iso/Sorting/IsoSortSettings.asset";

    [MenuItem("Tools/IdleCloud/Create Slime Prefab")]
    private static void CreateSlimePrefab()
    {
        if (File.Exists(TargetPrefabPath))
        {
            MigrateExistingPrefab();
            return;
        }

        CreateNewPrefab();
    }

    private static void CreateNewPrefab()
    {
        Sprite[] idleFrames = LoadSortedSprites(IdleTexturePath);
        Sprite[] walkFrames = LoadSortedSprites(WalkTexturePath);
        if (idleFrames.Length == 0 || walkFrames.Length == 0)
        {
            Debug.LogError($"[SlimePrefabGen] Failed to load sprites from '{IdleTexturePath}' or '{WalkTexturePath}'.");
            return;
        }

        var go = new GameObject("Slime");
        try
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = idleFrames[0];

            var anim = go.AddComponent<SpriteSheetAnimator>();
            anim.fps = 8f;

            var ctrl = go.AddComponent<EnemyController>();
            ctrl.idleFrames = idleFrames;
            ctrl.walkFrames = walkFrames;

            var sortingGroup = go.AddComponent<SortingGroup>();
            var sortController = go.AddComponent<EnemySortController>();
            WireSortController(sortController, sortingGroup);

            string dir = Path.GetDirectoryName(TargetPrefabPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                CreateFolderRecursive(dir);

            PrefabUtility.SaveAsPrefabAsset(go, TargetPrefabPath, out bool success);
            if (success)
                Debug.Log($"[SlimePrefabGen] Created {TargetPrefabPath} ({idleFrames.Length} idle frames, {walkFrames.Length} walk frames).");
            else
                Debug.LogError($"[SlimePrefabGen] SaveAsPrefabAsset failed for {TargetPrefabPath}.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void MigrateExistingPrefab()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(TargetPrefabPath);
        try
        {
            var legacy = contents.GetComponent<ElevationSorter>();
            if (legacy != null) Object.DestroyImmediate(legacy);

            var sortingGroup = contents.GetComponent<SortingGroup>();
            if (sortingGroup == null) sortingGroup = contents.AddComponent<SortingGroup>();

            var sortController = contents.GetComponent<EnemySortController>();
            if (sortController == null) sortController = contents.AddComponent<EnemySortController>();

            WireSortController(sortController, sortingGroup);

            PrefabUtility.SaveAsPrefabAsset(contents, TargetPrefabPath, out bool success);
            if (success)
                Debug.Log($"[SlimePrefabGen] Migrated {TargetPrefabPath} — ElevationSorter removed, SortingGroup + EnemySortController wired.");
            else
                Debug.LogError($"[SlimePrefabGen] SaveAsPrefabAsset failed while migrating {TargetPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // EnemySortController's sortSettings/sortingGroup are private [SerializeField]s with no public
    // setter (by design — wired once at authoring time). Only assigns when empty, so re-running the
    // menu command never clobbers a deliberately swapped IsoSortSettings asset.
    private static void WireSortController(EnemySortController sortController, SortingGroup sortingGroup)
    {
        var so = new SerializedObject(sortController);

        var sortSettingsProp = so.FindProperty("sortSettings");
        if (sortSettingsProp.objectReferenceValue == null)
        {
            var settings = AssetDatabase.LoadAssetAtPath<IsoSortSettings>(IsoSortSettingsPath);
            if (settings == null)
                Debug.LogError($"[SlimePrefabGen] IsoSortSettings asset not found at {IsoSortSettingsPath} — EnemySortController.sortSettings left unassigned, sorting will not update.");
            else
                sortSettingsProp.objectReferenceValue = settings;
        }

        var sortingGroupProp = so.FindProperty("sortingGroup");
        if (sortingGroupProp.objectReferenceValue == null)
            sortingGroupProp.objectReferenceValue = sortingGroup;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite[] LoadSortedSprites(string texturePath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(texturePath)
            .OfType<Sprite>()
            .OrderBy(s => s.name, System.StringComparer.Ordinal)
            .ToArray();
    }

    private static void CreateFolderRecursive(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
