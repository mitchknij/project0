// OfflineReportPanel.cs — Modal die het offline-voortgangsrapport toont na herstart.
// Wordt door GameFlowController getoond zodra GameManager.OfflineReport niet-null is;
// Claim-knop wist het rapport en verbergt het paneel.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    public class OfflineReportPanel : MonoBehaviour, IPanelView
    {
        // ── Referenties (ingesteld door UIBuilder) ────────────────────────────

        [HideInInspector] public TextMeshProUGUI elapsedLabel;
        [HideInInspector] public RectTransform    listContainer;
        [HideInInspector] public Button           claimButton;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        // Start (niet Awake): UIBuilder wijst de publieke velden pas toe NA
        // AddComponent<OfflineReportPanel>(), dus Awake zou nog met null-velden draaien.
        private void Start()
        {
            if (claimButton != null)
                claimButton.onClick.AddListener(OnClaim);
        }

        // ── Acties ────────────────────────────────────────────────────────────

        private void OnClaim()
        {
            GameManager.Instance?.ClaimOfflineReport();
            Hide();
        }

        // ── IPanelView ────────────────────────────────────────────────────────

        public void Refresh()
        {
            var report = GameManager.Instance?.OfflineReport;
            if (report == null || listContainer == null) return;

            if (elapsedLabel != null)
            {
                var text = $"You were away for {FormatDuration(report.ElapsedMs)}";
                if (report.CappedMs < report.ElapsedMs)
                    text += $"  (progress capped at {FormatDuration(report.CappedMs)})";
                elapsedLabel.text = text;
            }

            foreach (Transform t in listContainer) Destroy(t.gameObject);

            foreach (var r in report.Characters ?? new List<OfflineCharacterReport>())
                AddCharacterRows(r);
        }

        private void AddCharacterRows(OfflineCharacterReport r)
        {
            string targetName = ResolveTargetName(r.TargetId);

            var nameLbl = UIHelpers.CreateLabel(listContainer, $"{r.CharacterName}  -  {r.Kind}: {targetName}",
                15, bold: true, align: TMPro.TextAlignmentOptions.Left);
            nameLbl.color = UITheme.TextGold;
            UIHelpers.AddLayout(nameLbl.gameObject, preferredH: 26);

            string lvls = r.LevelsGained > 0 ? $" (+{r.LevelsGained} levels)" : "";
            var detailLbl = UIHelpers.CreateLabel(listContainer,
                $"Actions: {r.Actions}   XP: +{r.XpGained}{lvls}   Coins: +{r.CoinsGained}",
                15, align: TMPro.TextAlignmentOptions.Left);
            detailLbl.color = UITheme.TextDim;
            UIHelpers.AddLayout(detailLbl.gameObject, preferredH: 22);

            foreach (var s in r.Loot ?? new List<ItemStack>())
            {
                var itemName = ResolveItemName(s.ItemId);
                var lbl = UIHelpers.CreateLabel(listContainer, $"   {itemName} x{s.Qty}",
                    15, align: TMPro.TextAlignmentOptions.Left);
                lbl.color = UITheme.TextMain;
                UIHelpers.AddLayout(lbl.gameObject, preferredH: 20);
            }

            foreach (var s in r.LootOverflow ?? new List<ItemStack>())
            {
                var itemName = ResolveItemName(s.ItemId);
                var lbl = UIHelpers.CreateLabel(listContainer, $"   INVENTORY FULL: {itemName} x{s.Qty}",
                    15, align: TMPro.TextAlignmentOptions.Left);
                lbl.color = UITheme.Red;
                UIHelpers.AddLayout(lbl.gameObject, preferredH: 20);
            }
        }

        private static string ResolveItemName(string itemId)
        {
            RuntimeContent.Items.TryGetValue(itemId, out ItemDef def);
            return def?.Name ?? itemId;
        }

        private static string ResolveTargetName(string targetId)
        {
            if (RuntimeContent.Monsters.TryGetValue(targetId, out var monster)) return monster.Name;
            if (RuntimeContent.Nodes.TryGetValue(targetId, out var node)) return node.Name;
            return targetId;
        }

        private static string FormatDuration(long ms)
        {
            long hours = ms / 3600000;
            long minutes = (ms % 3600000) / 60000;
            if (hours > 0) return $"{hours}h {minutes}m";
            if (minutes > 0) return $"{minutes}m";
            return "under a minute";
        }

        public void Show()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()    => gameObject.SetActive(false);
        public bool IsVisible => gameObject.activeSelf;
    }
}
