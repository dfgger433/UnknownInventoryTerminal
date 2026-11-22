using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class TerminalDragGestureTests
{
    [TestMethod]
    public void DragCanConfirmWithLeftBeforeReleasingASeparateDragKey()
    {
        TerminalDragGesture.Begin(dragReleased: false, leftPressed: false, rightPressed: false)
            .Should().Be(TerminalDragStart.None);
        TerminalDragGesture.Begin(dragReleased: false, leftPressed: true, rightPressed: false)
            .Should().Be(TerminalDragStart.Confirm);
        TerminalDragGesture.Begin(dragReleased: true, leftPressed: false, rightPressed: false)
            .Should().Be(TerminalDragStart.Arm, "releasing a left-mouse drag must not confirm it");
        TerminalDragGesture.Begin(dragReleased: true, leftPressed: true, rightPressed: true)
            .Should().Be(TerminalDragStart.Cancel);
    }

    [TestMethod]
    public void UseWaitsForANewLeftPressAfterDrop()
    {
        TerminalDragGesture.Step(TerminalDragAction.Use, true, false, false, false, false)
            .Should().Be(TerminalDragStep.Wait, "releasing the drag key only arms the button");
        TerminalDragGesture.Step(TerminalDragAction.Use, true, false, false, true, false)
            .Should().Be(TerminalDragStep.Wait, "holding left without a new press cannot use the item");
        TerminalDragGesture.Step(TerminalDragAction.Use, true, false, true, true, false)
            .Should().Be(TerminalDragStep.Use);
    }

    [TestMethod]
    [DataRow(false, true, false, (int)TerminalDragAfterUse.End)]
    [DataRow(false, true, true, (int)TerminalDragAfterUse.End)]
    [DataRow(true, false, false, (int)TerminalDragAfterUse.End)]
    [DataRow(true, false, true, (int)TerminalDragAfterUse.End)]
    [DataRow(true, true, false, (int)TerminalDragAfterUse.KeepArmed)]
    [DataRow(true, true, true, (int)TerminalDragAfterUse.ResumeDrag)]
    public void UseOnlyRetainsAnEligibleItemWhenTheSwitchIsOn(bool keep, bool sameTarget, bool held, int expected)
    {
        TerminalDragGesture.AfterUse(keep, sameTarget, held).Should().Be((TerminalDragAfterUse)expected);
    }

    [TestMethod]
    public void RetainedUseAllowsAnotherClickButNotAutomaticRepeat()
    {
        TerminalDragStep AtUse(bool pressed, bool held) =>
            TerminalDragGesture.Step(TerminalDragAction.Use, true, false, pressed, held, false);

        AtUse(true, true).Should().Be(TerminalDragStep.Use);
        TerminalDragGesture.AfterUse(keepDragging: true, sameUseTarget: true, dragKeyStillHeld: false)
            .Should().Be(TerminalDragAfterUse.KeepArmed);
        AtUse(false, true).Should().Be(TerminalDragStep.Wait, "holding the first click must not use it again");
        AtUse(false, false).Should().Be(TerminalDragStep.Wait, "releasing is not a second use");
        AtUse(true, true).Should().Be(TerminalDragStep.Use);
    }

    [TestMethod]
    public void DrainRequiresAFreshPressAndStopsOnRelease()
    {
        TerminalDragStep AtButton(bool pressed, bool held, bool draining) =>
            TerminalDragGesture.Step(TerminalDragAction.Drain, true, false, pressed, held, draining);

        AtButton(false, false, false).Should().Be(TerminalDragStep.Wait);
        AtButton(false, true, false).Should().Be(TerminalDragStep.Wait);
        AtButton(true, true, false).Should().Be(TerminalDragStep.Drain);
        AtButton(false, true, true).Should().Be(TerminalDragStep.Drain);
        AtButton(false, false, true).Should().Be(TerminalDragStep.Cancel);
    }

    [TestMethod]
    [DataRow(false, false)] // pointer left, or the action is no longer eligible
    [DataRow(true, true)] // right-click cancels even if left is pressed
    public void CancellationAlwaysWinsOverConfirmation(bool overEligibleTarget, bool rightPressed)
    {
        foreach (var action in new[] { TerminalDragAction.Drain, TerminalDragAction.Use })
            TerminalDragGesture.Step(action, overEligibleTarget, rightPressed, true, true, false)
                .Should().Be(TerminalDragStep.Cancel);
    }
}
