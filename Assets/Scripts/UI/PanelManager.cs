// PanelManager.cs — Beheert welk panel actief is.
// Registreer alle panels via Register(); gebruik daarna ShowOnly / HideAll.

using UnityEngine;

namespace IdleCloud.UI
{
    public class PanelManager : MonoBehaviour
    {
        private IPanelView[] _all;

        /// <summary>Registreer alle panels die door PanelManager beheerd worden.</summary>
        public void Register(IPanelView[] panels) => _all = panels;

        /// <summary>Toont alleen het opgegeven panel; verbergt alle andere.</summary>
        public void ShowOnly(IPanelView toShow)
        {
            if (_all == null) return;
            foreach (var p in _all)
            {
                if (p == toShow) p.Show();
                else             p.Hide();
            }
        }

        /// <summary>Verbergt alle geregistreerde panels.</summary>
        public void HideAll()
        {
            if (_all == null) return;
            foreach (var p in _all) p.Hide();
        }

    }
}
