using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using IdleCloud.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace IdleCloud.Tests
{
    public class GameplayLoopSmokeTests
    {
        [UnityTest]
        public IEnumerator CombatCommand_PublishesManagerResultAndMovementRequest()
        {
            GameManager manager = CreateManager();
            bool eventReceived = false;
            manager.ActiveCombatResolved += _ => eventReceived = true;

            Assert.That(manager.StartActiveCombat("slime"), Is.True);
            ActiveCombatTickResult result = manager.TickActiveCombat(
                "playmode.slime.01",
                "slime",
                new CombatWorldFacts
                {
                    TargetAvailable = true,
                    TargetInRange = false,
                    LineOfSight = true,
                    Distance = 3.0,
                },
                new List<CombatCommand>(),
                System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            Assert.That(eventReceived, Is.True);
            Assert.That(result.Simulation.Events.Exists(item => item.Kind == CombatEventKind.MovementRequested), Is.True);
            yield return DestroyManager(manager);
        }

        [UnityTest]
        public IEnumerator GatheringNodeSelection_AssignsTheCanonicalHarvestActivity()
        {
            GameManager manager = CreateManager();
            var nodeObject = new GameObject("PlayModeGatheringNode");
            nodeObject.AddComponent<CircleCollider2D>();
            GatheringNodeView node = nodeObject.AddComponent<GatheringNodeView>();
            node.Configure("playmode.wildflower.01", "wildflower_patch");

            node.Select();
            Character selected = manager.GetSelectedCharacter();

            Assert.That(selected.Activity.Kind, Is.EqualTo(ActivityKind.Gathering));
            Assert.That(selected.Activity.TargetId, Is.EqualTo("wildflower_patch"));
            Object.Destroy(nodeObject);
            yield return DestroyManager(manager);
        }

        [UnityTest]
        public IEnumerator CombatView_SelectedPlacedTarget_BecomesTheActiveWorldTarget()
        {
            var playerObject = new GameObject("PlayModePlayer");
            playerObject.AddComponent<Rigidbody2D>();
            PlayerController player = playerObject.AddComponent<PlayerController>();
            var targetObject = new GameObject("PlayModeSlime");
            targetObject.AddComponent<CircleCollider2D>();
            CombatTargetView target = targetObject.AddComponent<CombatTargetView>();
            target.ConfigureRuntimeIdentity("playmode.slime.01", "slime");
            var combatObject = new GameObject("PlayModeCombatView");
            CombatView combatView = combatObject.AddComponent<CombatView>();
            combatView.Configure(player, new[] { target });

            player.SelectCombatTarget(target);

            Assert.That(combatView.CurrentTarget, Is.SameAs(target));
            Object.Destroy(combatObject);
            Object.Destroy(targetObject);
            Object.Destroy(playerObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CombatView_ManualMovementClearsSelectedTargetUntilExplicitReselection()
        {
            GameManager manager = CreateManager();
            var playerObject = new GameObject("ManualMovePlayer");
            playerObject.AddComponent<Rigidbody2D>();
            PlayerController player = playerObject.AddComponent<PlayerController>();
            CombatTargetView target = CreateCombatTarget("ManualMoveSlime", "manual.slime.01");
            var combatObject = new GameObject("ManualMoveCombatView");
            CombatView combatView = combatObject.AddComponent<CombatView>();
            combatView.Configure(player, new[] { target });

            player.SelectCombatTarget(target);
            Assert.That(combatView.CurrentTarget, Is.SameAs(target));

            combatView.CancelCombatForManualMovement();
            Assert.That(combatView.CurrentTarget, Is.Null);

            yield return null;
            Assert.That(combatView.CurrentTarget, Is.Null,
                "Auto target acquisition must remain suspended after a manual walk.");

            manager.ToggleAutoCombat();
            yield return null;
            manager.ToggleAutoCombat();
            yield return null;
            Assert.That(combatView.CurrentTarget, Is.SameAs(target));

            Object.Destroy(combatObject);
            Object.Destroy(target.gameObject);
            Object.Destroy(playerObject);
            yield return DestroyManager(manager);
        }

        [UnityTest]
        public IEnumerator AutoToggleOff_CombatViewAcquiresNoTarget()
        {
            GameManager manager = CreateManager();
            var playerObject = new GameObject("AutoTogglePlayer");
            playerObject.AddComponent<Rigidbody2D>();
            PlayerController player = playerObject.AddComponent<PlayerController>();
            var targetObject = new GameObject("AutoToggleSlime");
            targetObject.AddComponent<CircleCollider2D>();
            CombatTargetView target = targetObject.AddComponent<CombatTargetView>();
            target.ConfigureRuntimeIdentity("playmode.slime.auto", "slime");
            var combatObject = new GameObject("AutoToggleCombat");
            CombatView combatView = combatObject.AddComponent<CombatView>();
            combatView.Configure(player, new[] { target });

            manager.ToggleAutoCombat();
            yield return null;
            Assert.That(combatView.CurrentTarget, Is.Null);
            manager.ToggleAutoCombat();
            yield return null;
            Assert.That(combatView.CurrentTarget, Is.SameAs(target));

            Object.Destroy(combatObject);
            Object.Destroy(targetObject);
            Object.Destroy(playerObject);
            yield return DestroyManager(manager);
        }

        [UnityTest]
        public IEnumerator MainHud_AutoButtonBindsOnFirstPanelEnable()
        {
            GameManager manager = CreateManager();
            var hudObject = new GameObject("CombatHud");
            hudObject.SetActive(false);
            System.Type hudType = System.Type.GetType("IdleCloud.UI.MainHudPanel, IdleCloud.UI");
            System.Type textType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Assert.That(hudType, Is.Not.Null);
            Assert.That(textType, Is.Not.Null);
            Component hud = hudObject.AddComponent(hudType);
            Button autoButton = CreateButton(hudObject.transform, "AutoButton");

            SetHudField(hudType, hud, "nameLabel", CreateComponent(hudObject.transform, "Name", textType));
            SetHudField(hudType, hud, "hpFill", CreateImage(hudObject.transform, "HpFill"));
            SetHudField(hudType, hud, "hpLabel", CreateComponent(hudObject.transform, "Hp", textType));
            SetHudField(hudType, hud, "xpFill", CreateImage(hudObject.transform, "XpFill"));
            SetHudField(hudType, hud, "xpLabel", CreateComponent(hudObject.transform, "Xp", textType));
            SetHudField(hudType, hud, "goldLabel", CreateComponent(hudObject.transform, "Gold", textType));
            SetHudField(hudType, hud, "mapLabel", CreateComponent(hudObject.transform, "Map", textType));
            SetHudField(hudType, hud, "activityLabel", CreateComponent(hudObject.transform, "Activity", textType));
            SetHudField(hudType, hud, "autoToggleButton", autoButton);
            SetHudField(hudType, hud, "autoToggleLabel", CreateComponent(hudObject.transform, "AutoLabel", textType));

            hudObject.SetActive(true);
            autoButton.onClick.Invoke();

            Assert.That(manager.AutoCombatEnabled, Is.False);
            Object.Destroy(hudObject);
            yield return DestroyManager(manager);
        }

        [UnityTest]
        public IEnumerator KillReward_AdvancesToNextAvailableTarget()
        {
            GameManager manager = CreateManager();
            var playerObject = new GameObject("AdvancePlayer");
            playerObject.AddComponent<Rigidbody2D>();
            PlayerController player = playerObject.AddComponent<PlayerController>();
            CombatTargetView first = CreateCombatTarget("AdvanceFirst", "playmode.slime.first");
            CombatTargetView second = CreateCombatTarget("AdvanceSecond", "playmode.slime.second");
            second.transform.position = new Vector3(1f, 0f, 0f);
            var combatObject = new GameObject("AdvanceCombat");
            CombatView combatView = combatObject.AddComponent<CombatView>();
            combatView.Configure(player, new[] { first, second });

            yield return null;
            Assert.That(combatView.CurrentTarget, Is.SameAs(first));
            first.Defeat();
            yield return null;
            yield return null;
            Assert.That(combatView.CurrentTarget, Is.SameAs(second));

            Object.Destroy(combatObject);
            Object.Destroy(first.gameObject);
            Object.Destroy(second.gameObject);
            Object.Destroy(playerObject);
            yield return DestroyManager(manager);
        }

        [UnityTest]
        public IEnumerator SceneMapEfficiency_RefreshesTheActiveCombatSnapshot()
        {
            GameManager manager = CreateManager();
            Assert.That(manager.StartActiveCombat("slime"), Is.True);

            Assert.That(manager.ConfigureSceneMapEfficiency("grass_1", 0.5, 3000.0), Is.True);
            EfficiencySnapshot snapshot = manager.GetSelectedCharacter().Efficiency;

            Assert.That(snapshot.MapDensity, Is.EqualTo(0.5));
            Assert.That(snapshot.TravelOverheadMs, Is.EqualTo(3000.0));
            yield return DestroyManager(manager);
        }

        [UnityTest]
        public IEnumerator CombatSpatialAdapter_CapturesLogicalActorsWithoutSpriteGeometry()
        {
            var playerObject = new GameObject("SpatialPlayer");
            playerObject.transform.position = new Vector3(2f, 3f, 0f);
            playerObject.AddComponent<Rigidbody2D>();
            PlayerController player = playerObject.AddComponent<PlayerController>();
            CombatTargetView target = CreateCombatTarget("SpatialSlime", "spatial.slime.01");
            target.transform.position = new Vector3(3f, 3f, 0f);
            yield return null;

            CombatSpatialFrame frame = CombatSpatialAdapter.Capture(
                "player", player, new[] { target }, null, 0.25);

            Assert.That(frame.Actors, Has.Count.EqualTo(2));
            Assert.That(frame.Actors[0].ActorId, Is.EqualTo("player"));
            Assert.That(frame.Actors[0].GroundPosition.X, Is.EqualTo(2.0).Within(0.0001));
            Assert.That(frame.Actors[1].ActorId, Is.EqualTo("spatial.slime.01"));
            Assert.That(frame.Actors[1].GroundPosition.X, Is.EqualTo(3.0).Within(0.0001));

            Object.Destroy(target.gameObject);
            Object.Destroy(playerObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointerSelection_PrefersSlimeWhenTerrainColliderAlsoOverlapsClick()
        {
            var terrain = new GameObject("OverlappingTerrain");
            BoxCollider2D terrainCollider = terrain.AddComponent<BoxCollider2D>();
            terrainCollider.size = new Vector2(4f, 4f);

            var targetObject = new GameObject("ClickableSlime");
            CombatTargetView target = targetObject.AddComponent<CombatTargetView>();
            target.ConfigureRuntimeIdentity("click.slime.01", "slime");
            Physics2D.SyncTransforms();

            CombatTargetView selected = PlayerController.FindCombatTargetAt(Vector2.zero);

            Assert.That(targetObject.GetComponent<CircleCollider2D>(), Is.Not.Null,
                "CombatTargetView should create its click trigger when the prefab has no collider.");
            Assert.That(selected, Is.SameAs(target));

            Object.Destroy(targetObject);
            Object.Destroy(terrain);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SessionBackedEquipmentBankAndCrafting_CommitThroughGameManager()
        {
            GameManager manager = CreateManager();
            string characterId = manager.SelectedCharacterId;

            manager.DebugGrantItem("copper_pickaxe", 1);
            manager.EquipItem("copper_pickaxe");
            Assert.That(manager.GetSelectedCharacter().Equipment[EquipSlot.Tool], Is.EqualTo("copper_pickaxe"));

            manager.UnequipItem(EquipSlot.Tool);
            Assert.That(manager.GetSelectedCharacter().Equipment.ContainsKey(EquipSlot.Tool), Is.False);

            manager.DebugGrantItem("copper_ore", 6);
            manager.DebugGrantItem("oak_log", 4);
            manager.Deposit("copper_ore", 6);
            manager.Deposit("oak_log", 4);
            manager.DebugGrantCoins(30);
            int pickaxesBeforeCraft = CountInventoryItem(manager.GetSelectedCharacter(), "copper_pickaxe");
            manager.CraftRecipe("recipe_copper_pickaxe");

            Account account = manager.Account;
            Assert.That(BankHelper.CountItem(account.Bank, "copper_ore"), Is.EqualTo(0));
            Assert.That(BankHelper.CountItem(account.Bank, "oak_log"), Is.EqualTo(0));
            // Scope change F_0.7.0: crafted output lands in the character inventory, not the bank.
            Assert.That(BankHelper.CountItem(account.Bank, "copper_pickaxe"), Is.EqualTo(0));
            Assert.That(CountInventoryItem(manager.GetSelectedCharacter(), "copper_pickaxe"),
                Is.EqualTo(pickaxesBeforeCraft + 1));
            Assert.That(account.Bank.Coins, Is.EqualTo(0));
            Assert.That(manager.Session.BankRevision, Is.GreaterThan(0));
            bool crafted = false;
            foreach (SessionTraceEntry entry in manager.SessionTrace)
                if (entry.CharacterId == characterId && entry.CommandName == "craft") crafted = true;
            Assert.That(crafted, Is.True);

            yield return DestroyManager(manager);
        }

        private static int CountInventoryItem(Character character, string itemId)
        {
            int total = 0;
            foreach (ItemStack stack in character?.Inventory ?? new System.Collections.Generic.List<ItemStack>())
                if (stack != null && stack.ItemId == itemId) total += stack.Qty;
            return total;
        }

        private static GameManager CreateManager()
        {
            var managerObject = new GameObject("PlayModeGameManager");
            managerObject.SetActive(false);
            GameManager manager = managerObject.AddComponent<GameManager>();
            SkillContentRegistryAsset registry = Resources.Load<SkillContentRegistryAsset>("SkillContentRegistry");
            Assert.That(registry, Is.Not.Null, "The production skill registry must be available to PlayMode tests.");
            FieldInfo registryField = typeof(GameManager).GetField("skillContentRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(registryField, Is.Not.Null);
            registryField.SetValue(manager, registry);

            OfflineProgressionConfigAsset offlineProgression = Resources.Load<OfflineProgressionConfigAsset>("OfflineProgressionConfig");
            AutoCombatPolicyConfigAsset autoCombatPolicy = Resources.Load<AutoCombatPolicyConfigAsset>("AutoCombatPolicyConfig");
            CombatBalanceConfigAsset combatBalance = Resources.Load<CombatBalanceConfigAsset>("CombatBalanceConfig");
            ProgressionBalanceConfigAsset progressionBalance = Resources.Load<ProgressionBalanceConfigAsset>("ProgressionBalanceConfig");
            Assert.That(offlineProgression, Is.Not.Null, "The production offline progression config must be available to PlayMode tests.");
            Assert.That(autoCombatPolicy, Is.Not.Null, "The production auto-combat policy must be available to PlayMode tests.");
            Assert.That(combatBalance, Is.Not.Null, "The production combat balance must be available to PlayMode tests.");
            Assert.That(progressionBalance, Is.Not.Null, "The production progression balance must be available to PlayMode tests.");
            typeof(GameManager).GetField("offlineProgressionConfig", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, offlineProgression);
            typeof(GameManager).GetField("autoCombatPolicy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, autoCombatPolicy);
            typeof(GameManager).GetField("combatBalanceConfig", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, combatBalance);
            typeof(GameManager).GetField("progressionBalanceConfig", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, progressionBalance);
            managerObject.SetActive(true);
            manager.CreateFamily("Test Family");
            manager.CreateCharacter("Tester", ClassId.Beginner);
            manager.SelectCharacter(manager.Account.Characters[0].Id);
            return manager;
        }

        private static IEnumerator DestroyManager(GameManager manager)
        {
            Object.Destroy(manager.gameObject);
            yield return null;
        }

        private static CombatTargetView CreateCombatTarget(string name, string entityId)
        {
            var targetObject = new GameObject(name);
            targetObject.AddComponent<CircleCollider2D>();
            CombatTargetView target = targetObject.AddComponent<CombatTargetView>();
            target.ConfigureRuntimeIdentity(entityId, "slime");
            return target;
        }

        private static Button CreateButton(Transform parent, string name)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            return buttonObject.AddComponent<Button>();
        }

        private static Image CreateImage(Transform parent, string name)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            return imageObject.AddComponent<Image>();
        }

        private static Component CreateComponent(Transform parent, string name, System.Type type)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            return textObject.AddComponent(type);
        }

        private static void SetHudField(System.Type hudType, Component hud, string fieldName, object value)
        {
            hudType.GetField(fieldName)?.SetValue(hud, value);
        }
    }

    // Colocated here instead of its own file: IdleCloud.Tests.PlayMode.csproj lists Compile items
    // explicitly and only regenerates while the Unity Editor is focused, so a new .cs file would
    // break the `dotnet build` gate until the next regeneration.
    public class GridPathfinderQueryTests
    {
        private sealed class StubHeightProvider : ITerrainHeightProvider
        {
            public readonly Dictionary<Vector2Int, int> Heights = new Dictionary<Vector2Int, int>();

            public bool TryGetHeight(int x, int y, out int height)
                => Heights.TryGetValue(new Vector2Int(x, y), out height);
        }

        [Test]
        public void GridQueries_CellDistanceAndLineOfSight_FollowTerrainHeights()
        {
            ITerrainHeightProvider previous = TerrainHeightService.Current;
            var gridObject = new GameObject("QueryGrid");
            gridObject.AddComponent<Grid>(); // default rectangular 1x1 cells: world (x.5, y.5) => cell (x, y)
            GridPathfinder pathfinder = gridObject.AddComponent<GridPathfinder>();
            var stub = new StubHeightProvider();
            TerrainHeightService.Current = stub;

            try
            {
                Assert.That(pathfinder.TryGetCell(new Vector3(0.5f, 0.5f), out Vector2Int cell), Is.True);
                Assert.That(cell, Is.EqualTo(new Vector2Int(0, 0)));

                // Chebyshev: (0,0) -> (3,2) is 3 steps with diagonals.
                Assert.That(pathfinder.CellDistance(new Vector3(0.5f, 0.5f), new Vector3(3.5f, 2.5f)), Is.EqualTo(3));

                // Adjacent cells (no intermediate) are always visible.
                Assert.That(pathfinder.HasLineOfSight(new Vector3(0.5f, 0.5f), new Vector3(1.5f, 1.5f)), Is.True);

                // Flat ground along a straight line is visible.
                Assert.That(pathfinder.HasLineOfSight(new Vector3(0.5f, 0.5f), new Vector3(4.5f, 0.5f)), Is.True);

                // An intermediate cell taller than both endpoints blocks sight.
                stub.Heights[new Vector2Int(2, 0)] = 3;
                Assert.That(pathfinder.HasLineOfSight(new Vector3(0.5f, 0.5f), new Vector3(4.5f, 0.5f)), Is.False);

                // A ridge merely equal to the endpoints' height does not block.
                stub.Heights[new Vector2Int(0, 0)] = 3;
                stub.Heights[new Vector2Int(4, 0)] = 3;
                Assert.That(pathfinder.HasLineOfSight(new Vector3(0.5f, 0.5f), new Vector3(4.5f, 0.5f)), Is.True);

                // Sight is symmetric: an oblique ridge blocks (or not) identically both ways.
                stub.Heights.Clear();
                stub.Heights[new Vector2Int(1, 1)] = 5;
                Vector3 lowEnd = new Vector3(0.5f, 0.5f);
                Vector3 highEnd = new Vector3(4.5f, 2.5f);
                Assert.That(pathfinder.HasLineOfSight(lowEnd, highEnd), Is.False);
                Assert.That(pathfinder.HasLineOfSight(highEnd, lowEnd), Is.EqualTo(pathfinder.HasLineOfSight(lowEnd, highEnd)));
            }
            finally
            {
                TerrainHeightService.Current = previous;
                Object.DestroyImmediate(gridObject);
            }
        }
    }
}
