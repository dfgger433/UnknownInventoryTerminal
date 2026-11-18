using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal
{
    internal enum NativeInteraction { ContextMenu, Drag }

    // IMGUI owns the search/chrome; the reserved content area belongs to native UGUI buttons.
    internal sealed class TerminalWindow
    {
        private readonly Func<IReadOnlyList<InventoryEntry>> _capture;
        private readonly Action<IReadOnlyList<InventoryEntry>> _publishNativeEntries;
        private readonly Action<Rect, InventoryViewMode>? _configureNativePanel;
        private readonly Action<Exception> _report;
        private IReadOnlyList<InventoryEntry> _snapshot = Array.Empty<InventoryEntry>();
        private IReadOnlyList<InventoryRow> _rows = Array.Empty<InventoryRow>();
        private Rect _rect = new Rect(70, 60, 900, 600);
        private string _lastQuery = "", _status = "";
        private InventoryViewMode _lastMode;
        private float _nextRefresh;
        private bool _closeRequested;
        private GUIStyle? _windowStyle;
        internal bool Visible { get; private set; }
        internal InventoryViewMode Mode { get; set; }
        internal IReadOnlyList<InventoryEntry> Snapshot => _snapshot;
        internal IReadOnlyList<InventoryRow> Rows => _rows;
        internal string Status => _status;
        internal string ProjectedQuery => _lastQuery;
        internal event Action? StateChanged;

        internal TerminalWindow(Func<IReadOnlyList<InventoryEntry>> capture,
            Action<IReadOnlyList<InventoryEntry>> publishNativeEntries,
            Action<Exception> report,
            Action<Rect, InventoryViewMode>? configureNativePanel = null)
        {
            _capture = capture;
            _publishNativeEntries = publishNativeEntries;
            _report = report;
            _configureNativePanel = configureNativePanel;
        }

        internal void Open() { Visible = true; _closeRequested = false; FitToScreen(); Refresh(); StateChanged?.Invoke(); }
        internal void Close()
        {
            Visible = false;
            _snapshot = Array.Empty<InventoryEntry>();
            _rows = Array.Empty<InventoryRow>();
            _publishNativeEntries(Array.Empty<InventoryEntry>());
            StateChanged?.Invoke();
        }
        internal void Update()
        {
            if (!Visible) return;
            if (_closeRequested) { Close(); return; }
            if (Time.unscaledTime >= _nextRefresh) Refresh();
            if (_lastQuery != SearchApi.Query || _lastMode != Mode) Rebuild();
        }
        internal void RefreshNow() => Refresh();

        private void Refresh()
        {
            _nextRefresh = Time.unscaledTime + 0.5f;
            try
            {
                _snapshot = _capture();
                Rebuild();
            }
            catch (Exception ex)
            {
                _snapshot = Array.Empty<InventoryEntry>();
                _rows = Array.Empty<InventoryRow>();
                _publishNativeEntries(Array.Empty<InventoryEntry>());
                _lastQuery = SearchApi.Query;
                _lastMode = Mode;
                _status = "Inventory could not be read. Check BepInEx log, then refresh.";
                _report(ex);
                StateChanged?.Invoke();
            }
        }
        private void Rebuild()
        {
            _lastQuery = SearchApi.Query;
            _lastMode = Mode;
            _rows = InventoryProjection.Build(_snapshot, Mode, SearchApi.Matchers, SearchApi.Query);
            ConfigureNativePanel();
            // Filtering happens once, before grouping. Counts and native representatives use these same instances.
            var matching = _rows.SelectMany(row => row.Instances).ToArray();
            _publishNativeEntries(matching);
            _status = matching.Length + " / " + _snapshot.Count + " items  |  " + _rows.Count + " matching groups";
            StateChanged?.Invoke();
        }
        private void FitToScreen()
        {
            _rect.width = Mathf.Min(900, Mathf.Max(1, Screen.width - 16));
            _rect.height = Mathf.Min(600, Mathf.Max(1, Screen.height - 16));
            _rect.x = Mathf.Clamp(_rect.x, 0, Mathf.Max(0, Screen.width - _rect.width));
            _rect.y = Mathf.Clamp(_rect.y, 0, Mathf.Max(0, Screen.height - _rect.height));
        }
        private void ConfigureNativePanel() => _configureNativePanel?.Invoke(
            new Rect(_rect.x + 14, _rect.y + 104, Mathf.Max(1, _rect.width - 28), Mathf.Max(1, _rect.height - 174)), Mode);

        internal void Draw()
        {
            if (!Visible) return;
            FitToScreen();
            if (_windowStyle == null)
            {
                _windowStyle = new GUIStyle(GUI.skin.window);
                foreach (var state in new[] { _windowStyle.normal, _windowStyle.hover, _windowStyle.active, _windowStyle.focused,
                    _windowStyle.onNormal, _windowStyle.onHover, _windowStyle.onActive, _windowStyle.onFocused }) state.background = null;
            }
            // Leave the native content transparent even on renderers that draw IMGUI after overlay canvases.
            _rect = GUI.Window(0x554954, _rect, DrawContents, "", _windowStyle);
            ConfigureNativePanel();
            var current = Event.current;
            if (_rect.Contains(current.mousePosition) && (current.isMouse || current.type == EventType.ScrollWheel)) current.Use();
        }
        private void DrawContents(int id)
        {
            GUI.Box(new Rect(0, 0, _rect.width, 102), "");
            GUI.Box(new Rect(0, _rect.height - 68, _rect.width, 68), "");
            GUI.Label(new Rect(14, 2, _rect.width - 28, 22), "Inventory terminal");
            GUI.Label(new Rect(14, 34, 48, 24), "Search");
            GUI.SetNextControlName("UIT.Search");
            SearchApi.Query = GUI.TextField(new Rect(65, 30, Mathf.Max(1, _rect.width - 271), 28), SearchApi.Query, 256);
            if (GUI.Button(new Rect(_rect.width - 200, 30, 50, 28), "Clear")) SearchApi.Query = "";
            if (GUI.Button(new Rect(_rect.width - 144, 30, 70, 28), "Refresh")) Refresh();
            if (GUI.Button(new Rect(_rect.width - 68, 30, 54, 28), "Close")) _closeRequested = true;
            Mode = GUI.Toolbar(new Rect(14, 68, Mathf.Min(320, _rect.width - 28), 26), (int)Mode,
                new[] { "All items (merged)", "By container" }) == 0 ? InventoryViewMode.Merged : InventoryViewMode.ByContainer;
            if (_lastQuery != SearchApi.Query || _lastMode != Mode) Rebuild();
            GUI.Label(new Rect(14, _rect.height - 64, _rect.width - 28, 22), _status);
            GUI.Label(new Rect(14, _rect.height - 42, _rect.width - 28, 34),
                "Use the original item controls on a tile. Each tile acts on one real item; its count is matching instances.");
            GUI.DragWindow(new Rect(0, 0, _rect.width, 22));
        }
    }
}
