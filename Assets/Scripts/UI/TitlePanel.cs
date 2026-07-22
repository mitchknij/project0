// TitlePanel.cs — Titelscherm: familienaam invoeren en starten.
// Wanneer de gebruiker op "Spelen" klikt, roept dit GameManager.CreateFamily() aan;
// GameFlowController detecteert Account != null en schakelt naar CharSelectPanel.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    public class TitlePanel : MonoBehaviour, IPanelView
    {
        // ── Referenties (ingesteld door UIBuilder) ────────────────────────────

        [HideInInspector] public GameFlowController flowController;
        [HideInInspector] public TMP_InputField     nameInput;
        [HideInInspector] public Button             playButton;
        [HideInInspector] public TextMeshProUGUI    statusText;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            if (playButton != null)
                playButton.onClick.AddListener(OnPlay);
        }

        // ── Acties ────────────────────────────────────────────────────────────

        private void OnPlay()
        {
            var name = nameInput != null ? nameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Enter a family name.");
                return;
            }
            SetStatus("");
            GameManager.Instance.CreateFamily(name);
            flowController?.ForceRefresh();
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        // ── IPanelView ────────────────────────────────────────────────────────

        public void Refresh() { /* stateless — niets te vernieuwen */ }

        public void Show()
        {
            gameObject.SetActive(true);
            if (nameInput != null) nameInput.text = "";
            SetStatus("");
        }

        public void Hide()     => gameObject.SetActive(false);
        public bool IsVisible  => gameObject.activeSelf;
    }
}
