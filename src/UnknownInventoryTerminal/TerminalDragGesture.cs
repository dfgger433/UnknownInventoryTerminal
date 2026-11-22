namespace UnknownInventoryTerminal
{
    internal enum TerminalDragAction { Drain, Use }
    internal enum TerminalDragStep { Wait, Cancel, Drain, Use }

    // The original drag-key release only arms the button. Confirmation is a new left press.
    internal static class TerminalDragGesture
    {
        internal static TerminalDragStep Step(TerminalDragAction action, bool eligible, bool overTarget,
            bool cancelPressed, bool leftPressed, bool leftHeld, bool draining)
        {
            if (!eligible || !overTarget || cancelPressed) return TerminalDragStep.Cancel;
            if (action == TerminalDragAction.Use) return leftPressed ? TerminalDragStep.Use : TerminalDragStep.Wait;
            if (leftHeld && (leftPressed || draining)) return TerminalDragStep.Drain;
            return draining ? TerminalDragStep.Cancel : TerminalDragStep.Wait;
        }
    }
}
