// UITheme.cs -- Pixel-art fantasy theme: palette, procedurally generated 9-slice
// frame sprites, and the TMP font pipeline. All UI chrome runs through this class
// so the style stays consistent. Sprites are generated once at boot.

using System.Collections.Generic;
using UnityEngine;
using TMPro;
using IdleCloud.Core;

namespace IdleCloud.UI
{
    public static class UITheme
    {
        // -- Palette (sampled from the Assets/Art/UI mockups) ----------------

        public static readonly Color Navy      = Hex("0D1220");
        public static readonly Color NavyLight = Hex("141B2E");
        public static readonly Color NavyInset = Hex("090D18");
        public static readonly Color Outline   = Hex("05070D");

        public static readonly Color Gold     = Hex("C9A24B");
        public static readonly Color GoldDark = Hex("8A6C33");
        public static readonly Color GoldPale = Hex("E8CE8F");

        public static readonly Color TextMain = Hex("F2EFE1");
        public static readonly Color TextDim  = Hex("9BA3B5");
        public static readonly Color TextGold = Hex("E8C868");
        public static readonly Color SkillBarAutoHighlightTint = GoldPale;

        public static readonly Color HpRed    = Hex("C43B2B");
        public static readonly Color MpBlue   = Hex("2F6FD0");
        public static readonly Color XpGold   = Hex("D9A93A");
        public static readonly Color Green    = Hex("3F9E4D");
        public static readonly Color Red      = Hex("B03030");
        public static readonly Color Disabled = Hex("3A4152");

        public static class Layout
        {
            public const float StandardSpacing = 8f;
            public const float StandardPadding = 10f;

            public const float HudHeight = 132f;
            public const float NavButtonWidth = 84f;
            public const float NavButtonHeight = 36f;
            public const float SkillSlotSize = 50f;
            public const int SkillBarSlotCount = Character.SkillBarSlots;
            public const float SkillBarSlotSize = SkillSlotSize;
            public const float SkillBarSpacing = 8f;
            public const float SkillBarBottomOffset = 8f;
            public const float SkillBarAutoHighlightSeconds = 0.35f;
            public const float SkillBarAutoHighlightAlpha = 0.28f;
            public const float LevelUpBannerWidth = 520f;
            public const float LevelUpBannerHeight = 96f;
            public const float LevelUpBannerFontSize = 30f;
            public const float LevelUpBannerPadding = 16f;
            public const float CenterBlockW = 745f;
            public const float CenterBlockMinW = 420f;
            public const float EdgePad = 8f;
            public const float HudWidth =
                10f * NavButtonWidth + CenterBlockW +
                10f * StandardSpacing + 2f * EdgePad;

            public const float LootFeedWidth = 300f;
            public const float LootFeedHeight = 190f;
            public const float LootFeedHeaderHeight = 26f;
            public const float LootFeedEntryHeight = 22f;
            public const float LootFeedSpacing = 4f;
            public const float LootFeedPadding = 8f;
            public const int LootFeedMaxEntries = 6;
            public const float LootFeedTopOffset = 12f;
            public const float LootFeedRightOffset = 12f;

            public const float CanvasReferenceWidth = 1920f;
            public const float CanvasReferenceHeight = 1080f;

            public const float TitleCardWidth = 520f;
            public const float TitleCardHeight = 420f;
            public const float TitleCardSpacing = 14f;
            public const float TitleCardPadding = 40f;
            public const float CharacterSelectCardWidth = 640f;
            public const float CharacterSelectCardHeight = 560f;
            public const float CharacterSelectSpacing = 16f;
            public const float CharacterSelectPadding = 32f;
            public const float CharacterCreateCardWidth = 700f;
            public const float CharacterCreateCardHeight = 660f;
            public const float CharacterCreateSpacing = 12f;
            public const float CharacterCreatePadding = 32f;
            public const float OfflineReportCardWidth = 640f;
            public const float OfflineReportCardHeight = 560f;
            public const float OfflineReportSpacing = 12f;
            public const float OfflineReportPadding = 28f;
            public const float SubPanelCardWidth = 660f;
            public const float SubPanelCardHeight = 500f;
            public const float SubPanelSpacing = 10f;
            public const float SubPanelPadding = 20f;

            public const float BarHeight = 24f;
            public const float TitleHeaderHeight = 72f;
            public const float SubtitleHeight = 32f;
            public const float FieldLabelHeight = 22f;
            public const float InputHeight = 52f;
            public const float StatusHeight = 22f;
            public const float PrimaryButtonHeight = 58f;
            public const float PanelHeaderHeight = 52f;
            public const float PanelListHeight = 360f;
            public const float SecondaryButtonHeight = 54f;
            public const float ClassRowHeight = 58f;
            public const float ClassDescriptionHeight = 150f;
            public const float BottomRowHeight = 54f;
            public const float BottomRowSpacing = 14f;
            public const float HudCenterSpacing = 2f;
            public const float HudNameRowHeight = 22f;
            public const float HudInfoSpacing = 8f;
            public const float HudGoldLabelWidth = 140f;
            public const float HudMapRowHeight = 18f;
            public const float HudActivityLabelWidth = 220f;
            public const float OfflineHeaderHeight = 48f;
            public const float OfflineElapsedHeight = 26f;
            public const float OfflineListHeight = 320f;
            public const float ClaimButtonHeight = 52f;
            public const float SubPanelHeaderHeight = 40f;
            public const float CloseButtonWidth = 36f;
            public const float ContentListHeight = 400f;
            public const float SkillPanelRowHeight = 96f;
            public const float SkillPanelSpacing = 4f;
            public const float SkillPanelPadding = 8f;
            public const float SkillPanelHeaderHeight = 22f;
            public const float SkillPanelCooldownWidth = 64f;
            public const float SkillPanelDescriptionHeight = 32f;
            public const float SkillPanelMechanicHeight = 16f;
            public const float InventoryHeaderHeight = 20f;
            public const float InventoryListHeight = 370f;
            public const float BankCoinsHeight = 22f;
            public const float BankColumnsHeight = 355f;
            public const float TalentPointsHeight = 24f;
            public const float TalentListHeight = 330f;
            public const float ResetButtonHeight = 44f;
            public const float BankColumnSpacing = 4f;
            public const float BankColumnHeaderHeight = 20f;
        }

        private static TMP_FontAsset _headerFont;
        private static TMP_FontAsset _bodyFont;

        public static TMP_FontAsset HeaderFont
        {
            get
            {
                if (_headerFont == null) _headerFont = LoadFont("UI/Fonts/PressStart2P-Regular");
                return _headerFont;
            }
        }

        public static TMP_FontAsset BodyFont
        {
            get
            {
                if (_bodyFont == null) _bodyFont = LoadFont("UI/Fonts/VT323-Regular");
                return _bodyFont;
            }
        }

        private static TMP_FontAsset LoadFont(string resourcePath)
        {
            var font = Resources.Load<Font>(resourcePath);
            if (font == null)
            {
                Debug.LogWarning($"[UITheme] Font '{resourcePath}' not found -- using the TMP default as a fallback.");
                return TMP_Settings.defaultFontAsset;
            }

            var asset = TMP_FontAsset.CreateFontAsset(font);
            if (asset == null) return TMP_Settings.defaultFontAsset;

            if (TMP_Settings.defaultFontAsset != null)
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            return asset;
        }

        private static Sprite _panelFrame, _buttonFrame, _slotFrame, _insetFrame, _barFrame, _barFill;

        public static Sprite PanelFrame
        {
            get
            {
                if (_panelFrame == null) _panelFrame = MakeFrame(48, 4,
                    new (int, Color)[] { (2, Outline), (4, Gold), (2, Outline) }, Navy, 12);
                return _panelFrame;
            }
        }

        public static Sprite ButtonFrame
        {
            get
            {
                if (_buttonFrame == null) _buttonFrame = MakeFrame(48, 3,
                    new (int, Color)[] { (2, Outline), (3, Gold), (1, Outline) }, NavyLight, 10);
                return _buttonFrame;
            }
        }

        public static Sprite SlotFrame
        {
            get
            {
                if (_slotFrame == null) _slotFrame = MakeFrame(48, 3,
                    new (int, Color)[] { (2, Outline), (3, GoldDark), (1, Outline) }, NavyInset, 10);
                return _slotFrame;
            }
        }

        public static Sprite InsetFrame
        {
            get
            {
                if (_insetFrame == null) _insetFrame = MakeFrame(24, 2,
                    new (int, Color)[] { (1, Outline), (1, GoldDark) }, NavyInset, 5);
                return _insetFrame;
            }
        }

        public static Sprite BarFrame
        {
            get
            {
                if (_barFrame == null) _barFrame = MakeFrame(24, 2,
                    new (int, Color)[] { (1, Outline), (2, GoldDark), (1, Outline) }, NavyInset, 6);
                return _barFrame;
            }
        }

        public static Sprite BarFill
        {
            get
            {
                if (_barFill == null) _barFill = MakeSolid(4, Color.white);
                return _barFill;
            }
        }

        private static Sprite MakeFrame(int size, int chamfer,
            (int width, Color color)[] rings, Color fill, float borderPx)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
            };

            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = Mathf.Min(x, size - 1 - x);
                    int cy = Mathf.Min(y, size - 1 - y);
                    int cornerDist = cx + cy;

                    if (cornerDist < chamfer)      { tex.SetPixel(x, y, clear);   continue; }
                    if (cornerDist == chamfer)     { tex.SetPixel(x, y, Outline); continue; }

                    int edgeDist = Mathf.Min(cx, cy);
                    Color c = fill;
                    int cursor = 0;
                    foreach (var (width, color) in rings)
                    {
                        if (edgeDist < cursor + width) { c = color; break; }
                        cursor += width;
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply(false, false);

            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(borderPx, borderPx, borderPx, borderPx));
        }

        private static Sprite MakeSolid(int size, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
            };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
