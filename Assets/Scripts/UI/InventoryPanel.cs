using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    public class InventoryPanel : MonoBehaviour, IPanelView
    {
        [HideInInspector] public RectTransform listContainer;
        [HideInInspector] public TextMeshProUGUI headerLabel;

        private int _lastCount = -1;

        public void Refresh()
        {
            var ch = GameManager.Instance?.GetSelectedCharacter();
            if (ch == null) return;
            int count = ch.Inventory?.Count ?? 0;
            if (count == _lastCount) return;
            _lastCount = count;
            RebuildList(ch);
        }

        private void RebuildList(Character ch)
        {
            if (listContainer == null) return;
            foreach (Transform t in listContainer) Destroy(t.gameObject);

            int used = ch.Inventory?.Count ?? 0;
            if (headerLabel != null)
                headerLabel.text = $"Inventory  ({used}/{ch.MaxInventorySlots})";

            if (used == 0) { AddNote("Inventory empty."); return; }

            foreach (var stack in ch.Inventory)
            {
                RuntimeContent.Items.TryGetValue(stack.ItemId, out ItemDef def);
                string name    = def?.Name ?? stack.ItemId;
                bool equippable = def?.Slot != null;
                string itemId  = stack.ItemId;

                ItemSlotWidget.Create(listContainer, name, stack.Qty,
                    equippable ? "Equip" : "-",
                    equippable ? UIHelpers.AccentBlue : UIHelpers.AccentGray,
                    equippable ? () => { GameManager.Instance.EquipItem(itemId); _lastCount = -1; Refresh(); }
                               : (System.Action)null);
            }
        }

        private void AddNote(string text)
        {
            var go  = new GameObject("Note");
            go.transform.SetParent(listContainer, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = 14;
            tmp.color     = new Color(0.45f, 0.45f, 0.50f);
            tmp.alignment = TextAlignmentOptions.Left;
            UIHelpers.AddLayout(go, preferredH: 28);
        }

        public void Show()  { _lastCount = -1; gameObject.SetActive(true); Refresh(); }
        public void Hide()  => gameObject.SetActive(false);
        public bool IsVisible => gameObject.activeSelf;
    }
}
