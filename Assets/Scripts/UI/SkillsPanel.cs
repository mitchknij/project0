using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using TMPro;

namespace IdleCloud.UI
{
    /// <summary>Lists the selected class's skills and provides their drag sources.</summary>
    public class SkillsPanel : MonoBehaviour, IPanelView
    {
        [HideInInspector] public RectTransform listContainer;
        [HideInInspector] public SkillDragController dragController;
        [HideInInspector] public TextMeshProUGUI pointsLabel;
        [HideInInspector] public Button resetButton;
        private string _renderedCharacterId;
        private long _renderedRevision = -1;

        private void Start()
        {
            if (resetButton != null) resetButton.onClick.AddListener(OnReset);
        }

        public void Refresh()
        {
            Character character = GameManager.Instance?.GetSelectedCharacter();
            if (!IsVisible || character == null || (dragController != null && dragController.HasPayload)) return;
            if (_renderedCharacterId != character.Id || _renderedRevision != character.CharacterRevision)
                Rebuild();
        }

        private void OnReset()
        {
            GameManager.Instance?.DevelopmentRespecSkills();
            Rebuild();
        }

        private void Rebuild()
        {
            if (listContainer == null) return;
            foreach (Transform child in listContainer) Destroy(child.gameObject);

            Character character = GameManager.Instance?.GetSelectedCharacter();
            ClassDef classDef = character == null ? null : RuntimeContent.Get(character.ClassId);
            _renderedCharacterId = character?.Id;
            _renderedRevision = character?.CharacterRevision ?? -1;
            if (pointsLabel != null && character != null)
                pointsLabel.text = $"Skill Points: {character.AvailableSkillPoints}    Spent: {character.SpentSkillPoints}";
            if (classDef?.Skills == null)
            {
                AddNote("No skills available.");
                return;
            }

            foreach (ClassSkillDef skill in classDef.Skills)
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.Id)) continue;
                AddSkillRow(character, skill);
            }
        }

        private void AddSkillRow(Character character, ClassSkillDef skill)
        {
            bool unlocked = SkillBuild.IsUnlocked(character, skill.Id);
            var row = new GameObject("SkillEntry_" + skill.Id);
            row.transform.SetParent(listContainer, false);
            UIHelpers.AddFrame(row, UITheme.InsetFrame);
            UIHelpers.AddLayout(row, preferredH: UITheme.Layout.SkillPanelRowHeight,
                minH: UITheme.Layout.SkillPanelRowHeight);
            UIHelpers.AddVLG(row,
                spacing: UITheme.Layout.SkillPanelSpacing,
                padding: UITheme.Layout.SkillPanelPadding,
                controlHeight: true);

            var header = new GameObject("Header");
            header.transform.SetParent(row.transform, false);
            UIHelpers.AddLayout(header, preferredH: UITheme.Layout.SkillPanelHeaderHeight);
            UIHelpers.AddHLG(header, spacing: UITheme.Layout.StandardSpacing, controlWidth: true);

            TextMeshProUGUI name = UIHelpers.CreateHeader(header.transform, skill.Name ?? skill.Id, 15);
            name.color = SkillDragController.SkillColor(skill);
            name.raycastTarget = false;
            UIHelpers.AddLayout(name.gameObject, flexW: 1);

            TextMeshProUGUI cooldown = UIHelpers.CreateLabel(header.transform,
                $"{skill.CooldownMs / 1000.0:0.##}s", 13, align: TextAlignmentOptions.Right);
            cooldown.color = UITheme.TextDim;
            cooldown.raycastTarget = false;
            UIHelpers.AddLayout(cooldown.gameObject, preferredW: UITheme.Layout.SkillPanelCooldownWidth);

            if (!unlocked)
            {
                Button unlock = UIHelpers.CreateButton(header.transform,
                    $"Unlock ({skill.SkillPointCost})", UIHelpers.AccentBlue);
                UIHelpers.AddLayout(unlock.gameObject, preferredW: 112);
                unlock.interactable = SkillBuild.CanUnlock(character, skill);
                string skillId = skill.Id;
                unlock.onClick.AddListener(() =>
                {
                    GameManager.Instance?.UnlockSkill(skillId);
                    Rebuild();
                });
            }

            TextMeshProUGUI description = UIHelpers.CreateLabel(row.transform,
                skill.Description ?? "-", 12, align: TextAlignmentOptions.Left);
            description.color = UITheme.TextMain;
            description.textWrappingMode = TextWrappingModes.Normal;
            description.raycastTarget = false;
            UIHelpers.AddLayout(description.gameObject, preferredH: UITheme.Layout.SkillPanelDescriptionHeight);

            TextMeshProUGUI mechanic = UIHelpers.CreateLabel(row.transform,
                $"{skill.BranchId} · Tier {skill.Tier} · {skill.Mechanic} · {(unlocked ? "UNLOCKED" : "LOCKED")}",
                12, align: TextAlignmentOptions.Left);
            mechanic.color = UITheme.TextDim;
            mechanic.raycastTarget = false;
            UIHelpers.AddLayout(mechanic.gameObject, preferredH: UITheme.Layout.SkillPanelMechanicHeight);

            var entry = row.AddComponent<SkillPanelEntry>();
            entry.SkillId = skill.Id;
            entry.DisplayName = skill.Name ?? skill.Id;
            entry.SkillColor = SkillDragController.SkillColor(skill);
            entry.DragController = dragController;
            entry.Unlocked = unlocked;
        }

        private void AddNote(string text)
        {
            TextMeshProUGUI note = UIHelpers.CreateLabel(listContainer, text, 14, align: TextAlignmentOptions.Left);
            note.color = UITheme.TextDim;
            UIHelpers.AddLayout(note.gameObject, preferredH: UITheme.Layout.SkillPanelMechanicHeight);
        }

        public void Show() { gameObject.SetActive(true); Rebuild(); }
        public void Hide() => gameObject.SetActive(false);
        public bool IsVisible => gameObject.activeSelf;
    }

    /// <summary>Panel-origin drag source kept beside SkillsPanel to avoid another UI file.</summary>
    public class SkillPanelEntry : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [HideInInspector] public string SkillId;
        [HideInInspector] public string DisplayName;
        [HideInInspector] public Color SkillColor;
        [HideInInspector] public SkillDragController DragController;
        [HideInInspector] public bool Unlocked;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Unlocked) return;
            DragController?.BeginDrag(SkillId, SkillDragSourceKind.SkillsPanel, -1,
                DisplayName, SkillColor, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            DragController?.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            DragController?.EndDrag();
        }
    }
}
