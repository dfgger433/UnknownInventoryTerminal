using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    // A native modal over the inventory canvas. The caller owns the transfer and validates it again on confirmation.
    internal sealed class FluidTransferDialog
    {
        private const int Width = 124, Inset = 10, SliderTop = 39;
        private readonly Ae2TerminalSkin _skin;
        private readonly RectTransform _root, _amountViewport, _lane;
        private readonly Image _frame, _amountFace, _railLeft, _railMiddle, _railRight, _thumb;
        private readonly TextMeshProUGUI _title, _amountText, _limit;
        private readonly TMP_InputField _amount;
        private readonly Slider _slider;
        private readonly DialogButton _transfer, _cancelButton;
        private readonly Selectable[] _tabOrder;
        private readonly Vector2Int _thumbSize, _railSize;
        private readonly bool _hasSliderArt;
        private int _maximumMl, _scale = 1, _closedFrame = -1;
        private bool _focusPending, _submitPending, _amountFocused;
        private Vector2Int _screenSize;
        private Action<int>? _confirm;

        internal FluidTransferDialog(Ae2TerminalSkin skin, Transform parent)
        {
            _skin = skin;
            var root = NativeFlatInventoryPanel.Add(parent, "UIT.FluidTransferDialog", typeof(Canvas), typeof(GraphicRaycaster));
            root.SetActive(false);
            _root = root.GetComponent<RectTransform>();
            NativeFlatInventoryPanel.Stretch(_root, Vector2.zero, Vector2.zero);
            var canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = TerminalCanvasOrder.ControlHint;
            canvas.overridePixelPerfect = true;
            canvas.pixelPerfect = true;

            var backdrop = Picture(_root, "Modal backdrop");
            backdrop.color = new Color32(48, 50, 58, 135);
            MakeHitSurface(backdrop);
            _frame = Picture(backdrop.transform, "Transfer frame");
            _skin.Apply(_frame, Ae2Image.SettingsBackground, Ae2TerminalSkin.Shell);
            _title = Label(_frame.transform, "Transfer title", TextAlignmentOptions.Left);
            _title.fontStyle = FontStyles.Bold;

            _amountFace = Picture(_frame.transform, "Millilitre input");
            MakeHitSurface(_amountFace);
            _amountViewport = NativeFlatInventoryPanel.Add(_amountFace.transform, "Input viewport", typeof(RectMask2D))
                .GetComponent<RectTransform>();
            _amountText = Label(_amountViewport, "Amount", TextAlignmentOptions.Right);
            NativeFlatInventoryPanel.Stretch(_amountText.rectTransform, Vector2.zero, Vector2.zero);
            _amountText.overflowMode = TextOverflowModes.Overflow;
            _amount = _amountFace.gameObject.AddComponent<TMP_InputField>();
            _amount.textViewport = _amountViewport;
            _amount.textComponent = _amountText;
            _amount.lineType = TMP_InputField.LineType.SingleLine;
            _amount.keyboardType = TouchScreenKeyboardType.NumberPad;
            _amount.richText = false;
            _amount.restoreOriginalTextOnEscape = false;
            _amount.onFocusSelectAll = true;
            _amount.selectionColor = new Color32(170, 216, 255, 200);
            _amount.transition = Selectable.Transition.None;
            _amount.targetGraphic = _amountFace;
            // Validate the whole input instead of deleting punctuation from a paste ("1.5" must not become "15").
            _amount.onValueChanged.AddListener(_ => SyncAmount());
            _amount.onSubmit.AddListener(_ => _submitPending = IsOpen);
            _limit = Label(_frame.transform, "Maximum and units", TextAlignmentOptions.Left);
            _limit.enableAutoSizing = true;

            var sliderHit = Picture(_frame.transform, "Millilitre slider");
            sliderHit.color = Color.clear;
            MakeHitSurface(sliderHit);
            _railLeft = Picture(sliderHit.transform, "Rail left cap");
            _railMiddle = Picture(sliderHit.transform, "Rail center column");
            _railRight = Picture(sliderHit.transform, "Rail right cap");
            _lane = NativeFlatInventoryPanel.Add(sliderHit.transform, "Thumb travel").GetComponent<RectTransform>();
            _thumb = Picture(_lane, "Thumb");
            _thumb.rectTransform.anchorMin = new Vector2(0, 0);
            _thumb.rectTransform.anchorMax = new Vector2(0, 1);
            _railSize = _skin.SourceSize(Ae2Image.FluidRailMiddle) ?? Vector2Int.zero;
            _thumbSize = _skin.SourceSize(Ae2Image.FluidThumb) ?? Vector2Int.zero;
            _hasSliderArt = _railSize.x == 1 && _railSize.y > 0 && _thumbSize.x > 0 && _thumbSize.y > 0 &&
                _skin.SourceSize(Ae2Image.FluidRailLeft) == _railSize && _skin.SourceSize(Ae2Image.FluidRailRight) == _railSize;
            if (_hasSliderArt)
            {
                _skin.Apply(_railLeft, Ae2Image.FluidRailLeft, Color.clear);
                _skin.Apply(_railMiddle, Ae2Image.FluidRailMiddle, Color.clear);
                _skin.Apply(_railRight, Ae2Image.FluidRailRight, Color.clear);
                _skin.Apply(_thumb, Ae2Image.FluidThumb, Color.clear);
            }
            _slider = sliderHit.gameObject.AddComponent<Slider>();
            _slider.handleRect = _thumb.rectTransform;
            _slider.targetGraphic = _thumb;
            _slider.direction = Slider.Direction.LeftToRight;
            _slider.onValueChanged.AddListener(value =>
            {
                if (!IsOpen || _maximumMl < 2) return;
                var ml = 1 + (int)Math.Round(value * (_maximumMl - 1d), MidpointRounding.AwayFromZero);
                _amount.SetTextWithoutNotify(ml.ToString(CultureInfo.InvariantCulture));
                SyncAmount(syncSlider: false);
            });

            _transfer = CreateButton("Transfer", Confirm);
            _cancelButton = CreateButton("Cancel", Hide);
            _tabOrder = new Selectable[] { _amount, _slider, _transfer, _cancelButton };
            foreach (var control in _tabOrder) control.navigation = new Navigation { mode = Navigation.Mode.Explicit };
        }

        internal bool IsOpen => _root && _root.gameObject.activeInHierarchy;
        // A release/keystroke that dismisses the modal still belongs to it for the rest of that frame.
        internal bool BlocksInput => IsOpen || _closedFrame == Time.frameCount;

        internal bool Contains(GameObject target) => IsOpen && target && target.transform.IsChildOf(_root);

        internal void Show(int maximumMl, Action<int> confirm, int scale)
        {
            Hide();
            _maximumMl = maximumMl;
            _confirm = confirm;
            _title.text = TerminalText.Get("fluidTransfer.title");
            _limit.text = TerminalText.Format("fluidTransfer.limit", _maximumMl);
            _transfer.Caption.text = TerminalText.Get("fluidTransfer.confirm");
            _cancelButton.Caption.text = TerminalText.Get("fluidTransfer.cancel");
            _slider.gameObject.SetActive(_hasSliderArt && _maximumMl > 1);
            _amount.SetTextWithoutNotify(_maximumMl > 0 ? _maximumMl.ToString(CultureInfo.InvariantCulture) : "");
            _focusPending = true;
            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
            _scale = Math.Max(1, scale);
            Layout();
            SyncAmount();
            FocusAmount();
            PaintAmount();
        }

        internal void Hide()
        {
            _confirm = null;
            _focusPending = _submitPending = false;
            if (!_root || !_root.gameObject.activeSelf) return;
            _closedFrame = Time.frameCount;
            var events = EventSystem.current;
            if (events && !events.alreadySelecting && events.currentSelectedGameObject is { } target && Contains(target))
                events.SetSelectedGameObject(null);
            _root.gameObject.SetActive(false);
        }

        internal void SetScale(int scale)
        {
            if (!IsOpen) return;
            scale = Math.Max(1, scale);
            if (_scale == scale && _screenSize.x == Screen.width && _screenSize.y == Screen.height) return;
            _scale = scale;
            Layout();
        }

        internal void Tick()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); return; }
            if (_submitPending)
            {
                _submitPending = false;
                Confirm();
                if (!IsOpen) return;
            }
            SetScale(_scale);
            if (_focusPending) FocusAmount();
            if (Input.GetKeyDown(KeyCode.Tab)) MoveFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (_amountFocused != _amount.isFocused) PaintAmount();
        }

        private void FocusAmount()
        {
            var events = EventSystem.current;
            if (!events || events.alreadySelecting) return;
            _focusPending = false;
            events.SetSelectedGameObject(_amount.gameObject);
            _amount.ActivateInputField();
            var caret = _amountViewport.GetComponentInChildren<TMP_SelectionCaret>();
            if (caret) caret.raycastTarget = false;
        }

        private void MoveFocus(bool backwards)
        {
            var events = EventSystem.current;
            if (!events || events.alreadySelecting) return;
            var current = Array.FindIndex(_tabOrder, control => control.gameObject == events.currentSelectedGameObject);
            if (current < 0 && !backwards) current = _tabOrder.Length - 1;
            if (current < 0) current = 0;
            for (var i = 0; i < _tabOrder.Length; i++)
            {
                current = (current + (backwards ? _tabOrder.Length - 1 : 1)) % _tabOrder.Length;
                var control = _tabOrder[current];
                if (!control.isActiveAndEnabled || !control.IsInteractable()) continue;
                if (control == _amount) FocusAmount();
                else events.SetSelectedGameObject(control.gameObject);
                return;
            }
        }

        private void Confirm()
        {
            if (!IsOpen) return;
            if (!TryGetAmount(out var ml))
            {
                FocusAmount();
                return;
            }
            var confirm = _confirm;
            Hide();
            confirm?.Invoke(ml);
        }

        private bool TryGetAmount(out int ml) => TerminalFluidAmount.TryParse(_amount.text, _maximumMl, out ml);

        private void SyncAmount(bool syncSlider = true)
        {
            var valid = TryGetAmount(out var ml);
            _transfer.interactable = valid;
            if (valid && syncSlider && _slider.gameObject.activeSelf)
                _slider.SetValueWithoutNotify((ml - 1) / (_maximumMl - 1f));
        }

        private void PaintAmount()
        {
            _amountFocused = _amount.isFocused;
            _skin.Apply(_amountFace, _amountFocused ? Ae2Image.SearchFocused : Ae2Image.Search, Ae2TerminalSkin.Slot);
            if (_amountFocused) _amountFace.color = _amountFace.sprite ? Ae2TerminalSkin.Slot : Ae2TerminalSkin.Ink;
            _amountText.color = _amountFocused ? Color.white : Ae2TerminalSkin.Ink;
        }

        private void Layout()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            var showSlider = _slider.gameObject.activeSelf;
            var buttonTop = showSlider ? 59 : 39;
            var height = (buttonTop + 22) * _scale;
            NativeFlatInventoryPanel.Position(_frame.rectTransform,
                new Rect((_screenSize.x - Width * _scale) / 2, (_screenSize.y - height) / 2, Width * _scale, height));
            if (_frame.hasBorder)
            {
                _frame.type = Image.Type.Sliced;
                _frame.pixelsPerUnitMultiplier = 1f / _scale;
            }
            Place(_title.rectTransform, Inset, 5, Width - 2 * Inset, 13);
            _title.fontSize = 9 * _scale;
            Place(_amountFace.rectTransform, Inset, 23, 53, 12);
            NativeFlatInventoryPanel.Stretch(_amountViewport, new Vector2(3 * _scale, 0), new Vector2(-3 * _scale, 0));
            _amountText.fontSize = 8 * _scale;
            Place(_limit.rectTransform, 67, 23, Width - Inset - 67, 12);
            _limit.fontSizeMin = 5 * _scale;
            _limit.fontSizeMax = 8 * _scale;
            _limit.fontSize = 8 * _scale;
            Place((RectTransform)_transfer.transform, Inset, buttonTop, 50, 18);
            Place((RectTransform)_cancelButton.transform, Width - Inset - 50, buttonTop, 50, 18);
            _transfer.SetScale(_scale);
            _cancelButton.SetScale(_scale);
            if (!showSlider) return;

            const int railWidth = Width - 2 * Inset;
            Place((RectTransform)_slider.transform, Inset, SliderTop, railWidth, _thumbSize.y);
            var railTop = (_thumbSize.y - _railSize.y) / 2;
            // Only the uniform center column is stretched. The caps keep exactly one source pixel each.
            Place(_railLeft.rectTransform, 0, railTop, 1, _railSize.y);
            Place(_railMiddle.rectTransform, 1, railTop, railWidth - 2, _railSize.y);
            Place(_railRight.rectTransform, railWidth - 1, railTop, 1, _railSize.y);
            var halfThumb = _thumbSize.x * _scale / 2f;
            NativeFlatInventoryPanel.Stretch(_lane, new Vector2(halfThumb, 0), new Vector2(-halfThumb, 0));
            _thumb.rectTransform.sizeDelta = new Vector2(_thumbSize.x * _scale, 0);
        }

        private void Place(RectTransform rect, int x, int y, int width, int height) =>
            NativeFlatInventoryPanel.Position(rect, new Rect(x * _scale, y * _scale, width * _scale, height * _scale));

        private static void MakeHitSurface(Image image)
        {
            TerminalHitSurface.Enable(image);
            NativeFlatInventoryPanel.Stretch(image.rectTransform, Vector2.zero, Vector2.zero);
        }

        private static Image Picture(Transform parent, string name)
        {
            var image = NativeFlatInventoryPanel.Add(parent, name, typeof(Image)).GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, TextAlignmentOptions align) =>
            NativeFlatInventoryPanel.CreateText(parent, name, 8, align);

        private DialogButton CreateButton(string name, Action pressed)
        {
            var hit = Picture(_frame.transform, name);
            hit.color = Color.clear;
            MakeHitSurface(hit);
            var button = hit.gameObject.AddComponent<DialogButton>();
            button.Initialize(_skin, Picture(hit.transform, "Face"), Label(hit.transform, "Caption", TextAlignmentOptions.Center));
            button.targetGraphic = hit;
            button.onClick.AddListener(() => pressed());
            return button;
        }

        private sealed class DialogButton : Button
        {
            private Ae2TerminalSkin? _skin;
            private Image? _face;
            private int _scale = 1;
            internal TextMeshProUGUI Caption { get; private set; } = null!;

            internal void Initialize(Ae2TerminalSkin skin, Image face, TextMeshProUGUI caption)
            {
                _skin = skin;
                _face = face;
                Caption = caption;
            }

            internal void SetScale(int scale)
            {
                _scale = scale;
                Caption.fontSize = 8 * scale;
                Paint(currentSelectionState);
            }

            protected override void DoStateTransition(SelectionState state, bool instant) => Paint(state);

            private void Paint(SelectionState state)
            {
                if (_skin == null || _face == null) return;
                var face = state switch
                {
                    SelectionState.Disabled => Ae2Image.SettingsOptionDisabled,
                    SelectionState.Pressed or SelectionState.Selected => Ae2Image.SettingsOptionSelected,
                    SelectionState.Highlighted => Ae2Image.SettingsOptionHover,
                    _ => Ae2Image.SettingsOption
                };
                _skin.Apply(_face, face, state == SelectionState.Disabled ? Ae2TerminalSkin.Slot : Ae2TerminalSkin.Focus);
                if (_face.hasBorder)
                {
                    _face.type = Image.Type.Sliced;
                    _face.pixelsPerUnitMultiplier = 1f / _scale;
                }
                NativeFlatInventoryPanel.Stretch(_face.rectTransform, Vector2.zero, Vector2.zero);
                NativeFlatInventoryPanel.Stretch(Caption.rectTransform, new Vector2(_scale, 2 * _scale), new Vector2(-_scale, 0));
                Caption.color = state == SelectionState.Disabled ? Color.white : Ae2TerminalSkin.Ink;
            }
        }
    }
}
