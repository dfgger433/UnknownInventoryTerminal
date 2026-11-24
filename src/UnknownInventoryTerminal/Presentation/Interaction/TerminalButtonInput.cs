namespace UnknownInventoryTerminal
{
    internal enum TerminalButton { None, Close, Sort, Direction, Style, Settings, Return, SettingsBack }

    // The same rectangles drive drawing and input, but clicks do not depend on IMGUI control IDs or repaint order.
    internal sealed class TerminalButtonInput
    {
        private TerminalButton _pressed;
        private int _pointerButton;
        internal TerminalButton Focused { get; private set; }
        internal bool SettingsOpen { get; private set; }
        internal const int SettingsCloseX = Ae2SettingsLayout.PanelWidth - 22, SettingsCloseY = 2;

        internal static TerminalButton SettingsHitTest(float x, float y) =>
            x >= SettingsCloseX && x < SettingsCloseX + Ae2TerminalLayout.ButtonWidth &&
            y >= SettingsCloseY && y < SettingsCloseY + Ae2TerminalLayout.ButtonHeight
                ? TerminalButton.SettingsBack : TerminalButton.None;

        internal static (int X, int Y) Origin(TerminalButton button) => button == TerminalButton.Close
            ? (Ae2TerminalLayout.CloseX, Ae2TerminalLayout.CloseY)
            : (Ae2TerminalLayout.ToolbarX, Ae2TerminalLayout.ToolbarY((int)button - (int)TerminalButton.Sort));

        internal static TerminalButton HitTest(Ae2TerminalLayout layout, float screenX, float screenY, bool canReturn)
        {
            var x = (screenX - layout.Left) / layout.Scale;
            var y = (screenY - layout.Top) / layout.Scale;
            if (Contains(TerminalButton.Close)) return TerminalButton.Close;
            var last = canReturn ? TerminalButton.Return : TerminalButton.Settings;
            for (var button = TerminalButton.Sort; button <= last; button++)
                if (Contains(button)) return button;
            return TerminalButton.None;

            bool Contains(TerminalButton button)
            {
                var (left, top) = Origin(button);
                return x >= left && x < left + Ae2TerminalLayout.ButtonWidth &&
                    y >= top && y < top + Ae2TerminalLayout.ButtonHeight;
            }
        }

        internal bool Press(TerminalButton hit, int pointerButton)
        {
            if (_pressed != TerminalButton.None) return false;
            Focused = TerminalButton.None;
            if (hit == TerminalButton.None || (pointerButton != 0 && (pointerButton != 1 || hit != TerminalButton.Style))) return false;
            Focused = _pressed = hit;
            _pointerButton = pointerButton;
            return true;
        }

        internal TerminalButton Release(TerminalButton hit, int pointerButton)
        {
            if (_pressed == TerminalButton.None || _pointerButton != pointerButton) return TerminalButton.None;
            var pressed = _pressed;
            _pressed = TerminalButton.None;
            if (hit != pressed) return TerminalButton.None;
            if (pressed == TerminalButton.Settings) SettingsOpen = true;
            if (pressed == TerminalButton.SettingsBack) SettingsOpen = false;
            return pressed;
        }

        internal void Reset()
        {
            Focused = _pressed = TerminalButton.None;
            SettingsOpen = false;
        }
    }
}
