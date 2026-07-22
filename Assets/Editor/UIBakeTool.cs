// UIBakeTool.cs — Editor-menu's om de code-gegenereerde UI te bakken naar een prefab
// met echte asset-referenties (sprites/fonts), zodat de UI in de editor bewerkbaar is.
// Menu: IdleCloud > UI > Export Theme Assets / Bake UI Prefab / Place GameUI In Scene.

using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using IdleCloud.UI;

public static class UIBakeTool
{
    private const string GenDir     = "Assets/Art/UI/Generated";
    private const string FontDir    = "Assets/Art/UI/Generated/Fonts";
    private const string PrefabDir  = "Assets/Prefabs/UI";
    private const string PrefabPath = "Assets/Prefabs/UI/GameUI.prefab";

    // ── Export ────────────────────────────────────────────────────────────────

    [MenuItem("IdleCloud/UI/Export Theme Assets")]
    public static void ExportThemeAssets()
    {
        int sprites = ExportSprites();
        int fonts   = ExportFonts();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[UIBake] Exported {sprites} sprites and {fonts} font assets (existing fonts reused).");
    }

    private static (string name, Sprite sprite)[] ThemeFrames() => new[]
    {
        ("PanelFrame",  UITheme.PanelFrame),
        ("ButtonFrame", UITheme.ButtonFrame),
        ("SlotFrame",   UITheme.SlotFrame),
        ("InsetFrame",  UITheme.InsetFrame),
        ("BarFrame",    UITheme.BarFrame),
        ("BarFill",     UITheme.BarFill),
    };

    private static int ExportSprites()
    {
        Directory.CreateDirectory(GenDir);
        int count = 0;
        foreach (var (name, sprite) in ThemeFrames())
        {
            // PNG-bytes overschrijven is veilig: guid blijft behouden zolang de .meta blijft.
            string path = $"{GenDir}/{name}.png";
            File.WriteAllBytes(path, sprite.texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode          = FilterMode.Point;
            importer.mipmapEnabled       = false;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.spriteBorder        = sprite.border;
            importer.SaveAndReimport();
            count++;
        }
        return count;
    }

    private static readonly (string assetName, string ttfPath)[] Fonts =
    {
        ("PressStart2P SDF", "Assets/Resources/UI/Fonts/PressStart2P-Regular.ttf"),
        ("VT323 SDF",        "Assets/Resources/UI/Fonts/VT323-Regular.ttf"),
    };

    private static int ExportFonts()
    {
        Directory.CreateDirectory(FontDir);
        int created = 0;
        foreach (var (assetName, ttfPath) in Fonts)
        {
            string assetPath = $"{FontDir}/{assetName}.asset";

            // Bestaand font-asset hergebruiken: verwijderen zou de guid (en dus alle
            // prefab-referenties) breken.
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) continue;

            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogError($"[UIBake] TTF not found: {ttfPath}");
                continue;
            }

            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9,
                GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            if (fa == null)
            {
                Debug.LogError($"[UIBake] CreateFontAsset failed for {ttfPath}");
                continue;
            }

            fa.name = assetName;
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.material.name     = assetName + " Material";
            fa.atlasTexture.name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
            if (TMP_Settings.defaultFontAsset != null)
                fa.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            EditorUtility.SetDirty(fa);
            created++;
        }
        return created;
    }

    // ── Bake ──────────────────────────────────────────────────────────────────

    [MenuItem("IdleCloud/UI/Bake UI Prefab")]
    public static void BakeUIPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("UI Bake", "Cannot bake while in Play mode.", "OK");
            return;
        }
        if (File.Exists(PrefabPath) &&
            !EditorUtility.DisplayDialog("Rebake UI?",
                "Assets/Prefabs/UI/GameUI.prefab already exists.\n\nRebaking REPLACES the prefab — all manual changes will be LOST.",
                "Rebake", "Cancel"))
            return;

        ExportThemeAssets();

        // Runtime-object -> asset mapping (identiteit: dezelfde UITheme statics als BuildAll gebruikt)
        var spriteMap = new Dictionary<Sprite, Sprite>();
        foreach (var (name, runtimeSprite) in ThemeFrames())
        {
            var asset = AssetDatabase.LoadAssetAtPath<Sprite>($"{GenDir}/{name}.png");
            if (asset == null)
            {
                Debug.LogError($"[UIBake] Aborted: exported sprite is missing: {GenDir}/{name}.png");
                return;
            }
            if (!spriteMap.ContainsKey(runtimeSprite)) spriteMap.Add(runtimeSprite, asset);
        }

        var fontMap = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        AddFontMapping(fontMap, UITheme.HeaderFont, $"{FontDir}/PressStart2P SDF.asset");
        AddFontMapping(fontMap, UITheme.BodyFont,   $"{FontDir}/VT323 SDF.asset");

        GameObject root = null;
        try
        {
            root = new GameObject("GameUI");
            UIBuilder.BuildAll(root.transform);

            int unmapped = 0;
            int invalidPersistent = 0;
            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                var s = img.sprite;
                if (s == null) continue;
                if (EditorUtility.IsPersistent(s))
                {
                    if (!ValidatePersistentAsset(s, GenDir, "sprite", img.transform)) invalidPersistent++;
                    continue;
                }
                if (spriteMap.TryGetValue(s, out var asset)) img.sprite = asset;
                else { Debug.LogError($"[UIBake] Unmapped runtime sprite at '{PathOf(img.transform)}'", img); unmapped++; }
            }
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                var f = tmp.font;
                if (f == null) continue;
                if (EditorUtility.IsPersistent(f))
                {
                    if (!ValidatePersistentAsset(f, FontDir, "font", tmp.transform)) invalidPersistent++;
                    continue;
                }
                if (fontMap.TryGetValue(f, out var asset)) tmp.font = asset;
                else { Debug.LogError($"[UIBake] Unmapped runtime font at '{PathOf(tmp.transform)}'", tmp); unmapped++; }
            }

            if (unmapped > 0 || invalidPersistent > 0)
            {
                Debug.LogError($"[UIBake] Aborted: {unmapped} unmapped runtime references and {invalidPersistent} invalid persistent asset references — prefab NOT saved.");
                return;
            }

            Directory.CreateDirectory(PrefabDir);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            if (!ok)
            {
                Debug.LogError("[UIBake] SaveAsPrefabAsset failed.");
                return;
            }
            Debug.Log($"[UIBake] Prefab saved: {PrefabPath}");
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        PlaceGameUIInScene();
    }

    private static void AddFontMapping(Dictionary<TMP_FontAsset, TMP_FontAsset> map,
        TMP_FontAsset runtimeFont, string assetPath)
    {
        if (runtimeFont == null || EditorUtility.IsPersistent(runtimeFont)) return; // validated on the built hierarchy
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (asset == null)
        {
            Debug.LogError($"[UIBake] Font asset missing: {assetPath}");
            return;
        }
        if (!map.ContainsKey(runtimeFont)) map.Add(runtimeFont, asset);
    }

    // ── Plaatsen in scene ────────────────────────────────────────────────────

    [MenuItem("IdleCloud/UI/Place GameUI In Scene")]
    public static void PlaceGameUIInScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[UIBake] Prefab not found: {PrefabPath} — run 'Bake UI Prefab' first.");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            UIInputBootstrap.EnsureEventSystem();
            EditorSceneManager.MarkSceneDirty(scene);
        }
        foreach (var rootGO in scene.GetRootGameObjects())
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(rootGO) == prefab)
            {
                Selection.activeGameObject = rootGO;
                Debug.Log("[UIBake] GameUI is already in the scene — selected.");
                return;
            }
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(inst, "Place GameUI");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = inst;
        Debug.Log("[UIBake] GameUI placed in the scene — save the scene (Ctrl+S).");
    }

    private static bool ValidatePersistentAsset(UnityEngine.Object asset, string allowedRoot,
        string assetKind, Transform owner)
    {
        string assetPath = AssetDatabase.GetAssetPath(asset);
        bool isBuiltIn = string.IsNullOrEmpty(assetPath) ||
            assetPath.IndexOf("unity_builtin_extra", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool isAllowed = !isBuiltIn &&
            assetPath.StartsWith(allowedRoot + "/", System.StringComparison.Ordinal);
        if (isAllowed) return true;

        string displayPath = string.IsNullOrEmpty(assetPath) ? "<empty>" : assetPath;
        string reason = isBuiltIn
            ? "Unity built-in assets are not allowed"
            : $"asset must be under '{allowedRoot}/'";
        Debug.LogError($"[UIBake] Aborted: persistent {assetKind} at '{PathOf(owner)}' uses '{displayPath}'; {reason}.", owner);
        return false;
    }

    private static string PathOf(Transform t)
        => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
