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
        private readonly LabelPair _title, _body, _group;
        private TextMeshProUGUI? _nativeTitle, _nativeBody;
        private Item? _lastFailure;
        private float _width, _height;
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
            _title = CreateLabel("Item name", TerminalItemTooltipLayout.TitleSize, new Color32(244, 242, 251, 255));
            _body = CreateLabel("Item details", TerminalItemTooltipLayout.DetailSize, new Color32(238, 234, 250, 255));
            _group = CreateLabel("Item group", TerminalItemTooltipLayout.DetailSize, new Color32(230, 224, 246, 255), richText: false);
        }

        internal void Hide() => _frame.gameObject.SetActive(false);

        internal void Show(Item item, int count, Vector2 pointer, int scale)
        {
            try
            {
                var (title, body) = TerminalItemTooltipContent.Read(item);
                if (string.IsNullOrWhiteSpace(title)) { Hide(); return; }
                var group = count > 1 ? TerminalText.Format("inventory.tooltip.group", count) : "";
                Draw(title, body ?? string.Empty, group, pointer, scale);
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
                _title.Text.text != title || _body.Text.text != body || _group.Text.text != group)
            {
                var availableWidth = screenWidth - 2 * margin;
                var availableHeight = screenHeight - 2 * margin;
                var textScale = TerminalItemTooltipLayout.ReadableScale(screenHeight);
                var padding = Mathf.Ceil(TerminalItemTooltipLayout.Padding * textScale);
                if (availableWidth <= 2 * padding || availableHeight <= 2 * padding) { Hide(); return; }
                Layout(title, body, group, availableWidth, availableHeight, scale, textScale, padding);
                _screenWidth = screenWidth;
                _screenHeight = screenHeight;
                _scale = scale;
            }
            var (x, y) = TerminalItemTooltipLayout.Place(pointer.x, screenHeight - pointer.y,
                _width, _height, screenWidth, screenHeight, 5 * scale, margin);
            NativeFlatInventoryPanel.Position(_frame.rectTransform, new Rect(x, y, _width, _height));
        }

        private void Layout(string title, string body, string group, float availableWidth, float availableHeight,
            int scale, float textScale, float padding)
        {
            _title.SetText(title);
            _body.SetText(body);
            _group.SetText(group);
            // Assign raw text before measuring: TMP's preferred size properties use its normal parser.
            var wanted = Mathf.Max(_title.PreferredWidth, Mathf.Max(_body.PreferredWidth, _group.PreferredWidth));
            var width = TerminalItemTooltipLayout.TextWidth(wanted, (availableWidth - 2 * padding) / textScale);
            var height = _title.Place(width, 0f);
            if (body.Length > 0)
            {
                height += TerminalItemTooltipLayout.DetailGap;
                height += _body.Place(width, height);
            }
            if (group.Length > 0)
            {
                height += TerminalItemTooltipLayout.GroupGap;
                height += _group.Place(width, height);
            }
            // Keep all the stat lines on screen; only shrink when a long tooltip exceeds the viewport.
            textScale = TerminalItemTooltipLayout.FitScale(textScale, availableHeight - 2 * padding, height);
            _width = Mathf.Ceil(width * textScale) + 2 * padding;
            _height = Mathf.Ceil(height * textScale) + 2 * padding;
            _skin.ApplyTooltipFrame(_frame, scale);
            NativeFlatInventoryPanel.Position(_textLayer, new Rect(padding, padding, width, height));
            _textLayer.localScale = new Vector3(textScale, textScale, 1f);
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
                _title.CopyNativeParser(title);
                _nativeTitle = title;
                changed = true;
            }
            if (body && !ReferenceEquals(_nativeBody, body))
            {
                _body.CopyNativeParser(body);
                _group.CopyNativeParser(body);
                _nativeBody = body;
                changed = true;
            }
            return changed;
        }

        private LabelPair CreateLabel(string name, float size, Color32 color, bool richText = true) =>
            new LabelPair(_textLayer, name, size, color, richText);

        private sealed class LabelPair
        {
            private readonly TextMeshProUGUI _shadow;
            private readonly bool _richText;
            internal TextMeshProUGUI Text { get; }
            internal float PreferredWidth => Text.gameObject.activeSelf ? Text.preferredWidth : 0f;

            internal LabelPair(Transform parent, string name, float size, Color32 color, bool richText)
            {
                _richText = richText;
                _shadow = Create(name + " shadow");
                Text = Create(name);
                // TMP lays out both passes identically, including CJK fallback glyphs and inline sprites.
                // Only the shadow mesh is recolored after parsing: raw text, colors and alpha tags stay intact.
                _shadow.OnPreRenderText += ShadowColor;

                TextMeshProUGUI Create(string labelName)
                {
                    var label = NativeFlatInventoryPanel.CreateText(parent, labelName, size, TextAlignmentOptions.TopLeft);
                    label.color = color;
                    label.richText = richText;
                    label.enableWordWrapping = true;
                    label.enableAutoSizing = false;
                    label.lineSpacing = 8f;
                    label.overflowMode = TextOverflowModes.Overflow;
                    return label;
                }
            }

            internal void CopyNativeParser(TextMeshProUGUI source)
            {
                Copy(Text);
                Copy(_shadow);

                void Copy(TextMeshProUGUI label)
                {
                    label.font = source.font;
                    label.fontSharedMaterial = source.fontSharedMaterial;
                    label.spriteAsset = source.spriteAsset;
                    label.styleSheet = source.styleSheet;
                    label.textStyle = source.textStyle;
                    label.textPreprocessor = _richText ? source.textPreprocessor : null;
                    label.parseCtrlCharacters = source.parseCtrlCharacters;
                    label.isRightToLeftText = source.isRightToLeftText;
                    label.tintAllSprites = source.tintAllSprites;
                    label.fontStyle = source.fontStyle;
                    label.fontWeight = source.fontWeight;
                    label.enableKerning = source.enableKerning;
                }
            }

            internal void SetText(string value)
            {
                Text.text = _shadow.text = value;
                Text.gameObject.SetActive(value.Length > 0);
                _shadow.gameObject.SetActive(value.Length > 0);
            }

            internal float Place(float width, float top)
            {
                NativeFlatInventoryPanel.Position(Text.rectTransform, new Rect(0f, top, width, 0f));
                var height = Mathf.Ceil(Text.preferredHeight);
                NativeFlatInventoryPanel.Position(Text.rectTransform, new Rect(0f, top, width, height));
                NativeFlatInventoryPanel.Position(_shadow.rectTransform, new Rect(1f, top + 1f, width, height));
                return height;
            }

            private static void ShadowColor(TMP_TextInfo text)
            {
                for (var i = 0; i < text.materialCount; ++i)
                {
                    var mesh = text.meshInfo[i];
                    var colors = mesh.colors32;
                    for (var v = 0; v < mesh.vertexCount; ++v)
                        colors[v] = new Color32(25, 24, 39, colors[v].a);
                }
            }
        }
    }
}
