using System;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    // The terminal owns the frame and pointer positioning. The game's tooltip objects are read-only templates.
    internal sealed class TerminalItemTooltip
    {
        private readonly ManualLogSource _log;
        private readonly Ae2TerminalSkin _skin;
        private readonly Image _frame;
        private readonly RectTransform _textLayer;
        private readonly TextMeshProUGUI _title, _body, _group;
        private TextMeshProUGUI? _nativeTitle, _nativeBody;
        private Item? _lastFailure;
        private float _width, _height, _nativeWidth, _nativeTitleHeight;
        private int _screenWidth, _screenHeight, _scale;

        internal TerminalItemTooltip(Transform parent, ManualLogSource log, Ae2TerminalSkin skin)
        {
            _log = log;
            _skin = skin;
            var overlay = TerminalCanvasOrder.AddTooltipLayer(parent, "UIT.ItemTooltipOverlay", TerminalCanvasOrder.ItemTooltip);
            _frame = NativeFlatInventoryPanel.Add(overlay, "Item tooltip", typeof(Image)).GetComponent<Image>();
            _frame.raycastTarget = false;
            _frame.gameObject.SetActive(false);
            // Keep the terminal's textured frame as the only background behind the native-styled text.
            _textLayer = NativeFlatInventoryPanel.Add(_frame.transform, "Item text").GetComponent<RectTransform>();
            _title = CreateLabel("Item name");
            _body = CreateLabel("Item details");
            _group = CreateLabel("Item group");
        }

        internal void Hide() => _frame.gameObject.SetActive(false);

        internal void Show(Item item, int count, Vector2 pointer, int scale)
        {
            try
            {
                var (title, body) = TerminalItemTooltipContent.Read(item);
                if (string.IsNullOrWhiteSpace(title)) { Hide(); return; }
                var group = count > 1 ? TerminalText.Format("inventory.tooltip.group", count) : "";
                Draw(title, body, group, pointer, scale);
                _lastFailure = null;
            }
            catch (Exception ex)
            {
                Hide();
                if (_lastFailure != item) _log.LogWarning("Could not read or display item tooltip for " + item.id + ": " + ex.Message);
                _lastFailure = item;
            }
        }

        private void Draw(string title, string body, string group, Vector2 pointer, int scale)
        {
            var margin = 4 * scale;
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            // Activate before the first measurement so TMP has initialized its CanvasRenderer and font assets.
            _frame.gameObject.SetActive(true);
            var nativeStyleChanged = RefreshNativeStyle();
            if (nativeStyleChanged || _screenWidth != screenWidth || _screenHeight != screenHeight || _scale != scale ||
                _title.text != title || _body.text != body || _group.text != group)
            {
                var availableWidth = screenWidth - 2 * margin;
                var availableHeight = screenHeight - 2 * margin;
                if (availableWidth <= 2 * margin || availableHeight <= 2 * margin) { Hide(); return; }
                Layout(title, body, group, availableWidth, availableHeight, scale, margin);
                _screenWidth = screenWidth;
                _screenHeight = screenHeight;
                _scale = scale;
            }
            var (x, y) = TerminalItemTooltipLayout.Place(pointer.x, screenHeight - pointer.y,
                _width, _height, screenWidth, screenHeight, 5 * scale, margin);
            NativeFlatInventoryPanel.Position(_frame.rectTransform, new Rect(x, y, _width, _height));
        }

        private void Layout(string title, string body, string group, float availableWidth, float availableHeight, int scale, float padding)
        {
            _title.text = title;
            _body.text = body;
            _group.text = group;
            _group.gameObject.SetActive(group.Length > 0);
            // Assign text first, like GlobalDark. TMP's preferred size properties run the normal input
            // parser (including textPreprocessor); its string-taking measurement overloads do not.
            var textScale = Screen.height / 1080f;
            var maxWidth = (availableWidth - 2 * padding) / textScale;
            var bodyWidth = Mathf.Min(maxWidth, _nativeWidth > 0f ? _nativeWidth : 500f);
            var width = Mathf.Min(maxWidth, Mathf.Ceil(Mathf.Max(_title.preferredWidth,
                Mathf.Min(bodyWidth, Mathf.Max(_body.preferredWidth, group.Length > 0 ? _group.preferredWidth : 0f)))));
            var titleHeight = Measure(_title, width, 0f, _nativeTitleHeight);
            var bodyHeight = Measure(_body, Mathf.Min(width, bodyWidth), titleHeight);
            var groupHeight = group.Length > 0 ? Measure(_group, Mathf.Min(width, bodyWidth), titleHeight + bodyHeight + 6f) + 6f : 0f;
            var height = titleHeight + bodyHeight + groupHeight;
            // Only shrink pathological, unusually long tooltips to fit the screen; never ellipsize a stat row.
            if (height > 0f) textScale = Mathf.Min(textScale, (availableHeight - 2 * padding) / height);
            _width = Mathf.Ceil(width * textScale) + 2 * padding;
            _height = Mathf.Ceil(height * textScale) + 2 * padding;
            _skin.ApplyTooltipFrame(_frame, scale);
            NativeFlatInventoryPanel.Position(_textLayer, new Rect(padding, padding, width, height));
            _textLayer.localScale = new Vector3(textScale, textScale, 1f);
        }

        private static float Measure(TextMeshProUGUI label, float width, float top, float minimumHeight = 0f)
        {
            NativeFlatInventoryPanel.Position(label.rectTransform, new Rect(0f, top, width, minimumHeight));
            var height = Mathf.Max(minimumHeight, Mathf.Ceil(label.preferredHeight));
            NativeFlatInventoryPanel.Position(label.rectTransform, new Rect(0f, top, width, height));
            return height;
        }

        private bool RefreshNativeStyle()
        {
            var native = GlobalDark.main;
            if (!native) return false;
            var title = native.tooltipNameObj;
            var body = native.tooltipDescObj;
            var changed = false;
            if (title && !ReferenceEquals(_nativeTitle, title))
            {
                CopyStyle(_title, title);
                _nativeTitle = title;
                _nativeTitleHeight = title.rectTransform.rect.height;
                changed = true;
            }
            if (body && !ReferenceEquals(_nativeBody, body))
            {
                CopyStyle(_body, body);
                CopyStyle(_group, body);
                _group.richText = false;
                _group.textPreprocessor = null;
                _nativeBody = body;
                _nativeWidth = body.rectTransform.rect.width;
                changed = true;
            }
            return changed;
        }

        private TextMeshProUGUI CreateLabel(string name)
        {
            var label = NativeFlatInventoryPanel.CreateText(_textLayer, name, 16, TextAlignmentOptions.TopLeft);
            label.color = Color.white;
            label.richText = true;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        private static void CopyStyle(TextMeshProUGUI label, TextMeshProUGUI source)
        {
            label.font = source.font;
            label.fontSharedMaterial = source.fontSharedMaterial;
            label.spriteAsset = source.spriteAsset;
            label.styleSheet = source.styleSheet;
            label.textStyle = source.textStyle;
            label.textPreprocessor = source.textPreprocessor;
            label.richText = source.richText;
            label.parseCtrlCharacters = source.parseCtrlCharacters;
            label.isRightToLeftText = source.isRightToLeftText;
            label.overrideColorTags = source.overrideColorTags;
            label.tintAllSprites = source.tintAllSprites;
            label.color = source.color;
            label.enableVertexGradient = source.enableVertexGradient;
            label.colorGradient = source.colorGradient;
            label.colorGradientPreset = source.colorGradientPreset;
            label.fontSize = source.fontSize;
            label.fontSizeMin = source.fontSizeMin;
            label.fontSizeMax = source.fontSizeMax;
            label.enableAutoSizing = source.enableAutoSizing;
            label.characterWidthAdjustment = source.characterWidthAdjustment;
            label.lineSpacingAdjustment = source.lineSpacingAdjustment;
            label.fontStyle = source.fontStyle;
            label.fontWeight = source.fontWeight;
            label.enableKerning = source.enableKerning;
            label.extraPadding = source.extraPadding;
            label.alignment = source.alignment;
            label.margin = source.margin;
            label.characterSpacing = source.characterSpacing;
            label.wordSpacing = source.wordSpacing;
            label.lineSpacing = source.lineSpacing;
            label.paragraphSpacing = source.paragraphSpacing;
            label.enableWordWrapping = source.enableWordWrapping;
            label.wordWrappingRatios = source.wordWrappingRatios;
        }
    }
}
