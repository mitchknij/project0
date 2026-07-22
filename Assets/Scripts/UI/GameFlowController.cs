// GameFlowController.cs — Boot-statemachine die de UI aanstuurt.
// Pollt GameManager.Instance (0.1 Hz) en schakelt tussen Title / CharSelect / CharCreate / Game.
// Zit in IdleCloud.UI zodat het zowel PanelManager als GameManager kan zien.

using System.Collections;
using UnityEngine;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    public class GameFlowController : MonoBehaviour
    {
        // ── Referenties (ingesteld door UIBuilder) ────────────────────────────

        [HideInInspector] public PanelManager    panelManager;
        [HideInInspector] public UIRefreshDriver refreshDriver;

        [HideInInspector] public TitlePanel           titlePanel;
        [HideInInspector] public CharacterSelectPanel charSelectPanel;
        [HideInInspector] public CharacterCreatePanel charCreatePanel;
        [HideInInspector] public MainHudPanel         mainHudPanel;
        [HideInInspector] public OfflineReportPanel   offlineReportPanel;

        // ── State ─────────────────────────────────────────────────────────────

        private enum FlowState { Unknown, Title, CharSelect, CharCreate, Game }
        private FlowState _state = FlowState.Unknown;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            // Registratie en HideAll zijn al gedaan door UIBuilder.Bootstrap()
            StartCoroutine(FlowLoop());
        }

        private IEnumerator FlowLoop()
        {
            // Wacht twee frames zodat GameManager.Start() zijn save kan laden
            yield return null;
            yield return null;

            while (true)
            {
                TickFlow();
                yield return new WaitForSeconds(0.1f);
            }
        }

        // ── State-machine ─────────────────────────────────────────────────────

        private void TickFlow()
        {
            // CharCreate is handmatige UI-navigatie — GameManager-state mag dit niet overrulen
            if (_state == FlowState.CharCreate) return;

            var gm = GameManager.Instance;
            if (gm == null) return;

            FlowState target;
            if (gm.Account == null)
                target = FlowState.Title;
            else if (gm.SelectedCharacterId == null)
                target = FlowState.CharSelect;
            else
                target = FlowState.Game;

            // Offline-rapport: modal tonen zodra we (nog steeds) in Game-state zitten
            if (target == FlowState.Game && gm.OfflineReport != null
                && offlineReportPanel != null && !offlineReportPanel.IsVisible)
            {
                offlineReportPanel.Show();
            }

            if (target == _state) return;
            _state = target;
            ApplyState();
        }

        private void ApplyState()
        {
            switch (_state)
            {
                case FlowState.Title:
                    panelManager.ShowOnly(titlePanel);
                    break;

                case FlowState.CharSelect:
                    panelManager.ShowOnly(charSelectPanel);
                    refreshDriver.RefreshAll();
                    break;

                case FlowState.Game:
                    panelManager.ShowOnly(mainHudPanel);
                    refreshDriver.RefreshAll();
                    break;
            }
        }

        // ── Navigatiemethoden voor panels ─────────────────────────────────────

        /// <summary>Toont het CharacterCreate-panel (aangeroepen vanuit CharSelectPanel).</summary>
        public void GoToCharCreate()
        {
            _state = FlowState.CharCreate;
            panelManager.ShowOnly(charCreatePanel);
            charCreatePanel?.Refresh();
        }

        /// <summary>Keert terug naar CharSelect (aangeroepen vanuit CharCreatePanel).</summary>
        public void GoToCharSelect()
        {
            _state = FlowState.CharSelect;
            panelManager.ShowOnly(charSelectPanel);
            refreshDriver.RefreshAll();
        }

        /// <summary>
        /// Herdetecteert de huidige state op basis van GameManager en past de UI aan.
        /// Aanroepen na elke GameManager-mutatie.
        /// </summary>
        public void ForceRefresh()
        {
            _state = FlowState.Unknown;
            TickFlow();
            refreshDriver.RefreshAll();
        }
    }
}
