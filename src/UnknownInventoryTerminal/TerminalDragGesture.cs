namespace UnknownInventoryTerminal
{
    internal enum TerminalDragStep { Wait, Cancel, Drain, StopDrain, Use }
    internal enum TerminalDragStart { None, Arm, Confirm, Cancel }
    internal enum TerminalDragAfterAction { End, KeepArmed, ResumeDrag }

    internal static class TerminalDragGesture
    {
        // Releasing the drag key alone never performs an action. If left is a separate
        // input, a new left press over the action can confirm even before the drag key is released.
        internal static TerminalDragStart Begin(bool dragReleased, bool leftPressed, bool rightPressed) =>
            rightPressed ? TerminalDragStart.Cancel : leftPressed ? TerminalDragStart.Confirm :
            dragReleased ? TerminalDragStart.Arm : TerminalDragStart.None;

        internal static TerminalDragStep Step(TerminalDragAction action, bool overEligibleTarget,
            bool cancelPressed, bool leftPressed, bool leftHeld, bool draining)
        {
            if (!overEligibleTarget || cancelPressed) return TerminalDragStep.Cancel;
            if (action == TerminalDragAction.Use) return leftPressed ? TerminalDragStep.Use : TerminalDragStep.Wait;
            if (leftPressed && leftHeld) return TerminalDragStep.Drain;
            return draining && !leftHeld ? TerminalDragStep.StopDrain : TerminalDragStep.Wait;
        }

        internal static TerminalDragAfterAction AfterAction(bool keepDragging, bool sameActionTarget, bool dragKeyStillHeld) =>
            !keepDragging || !sameActionTarget ? TerminalDragAfterAction.End :
            dragKeyStillHeld ? TerminalDragAfterAction.ResumeDrag : TerminalDragAfterAction.KeepArmed;
    }
}
