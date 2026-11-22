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

        // The caption shares the sprite's hover displacement; keep one control ID for the entire row.
        internal bool Toggle(Rect bounds, int scale, bool value, string text, GUIStyle label)
        {
            var clicked = GUI.Button(bounds, GUIContent.none, GUIStyle.none);
            if (Event.current.type == EventType.Repaint)
            {
                var hover = bounds.Contains(Event.current.mousePosition);
                var face = value ? (hover ? Ae2Image.SettingsToggleOnHover : Ae2Image.SettingsToggleOn) :
                    (hover ? Ae2Image.SettingsToggleOffHover : Ae2Image.SettingsToggleOff);
                var size = _skin.SourceSize(face) ?? new Vector2Int(12, 12);
                var y = bounds.y + (hover ? scale : 0);
                _skin.Draw(new Rect(bounds.x, y + (int)(bounds.height - size.y * scale) / 2, size.x * scale, size.y * scale),
                    face, value ? Ae2TerminalSkin.Focus : Ae2TerminalSkin.Slot);
                var caption = new Rect(bounds.x + 14 * scale, y, bounds.width - 14 * scale, bounds.height);
                label.Draw(caption, new GUIContent(text), false, false, false, false);
            }
            return clicked ? !value : value;
        }
    }
}
