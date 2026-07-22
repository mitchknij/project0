using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using IdleCloud.Core;

namespace IdleCloud.UI
{
    public enum SkillDragSourceKind
    {
        SkillsPanel,
        SkillBar,
    }

    /// <summary>
    /// Owns the one active skill drag payload and its reusable, non-raycastable ghost.
    /// </summary>
    public class SkillDragController : MonoBehaviour
    {
        [HideInInspector] public RectTransform ghost;
        [HideInInspector] public TextMeshProUGUI ghostLabel;

        private bool _hasPayload;
        private bool _dropHandled;
        private string _skillId;
        private SkillDragSourceKind _source;
        private int _sourceSlotIndex = -1;

        public bool HasPayload => _hasPayload;
        public bool DropHandled => _dropHandled;
        public string SkillId => _skillId;
        public SkillDragSourceKind Source => _source;
        public int SourceSlotIndex => _sourceSlotIndex;

        public void BeginDrag(string skillId, SkillDragSourceKind source, int sourceSlotIndex,
            string displayName, Color color, PointerEventData eventData)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return;

            _hasPayload = true;
            _dropHandled = false;
            _skillId = skillId;
            _source = source;
            _sourceSlotIndex = sourceSlotIndex;

            if (ghostLabel != null)
            {
                ghostLabel.text = displayName ?? skillId;
                ghostLabel.color = color;
            }
            if (ghost != null)
            {
                ghost.gameObject.SetActive(true);
                UpdateDrag(eventData);
            }
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (!_hasPayload || eventData == null || ghost == null) return;
            ghost.position = eventData.position;
        }

        public void MarkDropHandled() => _dropHandled = true;

        public void EndDrag()
        {
            if (ghost != null) ghost.gameObject.SetActive(false);
            _hasPayload = false;
            _dropHandled = false;
            _skillId = null;
            _sourceSlotIndex = -1;
        }

        public static Color SkillColor(ClassSkillDef skill)
        {
            if (skill == null) return UITheme.TextDim;
            return ColorUtility.TryParseHtmlString("#" + skill.AoeColor.ToString("X6"), out Color color)
                ? color
                : UITheme.TextGold;
        }
    }
}
