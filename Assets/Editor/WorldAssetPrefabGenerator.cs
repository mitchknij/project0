// WorldAssetPrefabGenerator.cs — Editor tool that turns source sprites (e.g. Assets/Art/Tilesets/trees.png's
// sub-sprites) into world-object prefabs built on a shared Tree_Base profile (SortingGroup +
// WorldObjectSortController + ProjectedSpriteShadow + Visual/FootAnchor/ProjectedShadow children), reusing
// the existing ISO sorting/shadow architecture unmodified. Menu: Tools > IdleCloud > World Asset Prefab
// Generator.
//
// Safety: never edits source textures; never overwrites an existing prefab unless the user explicitly
// enables overwrite mode and confirms; overwrite mode only ever re-points the Visual sprite (all other
// hand-tuned values on that prefab variant are preserved); base-hierarchy creation is idempotent (checks
// transform.Find before adding any child/component).

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Iso.Sorting;

public class WorldAssetPrefabGenerator : EditorWindow
{
    private const string DefaultBasePrefabPath = "Assets/Prefabs/World/Tree_Base.prefab";
    private const string DefaultTargetFolder = "Assets/Prefabs/World/Trees";
    private const string WorldLitSortingLayer = "WorldLit";
    private const string LitMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";
    private const string TerrainBlockVisualPrefabPath = "Assets/Iso/Sorting/Prefabs/TerrainBlockVisual.prefab";
    private const string IsoSortSettingsPath = "Assets/Iso/Sorting/IsoSortSettings.asset";

    private enum SourceMode { Folder, SelectedObjects }
    private enum PlanAction { Create, Skip, Overwrite, Fail }

    private class PlanEntry
    {
        public Sprite sprite;
        public string prefabName;
        public string targetPath;
        public PlanAction action;
        public string reason;
    }

    private SourceMode sourceMode = SourceMode.Folder;
    private readonly List<Object> explicitSources = new List<Object>();
    private DefaultAsset sourceFolder;
    private string nameFilter = "trees_*";
    private GameObject basePrefab;
    private string targetFolder = DefaultTargetFolder;
    private bool overwriteMode;

    private List<PlanEntry> _plan = new List<PlanEntry>();
    private Vector2 _scroll;

    [MenuItem("Tools/IdleCloud/World Asset Prefab Generator")]
    public static void ShowWindow()
    {
        GetWindow<WorldAssetPrefabGenerator>("World Asset Prefab Generator");
    }

    private void OnEnable()
    {
        if (basePrefab == null)
            basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBasePrefabPath);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        sourceMode = (SourceMode)EditorGUILayout.EnumPopup("Mode", sourceMode);

        if (sourceMode == SourceMode.Folder)
        {
            sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField("Source Folder", sourceFolder, typeof(DefaultAsset), false);
            nameFilter = EditorGUILayout.TextField("Name Filter (e.g. trees_*)", nameFilter);
            EditorGUILayout.HelpBox("Scans the folder for Sprite sub-assets (e.g. trees.png's trees_0..trees_11) and Tile assets (resolved to their sprite), matched against the filter.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Drag individual Sprite or Tile assets below.", MessageType.Info);
            DrawExplicitSourcesList();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Base Prefab / Target", EditorStyles.boldLabel);
        basePrefab = (GameObject)EditorGUILayout.ObjectField("Base Prefab", basePrefab, typeof(GameObject), false);
        if (GUILayout.Button("Create/Update Base Prefab"))
            basePrefab = EnsureBasePrefab();
        targetFolder = EditorGUILayout.TextField("Target Prefab Folder", targetFolder);

        EditorGUILayout.Space();
        overwriteMode = EditorGUILayout.ToggleLeft("Overwrite existing prefabs (re-points sprite only)", overwriteMode);
        if (overwriteMode)
            EditorGUILayout.HelpBox("Overwrite mode only re-assigns the Visual sprite on existing prefabs — every other hand-tuned value (shadow settings, foot anchor, sort fields) is preserved. You will be asked to confirm before anything is written.", MessageType.Warning);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Dry Run / Preview")) _plan = BuildPlan();
            if (GUILayout.Button("Generate")) RunGenerate();
        }

        DrawPlanReport();
    }

    private void DrawExplicitSourcesList()
    {
        Object toAdd = EditorGUILayout.ObjectField("Add Sprite/Tile", null, typeof(Object), false);
        if (toAdd != null && !explicitSources.Contains(toAdd)) explicitSources.Add(toAdd);

        for (int i = explicitSources.Count - 1; i >= 0; i--)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(explicitSources[i], typeof(Object), false);
                if (GUILayout.Button("x", GUILayout.Width(20))) explicitSources.RemoveAt(i);
            }
        }
    }

    private void DrawPlanReport()
    {
        if (_plan == null || _plan.Count == 0) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
        foreach (var entry in _plan)
            EditorGUILayout.LabelField($"[{entry.action}] {entry.prefabName} — {entry.reason}");
        EditorGUILayout.EndScrollView();
    }

    // ── Base prefab ──────────────────────────────────────────────────────────

    private static GameObject EnsureBasePrefab()
    {
        string dir = Path.GetDirectoryName(DefaultBasePrefabPath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            CreateFolderRecursive(dir);

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBasePrefabPath);
        bool isNew = existingPrefab == null;
        GameObject root = isNew ? new GameObject("Tree_Base") : (GameObject)PrefabUtility.InstantiatePrefab(existingPrefab);

        try
        {
            EnsureBaseHierarchy(root);

            PrefabUtility.SaveAsPrefabAsset(root, DefaultBasePrefabPath, out bool ok);
            if (!ok)
            {
                Debug.LogError("[TreePrefabGen] Failed to save Tree_Base.prefab.");
                return existingPrefab;
            }
            Debug.Log($"[TreePrefabGen] {(isNew ? "Created" : "Updated")} base prefab: {DefaultBasePrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBasePrefabPath);
    }

    private static void EnsureBaseHierarchy(GameObject root)
    {
        var sortingGroup = root.GetComponent<SortingGroup>();
        if (sortingGroup == null) sortingGroup = root.AddComponent<SortingGroup>();
        // Set once at authoring time as well as at runtime (WorldObjectSortController.Recompute) — the
        // runtime path early-returns whenever it can't resolve a Grid (e.g. previewing the prefab asset
        // outside a scene), which left this stuck on "Default" until the object was actually placed.
        sortingGroup.sortingLayerName = WorldLitSortingLayer;
        var sortController = root.GetComponent<WorldObjectSortController>();
        if (sortController == null) sortController = root.AddComponent<WorldObjectSortController>();
        if (root.GetComponent<ProjectedSpriteShadow>() == null) root.AddComponent<ProjectedSpriteShadow>();
        WireSortController(sortController, sortingGroup);

        if (root.transform.Find("FootAnchor") == null)
        {
            var go = new GameObject("FootAnchor");
            go.transform.SetParent(root.transform, false);
        }

        Transform visual = root.transform.Find("Visual");
        if (visual == null)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(root.transform, false);
            visual = go.transform;
        }
        SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();
        if (visualRenderer == null) visualRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
        visualRenderer.sortingLayerName = WorldLitSortingLayer;
        // AddComponent<SpriteRenderer>() auto-assigns the built-in unlit "Sprites-Default" material — it's
        // never actually null — so that default (as well as an unset slot) must be swapped for the lit
        // material, or the sprite never responds to 2D lights. A deliberately assigned different material
        // is left alone.
        if (visualRenderer.sharedMaterial == null || visualRenderer.sharedMaterial.name == "Sprites-Default")
        {
            Material lit = ResolveLitSpriteMaterial();
            if (lit != null) visualRenderer.sharedMaterial = lit;
        }

        // ProjectedSpriteShadow.EnsureShadowRenderer (called from its own Awake) is idempotent via
        // transform.Find("ProjectedShadow") — pre-creating the child here just makes it visible in the
        // Editor immediately, without relying on Play mode / Awake to run first.
        Transform shadowChild = root.transform.Find("ProjectedShadow");
        if (shadowChild == null)
        {
            var go = new GameObject("ProjectedShadow");
            go.transform.SetParent(root.transform, false);
            shadowChild = go.transform;
        }
        if (shadowChild.GetComponent<SpriteRenderer>() == null)
            shadowChild.gameObject.AddComponent<SpriteRenderer>();
    }

    // WorldObjectSortController.sortSettings/sortingGroup are private [SerializeField]s with no public
    // setter (by design — they're wired once at authoring time, not touched at runtime). Only assigns
    // when empty, so re-running "Create/Update Base Prefab" never clobbers a deliberately swapped
    // IsoSortSettings asset.
    private static void WireSortController(WorldObjectSortController sortController, SortingGroup sortingGroup)
    {
        var so = new SerializedObject(sortController);

        var sortSettingsProp = so.FindProperty("sortSettings");
        if (sortSettingsProp.objectReferenceValue == null)
        {
            var settings = AssetDatabase.LoadAssetAtPath<IsoSortSettings>(IsoSortSettingsPath);
            if (settings == null)
                Debug.LogError($"[TreePrefabGen] IsoSortSettings asset not found at {IsoSortSettingsPath} — WorldObjectSortController.sortSettings left unassigned, sorting will not update.");
            else
                sortSettingsProp.objectReferenceValue = settings;
        }

        var sortingGroupProp = so.FindProperty("sortingGroup");
        if (sortingGroupProp.objectReferenceValue == null)
            sortingGroupProp.objectReferenceValue = sortingGroup;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material ResolveLitSpriteMaterial()
    {
        var terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerrainBlockVisualPrefabPath);
        if (terrainPrefab != null)
        {
            var sr = terrainPrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sharedMaterial != null) return sr.sharedMaterial;
        }

        string matPath = AssetDatabase.GUIDToAssetPath(LitMaterialGuid);
        return string.IsNullOrEmpty(matPath) ? null : AssetDatabase.LoadAssetAtPath<Material>(matPath);
    }

    // ── Source collection ────────────────────────────────────────────────────

    private List<Sprite> CollectSourceSprites()
    {
        if (sourceMode == SourceMode.SelectedObjects)
        {
            var result = new List<Sprite>();
            var seen = new HashSet<Sprite>();
            foreach (var obj in explicitSources)
            {
                Sprite sprite = ResolveSprite(obj);
                if (sprite != null && seen.Add(sprite)) result.Add(sprite);
            }
            return result;
        }

        if (sourceFolder == null) return new List<Sprite>();
        string folderPath = AssetDatabase.GetAssetPath(sourceFolder);
        return CollectSpritesFromFolder(folderPath, nameFilter);
    }

    private static Sprite ResolveSprite(Object obj)
    {
        if (obj is Sprite sprite) return sprite;
        if (obj is Tile tile) return tile.sprite;
        return null;
    }

    private static List<Sprite> CollectSpritesFromFolder(string folderPath, string filter)
    {
        var result = new List<Sprite>();
        var seen = new HashSet<Sprite>();

        string[] guids = AssetDatabase.FindAssets("t:Texture2D t:Tile", new[] { folderPath });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                Sprite sprite = ResolveSprite(obj);
                if (sprite == null || !GlobMatch(sprite.name, filter)) continue;
                if (seen.Add(sprite)) result.Add(sprite);
            }
        }

        result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return result;
    }

    private static bool GlobMatch(string name, string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return true;
        string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
    }

    // trees_0 -> Tree_0 (naive singularize of the prefix before the first underscore, then PascalCase).
    private static string ToPrefabName(string spriteName)
    {
        int underscoreIndex = spriteName.IndexOf('_');
        string prefix = underscoreIndex >= 0 ? spriteName.Substring(0, underscoreIndex) : spriteName;
        string suffix = underscoreIndex >= 0 ? spriteName.Substring(underscoreIndex) : string.Empty;

        if (prefix.Length > 1 && prefix.EndsWith("s"))
            prefix = prefix.Substring(0, prefix.Length - 1);

        return char.ToUpperInvariant(prefix[0]) + prefix.Substring(1) + suffix;
    }

    // ── Plan / generate ──────────────────────────────────────────────────────

    private List<PlanEntry> BuildPlan()
    {
        var entries = new List<PlanEntry>();
        var sprites = CollectSourceSprites();
        string folder = targetFolder.TrimEnd('/');

        foreach (var sprite in sprites)
        {
            string prefabName = ToPrefabName(sprite.name);
            string path = $"{folder}/{prefabName}.prefab";

            if (basePrefab == null)
            {
                entries.Add(new PlanEntry { sprite = sprite, prefabName = prefabName, targetPath = path, action = PlanAction.Fail, reason = "No base prefab assigned/created" });
                continue;
            }

            if (File.Exists(path))
            {
                entries.Add(new PlanEntry
                {
                    sprite = sprite,
                    prefabName = prefabName,
                    targetPath = path,
                    action = overwriteMode ? PlanAction.Overwrite : PlanAction.Skip,
                    reason = overwriteMode ? "exists — will overwrite sprite only" : "exists — skipped (enable overwrite mode to change)"
                });
            }
            else
            {
                entries.Add(new PlanEntry { sprite = sprite, prefabName = prefabName, targetPath = path, action = PlanAction.Create, reason = "new" });
            }
        }
        return entries;
    }

    private void RunGenerate()
    {
        _plan = BuildPlan();

        var toOverwrite = _plan.Where(p => p.action == PlanAction.Overwrite).ToList();
        if (toOverwrite.Count > 0)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Overwrite existing tree prefabs?",
                $"{toOverwrite.Count} prefab(s) already exist and will have their Visual sprite re-pointed. All other hand-tuned values (shadow settings, foot anchor, sort fields) are preserved.\n\nContinue?",
                "Overwrite", "Cancel");
            if (!confirmed)
                foreach (var p in toOverwrite) { p.action = PlanAction.Skip; p.reason = "overwrite cancelled by user"; }
        }

        int created = 0, overwritten = 0, skipped = 0, failed = 0;
        foreach (var entry in _plan)
        {
            switch (entry.action)
            {
                case PlanAction.Skip:
                    skipped++;
                    break;
                case PlanAction.Fail:
                    failed++;
                    Debug.LogError($"[TreePrefabGen] Failed: {entry.prefabName} — {entry.reason}");
                    break;
                case PlanAction.Create:
                {
                    string error = GenerateOne(entry.sprite, entry.prefabName, entry.targetPath);
                    if (error == null) { entry.reason = "created"; created++; }
                    else { entry.action = PlanAction.Fail; entry.reason = error; failed++; Debug.LogError($"[TreePrefabGen] Failed: {entry.prefabName} — {error}"); }
                    break;
                }
                case PlanAction.Overwrite:
                {
                    string error = TryOverwriteSprite(entry.targetPath, entry.sprite);
                    if (error == null) { entry.reason = "sprite re-pointed"; overwritten++; }
                    else { entry.action = PlanAction.Fail; entry.reason = error; failed++; Debug.LogError($"[TreePrefabGen] Failed: {entry.prefabName} — {error}"); }
                    break;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TreePrefabGen] Done — created {created}, overwritten {overwritten}, skipped {skipped}, failed {failed}.");
    }

    private string GenerateOne(Sprite sprite, string prefabName, string targetPath)
    {
        GameObject instance = null;
        try
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = prefabName;

            Transform visual = instance.transform.Find("Visual");
            SpriteRenderer sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (sr == null) return "Base prefab has no Visual/SpriteRenderer child";
            sr.sprite = sprite;

            string folder = Path.GetDirectoryName(targetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                CreateFolderRecursive(folder);

            PrefabUtility.SaveAsPrefabAsset(instance, targetPath, out bool ok);
            return ok ? null : "SaveAsPrefabAsset failed";
        }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance);
        }
    }

    private static string TryOverwriteSprite(string prefabPath, Sprite sprite)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform visual = contents.transform.Find("Visual");
            SpriteRenderer sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (sr == null) return "Visual/SpriteRenderer child not found";
            sr.sprite = sprite;

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath, out bool ok);
            return ok ? null : "SaveAsPrefabAsset failed";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
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
