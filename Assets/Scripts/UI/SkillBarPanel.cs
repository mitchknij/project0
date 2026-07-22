using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    /// <summary>Standalone eight-slot manual skillbar with hotkeys and cooldown state.</summary>
    public class SkillBarPanel : MonoBehaviour, IPanelView
    {
        [HideInInspector] public Button[] slotButtons;
        [HideInInspector] public Image[] slotCooldownFills;
        [HideInInspector] public Image[] slotAutoHighlights;
        [HideInInspector] public TextMeshProUGUI[] slotHotkeyLabels;
        [HideInInspector] public TextMeshProUGUI[] slotNameLabels;
        [HideInInspector] public SkillBarSlot[] slotComponents;

        private bool _buttonsAreBound;
        private bool _combatEventsBound;
        private float[] _feedbackUntil;
        private string[] _feedbackText;
        private Color[] _feedbackColor;
        private float[] _autoHighlightUntil;
        private long _lastAutoSelectionSequenceId = 0;
        private ActiveCombatState _lastObservedCombatState;
        private static readonly Key[] SkillBarHotkeys = new Key[Character.SkillBarSlots]
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8,
        };

        private void OnEnable()
        {
            BindButtons();
            BindCombatEvents();
        }

        private void Start()
        {
            BindButtons();
            BindCombatEvents();
            Refresh();
        }

        private void OnDisable()
        {
            if (_combatEventsBound && GameManager.Instance != null)
                GameManager.Instance.ActiveCombatResolved -= HandleCombatResult;
            _combatEventsBound = false;
        }

        private void BindButtons()
        {
            if (_buttonsAreBound || slotButtons == null) return;
            for (int index = 0; index < slotButtons.Length; index++)
            {
                int slotIndex = index;
                if (slotButtons[slotIndex] != null)
                    slotButtons[slotIndex].onClick.AddListener(() => TriggerSkillSlot(slotIndex));
            }
            _buttonsAreBound = true;
        }

        private void Update()
        {
            BindCombatEvents();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (int slotIndex = 0; slotIndex < Character.SkillBarSlots; slotIndex++)
                    if (keyboard[SkillBarHotkeys[slotIndex]].wasPressedThisFrame)
                        TriggerSkillSlot(slotIndex);
            }
            ActiveCombatState combat = GameManager.Instance?.ActiveCombatState;
            if (!ReferenceEquals(combat, _lastObservedCombatState))
            {
                // A new combat restarts the sequence counter, so a stale observed id could
                // coincide with the new combat's first id and swallow that pulse.
                _lastObservedCombatState = combat;
                _lastAutoSelectionSequenceId = 0;
            }
            AutoSkillSelectionDiagnostics selection = combat?.SkillRuntime?.LastAutoSelection;
            if (selection != null && selection.SelectedSlotIndex >= 0 &&
                selection.SelectionSequenceId != _lastAutoSelectionSequenceId)
            {
                _lastAutoSelectionSequenceId = selection.SelectionSequenceId;
                if (_autoHighlightUntil != null && selection.SelectedSlotIndex < _autoHighlightUntil.Length)
                    _autoHighlightUntil[selection.SelectedSlotIndex] =
                        Time.unscaledTime + UITheme.Layout.SkillBarAutoHighlightSeconds;
            }
            RefreshSkillCooldowns();
            RefreshFeedback();
            RefreshAutoHighlight();
        }

        private void BindCombatEvents()
        {
            if (_combatEventsBound || GameManager.Instance == null) return;
            GameManager.Instance.ActiveCombatResolved += HandleCombatResult;
            _combatEventsBound = true;
            int count = slotButtons?.Length ?? Character.SkillBarSlots;
            _feedbackUntil = new float[count];
            _feedbackText = new string[count];
            _feedbackColor = new Color[count];
            _autoHighlightUntil = new float[count];
        }

        public void TriggerSkillSlot(int slotIndex)
        {
            GameManager manager = GameManager.Instance;
            Character character = manager?.GetSelectedCharacter();
            if (character?.SkillBar == null || slotIndex < 0 || slotIndex >= character.SkillBar.Count) return;

        ClassSkillDef skill = FindSkill(RuntimeContent.Get(character.ClassId), character.SkillBar[slotIndex]);
            if (skill == null ||
                (skill.DamageMultiplier <= 0.0 && skill.ModifierDurationTicks <= 0 && skill.Buff == null)) return;

            ActiveCombatState combat = manager.ActiveCombatState;
            if (combat == null || string.IsNullOrWhiteSpace(combat.TargetId))
            {
                ShowFeedback(slotIndex, "NO TARGET", new Color(1f, 0.35f, 0.3f));
                return;
            }
            long nowMs = combat?.LastUpdatedAt ?? 0L;
            if (combat?.SkillNextReadyAt != null &&
                combat.SkillNextReadyAt.TryGetValue(skill.Id, out long readyAt) && readyAt > nowMs)
            {
                ShowFeedback(slotIndex, "COOLDOWN", new Color(1f, 0.55f, 0.2f));
                return;
            }
            manager.EnqueueCombatCommand(new CombatCommand
            {
                Kind = CombatCommandKind.TriggerSkill,
                SkillId = skill.Id,
            });
            ShowFeedback(slotIndex, "QUEUED", new Color(0.7f, 0.85f, 1f));
        }

        private void HandleCombatResult(ActiveCombatTickResult result)
        {
            if (result?.Simulation?.Events == null) return;
            Character character = GameManager.Instance?.GetSelectedCharacter();
            foreach (CombatEvent combatEvent in result.Simulation.Events)
            {
                if (combatEvent.Kind == CombatEventKind.SkillCastStarted)
                {
                    int slot = FindSlot(character, combatEvent.SkillId);
                    if (slot >= 0) ShowFeedback(slot, "CASTING", new Color(0.72f, 0.5f, 1f));
                }
                else if (combatEvent.Kind == CombatEventKind.SkillExecuted)
                {
                    int slot = FindSlot(character, combatEvent.SkillId);
                    if (slot >= 0) ShowFeedback(slot, "CAST!", new Color(0.4f, 1f, 0.55f));
                }
                else if (combatEvent.Kind == CombatEventKind.CommandRejected)
                {
                    int slot = FindSlot(character, combatEvent.TargetId);
                    if (slot >= 0)
                        ShowFeedback(slot, FriendlyReason(combatEvent.Reason), new Color(1f, 0.35f, 0.3f));
                }
            }
        }

        private static int FindSlot(Character character, string skillId)
        {
            if (character?.SkillBar == null || string.IsNullOrWhiteSpace(skillId)) return -1;
            for (int index = 0; index < character.SkillBar.Count && index < Character.SkillBarSlots; index++)
                if (character.SkillBar[index] == skillId) return index;
            return -1;
        }

        private static string FriendlyReason(string reason)
        {
            return reason switch
            {
                "skill_on_cooldown" => "COOLDOWN",
                "no_valid_target" => "NO TARGET",
                "target_out_of_range" => "OUT OF RANGE",
                "unknown_skill" => "UNKNOWN",
                "skill_locked" => "LOCKED",
                "unsupported_skill" => "UNSUPPORTED",
                _ => "REJECTED",
            };
        }

        private void ShowFeedback(int slotIndex, string message, Color color)
        {
            if (_feedbackUntil == null || slotIndex < 0 || slotIndex >= _feedbackUntil.Length) return;
            _feedbackUntil[slotIndex] = Time.unscaledTime + 0.8f;
            _feedbackText[slotIndex] = message;
            _feedbackColor[slotIndex] = color;
            RefreshFeedback();
        }

        private void RefreshFeedback()
        {
            if (_feedbackUntil == null || slotNameLabels == null) return;
            Character character = GameManager.Instance?.GetSelectedCharacter();
        ClassDef classDef = character == null ? null : RuntimeContent.Get(character.ClassId);
            int count = Mathf.Min(slotNameLabels.Length, Character.SkillBarSlots);
            for (int index = 0; index < count; index++)
            {
                TextMeshProUGUI label = slotNameLabels[index];
                if (label == null) continue;
                if (Time.unscaledTime < _feedbackUntil[index])
                {
                    label.text = _feedbackText[index];
                    label.color = _feedbackColor[index];
                    continue;
                }
                string skillId = character?.SkillBar != null && index < character.SkillBar.Count
                    ? character.SkillBar[index]
                    : null;
                ClassSkillDef skill = FindSkill(classDef, skillId);
                label.text = skill == null ? "-" : skill.Name ?? skill.Id;
                label.color = SkillDragController.SkillColor(skill);
            }
        }

        public void Refresh()
        {
            GameManager manager = GameManager.Instance;
            Character character = manager?.GetSelectedCharacter();
        ClassDef classDef = character == null ? null : RuntimeContent.Get(character.ClassId);
            int authoredButtonCount = slotButtons?.Length ?? Character.SkillBarSlots;
            int slotCount = Mathf.Min(authoredButtonCount, Character.SkillBarSlots);

            if (slotButtons != null)
                for (int index = 0; index < slotButtons.Length; index++)
                    if (slotButtons[index] != null)
                        slotButtons[index].gameObject.SetActive(index < Character.SkillBarSlots);

            for (int index = 0; index < slotCount; index++)
            {
                string skillId = character?.SkillBar != null && index < character.SkillBar.Count
                    ? character.SkillBar[index]
                    : null;
                ClassSkillDef skill = FindSkill(classDef, skillId);
                bool usable = skill != null &&
                    (skill.DamageMultiplier > 0.0 || skill.ModifierDurationTicks > 0 || skill.Buff != null);

                if (slotButtons != null && index < slotButtons.Length && slotButtons[index] != null)
                    slotButtons[index].interactable = usable;
                if (slotNameLabels != null && index < slotNameLabels.Length && slotNameLabels[index] != null)
                {
                    slotNameLabels[index].text = skill == null ? "-" : skill.Name ?? skill.Id;
                    slotNameLabels[index].color = SkillDragController.SkillColor(skill);
                }
                if (slotHotkeyLabels != null && index < slotHotkeyLabels.Length && slotHotkeyLabels[index] != null)
                    slotHotkeyLabels[index].text = (index + 1).ToString();
            }
            RefreshSkillCooldowns();
        }

        private void RefreshSkillCooldowns()
        {
            if (slotCooldownFills == null) return;
            GameManager manager = GameManager.Instance;
            Character character = manager?.GetSelectedCharacter();
        ClassDef classDef = character == null ? null : RuntimeContent.Get(character.ClassId);
            ActiveCombatState combat = manager?.ActiveCombatState;
            long nowMs = combat?.LastUpdatedAt ?? 0L;

            for (int index = 0; index < slotCooldownFills.Length; index++)
            {
                Image fill = slotCooldownFills[index];
                if (fill == null) continue;
                string skillId = character?.SkillBar != null && index < character.SkillBar.Count
                    ? character.SkillBar[index]
                    : null;
                ClassSkillDef skill = FindSkill(classDef, skillId);
                if (skill == null || skill.CooldownMs <= 0)
                {
                    fill.fillAmount = 0f;
                    continue;
                }

                long readyAtMs = combat?.SkillNextReadyAt != null &&
                    combat.SkillNextReadyAt.TryGetValue(skill.Id, out long value)
                    ? value
                    : nowMs;
                fill.fillAmount = Mathf.Clamp01((readyAtMs - nowMs) / (float)skill.CooldownMs);
            }
        }

        private void RefreshAutoHighlight()
        {
            if (_autoHighlightUntil == null || slotAutoHighlights == null) return;
            float now = Time.unscaledTime;
            float duration = UITheme.Layout.SkillBarAutoHighlightSeconds;
            int count = Mathf.Min(_autoHighlightUntil.Length, slotAutoHighlights.Length);
            for (int index = 0; index < count; index++)
            {
                Image highlight = slotAutoHighlights[index];
                if (highlight == null) continue;
                Color color = UITheme.SkillBarAutoHighlightTint;
                color.a = Mathf.Clamp01((_autoHighlightUntil[index] - now) / duration) *
                    UITheme.Layout.SkillBarAutoHighlightAlpha;
                highlight.color = color;
            }
        }

        private static ClassSkillDef FindSkill(ClassDef classDef, string skillId)
        {
            if (classDef?.Skills == null || string.IsNullOrWhiteSpace(skillId)) return null;
            foreach (ClassSkillDef skill in classDef.Skills)
                if (skill != null && skill.Id == skillId)
                    return skill;
            return null;
        }

        public void Show() { gameObject.SetActive(true); Refresh(); }
        public void Hide() => gameObject.SetActive(false);
        public bool IsVisible => gameObject.activeSelf;
    }
}
