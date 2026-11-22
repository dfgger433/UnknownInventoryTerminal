using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class TerminalDragGestureTests
{
    [TestMethod]
    public void UseWaitsForANewLeftPressAfterDrop()
    {
        TerminalDragGesture.Step(TerminalDragAction.Use, true, true, false, false, false, false)
            .Should().Be(TerminalDragStep.Wait, "releasing the drag key only arms the button");
        TerminalDragGesture.Step(TerminalDragAction.Use, true, true, false, false, true, false)
            .Should().Be(TerminalDragStep.Wait, "holding left without a new press cannot use the item");
        TerminalDragGesture.Step(TerminalDragAction.Use, true, true, false, true, true, false)
            .Should().Be(TerminalDragStep.Use);
    }

    [TestMethod]
    public void DrainRequiresAFreshPressAndStopsOnRelease()
    {
        TerminalDragStep AtButton(bool pressed, bool held, bool draining) =>
            TerminalDragGesture.Step(TerminalDragAction.Drain, true, true, false, pressed, held, draining);

        AtButton(false, false, false).Should().Be(TerminalDragStep.Wait);
        AtButton(false, true, false).Should().Be(TerminalDragStep.Wait);
        AtButton(true, true, false).Should().Be(TerminalDragStep.Drain);
        AtButton(false, true, true).Should().Be(TerminalDragStep.Drain);
        AtButton(false, false, true).Should().Be(TerminalDragStep.Cancel);
    }

    [TestMethod]
    [DataRow(false, true, false)] // item no longer eligible (empty, used, moved, or session blocked)
    [DataRow(true, false, false)] // pointer left the armed button
    [DataRow(true, true, true)] // right-click cancels even if left is pressed
    public void CancellationAlwaysWinsOverConfirmation(bool eligible, bool overTarget, bool rightPressed)
    {
        foreach (var action in new[] { TerminalDragAction.Drain, TerminalDragAction.Use })
            TerminalDragGesture.Step(action, eligible, overTarget, rightPressed, true, true, false)
                .Should().Be(TerminalDragStep.Cancel);
    }
}
