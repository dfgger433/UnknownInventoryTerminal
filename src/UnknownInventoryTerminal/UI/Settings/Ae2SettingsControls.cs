using System;
using UnityEngine;

namespace UnknownInventoryTerminal
{
    internal sealed class Ae2SettingsControls
    {
        private readonly Ae2TerminalSkin _skin;
        internal Ae2SettingsControls(Ae2TerminalSkin skin) => _skin = skin;

        internal bool Dropdown(Rect bounds, int scale, string text, GUIStyle label, bool open, bool enabled)
        {
            if (Event.current.type == EventType.Repaint)
            {
                var hover = bounds.Contains(Event.current.mousePosition);
                var face = Ae2DropdownAppearance.Field(open, enabled, hover, hover && Input.GetMouseButton(0));
                // The short faces are inset from the top. Their lower edge still meets the first option.
                var height = Math.Min(bounds.height, (_skin.SourceSize(face)?.y ?? Ae2SettingsLayout.FieldHeight) * scale);
                var painted = new Rect(bounds.x, bounds.yMax - height, bounds.width, height);
                _skin.Draw(painted, face, enabled && (hover || open) ? Ae2TerminalSkin.Focus : Ae2TerminalSkin.Slot);
                DrawText(painted, scale, text, label, field: true);
            }
            return enabled && GUI.Button(bounds, GUIContent.none, GUIStyle.none);
        }

        internal bool Option(Rect bounds, int scale, string text, GUIStyle label, bool selected, bool enabled = true)
        {
            if (Event.current.type == EventType.Repaint)
            {
                var hover = bounds.Contains(Event.current.mousePosition);
                var face = Ae2DropdownAppearance.Option(enabled, hover, selected);
                // Keep the caption inside the face and the hit box stable on hover.
                _skin.DrawPanel(bounds, face, scale, enabled && (hover || selected) ? Ae2TerminalSkin.Focus : Ae2TerminalSkin.Slot);
                DrawText(bounds, scale, text, label, field: false);
            }
            return enabled && GUI.Button(bounds, GUIContent.none, GUIStyle.none);
        }

        private static void DrawText(Rect bounds, int scale, string text, GUIStyle label, bool field)
        {
            var (x, y, width, height) = Ae2SettingsLayout.CaptionBounds((int)bounds.width, (int)bounds.height, scale, field);
            var caption = new Rect(bounds.x + x, bounds.y + y, width, height);
            var content = new GUIContent(text);
            var fontSize = label.fontSize;
            try
            {
                // Keep long translations on one line before clipping at the reserved arrow column.
                while (label.fontSize > 6 * scale && label.CalcSize(content).x > caption.width) label.fontSize--;
                // Draw a caption without allocating a GUI control ID on Repaint only.
                label.Draw(caption, content, false, false, false, false);
            }
            finally { label.fontSize = fontSize; }
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

        private static void DrawFittedText(Rect caption, string text, GUIStyle label, int scale)
        {
            var content = new GUIContent(text);
            var fontSize = label.fontSize;
            try
            {
                while (label.fontSize > 6 * scale && label.CalcSize(content).x > caption.width) label.fontSize--;
                label.Draw(caption, content, false, false, false, false);
            }
            finally { label.fontSize = fontSize; }
        }
    }
}
