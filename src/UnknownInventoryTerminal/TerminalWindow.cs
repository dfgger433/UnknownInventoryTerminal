using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal
{
    internal enum NativeInteraction { ContextMenu, Drag }

    // A deliberately small stage-1 inspection frontend; native game UI owns item actions.
    internal sealed class TerminalWindow
    {
        private readonly Func<IReadOnlyList<InventoryEntry>> _capture;
        private readonly Action<InventoryEntry, NativeInteraction> _interact;
        private readonly Action<Exception> _report;
        private IReadOnlyList<InventoryEntry> _snapshot = Array.Empty<InventoryEntry>();
        private IReadOnlyList<InventoryRow> _rows = Array.Empty<InventoryRow>();
        private Rect _rect = new Rect(70, 60, 900, 600);
        private Vector2 _scroll, _instancesScroll;
        private string _selectedType = "", _selectedContainer = "", _selectedInstance = "";
        private string _lastQuery = "", _status = "";
        private InventoryViewMode _lastMode;
        private float _nextRefresh;
        private bool _closeRequested;
        internal bool Visible { get; private set; }
        internal InventoryViewMode Mode { get; set; }

        internal TerminalWindow(Func<IReadOnlyList<InventoryEntry>> capture,
            Action<InventoryEntry, NativeInteraction> interact, Action<Exception> report)
        { _capture = capture; _interact = interact; _report = report; }

        internal void Open() { Visible = true; _closeRequested = false; Refresh(); }
        internal void Close() { Visible = false; _snapshot = Array.Empty<InventoryEntry>(); _rows = Array.Empty<InventoryRow>(); }
        internal void Update()
        {
            if (!Visible) return;
            if (_closeRequested) { Close(); return; }
            if (Time.unscaledTime >= _nextRefresh) Refresh();
            if (_lastQuery != SearchApi.Query || _lastMode != Mode) Rebuild();
        }
        private void Refresh()
        {
            _nextRefresh = Time.unscaledTime + 0.5f;
            try
            {
                _snapshot = _capture();
                Rebuild();
                _status = _snapshot.Count + " item instances / " + _rows.Count + " matching rows";
                if (!_snapshot.Any(e => e.InstanceKey == _selectedInstance)) _selectedInstance = "";
            }
            catch (Exception ex)
            {
                _snapshot = Array.Empty<InventoryEntry>();
                _rows = Array.Empty<InventoryRow>();
                _selectedInstance = "";
                _status = "Inventory could not be read. Check BepInEx log, then refresh.";
                _report(ex);
            }
        }
        private void Rebuild()
        {
            _lastQuery = SearchApi.Query; _lastMode = Mode;
            _rows = InventoryProjection.Build(_snapshot, Mode, SearchApi.Matchers, SearchApi.Query);
        }
        internal void Draw()
        {
            if (!Visible) return;
            _rect.width = Mathf.Min(900, Screen.width - 16);
            _rect.height = Mathf.Min(600, Screen.height - 16);
            _rect.x = Mathf.Clamp(_rect.x, 0, Mathf.Max(0, Screen.width - _rect.width));
            _rect.y = Mathf.Clamp(_rect.y, 0, Mathf.Max(0, Screen.height - _rect.height));
            _rect = GUI.Window(0x554954, _rect, DrawContents, "Inventory terminal - stage 1");
            // Block IMGUI events only over this window; gameplay input is gated separately.
            var current = Event.current;
            if (_rect.Contains(current.mousePosition) && (current.isMouse || current.type == EventType.ScrollWheel)) current.Use();
        }
        private void DrawContents(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(48));
            GUI.SetNextControlName("UIT.Search");
            SearchApi.Query = GUILayout.TextField(SearchApi.Query, 256);
            if (GUILayout.Button("Clear", GUILayout.Width(50))) SearchApi.Query = "";
            if (GUILayout.Button("Refresh", GUILayout.Width(65))) Refresh();
            if (GUILayout.Button("Close", GUILayout.Width(55))) _closeRequested = true;
            GUILayout.EndHorizontal();
            Mode = GUILayout.Toolbar((int)Mode, new[] { "All items (merged)", "By container" }) == 0
                ? InventoryViewMode.Merged : InventoryViewMode.ByContainer;
            GUILayout.BeginHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.Width(_rect.width * 0.52f), GUILayout.Height(_rect.height - 135));
            string previousContainer = "";
            foreach (var row in _rows)
            {
                var entry = row.First;
                if (Mode == InventoryViewMode.ByContainer && previousContainer != entry.ContainerKey)
                { GUILayout.Label(entry.ContainerName, GUI.skin.box); previousContainer = entry.ContainerKey; }
                GUILayout.BeginHorizontal();
                DrawIcon(entry, 30);
                if (GUILayout.Button(entry.DisplayName + "  x" + row.Count + "\n" + entry.TypeId, GUILayout.Height(38)))
                {
                    _selectedType = entry.TypeId;
                    _selectedContainer = Mode == InventoryViewMode.ByContainer ? entry.ContainerKey : "";
                    _selectedInstance = "";
                }
                GUILayout.EndHorizontal();
            }
            if (_rows.Count == 0) GUILayout.Label("No matching items. Clear search or enter a game.");
            GUILayout.EndScrollView();
            DrawInstances();
            GUILayout.EndHorizontal();
            GUILayout.Label(_status);
            GUILayout.Label("Choose a real instance to use the original menu or drag it.");
            GUI.DragWindow(new Rect(0, 0, _rect.width, 20));
        }
        private void DrawInstances()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            var row = _rows.FirstOrDefault(r => r.First.TypeId == _selectedType &&
                (Mode == InventoryViewMode.Merged || r.First.ContainerKey == _selectedContainer));
            if (row == null) { GUILayout.Label("Select an item type on the left."); GUILayout.EndVertical(); return; }
            GUILayout.Label(row.First.DisplayName + " - " + row.Count + " instances");
            _instancesScroll = GUILayout.BeginScrollView(_instancesScroll, GUILayout.Height(_rect.height - 235));
            foreach (var entry in row.Instances)
            {
                if (GUILayout.Toggle(_selectedInstance == entry.InstanceKey,
                    entry.ContainerName + "\ninstance " + entry.InstanceKey, GUI.skin.button)) _selectedInstance = entry.InstanceKey;
            }
            GUILayout.EndScrollView();
            var selected = row.Instances.FirstOrDefault(e => e.InstanceKey == _selectedInstance);
            GUI.enabled = selected != null;
            if (GUILayout.Button("Original item menu") && selected != null) _interact(selected, NativeInteraction.ContextMenu);
            if (GUILayout.Button("Drag with original controls") && selected != null) _interact(selected, NativeInteraction.Drag);
            GUI.enabled = true;
            GUILayout.EndVertical();
        }
        private static void DrawIcon(InventoryEntry entry, float size)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (entry.Icon is Sprite sprite && sprite && sprite.texture)
            {
                try
                {
                    var textureRect = sprite.textureRect;
                    GUI.DrawTextureWithTexCoords(rect, sprite.texture, new Rect(textureRect.x / sprite.texture.width,
                        textureRect.y / sprite.texture.height, textureRect.width / sprite.texture.width, textureRect.height / sprite.texture.height));
                }
                catch (InvalidOperationException) { /* Packed sprite has no rectangular UV region. */ }
            }
            else if (entry.Icon is Texture texture && texture) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
        }
    }
}
