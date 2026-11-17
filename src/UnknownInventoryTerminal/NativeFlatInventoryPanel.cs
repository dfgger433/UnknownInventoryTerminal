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
            internal readonly GameObject Root;
            internal readonly InvButton Button;
            internal readonly TextMeshProUGUI Count, Caption;
            internal string InstanceKey;
            internal Tile(GameObject root, InvButton button, TextMeshProUGUI count, TextMeshProUGUI caption, string instanceKey)
            { Root = root; Button = button; Count = count; Caption = caption; InstanceKey = instanceKey; }
        }

        // A grouped tile is a normal UI hit target, not an item action. Its click opens
        // the instance picker; picker tiles use the original InvButton path instead.
        private sealed class GroupClickTarget : MonoBehaviour, IPointerClickHandler
        {
            internal NativeFlatInventoryPanel Owner = null!;
            internal (string Type, string Parent) Key = (string.Empty, string.Empty);
            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left) Owner.OpenSelection(Key);
            }
        }

        private readonly ManualLogSource _log;
        private readonly Dictionary<(string Type, string Parent), Tile> _tiles = new Dictionary<(string, string), Tile>();
        private GameObject? _root;
        private RectTransform? _panel, _content;
        private GridLayoutGroup? _grid;
        private ScrollRect? _scroll;
        private TextMeshProUGUI? _empty, _backLabel;
        private Button? _backButton;
        private IReadOnlyList<InventoryEntry> _entries = Array.Empty<InventoryEntry>();
        private (string Type, string Parent)? _selectionKey;
        private Rect _bounds = new Rect(84, 164, 872, 426);
        private InventoryViewMode _mode;
        private bool _visible, _dirty = true, _prefabWarning;

        private readonly ContainerPlacementPanel _placement;

        internal NativeFlatInventoryPanel(ManualLogSource log) { _log = log; _placement = new ContainerPlacementPanel(log); }

        internal bool Contains(GameObject target) => _root && target &&
            (target.transform == _root!.transform || target.transform.IsChildOf(_root.transform));

        internal void SetEntries(IEnumerable<InventoryEntry> entries)
        {
            _entries = entries?.Where(e => e.NativeItem is Item item && item)
                .GroupBy(e => e.InstanceKey, StringComparer.Ordinal).Select(g => g.First()).ToArray()
                ?? Array.Empty<InventoryEntry>();
            _dirty = true;
            if (_visible) Synchronize();
        }

        // Bounds use IMGUI's top-left pixel coordinates; the overlay canvas uses bottom-left coordinates.
        internal void SetLayout(Rect bounds, InventoryViewMode mode)
        {
            _bounds = bounds;
            if (_mode != mode) { _mode = mode; _dirty = true; }
            ApplyLayout();
        }

        internal void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible) _selectionKey = null;
            var ready = visible && PlayerCamera.main && PlayerCamera.main.body;
            if (ready)
            {
                EnsureRoot();
                ApplyLayout();
                Synchronize();
                _placement.Refresh();
            }
            if (_root) _root!.SetActive(ready);
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
            // The shield is below us; native menus are promoted above us by the input bridge.
            canvas.sortingOrder = short.MaxValue - 1;

            var panel = new GameObject("Aggregated inventory", typeof(RectTransform), typeof(Image));
            panel.layer = _root.layer;
            panel.transform.SetParent(_root.transform, false);
            _panel = panel.GetComponent<RectTransform>();
            _panel.anchorMin = _panel.anchorMax = new Vector2(0, 1);
            _panel.pivot = new Vector2(0, 1);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.16f, 0.98f);

            var back = new GameObject("Back to item groups", typeof(RectTransform), typeof(Image), typeof(Button));
            back.layer = panel.layer;
            back.transform.SetParent(panel.transform, false);
            var backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0, 1);
            backRect.anchorMax = new Vector2(0, 1);
            backRect.pivot = new Vector2(0, 1);
            backRect.anchoredPosition = new Vector2(8, -8);
            backRect.sizeDelta = new Vector2(260, 30);
            back.GetComponent<Image>().color = new Color(0.24f, 0.29f, 0.34f, 1);
            _backButton = back.GetComponent<Button>();
            _backButton.onClick.AddListener(CloseSelection);
            _backLabel = CreateText(back.transform, "Back label", 14, TextAlignmentOptions.Center);
            _backLabel.rectTransform.anchorMin = Vector2.zero;
            _backLabel.rectTransform.anchorMax = Vector2.one;
            _backLabel.rectTransform.offsetMin = Vector2.zero;
            _backLabel.rectTransform.offsetMax = Vector2.zero;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.layer = panel.layer;
            viewport.transform.SetParent(panel.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(6, 6);
            // Reserve fixed space so starting a drag cannot move the source tiles.
            viewportRect.offsetMax = new Vector2(-6, -120);
            viewport.GetComponent<Image>().color = Color.clear;

            var content = new GameObject("Native item groups", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.layer = viewport.layer;
            content.transform.SetParent(viewport.transform, false);
            _content = content.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1);
            _content.sizeDelta = Vector2.zero;
            _content.anchoredPosition = Vector2.zero;
            _grid = content.GetComponent<GridLayoutGroup>();
            _grid.cellSize = new Vector2(88, 104);
            _grid.spacing = new Vector2(6, 6);
            _grid.padding = new RectOffset(4, 4, 4, 4);
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = viewport.GetComponent<ScrollRect>();
            _scroll.viewport = viewportRect;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.inertia = false;
            _scroll.scrollSensitivity = 30;
            _scroll.movementType = ScrollRect.MovementType.Clamped;

            _empty = CreateText(panel.transform, "Empty inventory", 18, TextAlignmentOptions.Center);
            _empty.rectTransform.anchorMin = Vector2.zero;
            _empty.rectTransform.anchorMax = Vector2.one;
            _empty.rectTransform.offsetMin = new Vector2(12, 12);
            _empty.rectTransform.offsetMax = new Vector2(-12, -120);
            _placement.Attach(panel.transform);
            _dirty = true;
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (!_panel || !_grid) return;
            _panel!.anchoredPosition = new Vector2(_bounds.x, -_bounds.y);
            _panel.sizeDelta = _bounds.size;
            _grid!.constraintCount = Mathf.Max(1, Mathf.FloorToInt((_bounds.width - 14) / 94));
        }

        private (string Type, string Parent) RowKey(InventoryRow row) =>
            (row.First.TypeId, _mode == InventoryViewMode.ByContainer ? row.First.ContainerKey : "");

        private void OpenSelection((string Type, string Parent) key)
        {
            _selectionKey = key;
            _dirty = true;
            Synchronize();
        }

        private void CloseSelection()
        {
            _selectionKey = null;
            _dirty = true;
            Synchronize();
        }

        private static bool InteractionInProgress() => PlayerCamera.main &&
            (PlayerCamera.main.dragItem || Input.GetKey(KeyBinds.GetBind("iteminteract")));

        private void Synchronize()
        {
            if (!_visible || !_dirty || !PlayerCamera.main || !PlayerCamera.main.body || InteractionInProgress()) return;
            EnsureRoot();
            if (!_content || !_empty) return;
            var rows = InventoryProjection.Build(_entries.Where(e => e.NativeItem is Item item && item),
                _mode, SearchApi.Matchers, "");
            var prefab = Resources.Load<GameObject>("ContainerItem");
            if (!prefab)
            {
                if (!_prefabWarning) _log.LogWarning("Native ContainerItem prefab is unavailable; flat interaction is disabled.");
                _prefabWarning = true;
                _empty!.text = "Native item buttons are unavailable. Check the BepInEx log, then refresh.";
                _empty.gameObject.SetActive(true);
                return;
            }
            var liveKeys = new HashSet<(string Type, string Parent)>();
            var selection = _selectionKey.HasValue
                ? rows.FirstOrDefault(row => RowKey(row) == _selectionKey.Value) : null;
            if (_selectionKey.HasValue && selection == null) _selectionKey = null;
            if (_selectionKey.HasValue && selection != null)
            {
                _backButton!.gameObject.SetActive(true);
                _backLabel!.text = "Back  |  " + selection.First.DisplayName + " (" + selection.Count.ToString(CultureInfo.InvariantCulture) + ")";
                int index = 0;
                foreach (var entry in selection.Instances)
                {
                    var key = (Type: entry.InstanceKey, Parent: "selection");
                    liveKeys.Add(key);
                    try
                    {
                        if (!_tiles.TryGetValue(key, out var tile) || !tile.Root || !tile.Button)
                        {
                            if (tile != null && tile.Root) DestroyTile(tile);
                            tile = CreateTile(prefab, entry, false);
                            _tiles[key] = tile;
                        }
                        tile.Button.refItem = (Item)entry.NativeItem;
                        tile.Button.slot = -1;
                        tile.Button.UpdateRefs();
                        tile.Button.UpdateGraphic();
                        tile.InstanceKey = entry.InstanceKey;
                        tile.Root.transform.SetSiblingIndex(index++);
                        tile.Count.text = "1";
                        tile.Caption.text = entry.DisplayName + "\n" + entry.ContainerName;
                    }
                    catch (Exception ex) { _log.LogWarning("Native item instance creation failed: " + ex.Message); }
                }
            }
            else
            {
                _backButton!.gameObject.SetActive(false);
                int index = 0;
                foreach (var row in rows)
                {
                    var key = RowKey(row);
                    liveKeys.Add(key);
                    try
                    {
                        if (!_tiles.TryGetValue(key, out var tile) || !tile.Root || !tile.Button)
                        {
                            if (tile != null && tile.Root) DestroyTile(tile);
                            tile = CreateTile(prefab, row.First, row.Count > 1, key);
                            _tiles[key] = tile;
                        }
                        var representative = row.Instances.FirstOrDefault(e => e.InstanceKey == tile.InstanceKey &&
                            ReferenceEquals(e.NativeItem, tile.Button.refItem)) ?? row.First;
                        tile.Button.refItem = (Item)representative.NativeItem;
                        tile.Button.slot = -1;
                        tile.Button.UpdateRefs();
                        tile.Button.UpdateGraphic();
                        tile.InstanceKey = representative.InstanceKey;
                        tile.Root.transform.SetSiblingIndex(index++);
                        tile.Count.text = row.Count.ToString(CultureInfo.InvariantCulture);
                        tile.Caption.text = representative.DisplayName + (_mode == InventoryViewMode.ByContainer ? "\n" + representative.ContainerName : "");
                    }
                    catch (Exception ex) { _log.LogWarning("Native item group creation failed for " + row.First.TypeId + ": " + ex.Message); }
                }
            }
            foreach (var key in _tiles.Keys.Where(key => !liveKeys.Contains(key)).ToArray())
            {
                DestroyTile(_tiles[key]);
                _tiles.Remove(key);
            }
            _empty!.text = rows.Count == 0 ? "No matching items. Clear search or enter a game." :
                "Native item buttons could not be created. Check the BepInEx log, then refresh.";
            _empty.gameObject.SetActive(_tiles.Count == 0);
            _dirty = false;
        }

        private Tile CreateTile(GameObject prefab, InventoryEntry entry, bool grouped,
            (string Type, string Parent)? groupKey = null)
        {
            var root = new GameObject((grouped ? "Item group " : "Item ") + entry.TypeId,
                typeof(RectTransform), typeof(Image));
            root.layer = _root!.layer;
            root.transform.SetParent(_content, false);
            root.GetComponent<Image>().color = new Color(0.20f, 0.23f, 0.26f, 1);
            root.GetComponent<Image>().raycastTarget = grouped;
            if (grouped && groupKey.HasValue)
            {
                var target = root.AddComponent<GroupClickTarget>();
                target.Owner = this;
                target.Key = groupKey.Value;
            }
            try
            {
                var native = UnityEngine.Object.Instantiate(prefab, root.transform);
                native.name = "Native item " + entry.InstanceKey;
                var button = native.GetComponent<InvButton>();
                if (!button) throw new InvalidOperationException("ContainerItem has no InvButton.");
                // slot >= 0 makes GetItem() ignore refItem and read a body equipment slot instead.
                button.slot = -1;
                button.centerButtons = false;
                button.woundView = false;
                button.refItem = (Item)entry.NativeItem;
                button.maxImageSize = 54;
                button.UpdateRefs();
                button.UpdateGraphic();
                var rect = native.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(0, -4);
                rect.sizeDelta = new Vector2(68, 68);
                // PlayerCamera looks for InvButton on the exact raycast hit, not on its parent.
                foreach (var child in native.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = root.layer;
                foreach (var graphic in native.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = !grouped && graphic.gameObject == native;

                var count = CreateText(native.transform, "Matching count", 19, TextAlignmentOptions.BottomRight);
                count.rectTransform.anchorMin = Vector2.zero;
                count.rectTransform.anchorMax = Vector2.one;
                count.rectTransform.offsetMin = new Vector2(2, 1);
                count.rectTransform.offsetMax = new Vector2(-3, -1);
                count.fontStyle = FontStyles.Bold;
                count.color = new Color(0.73f, 0.93f, 1, 1);
                count.enableWordWrapping = false;
                var caption = CreateText(root.transform, "Item name and container", 11, TextAlignmentOptions.Top);
                caption.rectTransform.anchorMin = Vector2.zero;
                caption.rectTransform.anchorMax = new Vector2(1, 0);
                caption.rectTransform.pivot = new Vector2(0.5f, 0);
                caption.rectTransform.offsetMin = new Vector2(3, 2);
                caption.rectTransform.offsetMax = new Vector2(-3, 31);
                caption.overflowMode = TextOverflowModes.Ellipsis;
                return new Tile(root, button, count, caption, entry.InstanceKey);
            }
            catch
            {
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
                throw;
            }
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.layer = parent.gameObject.layer;
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = PlayerCamera.main && PlayerCamera.main.containerText ? PlayerCamera.main.containerText.font : TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = new Color(0.90f, 0.93f, 0.95f, 1);
            text.alignment = alignment;
            text.raycastTarget = false;
            text.richText = false;
            return text;
        }

        private static void DestroyTile(Tile tile)
        {
            if (!tile.Root) return;
            // Destroy is deferred until frame end; deactivate first to stop stale raycast hits immediately.
            tile.Root.SetActive(false);
            UnityEngine.Object.Destroy(tile.Root);
        }

        public void Dispose()
        {
            _placement.Dispose();
            if (_root) { _root!.SetActive(false); UnityEngine.Object.Destroy(_root); }
            _root = null;
            _panel = null;
            _content = null;
            _grid = null;
            _scroll = null;
            _empty = null;
            _tiles.Clear();
            _entries = Array.Empty<InventoryEntry>();
            _visible = false;
            _dirty = true;
        }
    }
}
