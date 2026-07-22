using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    public class BankPanel : MonoBehaviour, IPanelView
    {
        [HideInInspector] public TextMeshProUGUI coinsLabel;
        [HideInInspector] public TextMeshProUGUI headerLabel;
        [HideInInspector] public RectTransform   bankContainer;
        [HideInInspector] public RectTransform   inventoryContainer;

        private int _lastBankCount = -1;
        private int _lastInvCount  = -1;

        public void Refresh()
        {
            var gm = GameManager.Instance;
            var ch = gm?.GetSelectedCharacter();
            if (ch == null) return;

            int bankCount = gm.Account?.Bank?.Slots?.Count ?? 0;
            int invCount  = ch.Inventory?.Count ?? 0;
            int coins     = gm.Account?.Bank?.Coins ?? 0;

            if (coinsLabel != null) coinsLabel.text = $"Gold: {coins}";

            if (bankCount != _lastBankCount || invCount != _lastInvCount)
            {
                _lastBankCount = bankCount;
                _lastInvCount  = invCount;
                RebuildBank(gm, ch);
            }
        }

        private void RebuildBank(GameManager gm, Character ch)
        {
            RebuildSection(bankContainer, gm.Account?.Bank?.Slots, "Bank empty.",
                (itemId) => { GameManager.Instance.Withdraw(itemId, 1); _lastBankCount = -1; Refresh(); },
                "Withdraw");

            RebuildSection(inventoryContainer, ch.Inventory, "Inventory empty.",
                (itemId) => { GameManager.Instance.Deposit(itemId, 1); _lastInvCount = -1; Refresh(); },
                "Deposit");
        }

        private void RebuildSection(RectTransform container, System.Collections.Generic.List<ItemStack> items,
            string emptyMsg, System.Action<string> onBtn, string btnLabel)
        {
            if (container == null) return;
            foreach (Transform t in container) Destroy(t.gameObject);

            if (items == null || items.Count == 0) { AddNote(container, emptyMsg); return; }

            foreach (var stack in items)
            {
                RuntimeContent.Items.TryGetValue(stack.ItemId, out ItemDef def);
                string name   = def?.Name ?? stack.ItemId;
                string itemId = stack.ItemId;
                ItemSlotWidget.Create(container, name, stack.Qty,
                    btnLabel, UIHelpers.AccentBlue,
                    () => onBtn(itemId));
            }
        }

        private void AddNote(RectTransform container, string text)
        {
            var go  = new GameObject("Note");
            go.transform.SetParent(container, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = 13;
            tmp.color     = new Color(0.45f, 0.45f, 0.50f);
            tmp.alignment = TextAlignmentOptions.Left;
            UIHelpers.AddLayout(go, preferredH: 24);
        }

        public void Show()  { _lastBankCount = -1; _lastInvCount = -1; gameObject.SetActive(true); Refresh(); }
        public void Hide()  => gameObject.SetActive(false);
        public bool IsVisible => gameObject.activeSelf;
    }
}
