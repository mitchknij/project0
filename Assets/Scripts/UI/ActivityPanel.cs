using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    public class ActivityPanel : MonoBehaviour, IPanelView
    {
        [HideInInspector] public RectTransform listContainer;

        private string       _lastMapId;
        private ActivityKind _lastKind = (ActivityKind)(-1);

        public void Refresh()
        {
            var gm = GameManager.Instance;
            var ch = gm?.GetSelectedCharacter();
            if (ch == null) return;

            var kind = ch.Activity?.Kind ?? ActivityKind.Idle;
            if (ch.MapId == _lastMapId && kind == _lastKind) return;
            _lastMapId = ch.MapId;
            _lastKind  = kind;
            RebuildList(ch);
        }

        private void RebuildList(Character ch)
        {
            if (listContainer == null) return;
            foreach (Transform t in listContainer) Destroy(t.gameObject);

            bool isIdle = ch.Activity == null || ch.Activity.Kind == ActivityKind.Idle;

            if (!isIdle)
            {
                var stopBtn = UIHelpers.CreateButton(listContainer, "Stop activity", UIHelpers.AccentRed);
                UIHelpers.AddLayout(stopBtn.gameObject, preferredH: 48);
                stopBtn.onClick.AddListener(() => { GameManager.Instance.Stop(); _lastKind = (ActivityKind)(-1); Refresh(); });
            }

            AddHeader("Monsters");
            bool anyMonster = false;
            foreach (var m in RuntimeContent.Monsters.Values)
            {
                if (!MapScope.Includes(m.MapId, ch.MapId)) continue;
                anyMonster = true;
                bool active = ch.Activity?.Kind == ActivityKind.Fighting && ch.Activity.TargetId == m.Id;
                string mId = m.Id;
                ItemSlotWidget.Create(listContainer, $"{m.Name}", 0,
                    active ? "Busy" : "Fight",
                    active ? UIHelpers.AccentGray : UIHelpers.AccentRed,
                    () => { GameManager.Instance.Assign(ActivityKind.Fighting, mId); _lastKind = (ActivityKind)(-1); Refresh(); });
            }
            if (!anyMonster) AddNote("No monsters on this map.");

            AddHeader("Resource Nodes");
            bool anyNode = false;
            foreach (var n in RuntimeContent.Nodes.Values)
            {
                if (!MapScope.Includes(n.MapId, ch.MapId)) continue;
                anyNode = true;
                var actKind = SkillToActivity(n.Skill);
                bool active = ch.Activity?.Kind == actKind && ch.Activity.TargetId == n.Id;
                string nId  = n.Id;
                ItemSlotWidget.Create(listContainer, $"{n.Name}  (Lv.{n.LevelReq})", 0,
                    active ? "Busy" : "Harvest",
                    active ? UIHelpers.AccentGray : UIHelpers.AccentGreen,
                    () => { GameManager.Instance.Assign(actKind, nId); _lastKind = (ActivityKind)(-1); Refresh(); });
            }
            if (!anyNode) AddNote("No nodes on this map.");
        }

        private static ActivityKind SkillToActivity(HarvestSkill s) => s switch
        {
            HarvestSkill.Mining   => ActivityKind.Mining,
            HarvestSkill.Chopping => ActivityKind.Chopping,
            _                     => ActivityKind.Gathering,
        };

        private void AddHeader(string text)
        {
            var go  = new GameObject("Hdr");
            go.transform.SetParent(listContainer, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = $"<b>{text}</b>";
            tmp.fontSize  = 14;
            tmp.color     = new Color(0.6f, 0.6f, 0.65f);
            tmp.alignment = TextAlignmentOptions.Left;
            UIHelpers.AddLayout(go, preferredH: 28, minH: 28);
        }

        private void AddNote(string text)
        {
            var go  = new GameObject("Note");
            go.transform.SetParent(listContainer, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = 13;
            tmp.color     = new Color(0.45f, 0.45f, 0.50f);
            tmp.alignment = TextAlignmentOptions.Left;
            UIHelpers.AddLayout(go, preferredH: 24, minH: 24);
        }

        public void Show()
        {
            _lastMapId = null;
            _lastKind  = (ActivityKind)(-1);
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()     => gameObject.SetActive(false);
        public bool IsVisible  => gameObject.activeSelf;
    }
}
