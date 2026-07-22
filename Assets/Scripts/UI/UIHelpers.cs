// UIHelpers.cs — Reusable factory methods for uGUI and TMP elements.
// All panels use these helpers so the style stays consistent.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IdleCloud.UI
{
    public static class UIHelpers
    {
        // ── Color palette ─────────────────────────────────────────────────────

        // Aliases to UITheme keep existing callers on the same palette.
        public static readonly Color PanelBg    = new Color(0.02f, 0.03f, 0.05f, 0.88f); // dim-overlay
        public static readonly Color CardBg     = UITheme.NavyLight;
        public static readonly Color CardBgDark = UITheme.NavyInset;
        public static readonly Color AccentBlue  = UITheme.MpBlue;
        public static readonly Color AccentGreen = UITheme.Green;
        public static readonly Color AccentGray  = UITheme.Disabled;
        public static readonly Color AccentRed   = UITheme.Red;

        // ── RectTransform helpers ─────────────────────────────────────────────

        /// <summary>Stretch a RectTransform to its full parent with optional pixel insets.</summary>
        public static RectTransform Stretch(GameObject go,
            float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            var rt = EnsureRT(go);
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.offsetMin  = new Vector2(left,  bottom);
            rt.offsetMax  = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchor an object at the center with a fixed width and height.</summary>
        public static RectTransform Center(GameObject go, float w, float h)
        {
            var rt = EnsureRT(go);
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>
        /// Return the object's RectTransform, or add one if it does not have one.
        /// AddComponent&lt;RectTransform&gt;() cannot replace a plain Transform; instead,
        /// add a transparent Image with [RequireComponent(RectTransform)] so Unity
        /// converts the Transform correctly.
        /// </summary>
        public static RectTransform EnsureRT(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) return rt;
            // Image's [RequireComponent(typeof(RectTransform))] converts the plain Transform.
            var img = go.AddComponent<Image>();
            img.color           = Color.clear;
            img.raycastTarget   = false;
            return go.GetComponent<RectTransform>();
        }

        // ── Layout helpers ────────────────────────────────────────────────────

        public static VerticalLayoutGroup AddVLG(GameObject go,
            float spacing = 12, float padding = 0, bool controlHeight = false)
        {
            EnsureRT(go); // VLG vereist een RectTransform
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing              = spacing;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = controlHeight;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            if (padding > 0)
                vlg.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            return vlg;
        }

        public static HorizontalLayoutGroup AddHLG(GameObject go,
            float spacing = 10, bool controlWidth = true)
        {
            EnsureRT(go); // HLG vereist een RectTransform
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = spacing;
            hlg.childControlWidth     = controlWidth;
            hlg.childControlHeight    = true;
            hlg.childForceExpandWidth  = controlWidth;
            hlg.childForceExpandHeight = true;
            return hlg;
        }

        public static LayoutElement AddLayout(GameObject go,
            float preferredW = -1, float preferredH = -1, float minH = -1, float flexW = -1,
            float minW = -1)
        {
            EnsureRT(go); // LayoutElement requires a RectTransform.
            var le = go.AddComponent<LayoutElement>();
            if (preferredW >= 0) le.preferredWidth  = preferredW;
            if (preferredH >= 0) le.preferredHeight = preferredH;
            if (minH       >= 0) le.minHeight       = minH;
            if (flexW      >= 0) le.flexibleWidth   = flexW;
            if (minW       >= 0) le.minWidth        = minW;
            return le;
        }

        // ── Image background ──────────────────────────────────────────────────

        public static Image AddBackground(GameObject go, Color color)
        {
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>Set a 9-slice frame sprite as the object's background.</summary>
        public static Image AddFrame(GameObject go, Sprite frame, Color? tint = null)
        {
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.sprite = frame;
            img.type   = Image.Type.Sliced;
            // Normalize the border thickness so each frame has an approximately 16-pixel edge.
            img.pixelsPerUnitMultiplier = Mathf.Max(1f, frame.border.x / 16f);
            img.color  = tint ?? Color.white;
            return img;
        }

        /// <summary>
        /// Create an HP/XP progress bar with a pixel frame, colored fill, and
        /// centered overlay label. Set the fill through fill.fillAmount (0..1).
        /// </summary>
        public static (Image fill, TextMeshProUGUI label) CreateBar(Transform parent, Color fillColor)
        {
            var root = new GameObject("Bar");
            root.transform.SetParent(parent, false);
            AddFrame(root, UITheme.BarFrame);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(root.transform, false);
            var fillRT = EnsureRT(fillGO);
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = new Vector2(4, 4);
            fillRT.offsetMax = new Vector2(-4, -4);
            var fill = fillGO.GetComponent<Image>();
            fill.sprite     = UITheme.BarFill;
            fill.color      = fillColor;
            fill.type       = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            var label = CreateLabel(root.transform, "", 14);
            var lblRT = (RectTransform)label.transform;
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = Vector2.zero;
            lblRT.offsetMax = Vector2.zero;
            label.raycastTarget = false;

            return (fill, label);
        }

        // ── Text ──────────────────────────────────────────────────────────────

        /// <summary>Create a TextMeshProUGUI label as a child of parent.</summary>
        public static TextMeshProUGUI CreateLabel(Transform parent, string text,
            float fontSize = 18, bool bold = false,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go  = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font      = bold ? UITheme.HeaderFont : UITheme.BodyFont;
            tmp.text      = text;
            tmp.fontSize  = bold ? fontSize * 0.75f : fontSize * 1.1f;
            tmp.color     = UITheme.TextMain;
            tmp.alignment = align;
            return tmp;
        }

        /// <summary>Create a gold header using the header font.</summary>
        public static TextMeshProUGUI CreateHeader(Transform parent, string text, float fontSize = 22)
        {
            var tmp = CreateLabel(parent, text, fontSize, bold: true);
            tmp.color = UITheme.TextGold;
            return tmp;
        }

        // ── Button ────────────────────────────────────────────────────────────

        /// <summary>Create a button with a 9-slice pixel frame and TMP text.</summary>
        public static Button CreateButton(Transform parent, string label, Color? bgColor = null,
            Sprite frameSprite = null)
        {
            var go  = new GameObject(label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = frameSprite != null ? frameSprite : UITheme.ButtonFrame;
            img.type   = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = img.sprite.border.x > 0f ? Mathf.Max(1f, img.sprite.border.x / 16f) : 1f;
            img.color  = Color.white;

            var btn    = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor      = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.highlightedColor = new Color(1.30f, 1.26f, 1.05f, 1f); // light gold hover glow
            colors.pressedColor     = new Color(0.70f, 0.70f, 0.70f, 1f);
            colors.selectedColor    = new Color(1.30f, 1.26f, 1.05f, 1f);
            colors.disabledColor    = new Color(0.45f, 0.45f, 0.50f, 0.9f);
            colors.fadeDuration     = 0.08f;
            btn.colors = colors;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var tmp       = textGO.AddComponent<TextMeshProUGUI>();
            tmp.font      = UITheme.HeaderFont;
            tmp.text      = label;
            tmp.fontSize  = 14;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = UITheme.TextGold;
            tmp.raycastTarget = false;

            var textRT    = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(6, 6);
            textRT.offsetMax = new Vector2(-6, -6);

            return btn;
        }

        // ── InputField ────────────────────────────────────────────────────────

        /// <summary>
        /// Create a TMP_InputField with the correct child structure
        /// (Text Area -> Placeholder + Text).
        /// </summary>
        public static TMP_InputField CreateInputField(Transform parent,
            string placeholder = "Enter text...", float fontSize = 18)
        {
            var go  = new GameObject("InputField");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = UITheme.InsetFrame;
            img.type   = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color  = Color.white;

            var input = go.AddComponent<TMP_InputField>();

            // Viewport
            var vpGO = new GameObject("Text Area");
            vpGO.transform.SetParent(go.transform, false);
            vpGO.AddComponent<RectMask2D>();
            var vpRT    = vpGO.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = new Vector2(8,  4);
            vpRT.offsetMax = new Vector2(-8, -4);
            input.textViewport = vpRT;

            // Placeholder
            var phGO   = new GameObject("Placeholder");
            phGO.transform.SetParent(vpGO.transform, false);
            var phTMP  = phGO.AddComponent<TextMeshProUGUI>();
            phTMP.font      = UITheme.BodyFont;
            phTMP.text      = placeholder;
            phTMP.fontSize  = fontSize * 1.1f;
            phTMP.color     = UITheme.TextDim;
            SetStretchRT(phGO);
            input.placeholder = phTMP;

            // Input text
            var txGO  = new GameObject("Text");
            txGO.transform.SetParent(vpGO.transform, false);
            var txTMP = txGO.AddComponent<TextMeshProUGUI>();
            txTMP.font     = UITheme.BodyFont;
            txTMP.text     = "";
            txTMP.fontSize = fontSize * 1.1f;
            txTMP.color    = UITheme.TextMain;
            SetStretchRT(txGO);
            input.textComponent = txTMP;

            return input;
        }

        private static void SetStretchRT(GameObject go)
        {
            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ── ScrollRect + Content ─────────────────────────────────────────────

        /// <summary>
        /// Create a vertically scrollable area and return the content RectTransform
        /// where child panels are added (with VerticalLayoutGroup and ContentSizeFitter).
        /// </summary>
        public static (ScrollRect scroll, RectTransform content) CreateScrollView(
            Transform parent, Color bgColor = default)
        {
            if (bgColor == default) bgColor = CardBgDark;

            var scrollGO  = new GameObject("ScrollView");
            scrollGO.transform.SetParent(parent, false);
            AddFrame(scrollGO, UITheme.InsetFrame);
            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            // Viewport
            var vpGO = new GameObject("Viewport");
            vpGO.transform.SetParent(scrollGO.transform, false);
            Stretch(vpGO);
            vpGO.AddComponent<RectMask2D>();
            scrollRect.viewport = vpGO.GetComponent<RectTransform>();

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(vpGO.transform, false);
            var contentRT      = EnsureRT(contentGO);
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot     = new Vector2(0.5f, 1);
            contentRT.sizeDelta = Vector2.zero;

            var vlg = AddVLG(contentGO, 6, 6, controlHeight: true);
            vlg.childControlHeight = true;

            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;
            return (scrollRect, contentRT);
        }
    }
}
