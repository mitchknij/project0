using System;
using System.Collections.Generic;
using System.Reflection;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.View;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace IdleCloud.Tests
{
    /// <summary>
    /// Guards the data-to-scene contract. A map may only be reachable when it has a
    /// registered, buildable scene with a matching context and arrival point.
    /// </summary>
    public class MapSceneRegressionTests
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string CatalogPath = "Assets/Settings/MapSceneCatalog.asset";
        private const string MapTeleporterPrefabPath = "Assets/Prefabs/World/MapTeleporter.prefab";
        private const string MapRespawnPointPrefabPath = "Assets/Prefabs/World/MapRespawnPoint.prefab";
        private const string CityServiceStationPrefabPath = "Assets/Prefabs/World/CityServiceStation.prefab";

        [SetUp]
        public void UseProductionContent()
        {
            RuntimeContent.UseLegacyContentForTests();
        }

        [TearDown]
        public void RestoreProductionContent()
        {
            RuntimeContent.UseLegacyContentForTests();
        }

        [Test]
        public void PlayableMapGraph_IsBidirectionalAndEveryReachableMapHasOneCatalogEntry()
        {
            MapSceneCatalog catalog = LoadCatalog();
            HashSet<string> reachable = FindReachableMapIds();
            HashSet<string> catalogIds = CatalogMapIds(catalog);

            Assert.That(catalogIds.SetEquals(reachable), Is.True,
                "The scene catalog must contain exactly the maps the player can reach from the starting map.");

            foreach (string mapId in reachable)
            {
                MapDef map = MapsRepo.Get(mapId);
                Assert.That(map, Is.Not.Null, mapId + " is reachable but has no MapDef.");
                foreach (string connectionId in map.Connections)
                {
                    MapDef destination = MapsRepo.Get(connectionId);
                    Assert.That(destination, Is.Not.Null,
                        $"{mapId} connects to missing map '{connectionId}'.");
                    Assert.That(destination.Connections, Does.Contain(mapId),
                        $"Connection {mapId} -> {connectionId} is not reversible.");
                }
            }
        }

        [Test]
        public void EveryPlayableMapScene_HasMatchingContextSpawnAndBuildSettingsEntry()
        {
            MapSceneCatalog catalog = LoadCatalog();
            HashSet<string> buildScenePaths = BuildScenePaths();

            foreach (string mapId in FindReachableMapIds())
            {
                Assert.That(catalog.TryGet(mapId, out MapSceneCatalog.Entry entry), Is.True,
                    "Missing catalog entry for " + mapId);
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.scenePath), Is.Not.Null,
                    $"{mapId} points at a missing scene asset: {entry.scenePath}");
                Assert.That(buildScenePaths, Does.Contain(entry.scenePath),
                    $"{mapId} scene is not enabled in Build Settings: {entry.scenePath}");

                Scene scene = SceneManager.GetSceneByPath(entry.scenePath);
                bool openedForTest = !scene.isLoaded;
                if (openedForTest)
                    scene = EditorSceneManager.OpenScene(entry.scenePath, OpenSceneMode.Additive);
                try
                {
                    WorldMapContext[] contexts = FindInScene<WorldMapContext>(scene);
                    Assert.That(contexts, Has.Length.EqualTo(1),
                        $"{entry.scenePath} must contain exactly one WorldMapContext.");
                    Assert.That(contexts[0].MapId, Is.EqualTo(mapId),
                        $"Catalog map ID and scene context disagree for {entry.scenePath}.");
                    Assert.That(contexts[0].ValidateConfiguration(), Is.Empty,
                        $"{entry.scenePath} has invalid authored combat or gathering content.");

                    Grid[] grids = FindInScene<Grid>(scene);
                    Assert.That(grids, Is.Not.Empty,
                        $"{entry.scenePath} has no Grid for SceneLoader to attach pathfinding to.");

                    MapSpawnPoint[] spawns = FindInScene<MapSpawnPoint>(scene);
                    Assert.That(Array.Exists(spawns, spawn => spawn.SpawnId == entry.defaultSpawnId), Is.True,
                        $"{entry.scenePath} has no '{entry.defaultSpawnId}' arrival point.");
                    Assert.That(contexts[0].PlayerSpawnPoint, Is.Not.Null,
                        $"{entry.scenePath} has no player respawn point or default arrival marker.");
                }
                finally
                {
                    if (openedForTest) EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        [Test]
        public void Bootstrap_StartsAtTheCatalogStartingMapAndUsesTheTransitionCatalog()
        {
            MapSceneCatalog catalog = LoadCatalog();
            Assert.That(catalog.TryGet(MapsRepo.StartingMapId, out MapSceneCatalog.Entry startingMap), Is.True);

            Scene bootstrap = SceneManager.GetSceneByPath(BootstrapScenePath);
            bool openedForTest = !bootstrap.isLoaded;
            if (openedForTest)
                bootstrap = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Additive);
            try
            {
                SceneLoader[] loaders = FindInScene<SceneLoader>(bootstrap);
                Assert.That(loaders, Has.Length.EqualTo(1), "Bootstrap needs exactly one SceneLoader.");
                var loaderData = new SerializedObject(loaders[0]);
                Assert.That(loaderData.FindProperty("startingMapScenePath").stringValue, Is.EqualTo(startingMap.scenePath));

                MapTransitionCoordinator[] coordinators = FindInScene<MapTransitionCoordinator>(bootstrap);
                Assert.That(coordinators, Has.Length.EqualTo(1), "Bootstrap needs exactly one MapTransitionCoordinator.");
                var coordinatorData = new SerializedObject(coordinators[0]);
                Assert.That(coordinatorData.FindProperty("mapCatalog").objectReferenceValue, Is.EqualTo(catalog));
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(bootstrap, removeScene: true);
            }
        }

        [Test]
        public void SharedFallbackMonster_IsAvailableOnEveryPlayableMap()
        {
            MonsterDef sharedMonster = MonstersRepo.Get("world_slime");
            Assert.That(sharedMonster, Is.Not.Null);
            Assert.That(sharedMonster.MapId, Is.EqualTo(MapScope.AnyMap));

            ResourceNodeDef sharedTree = NodesRepo.Get("world_oak_tree");
            ResourceNodeDef sharedOre = NodesRepo.Get("world_copper_vein");
            Assert.That(sharedTree, Is.Not.Null);
            Assert.That(sharedOre, Is.Not.Null);

            foreach (string mapId in FindReachableMapIds())
            {
                Assert.That(MapScope.Includes(sharedMonster.MapId, mapId), Is.True, mapId);
                Assert.That(MapScope.Includes(sharedTree.MapId, mapId), Is.True, mapId);
                Assert.That(MapScope.Includes(sharedOre.MapId, mapId), Is.True, mapId);
            }
        }

        [Test]
        public void MapTeleporterPrefab_HasAnEnabledTriggerAndPortalComponent()
        {
            GameObject teleporter = AssetDatabase.LoadAssetAtPath<GameObject>(MapTeleporterPrefabPath);
            Assert.That(teleporter, Is.Not.Null);
            Assert.That(teleporter.GetComponent<MapPortal>(), Is.Not.Null);

            Collider2D trigger = teleporter.GetComponent<Collider2D>();
            Assert.That(trigger, Is.Not.Null);
            Assert.That(trigger.isTrigger, Is.True);
            Assert.That(trigger.enabled, Is.True);
        }

        [Test]
        public void MapRespawnPointPrefab_HasTheDefaultArrivalMarker()
        {
            GameObject respawnPoint = AssetDatabase.LoadAssetAtPath<GameObject>(MapRespawnPointPrefabPath);
            Assert.That(respawnPoint, Is.Not.Null);

            MapSpawnPoint marker = respawnPoint.GetComponent<MapSpawnPoint>();
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.SpawnId, Is.EqualTo("default"));
        }

        [Test]
        public void CityServiceStationPrefab_HasAnEnabledTriggerAndDefaultsToBank()
        {
            GameObject station = AssetDatabase.LoadAssetAtPath<GameObject>(CityServiceStationPrefabPath);
            Assert.That(station, Is.Not.Null);

            Component service = station.GetComponent("CityServiceStation");
            Assert.That(service, Is.Not.Null);
            var serviceData = new SerializedObject(service);
            Assert.That(serviceData.FindProperty("serviceKind").enumValueIndex,
                Is.EqualTo((int)HubServiceKind.Bank));

            Collider2D trigger = station.GetComponent<Collider2D>();
            Assert.That(trigger, Is.Not.Null);
            Assert.That(trigger.enabled, Is.True);
            Assert.That(trigger.isTrigger, Is.True);
        }

        [Test]
        public void CityServiceStationPrefab_CanPersistEveryDefinedServiceKind()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CityServiceStationPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Component station = instance.GetComponent("CityServiceStation");
                Assert.That(station, Is.Not.Null);
                SerializedProperty serviceKind = new SerializedObject(station).FindProperty("serviceKind");
                Assert.That(serviceKind, Is.Not.Null);

                PropertyInfo exposedKind = station.GetType().GetProperty("ServiceKind");
                Assert.That(exposedKind, Is.Not.Null);
                foreach (HubServiceKind kind in Enum.GetValues(typeof(HubServiceKind)))
                {
                    serviceKind.enumValueIndex = (int)kind;
                    serviceKind.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    Assert.That((HubServiceKind)exposedKind.GetValue(station), Is.EqualTo(kind));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CityServiceStationPanelResolution_MapsEveryServiceToAnIntentionalOutcome()
        {
            Type hudType = FindLoadedType("IdleCloud.UI.MainHudPanel");
            Type bankType = FindLoadedType("IdleCloud.UI.BankPanel");
            Type craftingType = FindLoadedType("IdleCloud.UI.CraftingPanel");
            Type travelType = FindLoadedType("IdleCloud.UI.TravelPanel");
            Assert.That(hudType, Is.Not.Null);
            Assert.That(bankType, Is.Not.Null);
            Assert.That(craftingType, Is.Not.Null);
            Assert.That(travelType, Is.Not.Null);

            GameObject root = new GameObject("CityServiceStationTestHud");
            try
            {
                Component hud = root.AddComponent(hudType);
                var expectedPanels = new Dictionary<HubServiceKind, Component>
                {
                    { HubServiceKind.Bank, root.AddComponent(bankType) },
                    { HubServiceKind.Crafting, root.AddComponent(craftingType) },
                    { HubServiceKind.Teleport, root.AddComponent(travelType) },
                };
                hudType.GetField("bankPanel").SetValue(hud, expectedPanels[HubServiceKind.Bank]);
                hudType.GetField("craftingPanel").SetValue(hud, expectedPanels[HubServiceKind.Crafting]);
                hudType.GetField("travelPanel").SetValue(hud, expectedPanels[HubServiceKind.Teleport]);

                GameObject stationObject = AssetDatabase.LoadAssetAtPath<GameObject>(CityServiceStationPrefabPath);
                Component station = stationObject.GetComponent("CityServiceStation");
                MethodInfo resolvePanel = station.GetType().GetMethod("ResolvePanel",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(resolvePanel, Is.Not.Null);

                foreach (HubServiceKind kind in Enum.GetValues(typeof(HubServiceKind)))
                {
                    object resolved = resolvePanel.Invoke(null, new object[] { hud, kind });
                    if (expectedPanels.TryGetValue(kind, out Component expected))
                        Assert.That(resolved, Is.SameAs(expected), kind + " must open its matching HUD panel.");
                    else
                        Assert.That(resolved, Is.Null, kind + " must intentionally wait for its dedicated UI.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static MapSceneCatalog LoadCatalog()
        {
            MapSceneCatalog catalog = AssetDatabase.LoadAssetAtPath<MapSceneCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, "Map scene catalog asset is missing.");
            return catalog;
        }

        private static Type FindLoadedType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }

        private static HashSet<string> CatalogMapIds(MapSceneCatalog catalog)
        {
            SerializedProperty entries = new SerializedObject(catalog).FindProperty("entries");
            var ids = new HashSet<string>();
            for (int index = 0; index < entries.arraySize; index++)
            {
                string mapId = entries.GetArrayElementAtIndex(index).FindPropertyRelative("mapId").stringValue;
                Assert.That(ids.Add(mapId), Is.True, "Duplicate scene catalog map ID: " + mapId);
            }
            return ids;
        }

        private static HashSet<string> FindReachableMapIds()
        {
            var reachable = new HashSet<string>();
            var pending = new Queue<string>();
            pending.Enqueue(MapsRepo.StartingMapId);
            while (pending.Count > 0)
            {
                string mapId = pending.Dequeue();
                if (!reachable.Add(mapId)) continue;

                MapDef map = MapsRepo.Get(mapId);
                Assert.That(map, Is.Not.Null, "Missing map in traversal: " + mapId);
                foreach (string connectionId in map.Connections)
                    pending.Enqueue(connectionId);
            }
            return reachable;
        }

        private static HashSet<string> BuildScenePaths()
        {
            var paths = new HashSet<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled) paths.Add(scene.path);
            return paths;
        }

        private static T[] FindInScene<T>(Scene scene) where T : Component
        {
            var results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                results.AddRange(root.GetComponentsInChildren<T>(includeInactive: true));
            return results.ToArray();
        }
    }
}
