// SPDX-License-Identifier: GPL-3.0-only

using System;
using UnityEngine;

namespace UnknownInventoryTerminal
{
    internal sealed class Ae2SettingsControls
    {
        private readonly Ae2TerminalSkin _skin;
        private int _fieldHotControl, _optionHotControl, _pressedOption = -1;
        internal Ae2SettingsControls(Ae2TerminalSkin skin) => _skin = skin;

        internal void CancelOptionPress()
        {
            if (_optionHotControl != 0 && GUIUtility.hotControl == _optionHotControl) GUIUtility.hotControl = 0;
            _optionHotControl = 0;
            _pressedOption = -1;
        }

        internal void CancelFieldPress()
        {
            if (_fieldHotControl != 0 && GUIUtility.hotControl == _fieldHotControl) GUIUtility.hotControl = 0;
            _fieldHotControl = 0;
        }

        internal bool Dropdown(Rect bounds, int scale, string text, GUIStyle label, bool open, bool enabled)
        {
            // Text and arrow share the original full-width hit area. Holding the mouse
            // from another control (or outside the field) does not press this button.
            var id = GUIUtility.GetControlID(FocusType.Passive);
            var ev = Event.current;
            var hover = bounds.Contains(ev.mousePosition);
            enabled &= GUI.enabled;
            var captured = _fieldHotControl == id && GUIUtility.hotControl == id;
            if (captured && (!enabled || ev.type == EventType.Ignore))
            {
                CancelFieldPress();
                captured = false;
            }

            switch (ev.GetTypeForControl(id))
            {
                case EventType.MouseDown when enabled && hover && ev.button == 0 && GUIUtility.hotControl == 0:
                    GUIUtility.hotControl = _fieldHotControl = id;
                    ev.Use();
                    break;
                case EventType.MouseDrag when captured:
                    ev.Use();
                    break;
                case EventType.MouseUp when captured && ev.button == 0:
                    CancelFieldPress();
                    ev.Use();
                    return hover;
                case EventType.Repaint:
                    DrawDropdown(bounds, scale, text, label, open, enabled, hover, captured && hover);
                    break;
            }
            return false;
        }

        private void DrawDropdown(Rect bounds, int scale, string text, GUIStyle label, bool open, bool enabled, bool hovered, bool pressed)
        {
            var (face, ink, fill) = Ae2DropdownAppearance.Field(open, enabled, hovered, pressed);
            var frameHeight = _skin.SourceSize(face)?.y ?? Ae2DropdownAppearance.BundledFieldHeight(face);
            var (top, height, caption) = Ae2DropdownAppearance.FieldLayout(frameHeight, scale);
            // The frame includes the arrow. The caption uses exactly the same origin,
            // including the state offset; neither changes the full-height click bounds.
            _skin.DrawDropdownFrame(new Rect(bounds.x, bounds.y + top, bounds.width, height), face, open, ink, fill);
            DrawFittedText(new Rect(bounds.x + caption.X, bounds.y + caption.Y, caption.Width, caption.Height),
                text, label, scale, Argb(ink));
        }

        private static Color32 Argb(uint value) => new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24));

        internal void CancelHiddenOptionPress(int first, int count)
        {
            if (_pressedOption >= 0 && (_pressedOption < first || _pressedOption >= first + count)) CancelOptionPress();
        }

        internal bool Option(int optionIndex, Rect bounds, int scale, string text, GUIStyle label, bool selected, bool enabled = true)
        {
            var id = GUIUtility.GetControlID(FocusType.Passive);
            var ev = Event.current;
            var hover = bounds.Contains(ev.mousePosition);
            enabled &= GUI.enabled;
            var captured = _optionHotControl == id && GUIUtility.hotControl == id;
            if (captured && (!enabled || _pressedOption != optionIndex || ev.type == EventType.Ignore))
            {
                CancelOptionPress();
                captured = false;
            }

            switch (ev.GetTypeForControl(id))
            {
                case EventType.MouseDown when enabled && hover && ev.button == 0 && GUIUtility.hotControl == 0:
                    GUIUtility.hotControl = _optionHotControl = id;
                    _pressedOption = optionIndex;
                    ev.Use();
                    break;
                case EventType.MouseDrag when captured:
                    ev.Use();
                    break;
                case EventType.MouseUp when captured && ev.button == 0:
                    CancelOptionPress();
                    ev.Use();
                    return hover;
                case EventType.Repaint:
                    // Only the row that received the press can depress; do not move the hit rectangle.
                    var face = Ae2DropdownAppearance.Option(enabled, hover, pressed: captured && hover, selected);
                    var (top, height, caption, ink) = Ae2DropdownAppearance.OptionLayout(face, scale);
                    var painted = new Rect(bounds.x, bounds.y + top, bounds.width, height);
                    _skin.DrawPanel(painted, face, scale, enabled && (hover || selected) ? Ae2TerminalSkin.Focus : Ae2TerminalSkin.Slot);
                    DrawFittedText(new Rect(bounds.x + caption.X, bounds.y + caption.Y, caption.Width, caption.Height),
                        text, label, scale, Argb(ink));
                    break;
            }
            return false;
        }

        // Keep one hit target across the row, with the switch before its caption.
        internal bool Toggle(Rect bounds, int scale, bool value, string text, GUIStyle label)
        {
            var clicked = GUI.Button(bounds, GUIContent.none, GUIStyle.none);
            if (Event.current.type == EventType.Repaint)
            {
                var face = Ae2ToggleAppearance.Face(value, bounds.Contains(Event.current.mousePosition));
                var width = (int)bounds.width / scale;
                var height = (int)bounds.height / scale;
                var size = _skin.SourceSize(face) ?? new Vector2Int(Ae2SettingsLayout.ToggleWidth, Ae2SettingsLayout.ToggleHeight);
                var (switchX, switchY, switchWidth, switchHeight) = Ae2SettingsLayout.ToggleControlBounds(
                    height, size.x, size.y, scale);
                _skin.Draw(new Rect(bounds.x + switchX, bounds.y + switchY, switchWidth, switchHeight), face, Color.clear);

                var (captionX, captionY, captionWidth, captionHeight) = Ae2SettingsLayout.ToggleCaptionBounds(
                    width, height, size.x, scale);
                var caption = new Rect(bounds.x + captionX, bounds.y + captionY, captionWidth, captionHeight);
                DrawFittedText(caption, text, label, scale);
            }
            return clicked ? !value : value;
        }

        private static void DrawFittedText(Rect caption, string text, GUIStyle label, int scale, Color? ink = null)
        {
            var content = new GUIContent(text);
            var fontSize = label.fontSize;
            var textColor = label.normal.textColor;
            try
            {
                if (ink is { } color) label.normal.textColor = color;
                while (label.fontSize > 6 * scale && label.CalcSize(content).x > caption.width) label.fontSize--;
                label.Draw(caption, content, false, false, false, false);
            }
            finally
            {
                label.fontSize = fontSize;
                if (ink.HasValue) label.normal.textColor = textColor;
            }
        }
    }
}
