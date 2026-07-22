using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    public class MainHudPanel : MonoBehaviour, IPanelView
    {
        [HideInInspector] public TextMeshProUGUI nameLabel;
        [HideInInspector] public Image hpFill;
        [HideInInspector] public TextMeshProUGUI hpLabel;
        [HideInInspector] public Image xpFill;
        [HideInInspector] public TextMeshProUGUI xpLabel;
        [HideInInspector] public TextMeshProUGUI goldLabel;
        [HideInInspector] public TextMeshProUGUI mapLabel;
        [HideInInspector] public TextMeshProUGUI activityLabel;

        [HideInInspector] public ActivityPanel  activityPanel;
        [HideInInspector] public TravelPanel    travelPanel;
        [HideInInspector] public InventoryPanel inventoryPanel;
        [HideInInspector] public EquipmentPanel equipmentPanel;
        [HideInInspector] public BankPanel      bankPanel;
        [HideInInspector] public CraftingPanel  craftingPanel;
        [HideInInspector] public TalentsPanel   talentsPanel;
        [HideInInspector] public SkillsPanel    skillsPanel;

        [HideInInspector] public Button activityButton;
        [HideInInspector] public Button travelButton;
        [HideInInspector] public Button inventoryButton;
        [HideInInspector] public Button equipmentButton;
        [HideInInspector] public Button bankButton;
        [HideInInspector] public Button craftingButton;
        [HideInInspector] public Button talentsButton;
        [HideInInspector] public Button skillsButton;
        [HideInInspector] public Button autoToggleButton;
        [HideInInspector] public TextMeshProUGUI autoToggleLabel;
        [HideInInspector] public Button autoLootToggleButton;
        [HideInInspector] public TextMeshProUGUI autoLootToggleLabel;
        [HideInInspector] public SkillBarPanel skillBarPanel;

        [Header("XP Feedback")]
        [SerializeField, Min(0.01f)] private float xpPulseDuration = 0.2f;
        [SerializeField, Min(1f)] private float xpPulseScale = 1.08f;
        [SerializeField] private Color xpPulseColor = UITheme.GoldPale;

        private IPanelView _openSubPanel;
        private bool _combatControlsAreBound;
        private float _lastCanvasWidth = -1f;
        private GameManager _progressionManager;
        private Coroutine _xpPulseRoutine;
        private Color _xpBaseColor;
        private Vector3 _xpBaseScale = Vector3.one;
        private bool _xpPulseBaselineCaptured;

        private void OnEnable()
        {
            BindCombatControls();
            ClampWidthToCanvas();
        }

        private void Start()
        {
            BindButton(activityButton, "Activity", activityPanel);
            BindButton(travelButton, "Travel", travelPanel);
            BindButton(inventoryButton, "Inventory", inventoryPanel);
            BindButton(equipmentButton, "Equipment", equipmentPanel);
            BindButton(bankButton, "Bank", bankPanel);
            BindButton(craftingButton, "Crafting", craftingPanel);
            BindButton(talentsButton, "Talents", talentsPanel);
            BindButton(skillsButton, "Skills", skillsPanel);
            BindCombatControls();
            Refresh();
        }

        private void BindCombatControls()
        {
            if (_combatControlsAreBound) return;
            if (autoToggleButton == null && autoLootToggleButton == null) return;

            if (autoToggleButton != null)
                autoToggleButton.onClick.AddListener(HandleAutoToggle);
            if (autoLootToggleButton != null)
                autoLootToggleButton.onClick.AddListener(HandleAutoLootToggle);
            _combatControlsAreBound = true;
        }

        private void HandleAutoToggle()
        {
            GameManager.Instance?.ToggleAutoCombat();
            Refresh();
        }

        private void HandleAutoLootToggle()
        {
            GameManager.Instance?.ToggleAutoLoot();
            Refresh();
        }

        private void Update()
        {
            BindProgressionEvents();
            ClampWidthToCanvas();
        }

        private void OnDestroy()
        {
            UnsubscribeProgressionEvents();
            if (_xpPulseRoutine != null) StopCoroutine(_xpPulseRoutine);
            RestoreXpPulseBaseline();
        }

        private void OnDisable()
        {
            if (_xpPulseRoutine != null)
            {
                StopCoroutine(_xpPulseRoutine);
                _xpPulseRoutine = null;
            }
            RestoreXpPulseBaseline();
        }

        private void BindProgressionEvents()
        {
            GameManager current = GameManager.Instance;
            if (_progressionManager == current) return;

            UnsubscribeProgressionEvents();
            _progressionManager = current;
            if (_progressionManager != null)
                _progressionManager.XpAwarded += HandleXpAwarded;
        }

        private void UnsubscribeProgressionEvents()
        {
            if (_progressionManager == null) return;
            _progressionManager.XpAwarded -= HandleXpAwarded;
            _progressionManager = null;
        }

        private void HandleXpAwarded(XpAwardedEvent payload)
        {
            if (!isActiveAndEnabled || payload == null || xpFill == null) return;
            if (payload.CharacterXp <= 0 && payload.SkillXp <= 0) return;

            CaptureXpPulseBaseline();
            if (_xpPulseRoutine != null)
            {
                StopCoroutine(_xpPulseRoutine);
                RestoreXpPulseBaseline();
            }
            _xpPulseRoutine = StartCoroutine(AnimateXpPulse());
        }

        private void CaptureXpPulseBaseline()
        {
            if (_xpPulseBaselineCaptured || xpFill == null) return;
            _xpBaseColor = xpFill.color;
            _xpBaseScale = xpFill.transform.localScale;
            _xpPulseBaselineCaptured = true;
        }

        private IEnumerator AnimateXpPulse()
        {
            float duration = Mathf.Max(0.01f, xpPulseDuration);
            float elapsed = 0f;
            while (elapsed < duration && xpFill != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float pulse = progress < 0.5f
                    ? progress * 2f
                    : (1f - progress) * 2f;
                xpFill.transform.localScale = _xpBaseScale *
                    Mathf.Lerp(1f, xpPulseScale, pulse);
                xpFill.color = Color.Lerp(_xpBaseColor, xpPulseColor, pulse);
                yield return null;
            }
            RestoreXpPulseBaseline();
            _xpPulseRoutine = null;
        }

        private void RestoreXpPulseBaseline()
        {
            if (!_xpPulseBaselineCaptured || xpFill == null) return;
            xpFill.color = _xpBaseColor;
            xpFill.transform.localScale = _xpBaseScale;
        }

        // The HUD's baked width assumes the reference resolution; on narrower aspect
        // ratios the scaled canvas is thinner than the reference, so re-clamp whenever
        // the canvas width changes (window resize, aspect change).
        private void ClampWidthToCanvas()
        {
            var rt = transform as RectTransform;
            var parentRT = transform.parent as RectTransform;
            if (rt == null || parentRT == null) return;

            float canvasWidth = parentRT.rect.width;
            if (canvasWidth <= 0f || Mathf.Approximately(canvasWidth, _lastCanvasWidth)) return;
            _lastCanvasWidth = canvasWidth;
            rt.sizeDelta = new Vector2(
                Mathf.Min(UITheme.Layout.HudWidth, canvasWidth), rt.sizeDelta.y);
        }

        private void BindButton(Button button, string buttonName, IPanelView panel)
        {
            if (button == null)
            {
                Debug.LogWarning($"[MainHudPanel] Missing button reference for '{buttonName}'.", this);
                return;
            }

            if (panel == null) return;
            button.onClick.AddListener(() => ToggleSubPanel(panel));
        }

        public void ToggleSubPanel(IPanelView panel)
        {
            if (_openSubPanel == panel)
            {
                panel.Hide();
                _openSubPanel = null;
                return;
            }
            _openSubPanel?.Hide();
            _openSubPanel = panel;
            panel.Show();
        }

        /// <summary>Opens a world-service panel without treating a second trigger as a toggle-to-close.</summary>
        public void OpenSubPanel(IPanelView panel)
        {
            if (panel == null) return;
            if (_openSubPanel != panel)
            {
                _openSubPanel?.Hide();
                _openSubPanel = panel;
            }

            if (panel.IsVisible) panel.Refresh();
            else panel.Show();
        }

        public void CloseSubPanels()
        {
            _openSubPanel?.Hide();
            _openSubPanel = null;
        }

        public void Refresh()
        {
            var gm = GameManager.Instance;
            var ch = gm?.GetSelectedCharacter();
            if (ch == null) return;

            var cls    = RuntimeContent.Get(ch.ClassId);
            var stats  = Progression.EffectiveStats(ch, cls, new Dictionary<string, ItemDef>(RuntimeContent.Items), AccountBonuses.Zero());
            int maxHp  = CombatMath.MaxHp(ch.Level, stats);
            int xpNext = Progression.XpToNext(ch.Level);
            int coins  = gm.Account?.Bank?.Coins ?? 0;
            RuntimeContent.Maps.TryGetValue(ch.MapId, out var map);

            nameLabel.text = $"{ch.Name}  Lv.{ch.Level}";

            ActiveCombatState combat = gm.ActiveCombatState;
            int currentHp = combat == null ? maxHp : Mathf.Clamp(combat.PlayerHp, 0, maxHp);
            hpFill.fillAmount = maxHp > 0 ? currentHp / (float)maxHp : 0f;
            hpLabel.text      = $"HP {currentHp}/{maxHp}";

            float xpFrac = xpNext > 0 ? Mathf.Clamp01(ch.Xp / (float)xpNext) : 0f;
            xpFill.fillAmount = xpFrac;
            xpLabel.text      = $"XP {ch.Xp}/{xpNext}";

            goldLabel.text = $"{coins} gold";
            mapLabel.text  = map?.Name ?? ch.MapId;

            activityLabel.text = (ch.Activity == null || ch.Activity.Kind == ActivityKind.Idle)
                ? "Idle"
                : $"{ch.Activity.Kind}: {ch.Activity.TargetId}";

            if (autoToggleLabel != null)
            {
                bool enabled = gm.AutoCombatEnabled;
                autoToggleLabel.text = enabled ? "Auto ON" : "Auto OFF";
                autoToggleLabel.color = enabled ? UITheme.TextGold : UITheme.TextDim;
            }
            if (autoLootToggleLabel != null)
            {
                bool enabled = gm.Account?.AutoLoot ?? false;
                autoLootToggleLabel.text = enabled ? "Loot ON" : "Loot OFF";
                autoLootToggleLabel.color = enabled ? UITheme.TextGold : UITheme.TextDim;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            skillBarPanel?.Show();
            Refresh();
        }

        public void Hide()
        {
            skillBarPanel?.Hide();
            CloseSubPanels();
            gameObject.SetActive(false);
        }
        public bool IsVisible => gameObject.activeSelf;
    }
}
