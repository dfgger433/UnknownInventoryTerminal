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
        private readonly TextMeshProUGUI _title, _status, _placeholder;
        private readonly Image _searchFace;
        private readonly TerminalControlHint _hint;
        private readonly TMP_InputField _search;
        private readonly ButtonVisual?[] _buttons = new ButtonVisual[(int)TerminalButton.Return + 1];
        private Vector2 _pointer;
        private Ae2TerminalLayout _layout;

        internal NativeTerminalChrome(Transform parent, Ae2TerminalSkin skin, TerminalControlHint hint)
        {
            _skin = skin;
            _hint = hint;
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
            if (search.Contains(_pointer)) _hint.Offer(search, TerminalText.Get("terminal.searchHint"), below: true);
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
            }
            visual.Face.gameObject.SetActive(true);
            var (x, y) = TerminalButtonInput.Origin(button);
            var hit = Area(x, y, Ae2TerminalLayout.ButtonWidth, Ae2TerminalLayout.ButtonHeight);
            var hovered = hit.Contains(_pointer);
            var iconVisible = _skin.ApplyToolbarFace(visual.Face, visual.Icon, hit.position, icon, _layout.Scale, hovered, focused == button);
            visual.Fallback.gameObject.SetActive(!iconVisible);
            if (!iconVisible)
            {
                visual.Fallback.text = fallback;
                visual.Fallback.fontSize = 8 * _layout.Scale;
            }
            if (hovered) _hint.Offer(hit, hint, hintBelow);
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
