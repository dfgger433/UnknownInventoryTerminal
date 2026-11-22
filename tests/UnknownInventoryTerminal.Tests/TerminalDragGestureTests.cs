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
