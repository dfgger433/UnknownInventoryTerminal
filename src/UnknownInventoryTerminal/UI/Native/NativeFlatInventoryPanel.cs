using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal
{
    // Groups are display-only. Every native InvButton stays bound to one real Item.
    internal sealed class NativeFlatInventoryPanel : IDisposable
    {
        private sealed class Tile
        {
            internal GameObject Root = null!;
            internal InvButton Button = null!;
            internal TextMeshProUGUI Count = null!;
            internal TilePointer Pointer = null!;
            internal int PinColumn = -1;
        }

        private sealed class TilePointer : MonoBehaviour, IPointerClickHandler
        {
            internal NativeFlatInventoryPanel Owner = null!;
            internal (string Type, string Parent)? Group;
            internal int Count;
            internal InvButton? ItemButton;
            public void OnPointerClick(PointerEventData e)
            {
                if (Group.HasValue && e.button == PointerEventData.InputButton.Left) Owner.ChangeSelection(Group);
            }
        }

        private sealed class GridWheel : MonoBehaviour, IScrollHandler
        {
            internal NativeFlatInventoryPanel Owner = null!;
            public void OnScroll(PointerEventData e)
            {
                if (e.scrollDelta.y == 0 || InteractionInProgress()) return;
                var slider = Owner._slider!;
                slider.value -= Mathf.Sign(e.scrollDelta.y);
                e.Use();
            }
        }

        private sealed class PinnedRowWheel : MonoBehaviour, IScrollHandler
        {
            internal NativeFlatInventoryPanel Owner = null!;
            public void OnScroll(PointerEventData e)
            {
                if (e.scrollDelta.y == 0) return;
                Owner.MovePinPage(-(int)Mathf.Sign(e.scrollDelta.y));
                e.Use();
            }
        }

        private enum TileKind { Group, Instance, Pinned }
        private readonly ManualLogSource _log;
        private readonly Ae2TerminalSkin _skin;
        private readonly NativeLoadoutPanel _loadout;
        private readonly TerminalPins<Body, Item> _pins = new(Ae2TerminalLayout.Columns);
        private readonly Dictionary<(string Key, TileKind Kind), Tile> _tiles = new();
        private RectTransform? _pinnedRow;
        private Image? _pinnedGutter, _grooveTop, _pinnedHighlight;
        private TextMeshProUGUI? _pinPageLabel;
        private Button? _pinPager;
        private bool _hasPinnedRow, _hasPinnedPager;
        private int _storageTileCount;
        private GameObject? _root;
        private RectTransform? _windowSurface, _board, _panel, _viewport, _content, _lower, _extensionRoot;
        internal RectTransform? ExtensionRoot => _visible && _root && _root!.activeInHierarchy && _extensionRoot ? _extensionRoot : null;
        internal bool ContainsExtension(GameObject target) => _extensionRoot && target &&
            (target.transform == _extensionRoot || target.transform.IsChildOf(_extensionRoot!));
        private GridLayoutGroup? _grid;
        private Slider? _slider;
        private Image? _thumb;
        private InvButton? _automaticTarget;
        private TextMeshProUGUI? _empty;
        private TerminalItemTooltip? _itemTooltip;
        private TerminalControlHint? _controlHint;
        internal NativeTerminalChrome? Chrome { get; private set; }
        internal FluidTransferDialog? FluidDialog { get; private set; }

        internal void ShowFluidTransfer(int maximumMl, Action<int> confirm)
        {
            if (!_root || !_root!.activeInHierarchy) return;
            FluidDialog ??= new FluidTransferDialog(_skin, _root.GetComponent<GraphicRaycaster>());
            Chrome?.ReleaseSearchFocus();
            _itemTooltip?.Hide();
            _controlHint?.Hide();
            FluidDialog.Show(maximumMl, confirm, _layout.Scale);
        }
        private readonly List<RaycastResult> _hoverHits = new List<RaycastResult>();
        private PointerEventData? _hoverPointer;
        private EventSystem? _hoverEvents;
        internal bool CanReturnToGroups => _visible && _selectionKey.HasValue && _actionBar?.gameObject.activeSelf != true;
        internal void ReturnToGroups() => ChangeSelection(null);
        private Image? _storageHighlight;
        private TerminalDragActionTarget? _drainAction, _useAction, _pinAction;
        private RectTransform? _actionBar, _actionBarBackground;
        private Image[] _actionBarParts = null!;
        private Ae2Image _actionBarArt = Ae2Image.VerticalButtonsBg;
        private TerminalDragAction _dragActionLayout;
        private IReadOnlyList<InventoryEntry> _entries = Array.Empty<InventoryEntry>();
        private (string Type, string Parent)? _selectionKey;
        private Rect _bounds, _windowBounds, _scrollTrackBounds;
        private Ae2TerminalLayout _layout = new Ae2TerminalLayout(1280, 720);
        private Rect BoardBounds => new Rect(_windowBounds.x + _layout.BoardLeft, _windowBounds.y,
            Ae2TerminalLayout.BoardWidth * _layout.Scale, _layout.Height);
        private readonly List<Image> _boardParts = new List<Image>();
        private Image[] _toolbarParts = null!;
        private RectTransform? _buttonBar;
        private bool _visible, _dirty = true, _prefabWarning, _byAmount, _descending, _thumbEnabled;
        private int _scrollRow, _buttonBarButtonCount, _buttonBarScale;
        private string _query = "";

        internal NativeFlatInventoryPanel(ManualLogSource log, Ae2TerminalSkin skin)
        {
            _log = log; _skin = skin; _loadout = new NativeLoadoutPanel(log, skin);
        }

        // The transparent chrome is a blocker, not an inventory control. In particular,
        // clicking the IMGUI search field must not release text focus to the game.
        internal bool Contains(GameObject target) => _panel && target &&
            (target.transform == _panel || target.transform.IsChildOf(_panel!));

        // Fixed cell roots own hits; the game's InvButton animates its visual transform independently.
        internal void ResolveItemHit(ref RaycastResult hit)
        {
            var tile = hit.gameObject.GetComponent<TilePointer>();
            if (tile && tile.Group.HasValue) return;
            var button = tile ? tile.ItemButton : hit.gameObject.GetComponent<LoadoutCell>()?.Button;
            TerminalHitSurface.Relay(ref hit, button);
        }

        internal bool ContainsScreenPoint(Vector2 screenPoint) => _panel != null && _root != null && _root.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(_panel, screenPoint, null);

        // Filter only the game's tooltip lookup, never the shared click/drag raycasts.
        internal bool IsNativeItemTooltip(GameObject target) => Contains(target) && !ContainsExtension(target) &&
            (target.GetComponent<InvButton>() || target.GetComponent<TilePointer>() || target.GetComponent<LoadoutCell>());

        internal void DrawItemTooltip() => _itemTooltip?.Draw();

        internal void UpdateTooltips(PlayerCamera camera, bool blocked)
        {
            if (_itemTooltip == null || _controlHint == null) return;
            if (blocked || !_root!.activeInHierarchy)
            {
                _controlHint.Hide();
                _itemTooltip.Hide();
                return;
            }

            var pointer = (Vector2)Input.mousePosition;
            var target = ContainsScreenPoint(pointer) ? TopHoverTarget(pointer) : null;
            // The track owns the thumb's hit area even when the slider cannot scroll.
            if (target && _slider && target == _slider!.gameObject)
            {
                var anchor = _scrollTrackBounds;
                anchor.position += _bounds.position;
                // Follow the pointer vertically instead of anchoring to the top of a long track.
                anchor.y = Screen.height - pointer.y;
                anchor.height = 0;
                _controlHint.Offer(anchor, TerminalText.Get("inventory.scroll") + "\n" + TerminalText.Get("inventory.scroll.description"));
            }
            if (_pinPager && target == _pinPager!.gameObject)
            {
                var rect = (RectTransform)_pinPager.transform;
                var anchor = new Rect(_bounds.x + rect.anchoredPosition.x, _bounds.y - rect.anchoredPosition.y,
                    rect.sizeDelta.x, rect.sizeDelta.y);
                _controlHint.Offer(anchor, TerminalText.Format("inventory.pinned.pages", _pins.Page + 1, _pins.PageCount));
            }
            // Resolve after chrome so the current hovered drag action takes precedence.
            OfferDragActionHint(pointer);
            if (!_controlHint.Present(_layout.Scale) && !InteractionInProgress() &&
                target != null && Contains(target) && !ContainsExtension(target))
            {
                var tile = target.GetComponent<TilePointer>();
                var loadout = target.GetComponent<LoadoutCell>();
                var button = tile ? tile.ItemButton : loadout && loadout.IsCurrent(camera) ? loadout.Button : null;
                if (button && button!.gameObject.activeInHierarchy && (!button.isBody ||
                    (button.slot < camera.body.slots.Length && camera.body.slots[button.slot])))
                {
                    // Body slots use the terminal's validated local body, never Together's redirected button body.
                    var item = button.isBody ? camera.body.GetItem(button.slot) : button.refItem;
                    if (item && GameInventoryAdapter.Owns(camera.body, item))
                    {
                        _itemTooltip.Show(item, tile ? tile.Count : 1, pointer, _layout.Scale);
                        return;
                    }
                }
            }
            _itemTooltip.Hide();
        }

        private GameObject? TopHoverTarget(Vector2 pointer)
        {
            var events = EventSystem.current;
            if (!events) return null;
            if (_hoverEvents != events)
            {
                _hoverEvents = events;
                _hoverPointer = new PointerEventData(events);
            }
            _hoverPointer!.position = pointer;
            events.RaycastAll(_hoverPointer, _hoverHits);
            // Empty cells, controls and extensions occlude anything below them.
            var target = _hoverHits.Count > 0 ? _hoverHits[0].gameObject : null;
            _hoverHits.Clear();
            return target;
        }

        internal int EquipmentCount()
        {
            _loadout.Discover();
            return _loadout.EquipmentCount;
        }

        internal void UpdateLoadoutDragHighlight(PlayerCamera camera, Item? source) =>
            _loadout.UpdateDragHighlight(camera, source);

        internal InvButton? ResolveDrop(PlayerCamera camera)
        {
            if (!_viewport || !RectTransformUtility.RectangleContainsScreenPoint(_viewport, Input.mousePosition) ||
                !camera.dragItem || Input.GetKey(KeyBinds.GetBind("expanddesc")) ||
                ContainerPlacement.Mode == ContainerPlacementMode.Manual ||
                Vector2.Distance(Input.mousePosition, camera.clickPos) < 10f) return null;
            var candidate = ContainerPlacement.Candidates(camera.body, camera.dragItem).FirstOrDefault();
            if (candidate == null) return null;
            // This synthetic hit takes the native container path even if its loadout row is scrolled away.
            if (!_automaticTarget)
            {
                var prefab = Resources.Load<GameObject>("ContainerItem");
                if (!prefab || !prefab.GetComponent<InvButton>()) return null;
                var host = Add(_panel!, "Automatic storage destination");
                host.SetActive(false);
                _automaticTarget = UnityEngine.Object.Instantiate(prefab, host.transform).GetComponent<InvButton>();
                _automaticTarget.slot = -1;
                _automaticTarget.centerButtons = false;
            }
            _automaticTarget!.refItem = candidate.Item;
            return _automaticTarget;
        }

        internal void SetEntries(IEnumerable<InventoryEntry> entries)
        {
            if (_query != SearchApi.Query)
            {
                _itemTooltip?.Hide();
                _query = SearchApi.Query;
                _scrollRow = 0;
                _selectionKey = null;
            }
            _entries = entries.Where(e => e.NativeItem is Item item && item).ToArray();
            _dirty = true;
            if (_visible) Synchronize();
        }

        // Bounds use IMGUI's top-left pixel coordinates; the overlay canvas uses bottom-left coordinates.
        internal void SetLayout(Rect windowBounds, Rect bounds, Ae2TerminalLayout layout, bool byAmount, bool descending)
        {
            var geometryChanged = _layout.Scale != layout.Scale || _layout.Rows != layout.Rows;
            var orderChanged = _byAmount != byAmount || _descending != descending;
            _layout = layout;
            FluidDialog?.SetScale(layout.Scale);
            _byAmount = byAmount;
            _descending = descending;
            if (orderChanged) { _scrollRow = 0; _dirty = true; }
            var moved = _bounds != bounds;
            _bounds = bounds;
            _windowBounds = windowBounds;
            if (!_panel) return;
            if (moved || geometryChanged)
            {
                Position(_panel!, _bounds);
                Position(_windowSurface!, _windowBounds);
                Position(_board!, BoardBounds);
                Position(_buttonBar!, new Rect(_windowBounds.position, Vector2.zero));
            }
            if (geometryChanged) ApplyLayout();
        }

        internal void BlockPointerInput(bool block)
        {
            if (!_windowSurface) return;
            // EventSystem clicks (groups, sliders, extensions) bypass the game's filtered raycasts.
            var surface = _windowSurface!;
            if (block) surface.SetAsLastSibling();
            else surface.SetAsFirstSibling();
        }

        internal void SetVisible(bool visible, bool settingsOpen = false)
        {
            if (!visible || settingsOpen) FluidDialog?.Reset();
            if (visible && settingsOpen)
            {
                // Suspend the canvas without clearing the current search, group or scroll position.
                Chrome?.ReleaseSearchFocus();
                _itemTooltip?.Hide();
                _controlHint?.Hide();
                HideDragActions();
                _loadout.ClearDragHighlight();
                if (_root && _root!.activeSelf) _root.SetActive(false);
                return;
            }
            _visible = visible;
            if (!visible)
            {
                _selectionKey = null;
                _scrollRow = 0;
                _dirty = true;
                _loadout.Reset();
                Chrome?.ReleaseSearchFocus();
                _itemTooltip?.Hide();
                _controlHint?.Hide();
                HideDragActions();
                _loadout.ClearDragHighlight();
                if (_automaticTarget) _automaticTarget!.refItem = null!;
            }
            var ready = visible && PlayerCamera.main && PlayerCamera.main.body;
            if (ready)
            {
                EnsureRoot();
                EnsureExtensionRoot();
                _root!.SetActive(true);
                Synchronize();
                _loadout.Refresh(PlayerCamera.main.body);
                UpdateStorageHighlight();
            }
            else if (_root) _root!.SetActive(false);
        }

        private void EnsureExtensionRoot()
        {
            if (_extensionRoot) return;
            _extensionRoot = Add(_panel!, "UIT.ExtensionRoot", typeof(RectMask2D)).GetComponent<RectTransform>();
            Stretch(_extensionRoot, Vector2.zero, Vector2.zero);
        }

        private void EnsureRoot()
        {
            if (_root) return;
            FluidDialog?.Dispose();
            FluidDialog = null;
            _root = new GameObject("UIT.NativeFlatInventory", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) _root.layer = uiLayer;
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = TerminalCanvasOrder.Inventory;

            // The toolbar is polled, not raycast. Shield the window without covering live item slots or search.
            var surface = Add(_root.transform, "Terminal window hit surface", typeof(Image));
            var surfaceImage = surface.GetComponent<Image>();
            surfaceImage.color = Color.clear;
            TerminalHitSurface.Enable(surfaceImage);
            _windowSurface = surface.GetComponent<RectTransform>();
            Position(_windowSurface, _windowBounds);

            // vertical_buttons_bg.png sits behind the left toolbar and overlaps the board's left edge, as in AE2,
            // where VerticalButtonBar draws it in the background layer after the board.
            _board = Add(_root.transform, "AE2 terminal board").GetComponent<RectTransform>();
            Position(_board, BoardBounds);
            _buttonBar = Add(_root.transform, "AE2 vertical button bar").GetComponent<RectTransform>();
            Position(_buttonBar, new Rect(_windowBounds.position, Vector2.zero));
            // AE2's sprite metadata slices it vertically: 2 top rows and 4 bottom rows stay fixed, the middle stretches.
            _toolbarParts = CreateBarParts(_buttonBar);
            ApplyBarArt(_toolbarParts, Ae2Image.VerticalButtonsBg, flushWithBoardTop: true);

            var panel = Add(_root.transform, "AE2 storage", typeof(Image), typeof(GridWheel));
            _panel = panel.GetComponent<RectTransform>();
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = Color.clear;
            TerminalHitSurface.Enable(panelImage);
            panel.GetComponent<GridWheel>().Owner = this;

            var pinRow = Add(panel.transform, "Pinned item row", typeof(Image), typeof(PinnedRowWheel));
            _pinnedRow = pinRow.GetComponent<RectTransform>();
            pinRow.GetComponent<PinnedRowWheel>().Owner = this;
            var rowArt = pinRow.GetComponent<Image>();
            _skin.Apply(rowArt, Ae2Image.PinnedRow, Ae2TerminalSkin.Slot);
            TerminalHitSurface.Enable(rowArt);
            _pinnedHighlight = Ae2TerminalSkin.CreateSlotHighlight(_pinnedRow);
            _pinnedGutter = CreateDecoration(panel.transform, "Pinned row gutter", Ae2Image.PinnedRowGutter);
            _grooveTop = CreateDecoration(panel.transform, "Storage groove cap", Ae2Image.ScrollGrooveTop);
            var pager = Add(panel.transform, "Next pinned page", typeof(Image), typeof(Button), typeof(PinnedRowWheel));
            var pagerFace = pager.GetComponent<Image>();
            _skin.Apply(pagerFace, Ae2Image.Search, Ae2TerminalSkin.Slot);
            pager.GetComponent<PinnedRowWheel>().Owner = this;
            _pinPager = pager.GetComponent<Button>();
            _pinPager.targetGraphic = pagerFace;
            _pinPager.navigation = new Navigation { mode = Navigation.Mode.None };
            _pinPager.onClick.AddListener(() => MovePinPage(1));
            _pinPageLabel = CreateText(pager.transform, "Pinned page number", 5, TextAlignmentOptions.Center);
            _pinPageLabel.color = Color.white;
            Stretch(_pinPageLabel.rectTransform, Vector2.zero, Vector2.zero);

            var viewport = Add(panel.transform, "Storage slot viewport", typeof(RectMask2D));
            _viewport = viewport.GetComponent<RectTransform>();
            _storageHighlight = Ae2TerminalSkin.CreateSlotHighlight(_viewport);
            var content = Add(viewport.transform, "Storage slots", typeof(GridLayoutGroup));
            _content = content.GetComponent<RectTransform>();
            _grid = content.GetComponent<GridLayoutGroup>();
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = Ae2TerminalLayout.Columns;

            var scroll = Add(panel.transform, "Storage scrollbar", typeof(Image), typeof(Slider));
            var track = scroll.GetComponent<Image>();
            track.color = Color.clear;
            TerminalHitSurface.Enable(track);
            _slider = scroll.GetComponent<Slider>();
            _slider.direction = Slider.Direction.TopToBottom;
            _slider.wholeNumbers = true;
            _slider.navigation = new Navigation { mode = Navigation.Mode.None };
            // Preserve the native hover/press transitions. The disabled sprite already has its own tint.
            _slider.transition = Selectable.Transition.ColorTint;
            var thumbColors = _slider.colors;
            thumbColors.disabledColor = Color.white;
            _slider.colors = thumbColors;
            var handleLane = Add(scroll.transform, "Thumb travel").GetComponent<RectTransform>();
            Stretch(handleLane, Vector2.zero, Vector2.zero);
            _thumb = Add(handleLane, "AE2 thumb", typeof(Image)).GetComponent<Image>();
            // The track owns both hover and slider input, even over the thumb.
            _thumb.raycastTarget = false;
            _thumb.rectTransform.anchorMin = new Vector2(0, 1);
            _thumb.rectTransform.anchorMax = Vector2.one;
            _slider.handleRect = _thumb.rectTransform;
            _slider.targetGraphic = _thumb;
            _slider.onValueChanged.AddListener(value =>
            {
                // The game's item-interact binding can be left mouse, which is also how this slider is dragged.
                if (PlayerCamera.main && PlayerCamera.main.dragItem) _slider.SetValueWithoutNotify(_scrollRow);
                else MoveToRow((int)value);
            });
            _skin.Apply(_thumb, Ae2Image.ThumbDisabled, Ae2TerminalSkin.Slot);

            _empty = CreateText(panel.transform, "Empty storage", 17, TextAlignmentOptions.Center);
            _lower = Add(panel.transform, "Container storage base").GetComponent<RectTransform>();
            _loadout.Attach(_lower);
            _actionBar = Add(_root.transform, "AE2 drag action controls").GetComponent<RectTransform>();
            // Both rails composite over the board and below the inventory cells.
            _actionBarBackground = Add(_buttonBar, "AE2 drag action rail").GetComponent<RectTransform>();
            _actionBarParts = CreateBarParts(_actionBarBackground);
            ApplyBarArt(_actionBarParts, _actionBarArt = Ae2Image.VerticalButtonsBg, flushWithBoardTop: false);
            _drainAction = CreateDragAction(TerminalDragAction.Drain, Ae2Image.DragDrain);
            _useAction = CreateDragAction(TerminalDragAction.Use, Ae2Image.DragUse);
            _pinAction = CreateDragAction(TerminalDragAction.Pin, Ae2Image.DragPin);
            HideDragActions();
            _itemTooltip = new TerminalItemTooltip(_log, _skin);
            _controlHint = new TerminalControlHint(_root.transform, _skin);
            Chrome = new NativeTerminalChrome(_root.transform, _skin, _controlHint);
            Position(_panel, _bounds);
            ApplyLayout();
            _dirty = true;
        }

        private void ApplyLayout()
        {
            var scale = _layout.Scale;
            const int pitch = Ae2TerminalLayout.Pitch;
            var stride = pitch * scale;
            var gridWidth = stride * Ae2TerminalLayout.Columns;
            const int inset = Ae2TerminalLayout.Inset;
            var storageTop = _hasPinnedRow ? stride : 0;
            var storageHeight = _layout.StorageRows(_hasPinnedRow) * stride;
            Position(_viewport!, new Rect(inset * scale, storageTop, gridWidth, storageHeight));
            Position(_pinnedRow!, new Rect(inset * scale, 0, gridWidth, stride));
            Ae2TerminalSkin.SizeSlotHighlight(_pinnedHighlight!, scale);
            Ae2TerminalSkin.SizeSlotHighlight(_storageHighlight!, scale);
            // Cover the unused upper groove while the first row is reserved for pins.
            Position(_pinnedGutter!.rectTransform, new Rect(Ae2TerminalLayout.ThumbLeft(Ae2TerminalLayout.DefaultThumbWidth) * scale,
                0, Ae2TerminalLayout.DefaultThumbWidth * scale, stride));
            Position(_grooveTop!.rectTransform, new Rect(Ae2TerminalLayout.GrooveLeft * scale, stride,
                Ae2TerminalLayout.GrooveWidth * scale, Ae2TerminalLayout.GrooveBorder * scale));
            Position((RectTransform)_pinPager!.transform, new Rect((inset + Ae2TerminalLayout.GridWidth + 2) * scale,
                3 * scale, (Ae2TerminalLayout.BoardWidth - inset - Ae2TerminalLayout.GridWidth - 5) * scale, 12 * scale));
            _pinPageLabel!.fontSize = 6 * scale;
            // The thumb keeps its sprite's pixel size and is centered on the groove drawn in terminal.png.
            var thumbSize = _skin.SourceSize(Ae2Image.Thumb) ??
                new Vector2Int(Ae2TerminalLayout.DefaultThumbWidth, Ae2TerminalLayout.DefaultThumbHeight);
            _scrollTrackBounds = new Rect(Ae2TerminalLayout.ThumbLeft(thumbSize.x) * scale,
                _layout.ScrollTrackTop(_hasPinnedPager) * scale, thumbSize.x * scale,
                _layout.ScrollTrackHeight(_hasPinnedPager) * scale);
            Position(_slider!.GetComponent<RectTransform>(), _scrollTrackBounds);
            var handle = _thumb!.rectTransform;
            handle.sizeDelta = new Vector2(0, thumbSize.y * scale);
            handle.anchoredPosition = Vector2.zero;
            var halfThumb = thumbSize.y * scale / 2f;
            // Slider drives the handle's center. Equal insets land the edges on both ends even for a 15px thumb.
            Stretch((RectTransform)handle.parent, new Vector2(0, halfThumb), new Vector2(0, -halfThumb));
            Position(_empty!.rectTransform, new Rect(inset * scale, storageTop, gridWidth, storageHeight));
            Position(_lower!, new Rect(0, _layout.GridHeight, _bounds.width, _layout.PanelHeight - _layout.GridHeight));
            _loadout.SetLayout(_layout);
            LayoutBoard();
            LayoutButtonBar();
            _grid!.cellSize = new Vector2(stride, stride);
            foreach (var tile in _tiles.Values)
            {
                if (!tile.Button) continue;
                SizeTile(tile);
                if (tile.PinColumn >= 0) PositionPinnedTile(tile);
            }
            UpdateScroll(_storageTileCount);
        }

        // AE2 MEStorageScreen.drawBG: header, first row, middle rows, last row, then the bottom part, each at its source size.
        private void LayoutBoard()
        {
            var parts = new List<Ae2Image> { Ae2Image.Header, Ae2Image.FirstRow };
            for (var i = 2; i < _layout.Rows; i++) parts.Add(Ae2Image.Row);
            parts.Add(Ae2Image.LastRow);
            parts.Add(Ae2Image.Bottom);
            var scale = _layout.Scale;
            var y = 0;
            for (var i = 0; i < parts.Count; i++)
            {
                if (i == _boardParts.Count)
                {
                    var image = Add(_board!, "AE2 board part", typeof(Image)).GetComponent<Image>();
                    image.raycastTarget = false;
                    _boardParts.Add(image);
                }
                var part = _boardParts[i];
                part.gameObject.SetActive(true);
                part.name = "AE2 board " + parts[i];
                // Transparent pixels in terminal.png stay transparent; a missing part is not filled in.
                _skin.Apply(part, parts[i], Color.clear);
                var height = Ae2ScreenStyle.BoardPartHeight(parts[i]);
                Position(part.rectTransform, new Rect(0, y * scale, Ae2TerminalLayout.BoardWidth * scale, height * scale));
                y += height;
            }
            for (var i = parts.Count; i < _boardParts.Count; i++) _boardParts[i].gameObject.SetActive(false);
        }

        // AE2 VerticalButtonBar.drawBackgroundLayer: the background spans every visible toolbar button.
        private void LayoutButtonBar()
        {
            var scale = _layout.Scale;
            var buttons = Ae2TerminalLayout.ToolbarButtons + (CanReturnToGroups ? 1 : 0);
            if (_buttonBarButtonCount == buttons && _buttonBarScale == scale) return;
            _buttonBarButtonCount = buttons;
            _buttonBarScale = scale;
            LayoutBarParts(_toolbarParts, Ae2TerminalLayout.ButtonBarX * scale, Ae2TerminalLayout.ButtonBarY * scale,
                Ae2TerminalLayout.ButtonBarHeight(buttons), scale, flushWithBoardTop: true);
        }

        private TerminalDragActionTarget CreateDragAction(TerminalDragAction action, Ae2Image sprite)
        {
            var node = Add(_actionBar!, action + " action", typeof(Image), typeof(TerminalDragActionTarget));
            var hit = node.GetComponent<Image>();
            hit.color = Color.clear;
            TerminalHitSurface.Enable(hit);
            var target = node.GetComponent<TerminalDragActionTarget>();
            target.Action = action;
            target.Face = Add(node.transform, "Face", typeof(Image)).GetComponent<Image>();
            target.Face.raycastTarget = false;
            target.Icon = Add(target.Face.transform, "Icon", typeof(Image)).GetComponent<Image>();
            target.Icon.raycastTarget = false;
            target.IconArt = sprite;
            return target;
        }

        private Image CreateDecoration(Transform parent, string name, Ae2Image art)
        {
            var image = Add(parent, name, typeof(Image)).GetComponent<Image>();
            _skin.Apply(image, art, Ae2TerminalSkin.Shell);
            image.raycastTarget = false;
            return image;
        }

        internal void HideDragActions()
        {
            if (_actionBar && _actionBar!.gameObject.activeSelf)
            {
                _actionBar.gameObject.SetActive(false);
                LayoutButtonBar();
            }
            if (_actionBarBackground && _actionBarBackground!.gameObject.activeSelf) _actionBarBackground.gameObject.SetActive(false);
        }

        internal void UpdateDragActions(PlayerCamera camera, Item? item, TerminalDragAction armed, TerminalDragAction originalLayout)
        {
            if (!_actionBar || !item || !_visible || !_root!.activeInHierarchy) { HideDragActions(); return; }
            var available = AvailableDragActions(camera, item!);
            var visible = armed == TerminalDragAction.None ? available : available & armed;
            if (visible == TerminalDragAction.None) { HideDragActions(); return; }

            // Keep the confirmed/armed button under the pointer when its siblings are hidden.
            _dragActionLayout = armed == TerminalDragAction.None ? available : originalLayout;
            var scale = _layout.Scale;
            var (x, y, width, height) = _layout.DragActionBackgroundBounds(_dragActionLayout, armed);
            Position(_actionBar!, new Rect(_windowBounds.x + x, _windowBounds.y + y, width, height));
            Position(_actionBarBackground!, new Rect(x, y, width, height));
            var art = Ae2ButtonBarAppearance.DragBackground(_layout, y, height);
            if (art != _actionBarArt)
            {
                _actionBarArt = art;
                ApplyBarArt(_actionBarParts, art, flushWithBoardTop: false);
            }
            LayoutBarParts(_actionBarParts, 0, 0, height / scale, scale, flushWithBoardTop: false);
            _actionBarBackground!.gameObject.SetActive(true);
            _pinAction!.IconArt = _pins.Contains(item!) ? Ae2Image.DragUnpin : Ae2Image.DragPin;
            SetAction(_drainAction!);
            SetAction(_useAction!);
            SetAction(_pinAction);
            if (!_actionBar!.gameObject.activeSelf)
            {
                _actionBar.gameObject.SetActive(true);
                LayoutButtonBar();
            }

            void SetAction(TerminalDragActionTarget target)
            {
                var active = (visible & target.Action) != 0;
                if (target.gameObject.activeSelf != active) target.gameObject.SetActive(active);
                if (!active) return;
                var (left, topEdge, faceWidth, faceHeight) = _layout.DragActionBounds(_dragActionLayout, target.Action);
                Position((RectTransform)target.transform, new Rect(left - x, topEdge - y, faceWidth, faceHeight));
                target.Draw(_skin, scale, HitsDragAction(_dragActionLayout, target.Action, Input.mousePosition), armed == target.Action);
            }
        }

        private void OfferDragActionHint(Vector2 pointer)
        {
            OfferIfHovered(_drainAction!);
            OfferIfHovered(_useAction!);
            OfferIfHovered(_pinAction!);

            void OfferIfHovered(TerminalDragActionTarget target)
            {
                if (!target.gameObject.activeInHierarchy || !HitsDragAction(_dragActionLayout, target.Action, pointer)) return;
                var (x, y, width, height) = _layout.DragActionBounds(_dragActionLayout, target.Action);
                var key = target.HintKey;
                _controlHint!.Offer(new Rect(_layout.Left + x, _layout.Top + y, width, height),
                    TerminalText.Get(key) + "\n" + TerminalText.Get(key + ".description"));
            }
        }

        internal static bool CanDrain(PlayerCamera camera, Item item) => camera.liquidDrainObject &&
            camera.tradeMenu && !camera.tradeMenu.activeSelf &&
            item.TryGetComponent<WaterContainerItem>(out var liquid) && liquid.CurrentTotal > 0f;

        internal static bool CanUse(PlayerCamera camera, Item item) => !camera.blockItemUsage && item.Stats.usable;

        internal bool TryGetDragAction(GameObject target, out TerminalDragAction action)
        {
            if (target && target.activeInHierarchy && target.TryGetComponent<TerminalDragActionTarget>(out var marker) &&
                (marker == _drainAction || marker == _useAction || marker == _pinAction))
            {
                action = marker.Action;
                return true;
            }
            action = default;
            return false;
        }

        private bool HitsDragAction(TerminalDragAction actions, TerminalDragAction action, Vector2 pointer) =>
            _layout.HitsDragAction(actions, action, pointer.x, Screen.height - pointer.y);

        internal void TogglePin(Body body, Item item)
        {
            _pins.SetOwner(body);
            _pins.Toggle(item);
            _dirty = true;
        }

        private void MovePinPage(int direction)
        {
            if (InteractionInProgress() || !_hasPinnedRow || _pins.PageCount <= 1) return;
            _pins.MovePage(direction);
            _dirty = true;
            Synchronize();
        }

        private static TerminalDragAction AvailableDragActions(PlayerCamera camera, Item item) =>
            (CanDrain(camera, item) ? TerminalDragAction.Drain : TerminalDragAction.None) |
            (CanUse(camera, item) ? TerminalDragAction.Use : TerminalDragAction.None) |
            (!string.IsNullOrEmpty(item.id) ? TerminalDragAction.Pin : TerminalDragAction.None);

        internal TerminalDragAction? DragActionAtPointer(PlayerCamera camera, Item item, out TerminalDragAction actionLayout,
            TerminalDragAction armed, TerminalDragAction originalLayout)
        {
            actionLayout = TerminalDragAction.None;
            if (!_actionBar || !_visible || !_root!.activeInHierarchy) return null;
            var available = AvailableDragActions(camera, item);
            var pointer = (Vector2)Input.mousePosition;
            actionLayout = armed == TerminalDragAction.None ? available : originalLayout;
            if (armed != TerminalDragAction.None)
                return (available & armed) != 0 && HitsDragAction(actionLayout, armed, pointer) ? armed : null;
            if (HitsDragAction(actionLayout, TerminalDragAction.Pin, pointer)) return TerminalDragAction.Pin;
            if (HitsDragAction(actionLayout, TerminalDragAction.Drain, pointer)) return TerminalDragAction.Drain;
            if (HitsDragAction(actionLayout, TerminalDragAction.Use, pointer)) return TerminalDragAction.Use;
            return null;
        }

        private static void LayoutBarParts(Image[] parts, int left, int top, int height, int scale, bool flushWithBoardTop)
        {
            for (var i = 0; i < parts.Length; i++)
            {
                var (y, width, rows) = Ae2TerminalLayout.ButtonBarPatch(height, i, flushWithBoardTop);
                Position(parts[i].rectTransform, new Rect(left, top + y * scale, width * scale, rows * scale));
            }
        }

        private static Image[] CreateBarParts(RectTransform parent)
        {
            var parts = new Image[Ae2TerminalLayout.ButtonBarPatchCount];
            for (var i = 0; i < parts.Length; i++)
            {
                var image = Add(parent, "Button bar part " + i, typeof(Image)).GetComponent<Image>();
                image.raycastTarget = false;
                image.type = Image.Type.Simple;
                parts[i] = image;
            }
            return parts;
        }

        private void ApplyBarArt(Image[] parts, Ae2Image art, bool flushWithBoardTop)
        {
            // An older or customized skin may not ship the bottom-joining sprite.
            var size = _skin.SourceSize(art);
            if (size == null && art == Ae2Image.VerticalButtonsBgBottom)
            {
                art = Ae2Image.VerticalButtonsBg;
                size = _skin.SourceSize(art);
            }
            for (var i = 0; i < parts.Length; i++)
            {
                var (y, width, rows) = Ae2TerminalLayout.ButtonBarPatch(size?.y ?? 0, i, flushWithBoardTop);
                var part = parts[i];
                part.sprite = size.HasValue ? _skin.GetRows(art, y, width, rows) : null;
                part.color = part.sprite ? Color.white : Color.clear;
            }
        }

        private void MoveToRow(int row)
        {
            _scrollRow = row;
            Position(_content!, new Rect(0, -row * Ae2TerminalLayout.Pitch * _layout.Scale,
                Ae2TerminalLayout.Columns * Ae2TerminalLayout.Pitch * _layout.Scale,
                Ae2TerminalLayout.ContentRows(_storageTileCount) * Ae2TerminalLayout.Pitch * _layout.Scale));
        }

        private void UpdateScroll(int count)
        {
            var max = Mathf.Max(0, Ae2TerminalLayout.ContentRows(count) - _layout.StorageRows(_hasPinnedRow));
            _slider!.maxValue = max;
            _slider.interactable = max > 0;
            if (_thumbEnabled != (max > 0))
            {
                _thumbEnabled = max > 0;
                _skin.Apply(_thumb!, _thumbEnabled ? Ae2Image.Thumb : Ae2Image.ThumbDisabled, Ae2TerminalSkin.Slot);
            }
            var row = Mathf.Clamp(_scrollRow, 0, max);
            _slider.SetValueWithoutNotify(row);
            MoveToRow(row);
        }

        private (string Type, string Parent) RowKey(InventoryRow row) =>
            (row.First.TypeId, "");

        private void ChangeSelection((string Type, string Parent)? key)
        {
            _selectionKey = key;
            _scrollRow = 0;
            _dirty = true;
            Synchronize();
        }

        private static bool InteractionInProgress() => PlayerCamera.main &&
            (PlayerCamera.main.dragItem || Input.GetKey(KeyBinds.GetBind("iteminteract")));

        private void Synchronize()
        {
            if (!_visible || !_dirty || !PlayerCamera.main || !PlayerCamera.main.body || InteractionInProgress()) return;
            EnsureRoot();
            var body = PlayerCamera.main.body;
            _pins.SetOwner(body);
            _pins.Prune(item => GameInventoryAdapter.Owns(body, item));
            var hasPinnedRow = _pins.PageCount > 0;
            var hasPinnedPager = _pins.PageCount > 1;
            _pinnedRow!.gameObject.SetActive(hasPinnedRow);
            _pinnedGutter!.gameObject.SetActive(hasPinnedPager);
            _grooveTop!.gameObject.SetActive(hasPinnedPager);
            _pinPager!.gameObject.SetActive(hasPinnedPager);
            if (hasPinnedPager) _pinPageLabel!.text = $"{_pins.Page + 1}/{_pins.PageCount}";
            if (_hasPinnedRow != hasPinnedRow || _hasPinnedPager != hasPinnedPager)
            {
                _hasPinnedRow = hasPinnedRow;
                _hasPinnedPager = hasPinnedPager;
                ApplyLayout();
            }
            var rows = InventoryProjection.Build(_entries.Where(e => e.NativeItem is Item item && item && !_pins.Contains(item)),
                InventoryViewMode.Merged, SearchApi.Matchers, "");
            IEnumerable<InventoryRow> ordered = _byAmount
                ? (_descending ? rows.OrderByDescending(row => row.Count) : rows.OrderBy(row => row.Count))
                : (_descending ? rows.Reverse() : rows);
            var prefab = Resources.Load<GameObject>("ContainerItem");
            if (!prefab && !_prefabWarning)
            {
                _log.LogWarning("Native ContainerItem prefab is unavailable; flat interaction is disabled.");
                _prefabWarning = true;
            }
            var liveKeys = new HashSet<(string Key, TileKind Kind)>();
            var selection = _selectionKey.HasValue ? rows.FirstOrDefault(row => RowKey(row) == _selectionKey.Value) : null;
            if (selection == null) _selectionKey = null;
            var index = 0;
            if (prefab && selection != null)
            {
                foreach (var entry in selection.Instances)
                    Bind(prefab, (entry.InstanceKey, TileKind.Instance), (Item)entry.NativeItem, null, 1, ref index, liveKeys);
            }
            else if (prefab)
            {
                foreach (var row in ordered)
                {
                    var key = RowKey(row);
                    Bind(prefab, (key.Type, TileKind.Group), (Item)row.First.NativeItem, row.Count > 1 ? key : null,
                        row.Count, ref index, liveKeys);
                }
            }
            _storageTileCount = index;
            if (prefab && hasPinnedRow)
            {
                index = 0;
                foreach (var item in _pins.VisibleItems())
                    Bind(prefab, (GameInventoryAdapter.Key(item), TileKind.Pinned), item, null, 1, ref index, liveKeys);
            }
            foreach (var key in _tiles.Keys.Where(key => !liveKeys.Contains(key)).ToArray())
            {
                DestroyTile(_tiles[key]);
                _tiles.Remove(key);
            }
            LayoutButtonBar();
            _empty!.text = TerminalText.Get(!prefab ? "inventory.unavailable" : rows.Count == 0 ? "inventory.empty" : "inventory.buttonError");
            _empty.gameObject.SetActive(_storageTileCount == 0);
            UpdateScroll(_storageTileCount);
            _dirty = false;
        }

        private void Bind(GameObject prefab, (string Key, TileKind Kind) key, Item item,
            (string Type, string Parent)? group, int count, ref int index,
            HashSet<(string Key, TileKind Kind)> liveKeys)
        {
            try
            {
                if (!_tiles.TryGetValue(key, out var tile) || !tile.Root || !tile.Button)
                {
                    if (tile != null) DestroyTile(tile);
                    tile = CreateTile(prefab, item, key.Kind == TileKind.Pinned ? _pinnedRow! : _content!);
                    _tiles[key] = tile;
                }
                tile.Button.refItem = item;
                tile.Button.UpdateGraphic();
                tile.Pointer.Group = group;
                tile.Pointer.Count = count;
                // Group clicks stay on the fixed cell; singletons relay actions to the native InvButton.
                tile.Count.text = count > 1 ? count.ToString(CultureInfo.InvariantCulture) : "";
                if (key.Kind == TileKind.Pinned)
                {
                    tile.PinColumn = index;
                    PositionPinnedTile(tile);
                }
                else tile.Root.transform.SetSiblingIndex(index);
                index++;
                liveKeys.Add(key);
            }
            catch (Exception ex) { _log.LogWarning("Native item tile failed for " + item.id + ": " + ex.Message); }
        }

        private Tile CreateTile(GameObject prefab, Item item, Transform parent)
        {
            var root = Add(parent, "Item " + item.id, typeof(Image), typeof(TilePointer));
            var hitSurface = root.GetComponent<Image>();
            hitSurface.color = Color.clear;
            TerminalHitSurface.Enable(hitSurface);
            var pointer = root.GetComponent<TilePointer>();
            pointer.Owner = this;
            try
            {
                var native = UnityEngine.Object.Instantiate(prefab, root.transform);
                var button = native.GetComponent<InvButton>();
                if (!button) throw new InvalidOperationException("ContainerItem has no InvButton.");
                button.slot = -1;
                button.centerButtons = false;
                button.woundView = false;
                button.refItem = item;
                button.UpdateRefs();
                pointer.ItemButton = button;
                // The native InvButton continues to own the item; only its frame comes from the AE2 board.
                var image = native.GetComponent<Image>();
                _skin.ApplyButtonFace(image);
                foreach (var child in native.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = root.layer;
                foreach (var graphic in native.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                var count = CreateText(root.transform, "Item count", 16, TextAlignmentOptions.BottomRight);
                count.color = Color.white;
                count.fontStyle = FontStyles.Bold;
                Stretch(count.rectTransform, new Vector2(1, 1), new Vector2(-3, -1));
                var tile = new Tile { Root = root, Button = button, Count = count, Pointer = pointer };
                SizeTile(tile);
                return tile;
            }
            catch
            {
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
                throw;
            }
        }

        private void SizeTile(Tile tile)
        {
            var scale = _layout.Scale;
            var pitch = Ae2TerminalLayout.Pitch * scale;
            var rect = tile.Button.GetComponent<RectTransform>();
            Stretch(rect, new Vector2(1, 1) * scale, new Vector2(-1, -1) * scale);
            tile.Button.maxImageSize = pitch - 6 * scale;
            tile.Count.fontSize = 5 * scale;
        }

        private void PositionPinnedTile(Tile tile)
        {
            var pitch = Ae2TerminalLayout.Pitch * _layout.Scale;
            Position((RectTransform)tile.Root.transform, new Rect(tile.PinColumn * pitch, 0, pitch, pitch));
        }

        // Use board coordinates rather than tile raycasts so empty cells highlight too, without gaining an item action.
        private void UpdateStorageHighlight()
        {
            HighlightRow(_viewport!, _storageHighlight!, _layout.StorageRows(_hasPinnedRow));
            if (_hasPinnedRow) HighlightRow(_pinnedRow!, _pinnedHighlight!, 1);
        }

        private void HighlightRow(RectTransform viewport, Image highlight, int rows)
        {
            var pointer = Input.mousePosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, pointer, null, out var local) ||
                !viewport.rect.Contains(local))
            {
                highlight.gameObject.SetActive(false);
                return;
            }
            var pitch = Ae2TerminalLayout.Pitch * _layout.Scale;
            var column = Mathf.Clamp(Mathf.FloorToInt(local.x / pitch), 0, Ae2TerminalLayout.Columns - 1);
            var row = Mathf.Clamp(Mathf.FloorToInt(-local.y / pitch), 0, rows - 1);
            Position(highlight.rectTransform, new Rect(column * pitch, row * pitch, pitch, pitch));
            highlight.gameObject.SetActive(true);
        }

        internal static GameObject Add(Transform parent, string name, params Type[] components)
        {
            var node = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            node.transform.SetParent(parent, false);
            foreach (var component in components) node.AddComponent(component);
            return node;
        }

        internal static void Position(RectTransform transform, Rect bounds)
        {
            transform.anchorMin = transform.anchorMax = transform.pivot = new Vector2(0, 1);
            transform.anchoredPosition = new Vector2(bounds.x, -bounds.y);
            transform.sizeDelta = bounds.size;
        }

        internal static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        internal static TextMeshProUGUI CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment)
        {
            var text = Add(parent, name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.font = PlayerCamera.main && PlayerCamera.main.containerText ? PlayerCamera.main.containerText.font : TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = Ae2TerminalSkin.Ink;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.richText = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void DestroyTile(Tile tile)
        {
            if (!tile.Root) return;
            tile.Root.SetActive(false);
            UnityEngine.Object.Destroy(tile.Root);
        }

        public void Dispose()
        {
            FluidDialog?.Dispose();
            FluidDialog = null;
            if (_root) { _root!.SetActive(false); UnityEngine.Object.Destroy(_root); }
            _root = null;
            Chrome = null;
            _itemTooltip = null;
            _controlHint = null;
            _hoverEvents = null;
            _hoverPointer = null;
            _tiles.Clear();
        }
    }
}
