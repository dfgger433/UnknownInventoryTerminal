using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal
{
    // IMGUI owns the chrome; the storage area is reserved for the native UGUI canvas.
    internal sealed class TerminalWindow
    {
        private const string SearchControl = "UIT.Search";
        private readonly Ae2TerminalSkin _skin;
        private readonly Func<IReadOnlyList<InventoryEntry>> _capture;
        private readonly Func<int> _equipmentCount;
        private readonly Action<IReadOnlyList<InventoryEntry>> _publishNativeEntries;
        private readonly Action<Rect, Rect, Ae2TerminalLayout, bool, bool> _configureNativePanel;
        private readonly Action<Exception> _report;
        private IReadOnlyList<InventoryEntry> _snapshot = Array.Empty<InventoryEntry>();
        private IReadOnlyList<InventoryRow> _rows = Array.Empty<InventoryRow>();
        private Ae2TerminalLayout _layout;
        private Rect _rect;
        private string _lastQuery = "", _status = "";
        private float _nextRefresh;
        private bool _closeRequested, _focusSearch, _sortByAmount, _descending;
        private GUIStyle? _windowStyle, _titleStyle, _statusStyle, _searchStyle, _tooltipStyle, _iconStyle;
        private int _styleScale;
        internal bool Visible { get; private set; }
        internal bool CloseRequested => _closeRequested;
        internal IReadOnlyList<InventoryEntry> Snapshot => _snapshot;
        internal IReadOnlyList<InventoryRow> Rows => _rows;
        internal bool HasSnapshot { get; private set; }
        internal Rect ScreenBounds => new Rect(_rect.x, Screen.height - _rect.yMax, _rect.width, _rect.height);
        internal bool ContainsScreenPoint(Vector2 point) => Visible && ScreenBounds.Contains(point);

        internal TerminalWindow(Ae2TerminalSkin skin, Func<IReadOnlyList<InventoryEntry>> capture, Func<int> equipmentCount,
            Action<IReadOnlyList<InventoryEntry>> publishNativeEntries, Action<Exception> report,
            Action<Rect, Rect, Ae2TerminalLayout, bool, bool> configureNativePanel)
        {
            _skin = skin;
            _capture = capture;
            _equipmentCount = equipmentCount;
            _publishNativeEntries = publishNativeEntries;
            _report = report;
            _configureNativePanel = configureNativePanel;
        }

        internal void Open() { Visible = true; _closeRequested = false; FitToScreen(); Refresh(); }
        internal void Close()
        {
            Visible = false;
            _focusSearch = false;
            _snapshot = Array.Empty<InventoryEntry>();
            _rows = Array.Empty<InventoryRow>();
            _publishNativeEntries(Array.Empty<InventoryEntry>());
            HasSnapshot = false;
        }

        internal void Update()
        {
            if (!Visible) return;
            if (_closeRequested) return;
            FitToScreen();
            ConfigureNativePanel();
            if (Time.unscaledTime >= _nextRefresh) Refresh();
            else if (HasSnapshot && _lastQuery != SearchApi.Query) Rebuild();
        }

        private void Refresh()
        {
            _nextRefresh = Time.unscaledTime + 0.5f;
            try
            {
                _snapshot = _capture();
                Rebuild();
                HasSnapshot = true;
            }
            catch (Exception ex)
            {
                HasSnapshot = false;
                _snapshot = Array.Empty<InventoryEntry>();
                _rows = Array.Empty<InventoryRow>();
                _publishNativeEntries(Array.Empty<InventoryEntry>());
                _lastQuery = SearchApi.Query;
                _status = TerminalText.Get("terminal.unavailable");
                _report(ex);
            }
        }

        private void Rebuild()
        {
            _lastQuery = SearchApi.Query;
            _rows = InventoryProjection.Build(_snapshot, InventoryViewMode.Merged, SearchApi.Matchers, _lastQuery);
            ConfigureNativePanel();
            _publishNativeEntries(_rows.SelectMany(row => row.Instances).ToArray());
            _status = TerminalText.Format("terminal.status", _rows.Sum(row => row.Count), _snapshot.Count, _rows.Count);
        }

        private void FitToScreen()
        {
            var camera = PlayerCamera.main;
            var mainSlots = camera && camera.body ? camera.body.slots.Length : Ae2TerminalLayout.Columns;
            _layout = new Ae2TerminalLayout(Screen.width, Screen.height, mainSlots, _equipmentCount());
            _rect = new Rect(_layout.Left, _layout.Top, _layout.Width, _layout.Height);
        }

        private void ConfigureNativePanel() => _configureNativePanel(_rect,
            new Rect(_rect.x + _layout.BoardLeft, _rect.y + Ae2TerminalLayout.Header * _layout.Scale,
                Ae2TerminalLayout.BoardWidth * _layout.Scale, _layout.PanelHeight),
            _layout, _sortByAmount, _descending);

        internal void Draw()
        {
            if (!Visible) return;
            EnsureStyles();
            _tooltip = null;
            GUI.Window(0x554954, _rect, DrawContents, GUIContent.none, _windowStyle);
            if (_tooltip != null)
            {
                var tip = _tooltipBounds;
                tip.position += _rect.position;
                _skin.Draw(tip, Ae2TerminalSkin.Search, 0, 0, 128, 12, Ae2TerminalSkin.Ink);
                GUI.Label(tip, _tooltip, _tooltipStyle);
            }
            var current = Event.current;
            if (_rect.Contains(current.mousePosition) && (current.isMouse || current.type == EventType.ScrollWheel)) current.Use();
        }

        private void DrawContents(int id)
        {
            var s = _layout.Scale;
            var left = Ae2TerminalLayout.Rail;
            var footerY = Ae2TerminalLayout.Header * s + _layout.PanelHeight;
            const int board = Ae2TerminalLayout.BoardWidth;
            _skin.Draw(Units(left, 0, board - 2, Ae2TerminalLayout.Header), Ae2TerminalSkin.Terminal,
                0, 0, board - 2, 17, Ae2TerminalSkin.Shell);
            _skin.Draw(Units(left + board - 2, 0, 2, Ae2TerminalLayout.Header), Ae2TerminalSkin.Terminal,
                Ae2TerminalLayout.AtlasWidth - 2, 0, 2, 17, Ae2TerminalSkin.Shell);
            var footerHeight = Ae2TerminalLayout.Footer * s;
            _skin.Draw(new Rect(left * s, footerY, 2 * s, footerHeight), Ae2TerminalSkin.Terminal,
                0, 161, 2, 9, Ae2TerminalSkin.Shell);
            _skin.Draw(new Rect((left + 2) * s, footerY, (board - 4) * s, footerHeight), Ae2TerminalSkin.Terminal,
                2, 161, 1, 9, Ae2TerminalSkin.Shell);
            _skin.Draw(new Rect((left + board - 2) * s, footerY, 2 * s, footerHeight), Ae2TerminalSkin.Terminal,
                174, 161, 2, 9, Ae2TerminalSkin.Shell);
            GUI.Label(Units(left + 7, 2, 48, 17), TerminalText.Get("terminal.title"), _titleStyle);
            GUI.Label(new Rect((left + 7) * s, footerY + s, (board - 14) * s, 13 * s), _status, _statusStyle);

            var searchRect = Units(left + 55, 4, board - 79, 13);
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 1 && searchRect.Contains(ev.mousePosition))
            {
                SearchApi.Query = "";
                _focusSearch = true;
                ev.Use();
            }
            if (ev.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == SearchControl &&
                (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter))
            {
                GUI.FocusControl(null);
                ev.Use();
            }
            _skin.Draw(searchRect, Ae2TerminalSkin.Search, 0, GUI.GetNameOfFocusedControl() == SearchControl ? 24 : 0,
                128, 12, new Color32(91, 95, 117, 255));
            GUI.SetNextControlName(SearchControl);
            SearchApi.Query = GUI.TextField(searchRect, SearchApi.Query, 256, _searchStyle);
            if (_focusSearch && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(SearchControl);
                _focusSearch = false;
            }
            if (string.IsNullOrEmpty(SearchApi.Query) && GUI.GetNameOfFocusedControl() != SearchControl)
                GUI.Label(searchRect, TerminalText.Get("terminal.search"), _searchStyle);
            SetTooltip(searchRect, TerminalText.Get("terminal.searchHint"), below: true);

            if (IconButton(left + board - 22, 2, 96, 0, TerminalText.Get("terminal.close"), "X")) _closeRequested = true;
            if (IconButton(3, 22, _sortByAmount ? 16 : 0, 64, _sortByAmount ? TerminalText.Get("terminal.sortName") : TerminalText.Get("terminal.sortQuantity"), "#"))
            {
                _sortByAmount = !_sortByAmount;
                _descending = _sortByAmount;
            }
            if (IconButton(3, 43, _descending ? 16 : 0, 48, _descending ? TerminalText.Get("terminal.sortAscending") : TerminalText.Get("terminal.sortDescending"), _descending ? "v" : "^"))
                _descending = !_descending;
            if (IconButton(3, 64, 232, 200, TerminalText.Get("terminal.refresh"), "R", 8)) Refresh();
            if (HasSnapshot && _lastQuery != SearchApi.Query) Rebuild();
        }

        private bool IconButton(int px, int py, int x, int y, string tooltip, string fallback, int size = 16)
        {
            var rect = Units(px, py, 18, 20);
            var hovered = rect.Contains(Event.current.mousePosition);
            _skin.Draw(rect, Ae2TerminalSkin.States, hovered ? 212 : 176, 128, 18, 20, Ae2TerminalSkin.Shell);
            if (!_skin.Draw(Units(px + 1, py + 2, 16, 16), Ae2TerminalSkin.States, x, y, size, size, Color.clear))
                GUI.Label(rect, fallback, _iconStyle);
            SetTooltip(rect, tooltip, below: py < 20);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void SetTooltip(Rect control, string text, bool below)
        {
            if (Event.current.type != EventType.Repaint || !control.Contains(Event.current.mousePosition)) return;
            var label = new GUIContent(text);
            var gap = 2 * _layout.Scale;
            var width = Mathf.Min(_tooltipStyle!.CalcSize(label).x + 12 * _layout.Scale, _rect.width - 2 * gap);
            var x = Mathf.Clamp(below ? control.x : control.xMax + gap, gap, _rect.width - width - gap);
            var y = below ? control.yMax + gap : control.y + gap;
            // Draw outside the IMGUI window so it can overlap the storage canvas.
            _tooltip = label;
            _tooltipBounds = new Rect(x, y, width, 15 * _layout.Scale);
        }

        private GUIContent? _tooltip;
        private Rect _tooltipBounds;
        private Rect Units(float x, float y, float w, float h) => new Rect(x * _layout.Scale, y * _layout.Scale,
            w * _layout.Scale, h * _layout.Scale);

        private void EnsureStyles()
        {
            if (_styleScale == _layout.Scale) return;
            _styleScale = _layout.Scale;
            _windowStyle = new GUIStyle(GUIStyle.none);
            _titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                fontSize = 8 * _styleScale, padding = new RectOffset() };
            _titleStyle.normal.textColor = Ae2TerminalSkin.Ink;
            _statusStyle = new GUIStyle(_titleStyle) { fontStyle = FontStyle.Normal, fontSize = 7 * _styleScale };
            _iconStyle = new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter };
            _tooltipStyle = new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
            _tooltipStyle.normal.textColor = Color.white;
            _searchStyle = new GUIStyle(GUI.skin.textField) { fontSize = 8 * _styleScale, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(3 * _styleScale, 3 * _styleScale, 0, 0) };
            foreach (var state in new[] { _searchStyle.normal, _searchStyle.hover, _searchStyle.focused, _searchStyle.active,
                _searchStyle.onNormal, _searchStyle.onHover, _searchStyle.onFocused, _searchStyle.onActive })
            {
                state.background = null;
                state.textColor = Color.white;
            }
        }
    }
}
