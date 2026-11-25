// SPDX-License-Identifier: GPL-3.0-only
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class TerminalDragGestureTests
{
    [TestMethod]
    [DataRow(true, false, false, 24f, 60f, 121f, true)]
    [DataRow(false, false, false, 24f, 60f, 121f, false)] // still dragging
    [DataRow(true, true, false, 24f, 60f, 121f, false)] // cancel and release together
    [DataRow(true, false, true, 24f, 60f, 121f, false)] // anywhere inside, including blank chrome
    [DataRow(true, false, false, 24f, 60f, 100f, false)] // exactly 10px is not a drag
    [DataRow(true, false, false, 24f, 60f, 81f, false)]
    [DataRow(true, false, false, -1f, 60f, 121f, false)]
    [DataRow(true, false, false, 320f, 60f, 121f, false)]
    [DataRow(true, false, false, 24f, -1f, 121f, false)]
    [DataRow(true, false, false, 24f, 240f, 121f, false)]
    [DataRow(true, false, false, 319f, 239f, 121f, true)]
    public void DiscardRequiresReleaseOutsideTerminalButInsideGameScreen(bool released, bool cancelled, bool inside,
        float x, float y, float travelSquared, bool expected)
    {
        TerminalDragGesture.CanDiscard(released, cancelled, inside, x, y, 320, 240, travelSquared).Should().Be(expected);
    }

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
    [DataRow(false, true, false, (int)TerminalDragAfterAction.End)]
    [DataRow(false, true, true, (int)TerminalDragAfterAction.End)]
    [DataRow(true, false, false, (int)TerminalDragAfterAction.End)]
    [DataRow(true, false, true, (int)TerminalDragAfterAction.End)]
    [DataRow(true, true, false, (int)TerminalDragAfterAction.KeepArmed)]
    [DataRow(true, true, true, (int)TerminalDragAfterAction.ResumeDrag)]
    public void ActionOnlyRetainsAnEligibleItemWhenTheSwitchIsOn(bool keep, bool sameTarget, bool held, int expected)
    {
        TerminalDragGesture.AfterAction(keep, sameTarget, held).Should().Be((TerminalDragAfterAction)expected);
    }

    [TestMethod]
    public void RetainedUseAllowsAnotherClickButNotAutomaticRepeat()
    {
        TerminalDragStep AtUse(bool pressed, bool held) =>
            TerminalDragGesture.Step(TerminalDragAction.Use, true, false, pressed, held, false);

        AtUse(true, true).Should().Be(TerminalDragStep.Use);
        TerminalDragGesture.AfterAction(keepDragging: true, sameActionTarget: true, dragKeyStillHeld: false)
            .Should().Be(TerminalDragAfterAction.KeepArmed);
        TerminalDragGesture.Step(TerminalDragAction.Use, true, false, false, true, false)
            .Should().Be(TerminalDragStep.Wait, "the retained item must not auto-use while the click stays held");
        AtUse(false, true).Should().Be(TerminalDragStep.Wait, "holding the first click must not use it again");
        AtUse(false, false).Should().Be(TerminalDragStep.Wait, "releasing is not a second use");
        AtUse(true, true).Should().Be(TerminalDragStep.Use);
    }

    [TestMethod]
    public void DrainContinuesWhileHeldAndStopsAfterRelease()
    {
        TerminalDragStep AtButton(bool pressed, bool held, bool draining) =>
            TerminalDragGesture.Step(TerminalDragAction.Drain, true, false, pressed, held, draining);

        AtButton(false, false, false).Should().Be(TerminalDragStep.Wait);
        AtButton(false, true, false).Should().Be(TerminalDragStep.Wait);
        AtButton(true, true, false).Should().Be(TerminalDragStep.Drain);
        AtButton(false, true, true).Should().Be(TerminalDragStep.Drain, "native draining continues on each held frame");
        AtButton(false, false, true).Should().Be(TerminalDragStep.StopDrain);
        AtButton(true, true, false).Should().Be(TerminalDragStep.Drain, "a later click starts one more drain step");
    }

    [TestMethod]
    [DataRow(false, true, false, (int)TerminalDragAfterAction.End)]
    [DataRow(true, true, false, (int)TerminalDragAfterAction.KeepArmed)]
    [DataRow(true, true, true, (int)TerminalDragAfterAction.ResumeDrag)]
    [DataRow(true, false, false, (int)TerminalDragAfterAction.End)]
    public void DrainUsesTheSharedRetentionSwitchWithoutAutoRepeating(bool keep, bool sameTarget, bool dragHeld, int expected)
    {
        TerminalDragStep AtButton(bool pressed, bool held, bool draining) =>
            TerminalDragGesture.Step(TerminalDragAction.Drain, true, false, pressed, held, draining);

        AtButton(true, true, false).Should().Be(TerminalDragStep.Drain);
        AtButton(false, true, false).Should().Be(TerminalDragStep.Wait);
        TerminalDragGesture.AfterAction(keep, sameTarget, dragHeld).Should().Be((TerminalDragAfterAction)expected);
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
