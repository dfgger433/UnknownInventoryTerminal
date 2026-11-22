using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal
{
    // Inventory chrome and item hints share a Canvas stack. Only search owns a raycast;
    // toolbar clicks still use the fixed TerminalButtonInput hit rectangles.
    internal sealed class NativeTerminalChrome
    {
        private readonly Ae2TerminalSkin _skin;
        private readonly RectTransform _root, _searchViewport;
        private readonly TextMeshProUGUI _title, _status, _placeholder, _hintText;
        private readonly Image _searchFace, _hint;
        private readonly TMP_InputField _search;
        private readonly ButtonVisual?[] _buttons = new ButtonVisual[(int)TerminalButton.Return + 1];
        private Vector2 _pointer;
        private Ae2TerminalLayout _layout;
        private bool _hasHint;

        internal NativeTerminalChrome(Transform parent, Ae2TerminalSkin skin)
        {
            _skin = skin;
            _root = NativeFlatInventoryPanel.Add(parent, "UIT.InventoryChrome").GetComponent<RectTransform>();
            NativeFlatInventoryPanel.Stretch(_root, Vector2.zero, Vector2.zero);
            _title = Label(_root, "Terminal title", TextAlignmentOptions.Left);
            _title.fontStyle = FontStyles.Bold;
            _status = Label(_root, "Terminal status", TextAlignmentOptions.Left);

            _searchFace = Picture(_root, "Search field", raycast: true);
            _searchFace.gameObject.SetActive(false);
            _searchViewport = NativeFlatInventoryPanel.Add(_searchFace.transform, "Text area", typeof(RectMask2D))
                .GetComponent<RectTransform>();
            var inputText = Label(_searchViewport, "Query", TextAlignmentOptions.Left);
            inputText.overflowMode = TextOverflowModes.Overflow;
            inputText.color = Color.white;
            _placeholder = Label(_searchViewport, "Search placeholder", TextAlignmentOptions.Left);
            _placeholder.color = Color.white;
            _search = _searchFace.gameObject.AddComponent<TMP_InputField>();
            _search.textViewport = _searchViewport;
            _search.textComponent = inputText;
            _search.placeholder = _placeholder;
            _search.characterLimit = 256;
            _search.lineType = TMP_InputField.LineType.SingleLine;
            _search.richText = false;
            _search.restoreOriginalTextOnEscape = false;
            _search.navigation = new Navigation { mode = Navigation.Mode.None };
            _search.transition = Selectable.Transition.None;
            _search.targetGraphic = _searchFace;
            _search.onValueChanged.AddListener(value => SearchApi.Query = value);
            // Enter finishes editing and must not leave a selected field that Submit can reopen.
            _search.onSubmit.AddListener(_ => ClearSearchSelection());
            _searchFace.gameObject.SetActive(true);
            var caret = _searchViewport.GetComponentInChildren<TMP_SelectionCaret>();
            if (caret) caret.raycastTarget = false;

            _hint = Picture(_root, "Control hint");
            _hintText = Label(_hint.transform, "Hint text", TextAlignmentOptions.Center);
            _hintText.color = Color.white;
            _hint.gameObject.SetActive(false);
        }

        internal bool SearchFocused => _search.isActiveAndEnabled && _search.isFocused;

        internal void ReleaseSearchFocus()
        {
            if (_search.isFocused) _search.DeactivateInputField();
            ClearSearchSelection();
        }

        private void ClearSearchSelection()
        {
            var events = EventSystem.current;
            if (events && !events.alreadySelecting && events.currentSelectedGameObject == _search.gameObject)
                events.SetSelectedGameObject(null);
        }

        internal void DrawHeader(Ae2TerminalLayout layout, string status, bool blockInput)
        {
            _layout = layout;
            _pointer = Input.mousePosition;
            _pointer.y = Screen.height - _pointer.y;
            _hasHint = false;
            _title.text = TerminalText.Get("terminal.title");
            _status.text = status;
            Place(_title, Area(Ae2TerminalLayout.Rail + 7, 2, 46, 15), 8);
            Place(_status, Area(Ae2TerminalLayout.Rail + Ae2TerminalLayout.Inset, layout.StatusTop, 62,
                Ae2TerminalLayout.StatusHeight), 6);

            var size = _skin.SourceSize(Ae2Image.Search) ?? new Vector2Int(Ae2TerminalLayout.SearchWidth, 12);
            var search = Area(Ae2TerminalLayout.SearchX, Ae2TerminalLayout.SearchY,
                Mathf.Min(size.x, Ae2TerminalLayout.SearchWidth), Mathf.Min(size.y, Ae2TerminalLayout.SearchHeight));
            NativeFlatInventoryPanel.Position(_searchFace.rectTransform, search);
            NativeFlatInventoryPanel.Stretch(_searchViewport, new Vector2(3 * layout.Scale, 0), new Vector2(-3 * layout.Scale, 0));
            _search.textComponent.fontSize = _placeholder.fontSize = 8 * layout.Scale;
            _placeholder.text = SearchFocused ? "" : TerminalText.Get("terminal.search");
            _search.interactable = !blockInput;
            if (_search.text != SearchApi.Query) _search.SetTextWithoutNotify(SearchApi.Query);
            if (!blockInput && search.Contains(_pointer) && Input.GetMouseButtonDown(1))
            {
                SearchApi.Query = "";
                _search.SetTextWithoutNotify("");
                _search.ActivateInputField();
            }
            _skin.Apply(_searchFace, SearchFocused ? Ae2Image.SearchFocused : Ae2Image.Search, new Color32(91, 95, 117, 255));
            if (search.Contains(_pointer)) ShowHint(search, TerminalText.Get("terminal.searchHint"), below: true);
        }

        internal void DrawButton(TerminalButton button, TerminalButton focused, Ae2Image icon, string hint, string fallback,
            bool visible = true, bool hintBelow = false)
        {
            ref var visual = ref _buttons[(int)button];
            if (!visible)
            {
                if (visual != null) visual.Face.gameObject.SetActive(false);
                return;
            }
            if (visual == null)
            {
                var image = Picture(_root, button.ToString());
                visual = new ButtonVisual(image, Picture(image.transform, "Icon"), Label(image.transform, "Fallback", TextAlignmentOptions.Center));
                _hint.transform.SetAsLastSibling();
            }
            visual.Face.gameObject.SetActive(true);
            var (x, y) = TerminalButtonInput.Origin(button);
            var hit = Area(x, y, Ae2TerminalLayout.ButtonWidth, Ae2TerminalLayout.ButtonHeight);
            var hovered = hit.Contains(_pointer);
            var face = Ae2ToolbarAppearance.Face(hovered, focused == button);
            var offset = Ae2ToolbarAppearance.OffsetY(face);
            var faceSize = _skin.SourceSize(face) ?? new Vector2Int(Ae2TerminalLayout.ButtonWidth, Ae2TerminalLayout.ButtonHeight - offset);
            NativeFlatInventoryPanel.Position(visual.Face.rectTransform, Area(x, y + offset, faceSize.x, faceSize.y));
            _skin.Apply(visual.Face, face, Ae2TerminalSkin.Shell);
            if (_skin.SourceSize(icon) is { } size && _skin.VisibleBounds(icon) is { } bounds)
            {
                var scale = _layout.Scale;
                var (dx, dy) = Ae2ToolbarAppearance.CenterIcon(bounds, faceSize.x, faceSize.y, scale);
                NativeFlatInventoryPanel.Position(visual.Icon.rectTransform, new Rect(dx, dy, size.x * scale, size.y * scale));
                _skin.Apply(visual.Icon, icon, Color.clear);
                visual.Icon.gameObject.SetActive(true);
                visual.Fallback.gameObject.SetActive(false);
            }
            else
            {
                visual.Icon.gameObject.SetActive(false);
                visual.Fallback.gameObject.SetActive(true);
                visual.Fallback.text = fallback;
                visual.Fallback.fontSize = 8 * _layout.Scale;
            }
            if (hovered) ShowHint(hit, hint, hintBelow);
        }

        internal void FinishDraw() => _hint.gameObject.SetActive(_hasHint);

        private void ShowHint(Rect control, string text, bool below)
        {
            _hasHint = true;
            var scale = _layout.Scale;
            _hintText.fontSize = 8 * scale;
            var gap = 2 * scale;
            var width = Mathf.Min(_hintText.GetPreferredValues(text).x + 12 * scale, _layout.Width - 2 * gap);
            var x = Mathf.Clamp(below ? control.x : control.xMax + gap, _layout.Left + gap, _layout.Left + _layout.Width - width - gap);
            var y = below ? control.yMax + gap : control.y + gap;
            NativeFlatInventoryPanel.Position(_hint.rectTransform,
                new Rect(x, y, width, (_skin.SourceSize(Ae2Image.Tooltip)?.y ?? 15) * scale));
            _skin.Apply(_hint, Ae2Image.Tooltip, Ae2TerminalSkin.Ink);
            _hintText.text = text;
        }

        private Rect Area(int x, int y, int width, int height) => new Rect(_layout.Left + x * _layout.Scale,
            _layout.Top + y * _layout.Scale, width * _layout.Scale, height * _layout.Scale);

        private void Place(TextMeshProUGUI label, Rect bounds, int fontSize)
        {
            label.fontSize = fontSize * _layout.Scale;
            NativeFlatInventoryPanel.Position(label.rectTransform, bounds);
        }

        private static Image Picture(Transform parent, string name, bool raycast = false)
        {
            var image = NativeFlatInventoryPanel.Add(parent, name, typeof(Image)).GetComponent<Image>();
            image.raycastTarget = raycast;
            return image;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, TextAlignmentOptions alignment)
        {
            var label = NativeFlatInventoryPanel.CreateText(parent, name, 8, alignment);
            NativeFlatInventoryPanel.Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            return label;
        }

        private sealed class ButtonVisual(Image face, Image icon, TextMeshProUGUI fallback)
        {
            internal Image Face { get; } = face;
            internal Image Icon { get; } = icon;
            internal TextMeshProUGUI Fallback { get; } = fallback;
        }
    }
}
