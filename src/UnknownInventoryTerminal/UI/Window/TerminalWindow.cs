using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal
{
    // Inventory chrome uses the native canvas; only the separate settings page uses IMGUI.
    internal sealed class TerminalWindow
    {
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
        private bool _closeRequested, _sortByAmount, _descending;
        private GUIStyle? _titleStyle, _iconStyle, _settingsStyle;
        private int _settingsClosedFrame = -1;
        internal bool SettingsOpen => _buttons.SettingsOpen;
        internal bool BlocksInventoryInput => SettingsOpen || _settingsClosedFrame == Time.frameCount;
        private enum SettingsDropdown { None, Language, Placement }
        private SettingsDropdown _expanded;
        private int _optionScroll;
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
            _settingsClosedFrame = -1;
            ToggleDropdown(SettingsDropdown.None);
            _buttons.Reset();
            _snapshot = Array.Empty<InventoryEntry>();
            _rows = Array.Empty<InventoryRow>();
            _publishNativeEntries(Array.Empty<InventoryEntry>());
            HasSnapshot = false;
        }

        internal void Update(bool modalOwnsInput)
        {
            if (!Visible) return;
            if (_closeRequested) return;
            FitToScreen();
            if (modalOwnsInput) _buttons.Reset();
            else UpdateButtons();
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

        internal void DrawSettings()
        {
            if (!Visible || !SettingsOpen) return;
            EnsureStyles();
            DrawSettingsPage();
        }

        internal void DrawInventoryChrome(NativeTerminalChrome chrome)
        {
            if (!Visible || SettingsOpen || CloseRequested) return;
            chrome.DrawHeader(_layout, _status, BlocksInventoryInput);
            Draw(TerminalButton.Close, Ae2Image.Close, "terminal.close", "X", hintBelow: true);
            Draw(TerminalButton.Sort, _sortByAmount ? Ae2Image.SortAmount : Ae2Image.SortName,
                _sortByAmount ? "terminal.sortName" : "terminal.sortQuantity", "#");
            Draw(TerminalButton.Direction, _descending ? Ae2Image.SortDescending : Ae2Image.SortAscending,
                _descending ? "terminal.sortAscending" : "terminal.sortDescending", _descending ? "v" : "^");
            var style = TerminalText.Format("terminal.style.current", TerminalText.Get("terminal.style." + _layout.Style));
            chrome.DrawButton(TerminalButton.Style, _buttons.Focused, StyleIcon(_layout.Style), style, style);
            Draw(TerminalButton.Return, Ae2Image.Back, "terminal.button.return", "<", visible: _canReturn());
            var settings = TerminalText.Get("terminal.button.toggle");
            chrome.DrawButton(TerminalButton.Settings, _buttons.Focused, Ae2Image.Settings, settings, settings);

            void Draw(TerminalButton button, Ae2Image icon, string key, string fallback, bool visible = true, bool hintBelow = false) =>
                chrome.DrawButton(button, _buttons.Focused, icon, TerminalText.Get(key), fallback, visible, hintBelow);
        }

        private Rect SettingsBounds
        {
            get
            {
                var scale = _layout.Scale;
                var width = Ae2SettingsLayout.PanelWidth * scale;
                var height = Ae2SettingsLayout.PageHeight(VisibleOptionRows) * scale;
                var maxRows = Math.Min(OptionCapacity, Math.Max(TerminalText.Languages.Count, PlacementOptions.Length));
                var maxHeight = Ae2SettingsLayout.PageHeight(maxRows) * scale;
                var y = Math.Max(0, Math.Min((Screen.height - Ae2SettingsLayout.PanelHeight * scale) / 2, Screen.height - maxHeight));
                return new Rect((Screen.width - width) / 2, y, width, height);
            }
        }

        private int OptionCapacity => Math.Max(0, Math.Min(Ae2SettingsLayout.MaxVisibleOptions,
            (Screen.height / _layout.Scale - Ae2SettingsLayout.PanelHeight) / Ae2SettingsLayout.OptionHeight));

        private int VisibleOptionRows => _expanded switch
        {
            SettingsDropdown.Language => Math.Min(OptionCapacity, TerminalText.Languages.Count),
            SettingsDropdown.Placement => Math.Min(OptionCapacity, PlacementOptions.Length),
            _ => 0
        };

        private void ToggleDropdown(SettingsDropdown dropdown)
        {
            _expanded = _expanded == dropdown ? SettingsDropdown.None : dropdown;
            _optionScroll = 0;
        }
        private static readonly ContainerPlacementMode[] PlacementOptions =
            (ContainerPlacementMode[])Enum.GetValues(typeof(ContainerPlacementMode));

        private void DrawSettingsPage()
        {
            var page = SettingsBounds;
            var scale = _layout.Scale;
            // Keep layout stable for the whole IMGUI pass, even if a button changes the state.
            var expanded = _expanded;
            var rows = VisibleOptionRows;
            GUI.BeginGroup(page);
            try
            {
                _skin.DrawPanel(new Rect(0, 0, page.width, page.height), Ae2Image.SettingsBackground, scale, Ae2TerminalSkin.Shell);
                GUI.Label(Area(7, 4, 90, 12), TerminalText.Get("terminal.settings"), _titleStyle);
                var close = Area(TerminalButtonInput.SettingsCloseX, TerminalButtonInput.SettingsCloseY,
                    Ae2TerminalLayout.ButtonWidth, Ae2TerminalLayout.ButtonHeight);
                PaintButton(close, TerminalButton.SettingsBack, Ae2Image.Back, "<");

                Field(SettingsDropdown.Language, Ae2SettingsLayout.LanguageY,
                    SettingsBridge.Label(SettingsBridge.LanguageSetting), SettingsBridge.LanguageLabel(), TerminalText.Languages.Count);
                var placementY = Ae2SettingsLayout.FieldY(placement: true, expanded == SettingsDropdown.Language ? rows : 0);
                Field(SettingsDropdown.Placement, placementY, SettingsBridge.Label("ContainerPlacement"),
                    ContainerPlacement.Label(), PlacementOptions.Length);
                var keepDragging = SettingsBridge.KeepDraggingAfterUse;
                var toggle = Area(Ae2SettingsLayout.FieldX, Ae2SettingsLayout.KeepDraggingY(rows),
                    Ae2SettingsLayout.FieldWidth, Ae2SettingsLayout.KeepDraggingHeight);
                var next = _settingsControls.Toggle(toggle, scale, keepDragging,
                    SettingsBridge.Label(SettingsBridge.KeepDraggingAfterUseSetting), _settingsStyle!);
                if (next != keepDragging) SettingsBridge.KeepDraggingAfterUse = next;

                if (rows > 0 && _expanded == expanded)
                {
                    var list = Area(Ae2SettingsLayout.FieldX, Ae2SettingsLayout.ListY(expanded == SettingsDropdown.Placement),
                        Ae2SettingsLayout.FieldWidth, rows * Ae2SettingsLayout.OptionHeight);
                    if (expanded == SettingsDropdown.Language)
                        DrawOptions(list, rows, TerminalText.Languages, SettingsBridge.Language, TerminalText.LanguageName, SettingsBridge.SetLanguage);
                    else
                        DrawOptions(list, rows, PlacementOptions, ContainerPlacement.Mode,
                            mode => SettingsBridge.ChoiceLabel("ContainerPlacement", mode.ToString()), mode => ContainerPlacement.Mode = mode);
                }
            }
            finally { GUI.EndGroup(); }

            Rect Area(int x, int y, int width, int height) => new Rect(x * scale, y * scale, width * scale, height * scale);
            void Field(SettingsDropdown id, int y, string title, string caption, int optionCount)
            {
                GUI.Label(Area(Ae2SettingsLayout.FieldX, y - Ae2SettingsLayout.LabelHeight,
                    Ae2SettingsLayout.FieldWidth, Ae2SettingsLayout.LabelHeight), title, _titleStyle);
                if (_settingsControls.Dropdown(Area(Ae2SettingsLayout.FieldX, y, Ae2SettingsLayout.FieldWidth, Ae2SettingsLayout.FieldHeight),
                    scale, caption, _settingsStyle!, expanded == id, optionCount > 1 && OptionCapacity > 0))
                    ToggleDropdown(id);
            }
        }

        private void DrawOptions<T>(Rect bounds, int rows, IReadOnlyList<T> options, T selected, Func<T, string> label, Action<T> apply)
        {
            var scale = _layout.Scale;
            var pitch = Ae2SettingsLayout.OptionHeight * scale;
            var ev = Event.current;
            var maxScroll = Math.Max(0, options.Count - rows);
            _optionScroll = Mathf.Clamp(_optionScroll, 0, maxScroll);
            if (maxScroll > 0 && ev.type == EventType.ScrollWheel && bounds.Contains(ev.mousePosition))
            {
                _optionScroll = Mathf.Clamp(_optionScroll + Math.Sign(ev.delta.y), 0, maxScroll);
                ev.Use();
            }
            for (var i = 0; i < rows; i++)
            {
                var option = options[_optionScroll + i];
                var row = new Rect(bounds.x, bounds.y + i * pitch, bounds.width, pitch);
                if (!_settingsControls.Option(row, scale, label(option), _settingsStyle!, EqualityComparer<T>.Default.Equals(option, selected))) continue;
                apply(option);
                ToggleDropdown(SettingsDropdown.None);
                break;
            }
        }

        private static Ae2Image StyleIcon(TerminalHeight height) => height switch
        {
            TerminalHeight.Medium => Ae2Image.TerminalStyleMedium,
            TerminalHeight.Tall => Ae2Image.TerminalStyleTall,
            TerminalHeight.Full => Ae2Image.TerminalStyleFull,
            _ => Ae2Image.TerminalStyleSmall
        };

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

        private void EnsureStyles()
        {
            if (_styleScale == _layout.Scale) return;
            _styleScale = _layout.Scale;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                fontSize = 8 * _styleScale, padding = new RectOffset() };
            _titleStyle.normal.textColor = Ae2TerminalSkin.Ink;
            _iconStyle = new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter };
            _settingsStyle = new GUIStyle(_titleStyle) { fontStyle = FontStyle.Normal,
                fontSize = 8 * _styleScale, wordWrap = false, clipping = TextClipping.Clip, richText = false };
            _settingsStyle.normal.textColor = new Color32(23, 25, 32, 255);
        }
    }
}
