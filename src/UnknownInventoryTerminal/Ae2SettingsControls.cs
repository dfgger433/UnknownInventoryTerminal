using UnityEngine;

namespace UnknownInventoryTerminal
{
    // The hit rectangle stays fixed; the face, caption and marker are painted in one moving group.
    internal sealed class Ae2SettingsControls
    {
        private readonly Ae2TerminalSkin _skin;
        internal Ae2SettingsControls(Ae2TerminalSkin skin) => _skin = skin;

        internal bool Button(Rect bounds, int scale, string text, string marker, GUIStyle label,
            GUIStyle icon, bool option = false, bool selected = false)
        {
            if (Event.current.type == EventType.Repaint)
            {
                var hover = bounds.Contains(Event.current.mousePosition);
                var highlight = hover || selected;
                var face = option ? (highlight ? Ae2Image.SettingsOptionSelected : Ae2Image.SettingsOption) :
                    (highlight ? Ae2Image.SettingsFieldHover : Ae2Image.SettingsField);
                var offset = hover ? scale : 0;
                _skin.DrawPanel(new Rect(bounds.x, bounds.y + offset, bounds.width, bounds.height - offset),
                    face, scale, highlight ? Ae2TerminalSkin.Focus : Ae2TerminalSkin.Slot);
                GUI.Label(new Rect(bounds.x + 3 * scale, bounds.y + offset, bounds.width - 15 * scale, bounds.height), text, label);
                GUI.Label(new Rect(bounds.xMax - 11 * scale, bounds.y + offset, 9 * scale, bounds.height), marker, icon);
            }
            return GUI.Button(bounds, GUIContent.none, GUIStyle.none);
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
