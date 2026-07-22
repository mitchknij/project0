// TalentsPanel.cs — Toont beschikbare stat-/talentpunten en biedt allocatie + reset aan.

using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    public class TalentsPanel : MonoBehaviour, IPanelView
    {
        [HideInInspector] public RectTransform listContainer;
        [HideInInspector] public TextMeshProUGUI pointsLabel;
        [HideInInspector] public Button resetButton;

        private void Start()
        {
            if (resetButton != null)
                resetButton.onClick.AddListener(OnReset);
        }

        private void OnReset()
        {
            GameManager.Instance?.ResetTalents();
            Refresh();
        }

        public void Refresh()
        {
            if (listContainer == null) return;

            var gm = GameManager.Instance;
            var ch = gm?.GetSelectedCharacter();
            if (ch == null) return;

            int statPts   = ch.FreeStatPoints ?? 0;
            int talentPts = Talents.AvailableTalentPoints(ch);

            if (pointsLabel != null)
                pointsLabel.text = $"Stat Points: {statPts}    Talent Points: {talentPts}";

            foreach (Transform t in listContainer) Destroy(t.gameObject);

            // ── Stats sectie ───────────────────────────────────────────────
            var statsHdr = UIHelpers.CreateHeader(listContainer, "Stats", 14);
            statsHdr.alignment = TextAlignmentOptions.Left;
            UIHelpers.AddLayout(statsHdr.gameObject, preferredH: 30);

            var cls   = RuntimeContent.Get(ch.ClassId);
            var stats = Progression.EffectiveStats(ch, cls, new Dictionary<string, ItemDef>(RuntimeContent.Items), AccountBonuses.Zero());

            AddStatRow("Strength", stats.Strength, "strength", statPts);
            AddStatRow("Agility",  stats.Agility,  "agility",  statPts);
            AddStatRow("Wisdom",   stats.Wisdom,   "wisdom",   statPts);
            AddStatRow("Luck",     stats.Luck,     "luck",     statPts);

            // ── Talents sectie ─────────────────────────────────────────────
            var talHdr = UIHelpers.CreateHeader(listContainer, "Talents", 14);
            talHdr.alignment = TextAlignmentOptions.Left;
            UIHelpers.AddLayout(talHdr.gameObject, preferredH: 30);

            foreach (var d in RuntimeContent.Talents.Values.Where(def => Talents.TalentAvailableTo(def, ch)))
            {
                int cur = 0;
                ch.Talents?.TryGetValue(d.Id, out cur);
                string label = $"{d.Name}  {cur}/{d.MaxPoints}  -  {d.Description}";
                string talentId = d.Id;
                int curCopy = cur;

                var btn = ItemSlotWidget.Create(listContainer, label, 1, "+", UIHelpers.AccentBlue,
                    () => { GameManager.Instance.AllocateTalent(talentId); Refresh(); });
                btn.interactable = talentPts > 0 && curCopy < d.MaxPoints;
            }
        }

        private void AddStatRow(string name, int value, string key, int statPts)
        {
            var btn = ItemSlotWidget.Create(listContainer, $"{name}: {value}", 1, "+", UIHelpers.AccentBlue,
                () => { GameManager.Instance.AllocateStat(key); Refresh(); });
            btn.interactable = statPts > 0;
        }

        public void Show()  { gameObject.SetActive(true); Refresh(); }
        public void Hide()  => gameObject.SetActive(false);
        public bool IsVisible => gameObject.activeSelf;
    }
}
