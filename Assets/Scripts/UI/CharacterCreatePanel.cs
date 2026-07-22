// CharacterCreatePanel.cs — Personage aanmaken: kies klasse, geef naam op.
// SetClassButtons() wordt door UIBuilder aangeroepen nadat de knoppen zijn aangemaakt.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;

namespace IdleCloud.UI
{
    public class CharacterCreatePanel : MonoBehaviour, IPanelView
    {
        // ── Referenties (ingesteld door UIBuilder) ────────────────────────────

        [HideInInspector] public GameFlowController flowController;
        [HideInInspector] public TMP_InputField     nameInput;
        [HideInInspector] public TextMeshProUGUI    statusText;
        [HideInInspector] public TextMeshProUGUI    classDescText;
        [HideInInspector] public Button             createButton;
        [HideInInspector] public Button             backButton;
        [HideInInspector] public Button[]           classButtons;

        private static readonly ClassId[] ClassOrder =
            { ClassId.Beginner, ClassId.Warrior, ClassId.Archer, ClassId.Mage };

        private ClassId _selectedClass = ClassId.Beginner;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            if (backButton   != null) backButton.onClick.AddListener(OnBack);
            if (createButton != null) createButton.onClick.AddListener(OnCreate);
            if (classButtons == null || classButtons.Length == 0)
            {
                var classRow = transform.Find("Card/ClassRow");
                classButtons = classRow != null
                    ? classRow.GetComponentsInChildren<Button>(true)
                    : System.Array.Empty<Button>();
            }
            for (int i = 0; i < classButtons.Length && i < ClassOrder.Length; i++)
            {
                int index = i;
                var button = classButtons[i];
                if (button != null)
                    button.onClick.AddListener(() => SelectClass(ClassOrder[index], index));
            }
            SelectClass(ClassId.Beginner, 0);
            UpdateDesc();
        }

        // ── Klasseknop-bedrading (aangeroepen door UIBuilder) ─────────────────

        /// <summary>
        /// Koppelt de vier klasseknopen. Moet na AddComponent maar vóór de eerste frame
        /// worden aangeroepen — ook vóór Start().
        /// </summary>
        public void SetClassButtons(Button[] buttons)
        {
            classButtons = buttons ?? System.Array.Empty<Button>();
        }

        // ── Klasse-selectie ───────────────────────────────────────────────────

        private void SelectClass(ClassId id, int selectedIdx)
        {
            _selectedClass = id;

            if (classButtons != null)
            {
                for (int i = 0; i < classButtons.Length; i++)
                {
                    var img = classButtons[i]?.GetComponent<Image>();
                    if (img == null) continue;
                    img.color = (i == selectedIdx) ? UITheme.GoldPale : Color.white;
                }
            }
            UpdateDesc();
        }

        private void UpdateDesc()
        {
            if (classDescText == null) return;
            var def = RuntimeContent.Get(_selectedClass);
            if (def == null) { classDescText.text = ""; return; }
            classDescText.text =
                $"<b>{def.Name}</b>\n{def.Description}\n\n<color=#aaaaaa><size=13>{def.PassiveBonus}</size></color>";
        }

        // ── Acties ────────────────────────────────────────────────────────────

        private void OnCreate()
        {
            var charName = nameInput != null ? nameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(charName))
            {
                SetStatus("Enter a name for your character.");
                return;
            }

            GameManager.Instance.CreateCharacter(charName, _selectedClass);
            SetStatus("");
            if (nameInput != null) nameInput.text = "";
            flowController?.GoToCharSelect();
        }

        private void OnBack() => flowController?.GoToCharSelect();

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        // ── IPanelView ────────────────────────────────────────────────────────

        public void Refresh()
        {
            // Herlaad beschrijving (klasse-definitie kan niet veranderen, maar beschrijving wel)
            UpdateDesc();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            SetStatus("");
            if (nameInput != null) nameInput.text = "";
        }

        public void Hide()     => gameObject.SetActive(false);
        public bool IsVisible  => gameObject.activeSelf;
    }
}
