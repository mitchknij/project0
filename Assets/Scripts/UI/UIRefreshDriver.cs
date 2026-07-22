// UIRefreshDriver.cs — Drijft gepolde UI-verversing aan.
// Roept Refresh() aan op alle zichtbare panels op ~4 Hz.
// Roep RefreshAll() direct aan na elke GameManager-mutatie voor directe feedback.

using System.Collections;
using UnityEngine;

namespace IdleCloud.UI
{
    public class UIRefreshDriver : MonoBehaviour
    {
        private IPanelView[] _panels;
        private const float Interval = 0.25f; // 4 Hz

        /// <summary>Registreer de panels en start de refresh-loop.</summary>
        public void Register(IPanelView[] panels)
        {
            _panels = panels;
            StartCoroutine(RefreshLoop());
        }

        private IEnumerator RefreshLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Interval);
                RefreshAll();
            }
        }

        /// <summary>Directe verversing van alle zichtbare panels.</summary>
        public void RefreshAll()
        {
            if (_panels == null) return;
            foreach (var p in _panels)
                if (p.IsVisible) p.Refresh();
        }
    }
}
