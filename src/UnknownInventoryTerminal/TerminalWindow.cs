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
        private bool _closeRequested, _focusSearch, _sortByAmount, _descending;
        private GUIStyle? _titleStyle, _statusStyle, _searchStyle, _tooltipStyle, _iconStyle, _settingsStyle;
        private int _settingsClosedFrame = -1;
        internal bool SettingsOpen => _buttons.SettingsOpen;
        internal bool BlocksInventoryInput => SettingsOpen || _settingsClosedFrame == Time.frameCount;
        private enum SettingsDropdown { None, Language, Placement }
        private SettingsDropdown _expanded;
        private Vector2 _optionScroll;
        private readonly Ae2SettingsControls _settingsControls;
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
            _settingsControls = new Ae2SettingsControls(skin);
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
            _settingsClosedFrame = -1;
            ToggleDropdown(SettingsDropdown.None);
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
            if (_closeRequested || SettingsOpen) return;
            ConfigureNativePanel();
            if (Time.unscaledTime >= _nextRefresh) Refresh();
            else if (HasSnapshot && _lastQuery != SearchApi.Query) Rebuild();
        }

        private void UpdateButtons()
        {
            var pointer = Input.mousePosition;
            var topLeft = new Vector2(pointer.x, Screen.height - pointer.y);
            var local = (topLeft - SettingsBounds.position) / _layout.Scale;
            var hit = SettingsOpen ? TerminalButtonInput.SettingsHitTest(local.x, local.y) :
                TerminalButtonInput.HitTest(_layout, topLeft.x, topLeft.y, _canReturn());
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
                    case TerminalButton.SettingsBack:
                        if (!SettingsOpen) _settingsClosedFrame = Time.frameCount;
                        _focusSearch = false;
                        ToggleDropdown(SettingsDropdown.None);
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
            if (SettingsOpen)
            {
                DrawSettingsPage();
                return;
            }
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
            if (!BlocksInventoryInput && ev.type == EventType.MouseDown && ev.button == 1 && searchRect.Contains(ev.mousePosition))
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
            var guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !BlocksInventoryInput;
            try { SearchApi.Query = GUI.TextField(searchRect, SearchApi.Query, 256, _searchStyle); }
            finally { GUI.enabled = guiEnabled; }
            if (_focusSearch && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(SearchControl);
                _focusSearch = false;
            }
            if (string.IsNullOrEmpty(SearchApi.Query) && GUI.GetNameOfFocusedControl() != SearchControl)
                GUI.Label(searchRect, TerminalText.Get("terminal.search"), _searchStyle);
            if (!SettingsOpen) SetTooltip(searchRect, TerminalText.Get("terminal.searchHint"), below: true);

            DrawButton(TerminalButton.Close, Ae2Image.Close, TerminalText.Get("terminal.close"), "X", tooltipBelow: true);
            DrawButton(TerminalButton.Sort, _sortByAmount ? Ae2Image.SortAmount : Ae2Image.SortName,
                _sortByAmount ? TerminalText.Get("terminal.sortName") : TerminalText.Get("terminal.sortQuantity"), "#");
            DrawButton(TerminalButton.Direction, _descending ? Ae2Image.SortDescending : Ae2Image.SortAscending,
                _descending ? TerminalText.Get("terminal.sortAscending") : TerminalText.Get("terminal.sortDescending"), _descending ? "v" : "^");
            var height = _layout.Style;
            DrawButton(TerminalButton.Style, StyleIcon(height),
                TerminalText.Format("terminal.style.current", TerminalText.Get("terminal.style." + height)),
                TerminalText.Format("terminal.style.current", TerminalText.Get("terminal.style." + height)));
            if (_canReturn()) DrawButton(TerminalButton.Return, Ae2Image.Back, TerminalText.Get("terminal.button.return"), "<");
            DrawButton(TerminalButton.Settings, Ae2Image.Settings, TerminalText.Get("terminal.button.toggle"), TerminalText.Get("terminal.button.toggle"));
            if (HasSnapshot && _lastQuery != SearchApi.Query) Rebuild();
        }

        private Rect SettingsBounds
        {
            get
            {
                const int panelWidth = 124, panelHeight = 82;
                var scale = _layout.Scale;
                var width = panelWidth * scale;
                var height = panelHeight * scale;
                var rowHeight = OptionHeight * scale;
                var maximumOptions = Math.Max(TerminalText.Languages.Count, PlacementOptions.Length);
                var visibleRows = Math.Min(4, maximumOptions);
                var reserve = _expanded == SettingsDropdown.None ? 0 : visibleRows * rowHeight;
                var y = Math.Max(0, Math.Min((Screen.height - height) / 2, Screen.height - height - reserve));
                return new Rect((Screen.width - width) / 2, y, width, height);
            }
        }

        private const int OptionHeight = 17;
        private void ToggleDropdown(SettingsDropdown dropdown)
        {
            _expanded = _expanded == dropdown ? SettingsDropdown.None : dropdown;
            _optionScroll = Vector2.zero;
        }
        private static readonly ContainerPlacementMode[] PlacementOptions =
            (ContainerPlacementMode[])Enum.GetValues(typeof(ContainerPlacementMode));

        private void DrawSettingsPage()
        {
            var page = SettingsBounds;
            var scale = _layout.Scale;
            GUI.BeginGroup(page);
            try
            {
                _skin.DrawPanel(new Rect(0, 0, page.width, page.height), Ae2Image.SettingsBackground, scale, Ae2TerminalSkin.Shell);
                GUI.Label(Area(7, 5, 90, 18), TerminalText.Get("terminal.settings"), _titleStyle);
                var close = Area(TerminalButtonInput.SettingsCloseX, TerminalButtonInput.SettingsCloseY,
                    Ae2TerminalLayout.ButtonWidth, Ae2TerminalLayout.ButtonHeight);
                PaintButton(close, TerminalButton.SettingsBack, Ae2Image.Back, "<");

                GUI.Label(Area(7, 26, 110, 11), TerminalText.Get("terminal.settings.language"), _titleStyle);
                Field(SettingsDropdown.Language, Area(7, 38, 110, 14), TerminalText.LanguageName(SettingsBridge.Language));
                GUI.Label(Area(7, 54, 110, 10), TerminalText.Get("terminal.settings.placement"), _titleStyle);
                DrawPlacementField(Area(7, 65, 110, 13), ContainerPlacement.Label());

            }
            finally { GUI.EndGroup(); }

            if (_expanded != SettingsDropdown.None)
            {
                var count = _expanded == SettingsDropdown.Language ? TerminalText.Languages.Count : PlacementOptions.Length;
                var rowHeight = OptionHeight * scale;
                var list = new Rect(page.x + 7 * scale, page.yMax, 110 * scale, Math.Min(4, count) * rowHeight);
                if (_expanded == SettingsDropdown.Language)
                    DrawOptions(list, TerminalText.Languages, SettingsBridge.Language, TerminalText.LanguageName, SettingsBridge.SetLanguage);
                else
                    DrawOptions(list, PlacementOptions, ContainerPlacement.Mode,
                        mode => SettingsBridge.ChoiceLabel("ContainerPlacement", mode.ToString()), mode => ContainerPlacement.Mode = mode);
            }

            Rect Area(float x, float y, float width, float height) => new Rect(x * scale, y * scale, width * scale, height * scale);
            void Field(SettingsDropdown id, Rect bounds, string caption)
            {
                if (_settingsControls.Button(bounds, scale, caption, "", _settingsStyle!, _iconStyle!, open: _expanded == id))
                    ToggleDropdown(id);
            }
        }

        private void DrawPlacementField(Rect bounds, string caption)
        {
            var scale = _layout.Scale;
            if (Event.current.type == EventType.Repaint)
            {
                var hovered = bounds.Contains(Event.current.mousePosition);
                var open = _expanded == SettingsDropdown.Placement;
                var face = open
                    ? hovered ? Ae2Image.SettingsDropDownOpenHover : Ae2Image.SettingsDropDownOpen
                    : hovered ? Ae2Image.SettingsDropDownClosedHover : Ae2Image.SettingsDropDownClosed;
                var size = _skin.SourceSize(face) ?? new Vector2Int(111, 20);
                var x = bounds.xMax - size.x * scale;
                _skin.Draw(new Rect(x, bounds.y, size.x * scale, size.y * scale), face,
                    hovered ? Ae2TerminalSkin.Focus : Ae2TerminalSkin.Slot);
                GUI.Label(new Rect(bounds.x, bounds.y, bounds.width - size.x * scale, bounds.height), caption, _settingsStyle);
            }
            if (GUI.Button(bounds, GUIContent.none, GUIStyle.none)) ToggleDropdown(SettingsDropdown.Placement);
        }

        private void DrawOptions<T>(Rect bounds, IReadOnlyList<T> options, T selected, Func<T, string> label, Action<T> apply)
        {
            var scale = _layout.Scale;
            var pitch = OptionHeight * scale;
            var overflow = options.Count * pitch > bounds.height;
            var width = bounds.width - (overflow ? GUI.skin.verticalScrollbar.fixedWidth + scale : 0);
            if (Event.current.type == EventType.Repaint) _optionScroll.y = Mathf.Clamp(_optionScroll.y, 0, Mathf.Max(0, options.Count * pitch - bounds.height));
            _optionScroll = GUI.BeginScrollView(bounds, _optionScroll, new Rect(0, 0, width, options.Count * pitch));
            try
            {
                for (var i = 0; i < options.Count; i++)
                {
                    var option = options[i];
                    var isSelected = EqualityComparer<T>.Default.Equals(option, selected);
                    if (!_settingsControls.Button(new Rect(0, i * pitch, width, pitch - scale), scale, label(option),
                        isSelected ? "*" : "", _settingsStyle!, _iconStyle!, option: true, selected: isSelected)) continue;
                    apply(option);
                    ToggleDropdown(SettingsDropdown.None);
                    break;
                }
            }
            finally { GUI.EndScrollView(); }
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
            var (x, y) = TerminalButtonInput.Origin(button);
            var hit = Units(x, y, Ae2TerminalLayout.ButtonWidth, Ae2TerminalLayout.ButtonHeight);
            PaintButton(hit, button, icon, fallback);
            SetTooltip(hit, tooltip, below: tooltipBelow);
        }

        private void PaintButton(Rect hit, TerminalButton button, Ae2Image icon, string fallback)
        {
            var ev = Event.current;
            if (ev.type != EventType.Repaint) return;
            var scale = _layout.Scale;
            var face = Ae2ToolbarAppearance.Face(hit.Contains(ev.mousePosition), _buttons.Focused == button);
            var offset = Ae2ToolbarAppearance.OffsetY(face);
            var faceSize = _skin.SourceSize(face) ?? new Vector2Int(Ae2TerminalLayout.ButtonWidth, Ae2TerminalLayout.ButtonHeight - offset);
            var faceRect = new Rect(hit.x, hit.y + offset * scale, faceSize.x * scale, faceSize.y * scale);
            _skin.Draw(faceRect, face, Ae2TerminalSkin.Shell);
            if (_skin.SourceSize(icon) is { } size && _skin.VisibleBounds(icon) is { } visible)
            {
                var (dx, dy) = Ae2ToolbarAppearance.CenterIcon(visible, faceSize.x, faceSize.y, scale);
                _skin.Draw(new Rect(faceRect.x + dx, faceRect.y + dy, size.x * scale, size.y * scale), icon, Color.clear);
            }
            else GUI.Label(faceRect, fallback, _iconStyle);
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
            _settingsStyle = new GUIStyle(_titleStyle) { fontStyle = FontStyle.Normal,
                fontSize = 7 * _styleScale, wordWrap = true };
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
