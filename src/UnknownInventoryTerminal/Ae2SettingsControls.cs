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
            var hover = bounds.Contains(Event.current.mousePosition);
            var face = Ae2DropdownAppearance.Field(open, enabled, hover, hover && Input.GetMouseButton(0));
            // Short states are inset from the top. The lower edge always meets the first option.
            var height = Math.Min(bounds.height, (_skin.SourceSize(face)?.y ?? Ae2SettingsLayout.FieldHeight) * scale);
            var painted = new Rect(bounds.x, bounds.yMax - height, bounds.width, height);
            _skin.Draw(painted, face, enabled && (hover || open) ? Ae2TerminalSkin.Focus : Ae2TerminalSkin.Slot);
            DrawText(painted, scale, text, label, 21);
            return enabled && GUI.Button(bounds, GUIContent.none, GUIStyle.none);
        }

        internal bool Option(Rect bounds, int scale, string text, GUIStyle label, bool selected, bool enabled = true)
        {
            var hover = bounds.Contains(Event.current.mousePosition);
            var face = Ae2DropdownAppearance.Option(enabled, hover, selected);
            // Preserve full-width edges and corners while keeping the row hit box stable on hover.
            _skin.DrawPanel(bounds, face, scale, enabled && (hover || selected) ? Ae2TerminalSkin.Focus : Ae2TerminalSkin.Slot);
            DrawText(bounds, scale, text, label, 4);
            return enabled && GUI.Button(bounds, GUIContent.none, GUIStyle.none);
        }

        private static void DrawText(Rect bounds, int scale, string text, GUIStyle label, int rightInset)
        {
            var caption = new Rect(bounds.x + 4 * scale, bounds.y, bounds.width - (4 + rightInset) * scale, bounds.height);
            GUI.Label(caption, text, label);
        }

        // Kept for future independent on/off settings. The caption shares the sprite's hover displacement.
        internal bool Toggle(Rect bounds, int scale, bool value, string text, GUIStyle label)
        {
            if (Event.current.type == EventType.Repaint)
            {
                var hover = bounds.Contains(Event.current.mousePosition);
                var face = value ? (hover ? Ae2Image.SettingsToggleOnHover : Ae2Image.SettingsToggleOn) :
                    (hover ? Ae2Image.SettingsToggleOffHover : Ae2Image.SettingsToggleOff);
                var size = _skin.SourceSize(face) ?? new Vector2Int(12, 12);
                var y = bounds.y + (hover ? scale : 0);
                _skin.Draw(new Rect(bounds.x, y + (int)(bounds.height - size.y * scale) / 2, size.x * scale, size.y * scale),
                    face, value ? Ae2TerminalSkin.Focus : Ae2TerminalSkin.Slot);
                GUI.Label(new Rect(bounds.x + 14 * scale, y, bounds.width - 14 * scale, bounds.height), text, label);
            }
            return GUI.Button(bounds, GUIContent.none, GUIStyle.none) ? !value : value;
        }
    }
}
