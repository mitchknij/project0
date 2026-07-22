// UIBuilder.cs — Builds the complete Canvas hierarchy procedurally when the scene loads.
// [RuntimeInitializeOnLoadMethod] keeps this independent of a scene-owned GameObject.
// Phase B screens use the same pattern and are added to the Canvas.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.View;

namespace IdleCloud.UI
{
    public static class UIBuilder
    {
        private static bool _bootstrapStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_bootstrapStarted) return;
            _bootstrapStarted = true;

            var runnerObject = new GameObject("UIBuilderBootstrapRunner");
            Object.DontDestroyOnLoad(runnerObject);
            runnerObject.AddComponent<BootstrapRunner>();
        }

        private sealed class BootstrapRunner : MonoBehaviour
        {
            private void Start()
            {
                StartCoroutine(BuildWhenReady());
            }

            private IEnumerator BuildWhenReady()
            {
                SceneLoader[] loaders = Object.FindObjectsByType<SceneLoader>();
                SceneLoader loader = loaders.Length == 0 ? null : loaders[0];
                if (loader != null)
                {
                    while (!loader.InitialLoadCompleted)
                        yield return null;
                }
                else
                {
                    yield return null;
                }

                if (Object.FindObjectsByType<UIBootstrapper>().Length == 0)
                    BuildAll();

                Object.Destroy(gameObject);
            }
        }

        /// <summary>
        /// Build the full UI hierarchy (EventSystem + Canvas + panels + UIController).
        /// Optionally parent it under another transform for the editor bake tool.
        /// Returns the Canvas GameObject.
        /// </summary>
        public static GameObject BuildAll(Transform parent = null)
        {
            if (parent == null) UIInputBootstrap.EnsureEventSystem();
            var canvas = BuildCanvas();
            if (parent != null) canvas.transform.SetParent(parent, false);

            // Phase A panels
            var title   = BuildTitlePanel(canvas.transform);
            var charSel = BuildCharSelectPanel(canvas.transform);
            var charCre = BuildCharCreatePanel(canvas.transform);

            // Phase B sub-panels (hidden by default; shown by MainHudPanel buttons)
            var activity  = BuildSubPanel<ActivityPanel>(canvas.transform,  "ActivityPanel",  "Activity",    BuildActivityContent);
            var travel    = BuildSubPanel<TravelPanel>(canvas.transform,    "TravelPanel",    "Travel",      BuildTravelContent);
            var inventory = BuildSubPanel<InventoryPanel>(canvas.transform, "InventoryPanel", "Inventory",   BuildInventoryContent);
            var equipment = BuildSubPanel<EquipmentPanel>(canvas.transform, "EquipmentPanel", "Equipment",   BuildEquipmentContent);
            var bank      = BuildSubPanel<BankPanel>(canvas.transform,      "BankPanel",      "Bank",        BuildBankContent);
            var crafting  = BuildSubPanel<CraftingPanel>(canvas.transform, "CraftingPanel", "Crafting", BuildCraftingContent);
            var talents   = BuildSubPanel<TalentsPanel>(canvas.transform,  "TalentsPanel",  "Talents",  BuildTalentsContent);
            var skills    = BuildSubPanel<SkillsPanel>(canvas.transform,    "SkillsPanel",    "Skills",   BuildSkillsContent);

            // Phase B HUD
            var hud = BuildMainHudPanel(canvas.transform, activity, travel, inventory, equipment, bank, crafting, talents, skills);
            BuildLootFeedPanel(canvas.transform);

            // Add the drag controller and skillbar after every sub-panel overlay.
            // OfflineReportPanel is added after them and remains the topmost modal.
            var dragController = BuildSkillDragController(canvas.transform);
            var skillBar = BuildSkillBar(canvas.transform, dragController);
            hud.skillBarPanel = skillBar;
            skills.dragController = dragController;

            // Progression banner stays active and invisible through its CanvasGroup.
            BuildLevelUpBanner(canvas.transform);

            // Offline report modal is added after the HUD so it renders above it.
            var offline = BuildOfflineReportPanel(canvas.transform);

            // Controller
            var ctrlGO = new GameObject("UIController");
            if (parent != null) ctrlGO.transform.SetParent(parent, false);
            var flow   = ctrlGO.AddComponent<GameFlowController>();
            var pm     = ctrlGO.AddComponent<PanelManager>();
            var rd     = ctrlGO.AddComponent<UIRefreshDriver>();

            flow.panelManager    = pm;
            flow.refreshDriver   = rd;
            flow.titlePanel      = title;
            flow.charSelectPanel = charSel;
            flow.charCreatePanel = charCre;
            flow.mainHudPanel    = hud;
            flow.offlineReportPanel = offline;

            title.flowController   = flow;
            charSel.flowController = flow;
            charCre.flowController = flow;

            // UIBootstrapper registers panels with PanelManager/UIRefreshDriver in Awake.
            // Add it last because AddComponent invokes Awake immediately, and Awake reads
            // the flow and panel references configured above.
            ctrlGO.AddComponent<UIBootstrapper>();

            return canvas;
        }

        // ── Infrastructure ─────────────────────────────────────────────────────

        private static GameObject BuildCanvas()
        {
            var go     = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                UITheme.Layout.CanvasReferenceWidth, UITheme.Layout.CanvasReferenceHeight);
            scaler.matchWidthOrHeight  = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        // ── TitlePanel ────────────────────────────────────────────────────────

        private static TitlePanel BuildTitlePanel(Transform canvasT)
        {
            var root = new GameObject("TitlePanel");
            root.transform.SetParent(canvasT, false);
            UIHelpers.Stretch(root);
            UIHelpers.AddBackground(root, UIHelpers.PanelBg);

            var panel = root.AddComponent<TitlePanel>();

            // Centered card
            var card = new GameObject("Card");
            card.transform.SetParent(root.transform, false);
            UIHelpers.AddFrame(card, UITheme.PanelFrame);
            UIHelpers.Center(card, UITheme.Layout.TitleCardWidth, UITheme.Layout.TitleCardHeight);
            UIHelpers.AddVLG(card, UITheme.Layout.TitleCardSpacing, UITheme.Layout.TitleCardPadding);

            // Title
            var titleTmp = UIHelpers.CreateHeader(card.transform, "IdleCloud", 44);
            UIHelpers.AddLayout(titleTmp.gameObject, preferredH: UITheme.Layout.TitleHeaderHeight);

            // Subtitle
            var sub = UIHelpers.CreateLabel(card.transform, "An idle RPG adventure", 20);
            sub.color = UITheme.TextDim;
            UIHelpers.AddLayout(sub.gameObject, preferredH: UITheme.Layout.SubtitleHeight);

            // Breathing room
            Spacer(card.transform, UITheme.Layout.StandardSpacing * 2f);

            // Input label
            var inputLbl = UIHelpers.CreateLabel(card.transform, "Family Name", 16,
                align: TextAlignmentOptions.Left);
            inputLbl.color = UITheme.TextDim;
            UIHelpers.AddLayout(inputLbl.gameObject, preferredH: UITheme.Layout.FieldLabelHeight);

            // Input field
            var input = UIHelpers.CreateInputField(card.transform, "e.g. Cloudwalker...");
            UIHelpers.AddLayout(input.gameObject, preferredH: UITheme.Layout.InputHeight);
            panel.nameInput = input;

            // Error status
            var status = UIHelpers.CreateLabel(card.transform, "", 14);
            status.color = new Color(1f, 0.38f, 0.38f);
            UIHelpers.AddLayout(status.gameObject, preferredH: UITheme.Layout.StatusHeight);
            panel.statusText = status;

            // Play button
            var playBtn = UIHelpers.CreateButton(card.transform, "Play  >", UIHelpers.AccentBlue);
            UIHelpers.AddLayout(playBtn.gameObject, preferredH: UITheme.Layout.PrimaryButtonHeight);
            panel.playButton = playBtn;

            root.SetActive(false);
            return panel;
        }

        // ── CharacterSelectPanel ──────────────────────────────────────────────

        private static CharacterSelectPanel BuildCharSelectPanel(Transform canvasT)
        {
            var root = new GameObject("CharSelectPanel");
            root.transform.SetParent(canvasT, false);
            UIHelpers.Stretch(root);
            UIHelpers.AddBackground(root, UIHelpers.PanelBg);

            var panel = root.AddComponent<CharacterSelectPanel>();

            var card = new GameObject("Card");
            card.transform.SetParent(root.transform, false);
            UIHelpers.AddFrame(card, UITheme.PanelFrame);
            UIHelpers.Center(card,
                UITheme.Layout.CharacterSelectCardWidth,
                UITheme.Layout.CharacterSelectCardHeight);
            UIHelpers.AddVLG(card,
                UITheme.Layout.CharacterSelectSpacing,
                UITheme.Layout.CharacterSelectPadding);

            // Header
            var header = UIHelpers.CreateHeader(card.transform, "Choose Character", 28);
            UIHelpers.AddLayout(header.gameObject, preferredH: UITheme.Layout.PanelHeaderHeight);

            // Scroll list
            var scrollWrap = new GameObject("ScrollWrap");
            scrollWrap.transform.SetParent(card.transform, false);
            UIHelpers.AddLayout(scrollWrap.gameObject, preferredH: UITheme.Layout.PanelListHeight);

            var (scroll, content) = UIHelpers.CreateScrollView(scrollWrap.transform);
            UIHelpers.Stretch(scroll.gameObject);
            panel.listContainer = content;

            // New character button
            var newBtn = UIHelpers.CreateButton(card.transform, "+ New Character", UIHelpers.AccentGreen);
            UIHelpers.AddLayout(newBtn.gameObject, preferredH: UITheme.Layout.SecondaryButtonHeight);
            panel.newCharButton = newBtn;

            root.SetActive(false);
            return panel;
        }

        // ── CharacterCreatePanel ──────────────────────────────────────────────

        private static CharacterCreatePanel BuildCharCreatePanel(Transform canvasT)
        {
            var root = new GameObject("CharCreatePanel");
            root.transform.SetParent(canvasT, false);
            UIHelpers.Stretch(root);
            UIHelpers.AddBackground(root, UIHelpers.PanelBg);

            var panel = root.AddComponent<CharacterCreatePanel>();

            var card = new GameObject("Card");
            card.transform.SetParent(root.transform, false);
            UIHelpers.AddFrame(card, UITheme.PanelFrame);
            UIHelpers.Center(card, UITheme.Layout.CharacterCreateCardWidth, UITheme.Layout.CharacterCreateCardHeight);
            UIHelpers.AddVLG(card, UITheme.Layout.CharacterCreateSpacing, UITheme.Layout.CharacterCreatePadding);

            // Header
            var header = UIHelpers.CreateHeader(card.transform, "Create Character", 28);
            UIHelpers.AddLayout(header.gameObject, preferredH: UITheme.Layout.PanelHeaderHeight);

            // Class label
            var clsLbl = UIHelpers.CreateLabel(card.transform, "Choose a class", 16,
                align: TextAlignmentOptions.Left);
            clsLbl.color = UITheme.TextDim;
            UIHelpers.AddLayout(clsLbl.gameObject, preferredH: UITheme.Layout.FieldLabelHeight);

            // Row with four class buttons
            var clsRow = new GameObject("ClassRow");
            clsRow.transform.SetParent(card.transform, false);
            UIHelpers.AddLayout(clsRow.gameObject, preferredH: UITheme.Layout.ClassRowHeight);
            UIHelpers.AddHLG(clsRow, spacing: UITheme.Layout.StandardSpacing, controlWidth: true);

            var clsIds       = new[] { ClassId.Beginner, ClassId.Warrior, ClassId.Archer, ClassId.Mage };
            var classButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                string label = RuntimeContent.Get(clsIds[i])?.Name ?? clsIds[i].ToString();
                var btn = UIHelpers.CreateButton(clsRow.transform, label, UIHelpers.CardBg);
                classButtons[i] = btn;
            }

            // Class description (background plus separate text child to avoid duplicate graphics)
            var descWrap = new GameObject("ClassDesc");
            descWrap.transform.SetParent(card.transform, false);
            UIHelpers.AddFrame(descWrap, UITheme.InsetFrame);
            UIHelpers.AddLayout(descWrap.gameObject, preferredH: UITheme.Layout.ClassDescriptionHeight);

            var descTextGO  = new GameObject("DescText");
            descTextGO.transform.SetParent(descWrap.transform, false);
            var descTmp     = descTextGO.AddComponent<TextMeshProUGUI>(); // TMP adds the RectTransform
            var descRT      = (RectTransform)descTextGO.transform;
            descRT.anchorMin = Vector2.zero;
            descRT.anchorMax = Vector2.one;
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;
            descTmp.font     = UITheme.BodyFont;
            descTmp.fontSize = 16;
            descTmp.color    = UITheme.TextDim;
            descTmp.margin   = new Vector4(12, 8, 12, 8);
            panel.classDescText = descTmp;

            // Character name label
            var nameLbl = UIHelpers.CreateLabel(card.transform, "Character Name", 16,
                align: TextAlignmentOptions.Left);
            nameLbl.color = UITheme.TextDim;
            UIHelpers.AddLayout(nameLbl.gameObject, preferredH: UITheme.Layout.FieldLabelHeight);

            // Character name input
            var nameInput = UIHelpers.CreateInputField(card.transform, "e.g. Aldric the Bold...");
            UIHelpers.AddLayout(nameInput.gameObject, preferredH: UITheme.Layout.InputHeight);
            panel.nameInput = nameInput;

            // Error status
            var status = UIHelpers.CreateLabel(card.transform, "", 14);
            status.color = new Color(1f, 0.38f, 0.38f);
            UIHelpers.AddLayout(status.gameObject, preferredH: UITheme.Layout.StatusHeight);
            panel.statusText = status;

            // Bottom row: Back + Create
            var botRow = new GameObject("BottomRow");
            botRow.transform.SetParent(card.transform, false);
            UIHelpers.AddLayout(botRow.gameObject, preferredH: UITheme.Layout.BottomRowHeight);
            UIHelpers.AddHLG(botRow, spacing: UITheme.Layout.BottomRowSpacing, controlWidth: true);

            var backBtn   = UIHelpers.CreateButton(botRow.transform, "<  Back",     UIHelpers.AccentGray);
            var createBtn = UIHelpers.CreateButton(botRow.transform, "Create  >",   UIHelpers.AccentBlue);
            panel.backButton   = backBtn;
            panel.createButton = createBtn;

            // Wire class buttons; SetClassButtons also sets the initial description.
            panel.SetClassButtons(classButtons);

            root.SetActive(false);
            return panel;
        }

        // ── MainHudPanel ──────────────────────────────────────────────────────

        private static MainHudPanel BuildMainHudPanel(Transform canvasT,
            ActivityPanel activity, TravelPanel travel,
            InventoryPanel inventory, EquipmentPanel equipment, BankPanel bank,
            CraftingPanel crafting, TalentsPanel talents, SkillsPanel skills)
        {
            var root = new GameObject("MainHudPanel");
            root.transform.SetParent(canvasT, false);
            UIHelpers.AddBackground(root, Color.clear); // Image supplies the RectTransform.

            var rt = (RectTransform)root.transform;
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 4);
            RectTransform canvasRT = canvasT as RectTransform;
            float canvasWidth = canvasRT == null ? UITheme.Layout.CanvasReferenceWidth : canvasRT.rect.width;
            if (canvasWidth <= 0f) canvasWidth = UITheme.Layout.CanvasReferenceWidth;
            rt.sizeDelta = new Vector2(
                Mathf.Min(UITheme.Layout.HudWidth, canvasWidth), UITheme.Layout.HudHeight);

            var panel = root.AddComponent<MainHudPanel>();
            var hlg = UIHelpers.AddHLG(root, spacing: UITheme.Layout.StandardSpacing, controlWidth: true);
            hlg.padding = new RectOffset(
                (int)UITheme.Layout.EdgePad, (int)UITheme.Layout.EdgePad,
                0, 0);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment         = TextAnchor.MiddleCenter;

            Button NavBtn(string label)
            {
                var btn  = UIHelpers.CreateButton(root.transform, label);
                UIHelpers.AddLayout(btn.gameObject,
                    preferredW: UITheme.Layout.NavButtonWidth,
                    preferredH: UITheme.Layout.NavButtonHeight,
                    minW: UITheme.Layout.NavButtonWidth); // fixed: only the center block may shrink
                return btn;
            }

            // Eight text navigation buttons plus Auto combat and AutoLoot toggles.
            panel.inventoryButton = NavBtn("Inventory");
            panel.equipmentButton = NavBtn("Equipment");
            panel.activityButton  = NavBtn("Activity");
            panel.travelButton    = NavBtn("Travel");
            panel.craftingButton  = NavBtn("Crafting");
            panel.talentsButton   = NavBtn("Talents");
            panel.skillsButton    = NavBtn("Skills");
            panel.bankButton      = NavBtn("Bank");
            panel.autoToggleButton = NavBtn("Auto");
            panel.autoToggleLabel = panel.autoToggleButton.GetComponentInChildren<TextMeshProUGUI>();
            panel.autoLootToggleButton = NavBtn("Loot");
            panel.autoLootToggleLabel = panel.autoLootToggleButton.GetComponentInChildren<TextMeshProUGUI>();

            // Center stats block: name/gold, HP/XP bars, and map/activity.
            var center = new GameObject("HudCenter");
            center.transform.SetParent(root.transform, false);
            UIHelpers.AddFrame(center, UITheme.PanelFrame);
            LayoutElement centerLayout = UIHelpers.AddLayout(center,
                preferredW: UITheme.Layout.CenterBlockW,
                preferredH: UITheme.Layout.HudHeight,
                flexW: 1);
            centerLayout.minWidth = UITheme.Layout.CenterBlockMinW;
            UIHelpers.AddVLG(center,
                spacing: UITheme.Layout.HudCenterSpacing,
                padding: UITheme.Layout.StandardPadding,
                controlHeight: true);

            var nameRow = new GameObject("NameGoldRow");
            nameRow.transform.SetParent(center.transform, false);
            UIHelpers.AddLayout(nameRow, preferredH: UITheme.Layout.HudNameRowHeight);
            UIHelpers.AddHLG(nameRow, spacing: UITheme.Layout.HudInfoSpacing, controlWidth: true);

            var nameTMP = UIHelpers.CreateLabel(nameRow.transform, "", 16, bold: true,
                align: TextAlignmentOptions.Left);
            nameTMP.color = UITheme.TextGold;
            UIHelpers.AddLayout(nameTMP.gameObject, flexW: 1);
            panel.nameLabel = nameTMP;

            var goldTMP = UIHelpers.CreateLabel(nameRow.transform, "", 16,
                align: TextAlignmentOptions.Right);
            goldTMP.color = UITheme.TextGold;
            UIHelpers.AddLayout(goldTMP.gameObject, preferredW: UITheme.Layout.HudGoldLabelWidth);
            panel.goldLabel = goldTMP;

            var (hpFill, hpLabel) = UIHelpers.CreateBar(center.transform, UITheme.HpRed);
            UIHelpers.AddLayout(hpFill.transform.parent.gameObject, preferredH: UITheme.Layout.BarHeight);
            panel.hpFill = hpFill; panel.hpLabel = hpLabel;

            var (xpFill, xpLabel) = UIHelpers.CreateBar(center.transform, UITheme.XpGold);
            UIHelpers.AddLayout(xpFill.transform.parent.gameObject, preferredH: UITheme.Layout.BarHeight);
            panel.xpFill = xpFill; panel.xpLabel = xpLabel;

            var mapActRow = new GameObject("MapActivityRow");
            mapActRow.transform.SetParent(center.transform, false);
            UIHelpers.AddLayout(mapActRow, preferredH: UITheme.Layout.HudMapRowHeight);
            UIHelpers.AddHLG(mapActRow, spacing: UITheme.Layout.HudInfoSpacing, controlWidth: true);

            var mapTMP = UIHelpers.CreateLabel(mapActRow.transform, "", 13,
                align: TextAlignmentOptions.Left);
            mapTMP.color = UITheme.TextDim;
            UIHelpers.AddLayout(mapTMP.gameObject, flexW: 1);
            panel.mapLabel = mapTMP;

            var actTMP = UIHelpers.CreateLabel(mapActRow.transform, "", 13,
                align: TextAlignmentOptions.Right);
            actTMP.color = UITheme.TextDim;
            UIHelpers.AddLayout(actTMP.gameObject, preferredW: UITheme.Layout.HudActivityLabelWidth);
            panel.activityLabel = actTMP;

            panel.activityPanel  = activity;
            panel.travelPanel    = travel;
            panel.inventoryPanel = inventory;
            panel.equipmentPanel = equipment;
            panel.bankPanel      = bank;
            panel.craftingPanel  = crafting;
            panel.talentsPanel   = talents;
            panel.skillsPanel    = skills;

            root.SetActive(false);
            return panel;
        }

        private static LevelUpBannerPanel BuildLevelUpBanner(Transform canvasT)
        {
            var root = new GameObject("LevelUpBannerPanel");
            root.transform.SetParent(canvasT, false);
            RectTransform rt = UIHelpers.EnsureRT(root);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(
                UITheme.Layout.LevelUpBannerWidth,
                UITheme.Layout.LevelUpBannerHeight);

            Image frame = UIHelpers.AddFrame(root, UITheme.PanelFrame);
            frame.raycastTarget = false;
            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var panel = root.AddComponent<LevelUpBannerPanel>();
            TextMeshProUGUI label = UIHelpers.CreateHeader(
                root.transform, "", UITheme.Layout.LevelUpBannerFontSize);
            label.color = UITheme.TextGold;
            label.raycastTarget = false;
            UIHelpers.Stretch(
                label.gameObject,
                UITheme.Layout.LevelUpBannerPadding,
                UITheme.Layout.LevelUpBannerPadding,
                UITheme.Layout.LevelUpBannerPadding,
                UITheme.Layout.LevelUpBannerPadding);
            panel.canvasGroup = canvasGroup;
            panel.label = label;

            // Never deactivate this root: its Update method owns lazy event binding.
            root.SetActive(true);
            return panel;
        }

        private static SkillDragController BuildSkillDragController(Transform canvasT)
        {
            var root = new GameObject("SkillDragController");
            root.transform.SetParent(canvasT, false);
            UIHelpers.Stretch(root);

            var controller = root.AddComponent<SkillDragController>();
            var ghost = new GameObject("DragGhost");
            ghost.transform.SetParent(root.transform, false);
            Image ghostImage = UIHelpers.AddFrame(ghost, UITheme.ButtonFrame);
            ghostImage.raycastTarget = false;
            UIHelpers.Center(ghost, 150f, 42f);

            TextMeshProUGUI label = UIHelpers.CreateLabel(ghost.transform, "", 13,
                align: TextAlignmentOptions.Center);
            label.color = UITheme.TextGold;
            label.raycastTarget = false;
            UIHelpers.Stretch(label.gameObject, 8f, 4f, 8f, 4f);

            controller.ghost = (RectTransform)ghost.transform;
            controller.ghostLabel = label;
            ghost.SetActive(false);
            return controller;
        }

        private static SkillBarPanel BuildSkillBar(Transform canvasT, SkillDragController dragController)
        {
            var root = new GameObject("SkillBarPanel");
            root.transform.SetParent(canvasT, false);

            RectTransform rt = UIHelpers.EnsureRT(root);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f,
                UITheme.Layout.HudHeight + UITheme.Layout.SkillBarBottomOffset);
            rt.sizeDelta = new Vector2(
                UITheme.Layout.SkillBarSlotCount * UITheme.Layout.SkillBarSlotSize +
                (UITheme.Layout.SkillBarSlotCount - 1) * UITheme.Layout.SkillBarSpacing,
                UITheme.Layout.SkillBarSlotSize);

            var hlg = UIHelpers.AddHLG(root,
                spacing: UITheme.Layout.SkillBarSpacing,
                controlWidth: true);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            var panel = root.AddComponent<SkillBarPanel>();
            int slotCount = UITheme.Layout.SkillBarSlotCount;
            panel.slotButtons = new Button[slotCount];
            panel.slotCooldownFills = new Image[slotCount];
            panel.slotAutoHighlights = new Image[slotCount];
            panel.slotHotkeyLabels = new TextMeshProUGUI[slotCount];
            panel.slotNameLabels = new TextMeshProUGUI[slotCount];
            panel.slotComponents = new SkillBarSlot[slotCount];

            for (int index = 0; index < slotCount; index++)
            {
                Button slot = UIHelpers.CreateButton(root.transform,
                    "SkillBarSlot" + (index + 1), UIHelpers.CardBg);
                UIHelpers.AddLayout(slot.gameObject,
                    preferredW: UITheme.Layout.SkillBarSlotSize,
                    preferredH: UITheme.Layout.SkillBarSlotSize,
                    minW: UITheme.Layout.SkillBarSlotSize);
                panel.slotButtons[index] = slot;
                panel.slotNameLabels[index] = slot.GetComponentInChildren<TextMeshProUGUI>();

                var fillObject = new GameObject("CooldownFill");
                fillObject.transform.SetParent(slot.transform, false);
                Image fill = UIHelpers.AddBackground(fillObject, new Color(0f, 0f, 0f, 0.55f));
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Top;
                fill.fillAmount = 0f;
                fill.raycastTarget = false;
                RectTransform fillTransform = UIHelpers.EnsureRT(fillObject);
                fillTransform.anchorMin = Vector2.zero;
                fillTransform.anchorMax = Vector2.one;
                fillTransform.offsetMin = Vector2.zero;
                fillTransform.offsetMax = Vector2.zero;
                fillTransform.SetAsFirstSibling();
                panel.slotCooldownFills[index] = fill;

                var highlightObject = new GameObject("AutoHighlight");
                highlightObject.transform.SetParent(slot.transform, false);
                Color highlightColor = UITheme.SkillBarAutoHighlightTint;
                highlightColor.a = 0f;
                Image highlight = UIHelpers.AddBackground(highlightObject, highlightColor);
                highlight.raycastTarget = false;
                RectTransform highlightTransform = UIHelpers.EnsureRT(highlightObject);
                highlightTransform.anchorMin = Vector2.zero;
                highlightTransform.anchorMax = Vector2.one;
                highlightTransform.offsetMin = Vector2.zero;
                highlightTransform.offsetMax = Vector2.zero;
                highlightTransform.SetSiblingIndex(fillTransform.GetSiblingIndex() + 1);
                panel.slotAutoHighlights[index] = highlight;

                TextMeshProUGUI hotkey = UIHelpers.CreateLabel(slot.transform,
                    (index + 1).ToString(), 10, align: TextAlignmentOptions.BottomRight);
                RectTransform hotkeyTransform = (RectTransform)hotkey.transform;
                hotkeyTransform.anchorMin = Vector2.zero;
                hotkeyTransform.anchorMax = Vector2.one;
                hotkeyTransform.offsetMin = new Vector2(3f, 1f);
                hotkeyTransform.offsetMax = new Vector2(-3f, -1f);
                hotkey.raycastTarget = false;
                panel.slotHotkeyLabels[index] = hotkey;

                var slotComponent = slot.gameObject.AddComponent<SkillBarSlot>();
                slotComponent.SlotIndex = index;
                slotComponent.Panel = panel;
                slotComponent.DragController = dragController;
                panel.slotComponents[index] = slotComponent;
            }

            root.SetActive(false);
            return panel;
        }

        // ── OfflineReportPanel (modal, rendered above the rest) ───────────────
        private static LootFeedPanel BuildLootFeedPanel(Transform canvasT)
        {
            var root = new GameObject("LootFeedPanel");
            root.transform.SetParent(canvasT, false);
            UIHelpers.AddBackground(root, Color.clear);

            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(
                -UITheme.Layout.LootFeedRightOffset,
                -UITheme.Layout.LootFeedTopOffset);
            rt.sizeDelta = new Vector2(UITheme.Layout.LootFeedWidth, UITheme.Layout.LootFeedHeight);

            Image frame = UIHelpers.AddFrame(root, UITheme.InsetFrame);
            frame.raycastTarget = false;
            UIHelpers.AddVLG(
                root,
                UITheme.Layout.LootFeedSpacing,
                UITheme.Layout.LootFeedPadding,
                controlHeight: true);

            var panel = root.AddComponent<LootFeedPanel>();
            var header = UIHelpers.CreateHeader(root.transform, "Loot", 16);
            UIHelpers.AddLayout(header.gameObject, preferredH: UITheme.Layout.LootFeedHeaderHeight);

            var entries = new GameObject("Entries");
            entries.transform.SetParent(root.transform, false);
            UIHelpers.AddLayout(entries, preferredH: UITheme.Layout.LootFeedHeight -
                UITheme.Layout.LootFeedHeaderHeight - UITheme.Layout.LootFeedPadding * 2f);
            UIHelpers.AddVLG(entries, UITheme.Layout.LootFeedSpacing, controlHeight: true);
            panel.listContainer = (RectTransform)entries.transform;
            root.SetActive(true);
            return panel;
        }

        private static OfflineReportPanel BuildOfflineReportPanel(Transform canvasT)
        {
            var root = new GameObject("OfflineReportPanel");
            root.transform.SetParent(canvasT, false);
            UIHelpers.Stretch(root);
            UIHelpers.AddBackground(root, new Color(0f, 0f, 0f, 0.75f));

            var panel = root.AddComponent<OfflineReportPanel>();

            var card = new GameObject("Card");
            card.transform.SetParent(root.transform, false);
            UIHelpers.AddFrame(card, UITheme.PanelFrame);
            UIHelpers.Center(card,
                UITheme.Layout.OfflineReportCardWidth,
                UITheme.Layout.OfflineReportCardHeight);
            UIHelpers.AddVLG(card,
                UITheme.Layout.OfflineReportSpacing,
                UITheme.Layout.OfflineReportPadding,
                controlHeight: true);

            var hdr = UIHelpers.CreateHeader(card.transform, "Welcome Back!", 26);
            UIHelpers.AddLayout(hdr.gameObject, preferredH: UITheme.Layout.OfflineHeaderHeight);

            var elapsed = UIHelpers.CreateLabel(card.transform, "", 17);
            elapsed.color = UITheme.TextDim;
            UIHelpers.AddLayout(elapsed.gameObject, preferredH: UITheme.Layout.OfflineElapsedHeight);
            panel.elapsedLabel = elapsed;

            var scrollWrap = new GameObject("ScrollWrap");
            scrollWrap.transform.SetParent(card.transform, false);
            UIHelpers.AddLayout(scrollWrap, preferredH: UITheme.Layout.OfflineListHeight);
            var (scroll, content) = UIHelpers.CreateScrollView(scrollWrap.transform);
            UIHelpers.Stretch(scroll.gameObject);
            panel.listContainer = content;

            var claim = UIHelpers.CreateButton(card.transform, "Claim");
            UIHelpers.AddLayout(claim.gameObject, preferredH: UITheme.Layout.ClaimButtonHeight);
            panel.claimButton = claim;

            root.SetActive(false);
            return panel;
        }

        // ── Sub-panel builder (generic modal overlay) ─────────────────────────

        private static T BuildSubPanel<T>(Transform canvasT, string goName, string title,
            System.Action<Transform, T> buildContent) where T : MonoBehaviour, IPanelView
        {
            var root = new GameObject(goName);
            root.transform.SetParent(canvasT, false);
            UIHelpers.AddBackground(root, new Color(0f, 0f, 0f, 0.55f)); // Image → RT

            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0, UITheme.Layout.HudHeight); // leave room for the HUD below
            rt.offsetMax = Vector2.zero;

            var card = new GameObject("Card");
            card.transform.SetParent(root.transform, false);
            UIHelpers.AddFrame(card, UITheme.PanelFrame);
            UIHelpers.Center(card,
                UITheme.Layout.SubPanelCardWidth,
                UITheme.Layout.SubPanelCardHeight);
            UIHelpers.AddVLG(card,
                UITheme.Layout.SubPanelSpacing,
                UITheme.Layout.SubPanelPadding,
                controlHeight: true);

            // Header and close button
            var hdrRow = new GameObject("HdrRow");
            hdrRow.transform.SetParent(card.transform, false);
            UIHelpers.AddBackground(hdrRow, Color.clear);
            UIHelpers.AddLayout(hdrRow, preferredH: UITheme.Layout.SubPanelHeaderHeight);
            var hdrHlg = hdrRow.AddComponent<HorizontalLayoutGroup>();
            hdrHlg.childControlWidth = true; hdrHlg.childControlHeight = true;
            hdrHlg.childForceExpandWidth = false; hdrHlg.childForceExpandHeight = true;

            var hdrTMP = UIHelpers.CreateHeader(hdrRow.transform, title, 20);
            hdrTMP.alignment = TMPro.TextAlignmentOptions.Left;
            UIHelpers.AddLayout(hdrTMP.gameObject, flexW: 1);

            var closeBtn = UIHelpers.CreateButton(hdrRow.transform, "X", UIHelpers.AccentGray);
            UIHelpers.AddLayout(closeBtn.gameObject, preferredW: UITheme.Layout.CloseButtonWidth);

            var panel = root.AddComponent<T>();
            buildContent(card.transform, panel);

            root.SetActive(false);
            return panel;
        }

        // ── Content builders for sub-panels ──────────────────────────────────

        private static void BuildActivityContent(Transform card, ActivityPanel panel)
        {
            var (scroll, content) = UIHelpers.CreateScrollView(card, UIHelpers.CardBgDark);
            UIHelpers.AddLayout(scroll.gameObject, preferredH: UITheme.Layout.ContentListHeight);
            panel.listContainer = content;
        }

        private static void BuildTravelContent(Transform card, TravelPanel panel)
        {
            var (scroll, content) = UIHelpers.CreateScrollView(card, UIHelpers.CardBgDark);
            UIHelpers.AddLayout(scroll.gameObject, preferredH: UITheme.Layout.ContentListHeight);
            panel.listContainer = content;
        }

        private static void BuildInventoryContent(Transform card, InventoryPanel panel)
        {
            var hdr = UIHelpers.CreateLabel(card, "", 14, align: TextAlignmentOptions.Left);
            hdr.color = new Color(0.65f, 0.65f, 0.70f);
            UIHelpers.AddLayout(hdr.gameObject, preferredH: UITheme.Layout.InventoryHeaderHeight);
            panel.headerLabel = hdr;

            var (scroll, content) = UIHelpers.CreateScrollView(card, UIHelpers.CardBgDark);
            UIHelpers.AddLayout(scroll.gameObject, preferredH: UITheme.Layout.InventoryListHeight);
            panel.listContainer = content;
        }

        private static void BuildEquipmentContent(Transform card, EquipmentPanel panel)
        {
            var (scroll, content) = UIHelpers.CreateScrollView(card, UIHelpers.CardBgDark);
            UIHelpers.AddLayout(scroll.gameObject, preferredH: UITheme.Layout.ContentListHeight);
            panel.listContainer = content;
        }

        private static void BuildBankContent(Transform card, BankPanel panel)
        {
            var coinsLbl = UIHelpers.CreateLabel(card, "Gold: 0", 15, align: TextAlignmentOptions.Left);
            UIHelpers.AddLayout(coinsLbl.gameObject, preferredH: UITheme.Layout.BankCoinsHeight);
            panel.coinsLabel = coinsLbl;

            var cols = new GameObject("Cols");
            cols.transform.SetParent(card, false);
            UIHelpers.AddLayout(cols, preferredH: UITheme.Layout.BankColumnsHeight);
            var colsHlg = cols.AddComponent<HorizontalLayoutGroup>();
            colsHlg.spacing = UITheme.Layout.StandardSpacing;
            colsHlg.childControlWidth = true; colsHlg.childControlHeight = true;
            colsHlg.childForceExpandWidth = true; colsHlg.childForceExpandHeight = true;

            panel.bankContainer      = BankColumn(cols.transform, "Bank");
            panel.inventoryContainer = BankColumn(cols.transform, "Inventory");
        }

        private static void BuildCraftingContent(Transform card, CraftingPanel panel)
        {
            var (scroll, content) = UIHelpers.CreateScrollView(card, UIHelpers.CardBgDark);
            UIHelpers.AddLayout(scroll.gameObject, preferredH: UITheme.Layout.ContentListHeight);
            panel.listContainer = content;
        }

        private static void BuildSkillsContent(Transform card, SkillsPanel panel)
        {
            var pts = UIHelpers.CreateLabel(card, "", 16, align: TextAlignmentOptions.Left);
            pts.color = UITheme.TextGold;
            UIHelpers.AddLayout(pts.gameObject, preferredH: UITheme.Layout.BarHeight);
            panel.pointsLabel = pts;

            var (scroll, content) = UIHelpers.CreateScrollView(card, UIHelpers.CardBgDark);
            UIHelpers.AddLayout(scroll.gameObject, preferredH: UITheme.Layout.TalentListHeight);
            panel.listContainer = content;

            var resetBtn = UIHelpers.CreateButton(card, "Development Respec Skills");
            UIHelpers.AddLayout(resetBtn.gameObject, preferredH: UITheme.Layout.ResetButtonHeight);
            panel.resetButton = resetBtn;
        }

        private static void BuildTalentsContent(Transform card, TalentsPanel panel)
        {
            var pts = UIHelpers.CreateLabel(card, "", 16, align: TextAlignmentOptions.Left);
            pts.color = UITheme.TextGold;
            UIHelpers.AddLayout(pts.gameObject, preferredH: UITheme.Layout.BarHeight);
            panel.pointsLabel = pts;

            var (scroll, content) = UIHelpers.CreateScrollView(card, UIHelpers.CardBgDark);
            UIHelpers.AddLayout(scroll.gameObject, preferredH: UITheme.Layout.TalentListHeight);
            panel.listContainer = content;

            var resetBtn = UIHelpers.CreateButton(card, "Reset Talents");
            UIHelpers.AddLayout(resetBtn.gameObject, preferredH: UITheme.Layout.ResetButtonHeight);
            panel.resetButton = resetBtn;
        }

        private static RectTransform BankColumn(Transform parent, string title)
        {
            var col = new GameObject(title + "Col");
            col.transform.SetParent(parent, false);
            UIHelpers.AddVLG(col, UITheme.Layout.BankColumnSpacing, controlHeight: true);
            var hdr = UIHelpers.CreateLabel(col.transform, title, 13, bold: true);
            UIHelpers.AddLayout(hdr.gameObject, preferredH: UITheme.Layout.BankColumnHeaderHeight);
            var (scroll, content) = UIHelpers.CreateScrollView(col.transform, UIHelpers.CardBgDark);
            return content;
        }

        // ── Helper functions ──────────────────────────────────────────────────

        private static void Spacer(Transform parent, float height)
        {
            var go = new GameObject("Spacer");
            go.transform.SetParent(parent, false);
            UIHelpers.AddLayout(go, minH: height);
        }
    }
}
