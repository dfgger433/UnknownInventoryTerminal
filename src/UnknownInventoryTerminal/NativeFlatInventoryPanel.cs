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

        private sealed class TilePointer : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
        {
            internal NativeFlatInventoryPanel Owner = null!;
            internal (string Type, string Parent)? Group;
            internal Image Highlight = null!;
            public void OnPointerClick(PointerEventData e)
            {
                if (Group.HasValue && e.button == PointerEventData.InputButton.Left) Owner.ChangeSelection(Group);
            }
            public void OnPointerEnter(PointerEventData e) => Owner.Highlight(this);
            public void OnPointerExit(PointerEventData e) => OnDisable();
            private void OnDisable() { if (Owner?._hovered == this) Owner.Highlight(null); }
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
        private readonly List<(Image Cells, Image Rail)> _rowArt = new List<(Image, Image)>();
        private GameObject? _root;
        private RectTransform? _windowSurface, _panel, _viewport, _content, _lower, _extensionRoot;
        internal RectTransform? ExtensionRoot => _visible && _extensionRoot ? _extensionRoot : null;
        internal bool ContainsExtension(GameObject target) => _extensionRoot && target &&
            (target.transform == _extensionRoot || target.transform.IsChildOf(_extensionRoot!));
        private GridLayoutGroup? _grid;
        private Slider? _slider;
        private Image? _thumb;
        private InvButton? _automaticTarget;
        private TextMeshProUGUI? _empty, _backLabel;
        private UITooltip? _scrollTooltip, _backTooltip;
        private Button? _backButton;
        private TilePointer? _hovered;
        private IReadOnlyList<InventoryEntry> _entries = Array.Empty<InventoryEntry>();
        private (string Type, string Parent)? _selectionKey;
        private Rect _bounds, _windowBounds;
        private Ae2TerminalLayout _layout = new Ae2TerminalLayout(1280, 720);
        private bool _visible, _dirty = true, _prefabWarning, _byAmount, _descending, _thumbEnabled;
        private int _scrollRow;
        private string _query = "";

        internal NativeFlatInventoryPanel(ManualLogSource log, Ae2TerminalSkin skin)
        { _log = log; _skin = skin; _loadout = new NativeLoadoutPanel(log, skin); }

        // The transparent chrome is a blocker, not an inventory control. In particular,
        // clicking the IMGUI search field must not release text focus to the game.
        internal bool Contains(GameObject target) => _panel && target &&
            (target.transform == _panel || target.transform.IsChildOf(_panel!));

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
            if (_backTooltip)
            {
                _backTooltip!.tipName = TerminalText.Get("inventory.backTooltip");
                _backTooltip.tipDesc = TerminalText.Get("inventory.backTooltip.description");
            }
        }

        internal void SetEntries(IEnumerable<InventoryEntry> entries)
        {
            if (_query != SearchApi.Query)
            {
                _query = SearchApi.Query;
                _scrollRow = 0;
                _selectionKey = null;
                Highlight(null);
            }
            _entries = entries.Where(e => e.NativeItem is Item item && item).ToArray();
            _dirty = true;
            if (_visible) Synchronize();
        }

        // Bounds use IMGUI's top-left pixel coordinates; the overlay canvas uses bottom-left coordinates.
        internal void SetLayout(Rect windowBounds, Rect bounds, Ae2TerminalLayout layout, bool byAmount, bool descending)
        {
            var geometryChanged = _layout.Scale != layout.Scale || _layout.Rows != layout.Rows ||
                _layout.MainRows != layout.MainRows || _layout.EquipmentRows != layout.EquipmentRows;
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
            surface.GetComponent<Image>().color = Color.clear;
            _windowSurface = surface.GetComponent<RectTransform>();
            Position(_windowSurface, _windowBounds);

            var panel = Add(_root.transform, "AE2 storage", typeof(Image), typeof(GridWheel));
            _panel = panel.GetComponent<RectTransform>();
            panel.GetComponent<Image>().color = Ae2TerminalSkin.Shell;
            panel.GetComponent<GridWheel>().Owner = this;

            var viewport = Add(panel.transform, "Storage slot viewport", typeof(RectMask2D));
            _viewport = viewport.GetComponent<RectTransform>();
            var content = Add(viewport.transform, "Storage slots", typeof(GridLayoutGroup));
            _content = content.GetComponent<RectTransform>();
            _grid = content.GetComponent<GridLayoutGroup>();
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = Ae2TerminalLayout.Columns;

            var scroll = Add(panel.transform, "Storage scrollbar", typeof(Image), typeof(Slider));
            var track = scroll.GetComponent<Image>();
            track.color = Color.clear;
            _scrollTooltip = CreateTooltip(scroll, TerminalText.Get("inventory.scroll"), TerminalText.Get("inventory.scroll.description"));
            _slider = scroll.GetComponent<Slider>();
            _slider.direction = Slider.Direction.TopToBottom;
            _slider.wholeNumbers = true;
            _slider.navigation = new Navigation { mode = Navigation.Mode.None };
            _slider.transition = Selectable.Transition.None;
            var handleLane = Add(scroll.transform, "Thumb travel").GetComponent<RectTransform>();
            handleLane.anchorMin = Vector2.zero;
            handleLane.anchorMax = Vector2.one;
            _thumb = Add(handleLane, "AE2 thumb", typeof(Image)).GetComponent<Image>();
            _slider.handleRect = _thumb.rectTransform;
            _slider.targetGraphic = _thumb;
            _slider.onValueChanged.AddListener(value =>
            {
                // The game's item-interact binding can be left mouse, which is also how this slider is dragged.
                if (PlayerCamera.main && PlayerCamera.main.dragItem) _slider.SetValueWithoutNotify(_scrollRow);
                else MoveToRow((int)value);
            });
            _skin.Apply(_thumb, Ae2TerminalSkin.DisabledThumb, 0, 0, 12, 15, Ae2TerminalSkin.Slot);

            _empty = CreateText(panel.transform, "Empty storage", 17, TextAlignmentOptions.Center);
            _lower = Add(panel.transform, "Container storage base", typeof(Image)).GetComponent<RectTransform>();
            _skin.Apply(_lower.GetComponent<Image>(), Ae2TerminalSkin.Terminal, 0, 71, 195, 5, Ae2TerminalSkin.Shell);
            var back = Add(_lower, "Return to groups", typeof(Image), typeof(Button));
            _backTooltip = CreateTooltip(back, TerminalText.Get("inventory.backTooltip"), TerminalText.Get("inventory.backTooltip.description"));
            _backButton = back.GetComponent<Button>();

            // Set up background image to use Ae2TerminalSkin.States atlas
            var backImage = back.GetComponent<Image>();
            _skin.Apply(backImage, Ae2TerminalSkin.States, 176, 128, 18, 20, Ae2TerminalSkin.Slot);

            // Configure sprite swap for hover
            var spriteState = new SpriteState();
            _skin.Apply(backImage, Ae2TerminalSkin.States, 212, 128, 18, 20, Ae2TerminalSkin.Slot);
            spriteState.highlightedSprite = backImage.sprite;
            _skin.Apply(backImage, Ae2TerminalSkin.States, 176, 128, 18, 20, Ae2TerminalSkin.Slot);

            _backButton.transition = Selectable.Transition.SpriteSwap;
            _backButton.spriteState = spriteState;
            _backButton.onClick.AddListener(() => ChangeSelection(null));
            _backLabel = CreateText(back.transform, "Selection label", 13, TextAlignmentOptions.MidlineLeft);
            Stretch(_backLabel.rectTransform, new Vector2(6, 0), new Vector2(-6, 0));
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
            for (var i = 0; i < _layout.Rows; ++i)
            {
                if (i == _rowArt.Count)
                {
                    var cells = Add(_panel!, "Six AE2 slots", typeof(Image)).GetComponent<Image>();
                    var rail = Add(_panel!, "AE2 scrollbar track", typeof(Image)).GetComponent<Image>();
                    cells.raycastTarget = rail.raycastTarget = false;
                    cells.transform.SetAsFirstSibling();
                    rail.transform.SetAsFirstSibling();
                    _rowArt.Add((cells, rail));
                }
                var row = _rowArt[i];
                var sourceY = i == 0 ? 17 : i == _layout.Rows - 1 ? 53 : 35;
                const int split = Ae2TerminalLayout.Inset + Ae2TerminalLayout.GridWidth;
                const int tail = Ae2TerminalLayout.BoardWidth - split;
                _skin.Apply(row.Cells, Ae2TerminalSkin.Terminal, 0, sourceY, split, pitch, Ae2TerminalSkin.Slot);
                _skin.Apply(row.Rail, Ae2TerminalSkin.Terminal, split + Ae2TerminalLayout.AtlasRemovedWidth,
                    sourceY, tail, pitch, Ae2TerminalSkin.Shell);
                row.Cells.gameObject.SetActive(true);
                row.Rail.gameObject.SetActive(true);
                Position(row.Cells.rectTransform, new Rect(0, i * stride, split * scale, stride));
                Position(row.Rail.rectTransform, new Rect(split * scale, i * stride, tail * scale, stride));
            }
            for (var i = _layout.Rows; i < _rowArt.Count; ++i)
            {
                _rowArt[i].Cells.gameObject.SetActive(false);
                _rowArt[i].Rail.gameObject.SetActive(false);
            }
            Position(_viewport!, new Rect(7 * scale, 0, gridWidth, _layout.GridHeight));
            Position(_slider!.GetComponent<RectTransform>(), new Rect(Ae2TerminalLayout.ScrollbarLeft * scale, 0, 12 * scale, _layout.GridHeight));
            _thumb!.rectTransform.sizeDelta = new Vector2(0, 15 * scale);
            var travel = (RectTransform)_thumb.transform.parent;
            travel.offsetMin = new Vector2(0, 7 * scale);
            travel.offsetMax = new Vector2(0, -8 * scale);
            Position(_empty!.rectTransform, new Rect(7 * scale, 0, gridWidth, _layout.GridHeight));
            Position(_lower!, new Rect(0, _layout.GridHeight, _bounds.width, _layout.PanelHeight - _layout.GridHeight));
            _loadout.SetLayout(_layout);
            _backLabel!.fontSize = 7 * scale;
            // The back button is managed by Position, but should be stretched to fill its container if we want it to be responsive.
            // Based on the requirement, the button should stretch its middle part.
            // Using a simple Position currently defines its size.
            // If I change it to use anchors it might work.
            // Actually, the requirement says: "按钮伽宽峕四角 咊边框丕变，祇延展中部"
            // This suggests using a 9-slice or just stretching the middle part.
            // Since it's a UI button, if I set the anchors, it will stretch.
            // Let's set the anchors to be more flexible, but Position() in this codebase sets anchors to (0,1).
            // Maybe I should keep Position() for the initial layout but use a layout group or something?
            // No, the codebase uses manual positioning.
            // I will try to use the existing Stretch method, but I need to make sure the anchors are correct.
            // The existing Position method:
            // transform.anchorMin = transform.anchorMax = transform.pivot = new Vector2(0, 1);
            // This pins the button.
            // If I want it to stretch, I should use different anchors.
            // But Position() overrides them.
            // I'll keep the Position() call but add a Stretch() call for the internal image?
            // No, the back button *is* the image.
            Position(_backButton!.GetComponent<RectTransform>(), new Rect(7 * scale, 2 * scale, 41 * scale, 18 * scale));
            _grid!.cellSize = new Vector2(stride, stride);
            foreach (var tile in _tiles.Values) if (tile.Button) SizeTile(tile);
            UpdateScroll(_tiles.Count);
        }

        private void MoveToRow(int row)
        {
            if (row != _scrollRow) Highlight(null);
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
                _skin.Apply(_thumb!, _thumbEnabled ? Ae2TerminalSkin.Thumb : Ae2TerminalSkin.DisabledThumb,
                    0, 0, 12, 15, Ae2TerminalSkin.Slot);
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
            Highlight(null);
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
            _backButton!.gameObject.SetActive(selection != null);
            if (selection != null) _backLabel!.text = TerminalText.Get("inventory.back");
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
                // Reapply on every bind: inventory refresh can change a group to a singleton and vice versa.
                tile.Root.GetComponent<Image>().raycastTarget = group.HasValue;
                var image = tile.Button.GetComponent<Image>();
                image.raycastTarget = !group.HasValue;
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
            root.GetComponent<Image>().color = Color.clear;
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
                // Keep the exact InvButton raycast target, but let the AE2 texture provide its visible frame.
                var image = native.GetComponent<Image>();
                _skin.ApplyButtonFace(image);
                foreach (var child in native.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = root.layer;
                foreach (var graphic in native.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                var highlight = Add(root.transform, "Slot hover", typeof(Image)).GetComponent<Image>();
                highlight.raycastTarget = false;
                highlight.color = new Color(1, 1, 1, 0.23f);
                highlight.enabled = false;
                Stretch(highlight.rectTransform, new Vector2(1, 1), new Vector2(-1, -1));
                pointer.Highlight = highlight;
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

        private void Highlight(TilePointer? next)
        {
            if (_hovered && _hovered!.Highlight) _hovered.Highlight.enabled = false;
            _hovered = next;
            if (next && next!.Highlight) next.Highlight.enabled = true;
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
