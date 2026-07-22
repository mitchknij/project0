using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using IdleCloud.Core;

namespace IdleCloud.UI
{
    public static class ItemSlotWidget
    {
        /// <summary>
        /// Maakt een horizontale item-rij: [Naam · Qty]  [knop].
        /// Geeft de knop terug zodat de aanroeper een onClick-listener kan toevoegen.
        /// </summary>
        public static Button Create(Transform parent, string label, int qty,
            string btnLabel, Color btnColor, Action onClick)
        {
            var row = new GameObject("Slot_" + label);
            row.transform.SetParent(parent, false);
            UIHelpers.AddFrame(row, UITheme.SlotFrame);
            UIHelpers.AddLayout(row, preferredH: 48, minH: 48);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 8;
            hlg.padding               = new RectOffset(10, 10, 4, 4);
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childControlWidth     = true;
            hlg.childControlHeight    = true;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;

            // Naam + qty (neemt resterende breedte)
            var nameGO  = new GameObject("Name");
            nameGO.transform.SetParent(row.transform, false);
            var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text      = qty > 1 ? $"{label}  x{qty}" : label;
            nameTMP.font      = UITheme.BodyFont;
            nameTMP.fontSize  = 16;
            nameTMP.color     = UITheme.TextMain;
            nameTMP.alignment = TextAlignmentOptions.Left;
            UIHelpers.AddLayout(nameGO, flexW: 1);

            // Actie-knop
            var btn = UIHelpers.CreateButton(row.transform, btnLabel, btnColor);
            UIHelpers.AddLayout(btn.gameObject, preferredW: 100);
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        /// <summary>Lege slot-rij zonder knop (placeholder voor onbezette equipslot).</summary>
        public static void CreateEmpty(Transform parent, string slotName)
        {
            var row = new GameObject("Slot_Empty_" + slotName);
            row.transform.SetParent(parent, false);
            UIHelpers.AddFrame(row, UITheme.SlotFrame);
            UIHelpers.AddLayout(row, preferredH: 48, minH: 48);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding               = new RectOffset(10, 10, 4, 4);
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childControlWidth     = true;
            hlg.childControlHeight    = true;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;

            var lbl  = new GameObject("Label");
            lbl.transform.SetParent(row.transform, false);
            var tmp  = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text      = $"[{slotName}]  -  Empty";
            tmp.font      = UITheme.BodyFont;
            tmp.fontSize  = 15;
            tmp.color     = UITheme.TextDim;
            tmp.alignment = TextAlignmentOptions.Left;
        }
    }
}
