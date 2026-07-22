// CharacterSelectPanel.cs — Lijst van personages; selecteer of maak nieuw.
// Herbouwt de lijst alleen wanneer het aantal personages verandert (vuil-vlag).

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    public class CharacterSelectPanel : MonoBehaviour, IPanelView
    {
        // ── Referenties (ingesteld door UIBuilder) ────────────────────────────

        [HideInInspector] public GameFlowController flowController;
        [HideInInspector] public RectTransform      listContainer;   // ScrollRect-content
        [HideInInspector] public Button             newCharButton;

        private int _lastCharCount = -1;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            if (newCharButton != null)
                newCharButton.onClick.AddListener(() => flowController?.GoToCharCreate());
        }

        // ── IPanelView ────────────────────────────────────────────────────────

        public void Refresh()
        {
            var gm = GameManager.Instance;
            if (gm?.Account == null) return;

            int count = gm.Account.Characters?.Count ?? 0;
            if (count == _lastCharCount) return;
            _lastCharCount = count;
            RebuildList();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _lastCharCount = -1; // forceer herbouw bij tonen
            Refresh();
        }

        public void Hide()     => gameObject.SetActive(false);
        public bool IsVisible  => gameObject.activeSelf;

        // ── List rebuilding ───────────────────────────────────────────────────

        private void RebuildList()
        {
            if (listContainer == null) return;

            // Verwijder bestaande entries
            foreach (Transform child in listContainer)
                Destroy(child.gameObject);

            var gm = GameManager.Instance;
            if (gm?.Account?.Characters == null) return;

            foreach (var character in gm.Account.Characters)
                CreateCharEntry(character);
        }

        private void CreateCharEntry(Character character)
        {
            var entryGO = new GameObject($"Entry_{character.Name}");
            entryGO.transform.SetParent(listContainer, false);

            UIHelpers.AddBackground(entryGO, UIHelpers.CardBg);
            var le = UIHelpers.AddLayout(entryGO, preferredH: 72);
            le.minHeight = 72;

            // Rij: info links (flexibel), selecteerknop rechts (vaste breedte)
            var hlg  = entryGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 10;
            hlg.padding               = new RectOffset(14, 10, 4, 4);
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childControlWidth     = true;
            hlg.childControlHeight    = true;
            hlg.childForceExpandWidth  = false; // elk kind beheert eigen breedte
            hlg.childForceExpandHeight = true;

            // Info-tekst (neemt resterende breedte in)
            var infoGO  = new GameObject("Info");
            infoGO.transform.SetParent(entryGO.transform, false);
            var infoTMP = infoGO.AddComponent<TextMeshProUGUI>();
            var cls     = RuntimeContent.Get(character.ClassId);
            var clsName = cls != null ? cls.Name : character.ClassId.ToString();
            infoTMP.text     = $"<b>{character.Name}</b>  -  {clsName}  -  Level {character.Level}";
            infoTMP.fontSize = 18;
            infoTMP.color    = Color.white;
            infoTMP.alignment = TextAlignmentOptions.Left;
            UIHelpers.AddLayout(infoGO, flexW: 1); // neemt resterende breedte

            // Selecteer-knop
            string charId   = character.Id;
            var selectBtn   = UIHelpers.CreateButton(entryGO.transform, "Select", UIHelpers.AccentGreen);
            UIHelpers.AddLayout(selectBtn.gameObject, preferredW: 110);
            selectBtn.onClick.AddListener(() =>
            {
                GameManager.Instance.SelectCharacter(charId);
                flowController?.ForceRefresh();
            });
        }
    }
}
