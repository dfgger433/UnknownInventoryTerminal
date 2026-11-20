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
        private readonly Func<bool> _canReturn;
        private readonly Action _returnToGroups;
        private IReadOnlyList<InventoryEntry> _snapshot = Array.Empty<InventoryEntry>();
        private IReadOnlyList<InventoryRow> _rows = Array.Empty<InventoryRow>();
        private Ae2TerminalLayout _layout;
        private Rect _rect;
        private string _lastQuery = "", _status = "";
        private float _nextRefresh;
        private bool _closeRequested, _focusSearch, _sortByAmount, _descending, _settingsOpen;
        private GUIStyle? _titleStyle, _statusStyle, _searchStyle, _tooltipStyle, _iconStyle, _settingsStyle;
        internal bool SettingsOpen => _settingsOpen;
        private int _styleScale;
        private readonly TerminalButtonInput _buttons = new TerminalButtonInput();
        internal bool Visible { get; private set; }
        internal bool CloseRequested => _closeRequested;
        internal IReadOnlyList<InventoryEntry> Snapshot => _snapshot;
        internal IReadOnlyList<InventoryRow> Rows => _rows;
        internal bool HasSnapshot { get; private set; }
        internal Rect ScreenBounds => new Rect(_rect.x, Screen.height - _rect.yMax, _rect.width, _rect.height);
        internal bool ContainsScreenPoint(Vector2 point) => Visible && ScreenBounds.Contains(point);

        internal TerminalWindow(Ae2TerminalSkin skin, Func<IReadOnlyList<InventoryEntry>> capture, Func<int> equipmentCount,
            Action<IReadOnlyList<InventoryEntry>> publishNativeEntries, Action<Exception> report,
            Action<Rect, Rect, Ae2TerminalLayout, bool, bool> configureNativePanel, Func<bool> canReturn, Action returnToGroups)
        {
            _skin = skin;
            _capture = capture;
            _equipmentCount = equipmentCount;
            _publishNativeEntries = publishNativeEntries;
            _report = report;
            _configureNativePanel = configureNativePanel;
            _canReturn = canReturn;
            _returnToGroups = returnToGroups;
        }

        internal void Open() { Visible = true; _closeRequested = false; FitToScreen(); Refresh(); }
        internal void Close()
        {
            Visible = false;
            _focusSearch = false;
            _settingsOpen = false;
            _buttons.Reset();
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
            UpdateButtons();
            if (_closeRequested) return;
            ConfigureNativePanel();
            if (Time.unscaledTime >= _nextRefresh) Refresh();
            else if (HasSnapshot && _lastQuery != SearchApi.Query) Rebuild();
        }

        private void UpdateButtons()
        {
            var pointer = Input.mousePosition;
            var hit = TerminalButtonInput.HitTest(_layout, pointer.x, Screen.height - pointer.y, _canReturn());
            for (var button = 0; button <= 1; button++)
            {
                if (Input.GetMouseButtonDown(button) && _buttons.Press(hit, button)) Plugin.ReleaseSearchFocus();
                if (!Input.GetMouseButtonUp(button)) continue;
                switch (_buttons.Release(hit, button))
                {
                    case TerminalButton.Close: _closeRequested = true; break;
                    case TerminalButton.Sort:
                        _sortByAmount = !_sortByAmount;
                        _descending = _sortByAmount;
                        break;
                    case TerminalButton.Direction: _descending = !_descending; break;
                    case TerminalButton.Style:
                        SettingsBridge.TerminalHeight = Ae2TerminalLayout.Next(_layout.Style, backwards: button == 1);
                        FitToScreen();
                        break;
                    case TerminalButton.Settings:
                        _settingsOpen = !_settingsOpen;
                        Plugin.ReleaseSearchFocus();
                        break;
                    case TerminalButton.Return: _returnToGroups(); break;
                }
            }
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
            // Storage rows follow the terminal-style setting; polling the count still discovers CCL equipment slots.
            _equipmentCount();
            _layout = new Ae2TerminalLayout(Screen.width, Screen.height, SettingsBridge.TerminalHeight);
            var padding = Ae2TerminalLayout.ChromeTopPadding * _layout.Scale;
            _rect = new Rect(_layout.Left, _layout.Top - padding, _layout.Width, _layout.Height + padding);
        }

        private void ConfigureNativePanel() => _configureNativePanel(new Rect(_rect.x, _layout.Top, _layout.Width, _layout.Height),
            new Rect(_rect.x + _layout.BoardLeft, _layout.Top + Ae2TerminalLayout.Header * _layout.Scale,
                Ae2TerminalLayout.BoardWidth * _layout.Scale, _layout.PanelHeight),
            _layout, _sortByAmount, _descending);

        internal void Draw()
        {
            if (!Visible) return;
            EnsureStyles();
            _tooltip = null;
            // A layout group clips the drawing but does not create a full-window IMGUI input target.
            GUI.BeginGroup(_rect);
            try { DrawContents(); }
            finally { GUI.EndGroup(); }
            if (_tooltip != null)
            {
                var tip = _tooltipBounds;
                tip.position += _rect.position;
                _skin.Draw(tip, Ae2Image.Tooltip, Ae2TerminalSkin.Ink);
                GUI.Label(tip, _tooltip, _tooltipStyle);
            }
        }

        private void DrawContents()
        {
            // The board parts and the toolbar background are drawn by the native canvas below this window.
            const int left = Ae2TerminalLayout.Rail;
            GUI.Label(Units(left + 7, 2, 46, 15), TerminalText.Get("terminal.title"), _titleStyle);
            GUI.Label(Units(left + Ae2TerminalLayout.Inset, _layout.StatusTop, 62, Ae2TerminalLayout.StatusHeight),
                _status, _statusStyle);

            // Reserve the right end of the header for the close button.
            var searchSize = _skin.SourceSize(Ae2Image.Search) ?? new Vector2Int(Ae2TerminalLayout.SearchWidth, 12);
            var searchRect = Units(Ae2TerminalLayout.SearchX, Ae2TerminalLayout.SearchY,
                Mathf.Min(searchSize.x, Ae2TerminalLayout.SearchWidth), Mathf.Min(searchSize.y, Ae2TerminalLayout.SearchHeight));
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
            _skin.Draw(searchRect, GUI.GetNameOfFocusedControl() == SearchControl ? Ae2Image.SearchFocused : Ae2Image.Search,
                new Color32(91, 95, 117, 255));
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

            DrawButton(TerminalButton.Close, Ae2Image.Close, TerminalText.Get("terminal.close"), "X", tooltipBelow: true);
            DrawButton(TerminalButton.Sort, _sortByAmount ? Ae2Image.SortAmount : Ae2Image.SortName,
                _sortByAmount ? TerminalText.Get("terminal.sortName") : TerminalText.Get("terminal.sortQuantity"), "#");
            DrawButton(TerminalButton.Direction, _descending ? Ae2Image.SortDescending : Ae2Image.SortAscending,
                _descending ? TerminalText.Get("terminal.sortAscending") : TerminalText.Get("terminal.sortDescending"), _descending ? "v" : "^");
            var height = _layout.Style;
            DrawButton(TerminalButton.Style, StyleIcon(height), TerminalText.Get("terminal.style") + ": " +
                TerminalText.Get("terminal.style." + height), "H");
            if (_canReturn()) DrawButton(TerminalButton.Return, Ae2Image.Back, TerminalText.Get("inventory.backTooltip"), "<");
            DrawButton(TerminalButton.Settings, Ae2Image.NativeButtonFace, TerminalText.Get("terminal.settings"), "S");
            if (HasSnapshot && _lastQuery != SearchApi.Query) Rebuild();
            DrawSettingsPanel();
        }

        private void DrawSettingsPanel()
        {
            if (!_settingsOpen) return;
            var panel = Units(Ae2TerminalLayout.Rail + 10, Ae2TerminalLayout.Header + 12, 116, 92);
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 0 && !panel.Contains(ev.mousePosition))
            {
                _settingsOpen = false;
                ev.Use();
                return;
            }
            GUI.Box(panel, GUIContent.none, _settingsStyle);
            var title = new Rect(panel.x + 5 * _layout.Scale, panel.y + 3 * _layout.Scale,
                panel.width - 10 * _layout.Scale, 14 * _layout.Scale);
            GUI.Label(title, TerminalText.Get("terminal.settings"), _titleStyle);
            var language = new Rect(panel.x + 5 * _layout.Scale, panel.y + 23 * _layout.Scale,
                panel.width - 10 * _layout.Scale, 20 * _layout.Scale);
            if (GUI.Button(language, TerminalText.Format("terminal.settings.language", SettingsBridge.LanguageLabel()), _settingsStyle))
                SettingsBridge.CycleLanguage();
            var placement = new Rect(panel.x + 5 * _layout.Scale, panel.y + 47 * _layout.Scale,
                panel.width - 10 * _layout.Scale, 34 * _layout.Scale);
            if (GUI.Button(placement, TerminalText.Format("terminal.settings.placement", ContainerPlacement.Label()), _settingsStyle))
                ContainerPlacement.Cycle();
        }

        private static Ae2Image StyleIcon(TerminalHeight height) => height switch
        {
            TerminalHeight.Medium => Ae2Image.TerminalStyleMedium,
            TerminalHeight.Tall => Ae2Image.TerminalStyleTall,
            TerminalHeight.Full => Ae2Image.TerminalStyleFull,
            _ => Ae2Image.TerminalStyleSmall
        };

        private void DrawButton(TerminalButton button, Ae2Image icon, string tooltip, string fallback, bool tooltipBelow = false)
        {
            var ev = Event.current;
            if (ev.type != EventType.Repaint) return;
            var (x, y) = TerminalButtonInput.Origin(button);
            var hit = Units(x, y, Ae2TerminalLayout.ButtonWidth, Ae2TerminalLayout.ButtonHeight);
            var face = Ae2ToolbarAppearance.Face(hit.Contains(ev.mousePosition), _buttons.Focused == button);
            var offset = Ae2ToolbarAppearance.OffsetY(face);
            var faceSize = _skin.SourceSize(face) ?? new Vector2Int(Ae2TerminalLayout.ButtonWidth, Ae2TerminalLayout.ButtonHeight - offset);
            _skin.Draw(Units(x, y + offset, faceSize.x, faceSize.y), face, Ae2TerminalSkin.Shell);
            if (_skin.SourceSize(icon) is { } size && _skin.VisibleBounds(icon) is { } visible)
            {
                var (dx, dy) = Ae2ToolbarAppearance.CenterIcon(visible, faceSize.x, faceSize.y, _layout.Scale);
                var iconRect = Units(x, y + offset, size.x, size.y);
                iconRect.position += new Vector2(dx, dy);
                _skin.Draw(iconRect, icon, Color.clear);
            }
            else GUI.Label(Units(x, y + offset, faceSize.x, faceSize.y), fallback, _iconStyle);
            SetTooltip(hit, tooltip, below: tooltipBelow);
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
            _tooltipBounds = new Rect(x, y, width, (_skin.SourceSize(Ae2Image.Tooltip)?.y ?? 15) * _layout.Scale);
        }

        private GUIContent? _tooltip;
        private Rect _tooltipBounds;
        private Rect Units(float x, float y, float w, float h) => new Rect(x * _layout.Scale,
            (y + Ae2TerminalLayout.ChromeTopPadding) * _layout.Scale, w * _layout.Scale, h * _layout.Scale);

        private void EnsureStyles()
        {
            if (_styleScale == _layout.Scale) return;
            _styleScale = _layout.Scale;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                fontSize = 8 * _styleScale, padding = new RectOffset() };
            _titleStyle.normal.textColor = Ae2TerminalSkin.Ink;
            _statusStyle = new GUIStyle(_titleStyle) { fontStyle = FontStyle.Normal, fontSize = 6 * _styleScale };
            _iconStyle = new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter };
            _tooltipStyle = new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
            _tooltipStyle.normal.textColor = Color.white;
            _settingsStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter,
                fontSize = 7 * _styleScale, wordWrap = true, padding = new RectOffset(3 * _styleScale, 3 * _styleScale, 1 * _styleScale, 1 * _styleScale) };
            _settingsStyle.normal.textColor = Color.white;
            _settingsStyle.hover.textColor = Color.white;
            _settingsStyle.active.textColor = Color.white;
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
