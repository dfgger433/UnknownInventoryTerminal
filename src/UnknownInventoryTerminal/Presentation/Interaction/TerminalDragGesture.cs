// SPDX-License-Identifier: GPL-3.0-only

namespace UnknownInventoryTerminal
{
    internal enum TerminalDragStep { Wait, Cancel, Drain, StopDrain, Use }
    internal enum TerminalDragStart { None, Arm, Confirm, Cancel }
    internal enum TerminalDragAfterAction { End, KeepArmed, ResumeDrag }

    internal static class TerminalDragGesture
    {
        // Keep the native 10px movement threshold so a click near an edge never drops an item.
        internal static bool CanDiscard(bool dragReleased, bool cancelPressed, bool insideTerminal,
            float x, float y, int screenWidth, int screenHeight, float travelSquared) =>
            dragReleased && !cancelPressed && !insideTerminal &&
            x >= 0 && x < screenWidth && y >= 0 && y < screenHeight && travelSquared > 100f;

        // Releasing over a Use/Drain button only arms it. If left is a separate input,
        // a new left press over the action can confirm even before the drag key is released.
        internal static TerminalDragStart Begin(bool dragReleased, bool leftPressed, bool rightPressed) =>
            rightPressed ? TerminalDragStart.Cancel : leftPressed ? TerminalDragStart.Confirm :
            dragReleased ? TerminalDragStart.Arm : TerminalDragStart.None;

        internal static TerminalDragStep Step(TerminalDragAction action, bool overEligibleTarget,
            bool cancelPressed, bool leftPressed, bool leftHeld, bool draining)
        {
            if (!overEligibleTarget || cancelPressed) return TerminalDragStep.Cancel;
            if (action == TerminalDragAction.Use) return leftPressed ? TerminalDragStep.Use : TerminalDragStep.Wait;
            // Native draining is incremental. The first click starts it, then every held
            // frame continues the same native call until release or cancellation.
            if (leftHeld && (leftPressed || draining)) return TerminalDragStep.Drain;
            return draining && !leftHeld ? TerminalDragStep.StopDrain : TerminalDragStep.Wait;
        }

        internal static TerminalDragAfterAction AfterAction(bool keepDragging, bool sameActionTarget, bool dragKeyStillHeld) =>
            !keepDragging || !sameActionTarget ? TerminalDragAfterAction.End :
            dragKeyStillHeld ? TerminalDragAfterAction.ResumeDrag : TerminalDragAfterAction.KeepArmed;
    }
}
