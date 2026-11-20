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
            internal UITooltip GroupTooltip = null!;
        }

        private sealed class TilePointer : MonoBehaviour, IPointerClickHandler
        {
            internal NativeFlatInventoryPanel Owner = null!;
            internal (string Type, string Parent)? Group;
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

        private readonly ManualLogSource _log;
        private readonly Ae2TerminalSkin _skin;
        private readonly NativeLoadoutPanel _loadout;
        private readonly Dictionary<(string Key, string Parent, bool Picker), Tile> _tiles = new Dictionary<(string, string, bool), Tile>();
        private GameObject? _root;
        private RectTransform? _windowSurface, _board, _panel, _viewport, _content, _lower, _extensionRoot;
        internal RectTransform? ExtensionRoot => _visible && _extensionRoot ? _extensionRoot : null;
        internal bool ContainsExtension(GameObject target) => _extensionRoot && target &&
            (target.transform == _extensionRoot || target.transform.IsChildOf(_extensionRoot!));
        private GridLayoutGroup? _grid;
        private Slider? _slider;
        private Image? _thumb;
        private InvButton? _automaticTarget;
        private TextMeshProUGUI? _empty;
        private UITooltip? _scrollTooltip;
        internal bool CanReturnToGroups => _visible && _selectionKey.HasValue;
        internal void ReturnToGroups() => ChangeSelection(null);
        private Image? _storageHighlight;
        private IReadOnlyList<InventoryEntry> _entries = Array.Empty<InventoryEntry>();
        private (string Type, string Parent)? _selectionKey;
        private Rect _bounds, _windowBounds;
        private Ae2TerminalLayout _layout = new Ae2TerminalLayout(1280, 720);
        private Rect BoardBounds => new Rect(_windowBounds.x + _layout.BoardLeft, _windowBounds.y,
            Ae2TerminalLayout.BoardWidth * _layout.Scale, _layout.Height);
        private readonly List<Image> _boardParts = new List<Image>();
        private Image? _barTop, _barMiddle, _barBottom;
        private RectTransform? _buttonBar;
        private bool _visible, _dirty = true, _prefabWarning, _byAmount, _descending, _thumbEnabled;
        private int _scrollRow, _buttonBarButtonCount, _buttonBarScale;
        private string _query = "";

        internal NativeFlatInventoryPanel(ManualLogSource log, Ae2TerminalSkin skin)
        { _log = log; _skin = skin; _loadout = new NativeLoadoutPanel(log, skin); }

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

        internal int EquipmentCount()
        {
            _loadout.Discover();
            return _loadout.EquipmentCount;
        }

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

        private void RefreshTooltips()
        {
            if (_scrollTooltip)
            {
                _scrollTooltip!.tipName = TerminalText.Get("inventory.scroll");
                _scrollTooltip.tipDesc = TerminalText.Get("inventory.scroll.description");
            }
        }

        internal void SetEntries(IEnumerable<InventoryEntry> entries)
        {
            if (_query != SearchApi.Query)
            {
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

        internal void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible)
            {
                _selectionKey = null;
                _scrollRow = 0;
                _dirty = true;
                _loadout.Reset();
                if (_automaticTarget) _automaticTarget!.refItem = null!;
            }
            var ready = visible && PlayerCamera.main && PlayerCamera.main.body;
            if (ready)
            {
                EnsureRoot();
                EnsureExtensionRoot();
                _root!.SetActive(true);
                RefreshTooltips();
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
            _root = new GameObject("UIT.NativeFlatInventory", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) _root.layer = uiLayer;
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;

            // IMGUI controls do not own an EventSystem raycast. Cover only their window,
            // below the real tiles, so a native slot underneath a toolbar button cannot fire.
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
            var barSize = _skin.SourceSize(Ae2Image.VerticalButtonsBg);
            _barTop = BarPart("top", 0, Ae2TerminalLayout.ButtonBarTopBorder);
            _barMiddle = BarPart("middle", Ae2TerminalLayout.ButtonBarTopBorder, (barSize?.y ?? 0) -
                Ae2TerminalLayout.ButtonBarTopBorder - Ae2TerminalLayout.ButtonBarBottomBorder);
            _barBottom = BarPart("bottom", (barSize?.y ?? 0) - Ae2TerminalLayout.ButtonBarBottomBorder, Ae2TerminalLayout.ButtonBarBottomBorder);

            var panel = Add(_root.transform, "AE2 storage", typeof(Image), typeof(GridWheel));
            _panel = panel.GetComponent<RectTransform>();
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = Color.clear;
            TerminalHitSurface.Enable(panelImage);
            panel.GetComponent<GridWheel>().Owner = this;

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
            _scrollTooltip = CreateTooltip(scroll, TerminalText.Get("inventory.scroll"), TerminalText.Get("inventory.scroll.description"));
            _slider = scroll.GetComponent<Slider>();
            _slider.direction = Slider.Direction.TopToBottom;
            _slider.wholeNumbers = true;
            _slider.navigation = new Navigation { mode = Navigation.Mode.None };
            _slider.transition = Selectable.Transition.None;
            var handleLane = Add(scroll.transform, "Thumb travel").GetComponent<RectTransform>();
            Stretch(handleLane, Vector2.zero, Vector2.zero);
            _thumb = Add(handleLane, "AE2 thumb", typeof(Image)).GetComponent<Image>();
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
            Position(_viewport!, new Rect(inset * scale, 0, gridWidth, _layout.GridHeight));
            Ae2TerminalSkin.SizeSlotHighlight(_storageHighlight!, scale);
            // The thumb keeps its sprite's pixel size and is centered on the groove drawn in terminal.png.
            var thumbSize = _skin.SourceSize(Ae2Image.Thumb) ??
                new Vector2Int(Ae2TerminalLayout.DefaultThumbWidth, Ae2TerminalLayout.DefaultThumbHeight);
            Position(_slider!.GetComponent<RectTransform>(), new Rect(Ae2TerminalLayout.ThumbLeft(thumbSize.x) * scale,
                Ae2TerminalLayout.GrooveBorder * scale, thumbSize.x * scale, _layout.ScrollTrackHeight * scale));
            var handle = _thumb!.rectTransform;
            handle.sizeDelta = new Vector2(0, thumbSize.y * scale);
            handle.anchoredPosition = Vector2.zero;
            var halfThumb = thumbSize.y * scale / 2f;
            // Slider drives the handle's center. Equal insets land the edges on both ends even for a 15px thumb.
            Stretch((RectTransform)handle.parent, new Vector2(0, halfThumb), new Vector2(0, -halfThumb));
            Position(_empty!.rectTransform, new Rect(inset * scale, 0, gridWidth, _layout.GridHeight));
            Position(_lower!, new Rect(0, _layout.GridHeight, _bounds.width, _layout.PanelHeight - _layout.GridHeight));
            _loadout.SetLayout(_layout);
            LayoutBoard();
            LayoutButtonBar();
            _grid!.cellSize = new Vector2(stride, stride);
            foreach (var tile in _tiles.Values) if (tile.Button) SizeTile(tile);
            UpdateScroll(_tiles.Count);
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
            var height = Ae2TerminalLayout.ButtonBarHeight(buttons);
            const int top = Ae2TerminalLayout.ButtonBarTopBorder, bottom = Ae2TerminalLayout.ButtonBarBottomBorder;
            void Place(Image image, int y, int h) => Position(image.rectTransform, new Rect(Ae2TerminalLayout.ButtonBarX * scale,
                (Ae2TerminalLayout.ButtonBarY + y) * scale, Ae2TerminalLayout.ButtonBarWidth * scale, h * scale));
            Place(_barTop!, 0, top);
            Place(_barMiddle!, top, height - top - bottom);
            Place(_barBottom!, height - bottom, bottom);
        }

        private Image BarPart(string name, int top, int height)
        {
            var image = Add(_buttonBar!, "Button bar " + name, typeof(Image)).GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = _skin.GetRows(Ae2Image.VerticalButtonsBg, top, height);
            image.type = Image.Type.Simple;
            // Without the PNG the toolbar keeps its buttons and simply has no bar behind them.
            image.color = image.sprite ? Color.white : Color.clear;
            return image;
        }

        private void MoveToRow(int row)
        {
            _scrollRow = row;
            Position(_content!, new Rect(0, -row * Ae2TerminalLayout.Pitch * _layout.Scale,
                Ae2TerminalLayout.Columns * Ae2TerminalLayout.Pitch * _layout.Scale,
                Ae2TerminalLayout.ContentRows(_tiles.Count) * Ae2TerminalLayout.Pitch * _layout.Scale));
        }

        private void UpdateScroll(int count)
        {
            var max = Mathf.Max(0, Ae2TerminalLayout.ContentRows(count) - _layout.Rows);
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
            var rows = InventoryProjection.Build(_entries.Where(e => e.NativeItem is Item item && item), InventoryViewMode.Merged, SearchApi.Matchers, "");
            IEnumerable<InventoryRow> ordered = _byAmount
                ? (_descending ? rows.OrderByDescending(row => row.Count) : rows.OrderBy(row => row.Count))
                : (_descending ? rows.Reverse() : rows);
            var prefab = Resources.Load<GameObject>("ContainerItem");
            if (!prefab && !_prefabWarning)
            {
                _log.LogWarning("Native ContainerItem prefab is unavailable; flat interaction is disabled.");
                _prefabWarning = true;
            }
            var liveKeys = new HashSet<(string Key, string Parent, bool Picker)>();
            var selection = _selectionKey.HasValue ? rows.FirstOrDefault(row => RowKey(row) == _selectionKey.Value) : null;
            if (selection == null) _selectionKey = null;
            var index = 0;
            if (prefab && selection != null)
            {
                foreach (var entry in selection.Instances)
                    Bind(prefab, (entry.InstanceKey, "", true), entry, null, 1, ref index, liveKeys);
            }
            else if (prefab)
            {
                foreach (var row in ordered)
                {
                    var key = RowKey(row);
                    Bind(prefab, (key.Type, key.Parent, false), row.First, row.Count > 1 ? key : null,
                        row.Count, ref index, liveKeys);
                }
            }
            foreach (var key in _tiles.Keys.Where(key => !liveKeys.Contains(key)).ToArray())
            {
                DestroyTile(_tiles[key]);
                _tiles.Remove(key);
            }
            LayoutButtonBar();
            _empty!.text = TerminalText.Get(!prefab ? "inventory.unavailable" : rows.Count == 0 ? "inventory.empty" : "inventory.buttonError");
            _empty.gameObject.SetActive(_tiles.Count == 0);
            UpdateScroll(_tiles.Count);
            _dirty = false;
        }

        private void Bind(GameObject prefab, (string Key, string Parent, bool Picker) key, InventoryEntry entry,
            (string Type, string Parent)? group, int count, ref int index,
            HashSet<(string Key, string Parent, bool Picker)> liveKeys)
        {
            try
            {
                if (!_tiles.TryGetValue(key, out var tile) || !tile.Root || !tile.Button)
                {
                    if (tile != null) DestroyTile(tile);
                    tile = CreateTile(prefab, entry);
                    _tiles[key] = tile;
                }
                tile.Button.refItem = (Item)entry.NativeItem;
                tile.Button.UpdateGraphic();
                tile.Pointer.Group = group;
                // Keep all storage cells on a fixed, non-animated hit surface. Group clicks are handled on the cell;
                // singletons are mapped back to their native InvButton by the raycast filter.
                tile.Count.text = count > 1 ? count.ToString(CultureInfo.InvariantCulture) : "";
                // A group must stay a selection control, not become a native item action target.
                // Copy the representative item's tooltip onto the object the game raycasts.
                if (group.HasValue)
                {
                    var tooltip = tile.Button.GetComponent<UITooltip>();
                    if (tooltip)
                    {
                        tile.GroupTooltip.tipName = tooltip.tipName;
                        tile.GroupTooltip.tipDesc = tooltip.tipDesc;
                    }
                }
                tile.Root.transform.SetSiblingIndex(index++);
                liveKeys.Add(key);
            }
            catch (Exception ex) { _log.LogWarning("Native item tile failed for " + entry.TypeId + ": " + ex.Message); }
        }

        private Tile CreateTile(GameObject prefab, InventoryEntry entry)
        {
            var root = Add(_content!, "Item " + entry.TypeId, typeof(Image), typeof(TilePointer));
            var hitSurface = root.GetComponent<Image>();
            hitSurface.color = Color.clear;
            TerminalHitSurface.Enable(hitSurface);
            var pointer = root.GetComponent<TilePointer>();
            pointer.Owner = this;
            var tooltip = CreateTooltip(root, "", "");
            try
            {
                var native = UnityEngine.Object.Instantiate(prefab, root.transform);
                var button = native.GetComponent<InvButton>();
                if (!button) throw new InvalidOperationException("ContainerItem has no InvButton.");
                button.slot = -1;
                button.centerButtons = false;
                button.woundView = false;
                button.refItem = (Item)entry.NativeItem;
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
                var tile = new Tile { Root = root, Button = button, Count = count, Pointer = pointer, GroupTooltip = tooltip };
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

        // Use board coordinates rather than tile raycasts so empty cells highlight too, without gaining an item action.
        private void UpdateStorageHighlight()
        {
            var viewport = _viewport!;
            var highlight = _storageHighlight!;
            var pointer = Input.mousePosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, pointer, null, out var local) ||
                !viewport.rect.Contains(local))
            {
                highlight.gameObject.SetActive(false);
                return;
            }
            var pitch = Ae2TerminalLayout.Pitch * _layout.Scale;
            var column = Mathf.Clamp(Mathf.FloorToInt(local.x / pitch), 0, Ae2TerminalLayout.Columns - 1);
            var row = Mathf.Clamp(Mathf.FloorToInt(-local.y / pitch), 0, _layout.Rows - 1);
            Position(highlight.rectTransform, new Rect(column * pitch, row * pitch, pitch, pitch));
            highlight.gameObject.SetActive(true);
        }

        private static UITooltip CreateTooltip(GameObject target, string name, string description)
        {
            var tooltip = target.AddComponent<UITooltip>();
            tooltip.skipLocale = true;
            tooltip.tipName = name;
            tooltip.tipDesc = description;
            return tooltip;
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
            if (_root) { _root!.SetActive(false); UnityEngine.Object.Destroy(_root); }
            _root = null;
            _tiles.Clear();
        }
    }
}
