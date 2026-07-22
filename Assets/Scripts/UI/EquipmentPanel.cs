using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using System.Collections.Generic;

namespace IdleCloud.UI
{
    public class EquipmentPanel : MonoBehaviour, IPanelView
    {
        [HideInInspector] public RectTransform listContainer;

        private static readonly EquipSlot[] Slots =
            { EquipSlot.Weapon, EquipSlot.Helmet, EquipSlot.Chest, EquipSlot.Legs, EquipSlot.Tool };

        private Dictionary<EquipSlot, string> _lastEquip;

        public void Refresh()
        {
            var ch = GameManager.Instance?.GetSelectedCharacter();
            if (ch == null) return;
            if (EquipUnchanged(ch.Equipment)) return;
            _lastEquip = ch.Equipment != null
                ? new Dictionary<EquipSlot, string>(ch.Equipment) : null;
            RebuildList(ch);
        }

        private bool EquipUnchanged(Dictionary<EquipSlot, string> eq)
        {
            if (_lastEquip == null && eq == null) return true;
            if (_lastEquip == null || eq == null) return false;
            foreach (var s in Slots)
            {
                _lastEquip.TryGetValue(s, out string a);
                eq.TryGetValue(s, out string b);
                if (a != b) return false;
            }
            return true;
        }

        private void RebuildList(Character ch)
        {
            if (listContainer == null) return;
            foreach (Transform t in listContainer) Destroy(t.gameObject);

            foreach (var slot in Slots)
            {
                string slotName = slot.ToString();

                // Maak de variabele eerst netjes aan met een veilige beginwaarde (null)
                string itemId = null;
                if (ch.Equipment != null)
                {
                    ch.Equipment.TryGetValue(slot, out itemId);
                }

                if (string.IsNullOrEmpty(itemId))
                {
                    ItemSlotWidget.CreateEmpty(listContainer, slotName);
                }
                else
                {
                    RuntimeContent.Items.TryGetValue(itemId, out ItemDef def);
                    string name = def?.Name ?? itemId;
                    EquipSlot capturedSlot = slot;
                    ItemSlotWidget.Create(listContainer, $"[{slotName}]  {name}", 0,
                        "Unequip", UIHelpers.AccentGray,
                        () => { GameManager.Instance.UnequipItem(capturedSlot); _lastEquip = null; Refresh(); });
                }
            }
        }

        public void Show()  { _lastEquip = null; gameObject.SetActive(true); Refresh(); }
        public void Hide()  => gameObject.SetActive(false);
        public bool IsVisible => gameObject.activeSelf;
    }
}
