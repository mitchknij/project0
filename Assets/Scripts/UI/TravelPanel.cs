using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using IdleCloud.View;

namespace IdleCloud.UI
{
    public class TravelPanel : MonoBehaviour, IPanelView
    {
        [HideInInspector] public RectTransform listContainer;

        private string _lastMapId;

        private void OnEnable()
        {
            if (MapTransitionCoordinator.Instance != null)
                MapTransitionCoordinator.Instance.MapTransitionCompleted += HandleMapTransitionCompleted;
        }

        private void OnDisable()
        {
            if (MapTransitionCoordinator.Instance != null)
                MapTransitionCoordinator.Instance.MapTransitionCompleted -= HandleMapTransitionCompleted;
        }

        public void Refresh()
        {
            var ch = GameManager.Instance?.GetSelectedCharacter();
            if (ch == null || ch.MapId == _lastMapId) return;
            _lastMapId = ch.MapId;
            RebuildList(ch);
        }

        private void RebuildList(Character ch)
        {
            if (listContainer == null) return;
            foreach (Transform t in listContainer) Destroy(t.gameObject);

            RuntimeContent.Maps.TryGetValue(ch.MapId, out var map);
            if (map?.Connections == null || map.Connections.Count == 0)
            {
                AddNote("No connections available.");
                return;
            }

            foreach (var connId in map.Connections)
            {
                RuntimeContent.Maps.TryGetValue(connId, out var dest);
                if (dest == null) continue;
                string destId = connId;
                ItemSlotWidget.Create(listContainer,
                    $"{dest.Name}  (Recommended: Lv.{dest.RecommendedLevel})", 0,
                    "Travel", UIHelpers.AccentBlue,
                    () =>
                    {
                        MapTransitionCoordinator coordinator = MapTransitionCoordinator.Instance;
                        if (coordinator != null)
                            coordinator.RequestTravel(destId);
                        else if (GameManager.Instance.TryTravelTo(destId))
                        {
                            _lastMapId = null;
                            Refresh();
                        }
                    });
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

        public void Show()  { _lastMapId = null; gameObject.SetActive(true); Refresh(); }
        public void Hide()  => gameObject.SetActive(false);
        public bool IsVisible => gameObject.activeSelf;

        private void HandleMapTransitionCompleted(string _)
        {
            _lastMapId = null;
            Refresh();
        }
    }
}
