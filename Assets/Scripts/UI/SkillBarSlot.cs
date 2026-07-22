using UnityEngine;
using UnityEngine.EventSystems;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    /// <summary>Drag source and drop target for one persisted skillbar slot.</summary>
    public class SkillBarSlot : MonoBehaviour, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [HideInInspector] public int SlotIndex;
        [HideInInspector] public SkillBarPanel Panel;
        [HideInInspector] public SkillDragController DragController;

        public void OnDrop(PointerEventData eventData)
        {
            if (DragController == null || !DragController.HasPayload) return;

            GameManager manager = GameManager.Instance;
            if (manager != null)
            {
                if (DragController.Source == SkillDragSourceKind.SkillBar)
                    manager.SwapSkillBarSlots(DragController.SourceSlotIndex, SlotIndex);
                else
                    manager.AssignSkillToBar(DragController.SkillId, SlotIndex);
            }
            DragController.MarkDropHandled();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            ClassSkillDef skill = AssignedSkill();
            if (skill == null || DragController == null) return;

            DragController.BeginDrag(skill.Id, SkillDragSourceKind.SkillBar, SlotIndex,
                skill.Name ?? skill.Id, SkillDragController.SkillColor(skill), eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            DragController?.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (DragController == null) return;
            if (DragController.HasPayload && DragController.Source == SkillDragSourceKind.SkillBar &&
                !DragController.DropHandled)
                GameManager.Instance?.ClearSkillBarSlot(SlotIndex);
            DragController.EndDrag();
        }

        private ClassSkillDef AssignedSkill()
        {
            Character character = GameManager.Instance?.GetSelectedCharacter();
            if (character?.SkillBar == null || SlotIndex < 0 || SlotIndex >= character.SkillBar.Count) return null;

            string skillId = character.SkillBar[SlotIndex];
            if (string.IsNullOrWhiteSpace(skillId)) return null;
            ClassDef classDef = RuntimeContent.Get(character.ClassId);
            foreach (ClassSkillDef skill in classDef?.Skills ?? new System.Collections.Generic.List<ClassSkillDef>())
                if (skill != null && skill.Id == skillId)
                    return skill;
            return null;
        }
    }
}
