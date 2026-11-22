using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    // A single tooltip for search, ordinary toolbar buttons and drag actions.
    internal sealed class TerminalControlHint
    {
        private readonly Ae2TerminalSkin _skin;
        private readonly Image _frame;
        private readonly TextMeshProUGUI _label;
        private (Rect Anchor, string Text, bool Below)? _request;

        internal TerminalControlHint(Transform parent, Ae2TerminalSkin skin)
        {
            _skin = skin;
            var overlay = TerminalCanvasOrder.AddTooltipLayer(parent, "UIT.ControlHintOverlay", TerminalCanvasOrder.ControlHint);
            _frame = NativeFlatInventoryPanel.Add(overlay, "Control hint frame", typeof(Image)).GetComponent<Image>();
            _frame.raycastTarget = false;
            _label = NativeFlatInventoryPanel.CreateText(_frame.transform, "Control hint text", 8, TextAlignmentOptions.TopLeft);
            _label.enableWordWrapping = true;
            Hide();
        }

        internal void Offer(Rect anchor, string text, bool below = false) => _request = (anchor, text, below);

        internal bool Present(int scale)
        {
            var request = _request;
            _request = null;
            var margin = 4 * scale;
            var padding = 4 * scale;
            var maxWidth = Mathf.Min(154 * scale, Screen.width - 2 * margin) - 2 * padding;
            var maxHeight = Screen.height - 2 * margin - 2 * padding;
            if (request is not { } hint || maxWidth <= 0 || maxHeight <= 0)
            {
                Hide();
                return false;
            }

            _label.fontSize = 8 * scale;
            var width = Mathf.Min(maxWidth, Mathf.Ceil(_label.GetPreferredValues(hint.Text).x));
            var height = Mathf.Min(maxHeight, Mathf.Ceil(_label.GetPreferredValues(hint.Text, width, 0).y));
            var (x, y) = TerminalItemTooltipLayout.PlaceControl(hint.Anchor.xMin, hint.Anchor.yMin, hint.Anchor.xMax,
                hint.Anchor.yMax, hint.Below, width + 2 * padding, height + 2 * padding, Screen.width, Screen.height, 3 * scale, margin);
            NativeFlatInventoryPanel.Position(_frame.rectTransform, new Rect(x, y, width + 2 * padding, height + 2 * padding));
            NativeFlatInventoryPanel.Position(_label.rectTransform, new Rect(padding, padding, width, height));
            _skin.ApplyHintFrame(_frame, scale);
            _label.text = hint.Text;
            _frame.gameObject.SetActive(true);
            return true;
        }

        internal void Hide()
        {
            _request = null;
            _frame.gameObject.SetActive(false);
        }
    }
}
