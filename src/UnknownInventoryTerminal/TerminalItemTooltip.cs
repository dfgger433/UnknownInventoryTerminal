using System;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    // The terminal owns its tooltip canvas; the game's GlobalDark tooltip and its controls are not modified.
    internal sealed class TerminalItemTooltip
    {
        private readonly ManualLogSource _log;
        private readonly RectTransform _bounds, _background, _divider;
        private readonly TextMeshProUGUI _title, _body;
        private Item? _lastFailure;
        private float _width, _height;
        private int _screenWidth, _screenHeight, _scale;

        internal TerminalItemTooltip(Transform parent, ManualLogSource log)
        {
            _log = log;
            var overlay = NativeFlatInventoryPanel.Add(parent, "UIT.ItemTooltipOverlay", typeof(Canvas), typeof(CanvasGroup));
            NativeFlatInventoryPanel.Stretch(overlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            overlay.GetComponent<CanvasGroup>().blocksRaycasts = false;
            // The inventory chrome uses the parent canvas, so this order covers buttons as well as item slots.
            var canvas = overlay.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;
            _bounds = Fill(overlay.transform, "Item tooltip", new Color32(102, 66, 138, 255));
            _background = Fill(_bounds, "Background", new Color32(21, 14, 29, 250));
            _divider = Fill(_bounds, "Title separator", new Color32(73, 53, 90, 255));
            _title = CreateLabel("Item name", GlobalDark.main ? GlobalDark.main.tooltipNameObj : null);
            _body = CreateLabel("Item details", GlobalDark.main ? GlobalDark.main.tooltipDescObj : null);
            _title.color = new Color32(252, 247, 255, 255);
            _body.color = new Color32(224, 212, 236, 255);
            Hide();
        }

        internal void Hide() => _bounds.gameObject.SetActive(false);

        internal void Show(Item item, int count, Vector2 pointer, int scale)
        {
            try
            {
                var expanded = PlayerCamera.alwaysExpandDescriptions || Input.GetKey(KeyBinds.GetBind("expanddesc"));
                var (title, body) = TerminalItemTooltipContent.Read(item, expanded);
                if (string.IsNullOrWhiteSpace(title)) { Hide(); return; }
                if (count > 1) body = TerminalText.Format("inventory.tooltip.group", count) + "\n\n" + body;
                Draw(title, body, pointer, scale);
                _lastFailure = null;
            }
            catch (Exception ex)
            {
                Hide();
                if (_lastFailure != item) _log.LogWarning("Could not read or display item tooltip for " + item.id + ": " + ex.Message);
                _lastFailure = item;
            }
        }

        private void Draw(string title, string body, Vector2 pointer, int scale)
        {
            var margin = 4 * scale;
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            if (_screenWidth != screenWidth || _screenHeight != screenHeight || _scale != scale ||
                _title.text != title || _body.text != body)
            {
                var availableWidth = Mathf.Min(154 * scale, screenWidth - 2 * margin);
                var availableHeight = screenHeight - 2 * margin;
                if (availableWidth <= 2 * margin || availableHeight <= 4 * margin) { Hide(); return; }
                Layout(title, body, availableWidth, availableHeight, scale, margin);
                _screenWidth = screenWidth;
                _screenHeight = screenHeight;
                _scale = scale;
            }
            var (x, y) = TerminalItemTooltipLayout.Place(pointer.x, screenHeight - pointer.y,
                _width, _height, screenWidth, screenHeight, 5 * scale, margin);
            NativeFlatInventoryPanel.Position(_bounds, new Rect(x, y, _width, _height));
            _bounds.gameObject.SetActive(true);
        }

        private void Layout(string title, string body, float availableWidth, float availableHeight, int scale, float padding)
        {
            _title.fontSize = 8 * scale;
            _body.fontSize = 7 * scale;
            var maxTextWidth = availableWidth - 2 * padding;
            var width = Mathf.Clamp(Mathf.Ceil(Mathf.Max(_title.GetPreferredValues(title).x, _body.GetPreferredValues(body).x)),
                Mathf.Min(46 * scale, maxTextWidth), maxTextWidth);
            var titleHeight = Mathf.Min(Mathf.Ceil(_title.GetPreferredValues(title, width, 0).y), availableHeight - 4 * padding);
            var bodyY = titleHeight + 3 * padding;
            var bodyHeight = Mathf.Min(Mathf.Ceil(_body.GetPreferredValues(body, width, 0).y), availableHeight - bodyY - padding);
            _width = width + 2 * padding;
            _height = bodyY + bodyHeight + padding;
            NativeFlatInventoryPanel.Position(_background, new Rect(scale, scale, _width - 2 * scale, _height - 2 * scale));
            NativeFlatInventoryPanel.Position(_divider, new Rect(padding, titleHeight + 2 * padding, width, scale));
            NativeFlatInventoryPanel.Position(_title.rectTransform, new Rect(padding, padding, width, titleHeight));
            NativeFlatInventoryPanel.Position(_body.rectTransform, new Rect(padding, bodyY, width, bodyHeight));
            _title.text = title;
            _body.text = body;
        }

        private TextMeshProUGUI CreateLabel(string name, TextMeshProUGUI? source)
        {
            var label = NativeFlatInventoryPanel.CreateText(_bounds, name, 14, TextAlignmentOptions.TopLeft);
            // Keep the native font's locale fallbacks and inline stat sprites, but use terminal-owned labels.
            if (source && source!.font) label.font = source.font;
            if (source && source!.spriteAsset) label.spriteAsset = source.spriteAsset;
            label.richText = true;
            label.enableWordWrapping = true;
            return label;
        }

        private static RectTransform Fill(Transform parent, string name, Color color)
        {
            var image = NativeFlatInventoryPanel.Add(parent, name, typeof(Image)).GetComponent<Image>();
            image.raycastTarget = false;
            image.color = color;
            return image.rectTransform;
        }
    }
}
