using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class TerminalButtonInputTests
{
    [TestMethod]
    [DataRow(640, 480)]
    [DataRow(1920, 1080)]
    [DataRow(1921, 1081)]
    public void PaintedControlsHaveSeparateFullClickRegions(int width, int height)
    {
        var layout = new Ae2TerminalLayout(width, height, TerminalHeight.Full);
        Expect(143, -2, TerminalButton.Close);
        Expect(160, 17, TerminalButton.Close);
        Expect(161, 17, TerminalButton.None);
        Expect(149, 18, TerminalButton.None); // Scrollbar starts directly below close.
        Expect(79, 3, TerminalButton.None);   // Search is not a button.
        Expect(32, 20, TerminalButton.None);  // Storage cells are not chrome.
        Expect(8, 3, TerminalButton.Sort);
        Expect(25, 22, TerminalButton.Sort);
        Expect(8, 23, TerminalButton.None);
        Expect(8, 25, TerminalButton.Direction);
        Expect(8, 47, TerminalButton.Style);
        Expect(8, 69, TerminalButton.Settings);
        Expect(8, 91, TerminalButton.Return);
        Expect(8, 91, TerminalButton.None, canReturn: false);

        void Expect(int x, int y, TerminalButton button, bool canReturn = true) =>
            TerminalButtonInput.HitTest(layout, layout.Left + x * layout.Scale,
                layout.Top + y * layout.Scale, canReturn).Should().Be(button);
    }

    [TestMethod]
    public void LeftPressAndReleaseTriggersEachButtonOnceIndependentlyOfFocusAndRepaint()
    {
        var input = new TerminalButtonInput();
        foreach (var button in new[] { TerminalButton.Close, TerminalButton.Sort, TerminalButton.Direction, TerminalButton.Style, TerminalButton.Settings, TerminalButton.Return })
        {
            input.Press(button, 0).Should().BeTrue();
            input.Focused.Should().Be(button);
            input.Release(button, 0).Should().Be(button);
            input.Release(button, 0).Should().Be(TerminalButton.None);
        }
    }

    [TestMethod]
    public void ReleasesOutsideOnAnotherButtonOrAfterCloseDoNotActivate()
    {
        var input = new TerminalButtonInput();
        input.Press(TerminalButton.Sort, 0).Should().BeTrue();
        input.Release(TerminalButton.None, 0).Should().Be(TerminalButton.None);
        input.Press(TerminalButton.Style, 0).Should().BeTrue();
        input.Release(TerminalButton.Direction, 0).Should().Be(TerminalButton.None);
        input.Press(TerminalButton.Close, 0).Should().BeTrue();
        input.Reset();
        input.Focused.Should().Be(TerminalButton.None);
        input.Release(TerminalButton.Close, 0).Should().Be(TerminalButton.None);
    }

    [TestMethod]
    public void SettingsOpensAndClosesOnlyOnCompletedLeftClicks()
    {
        var input = new TerminalButtonInput();
        input.Press(TerminalButton.Settings, 0).Should().BeTrue();
        input.SettingsOpen.Should().BeFalse();
        input.Release(TerminalButton.Settings, 0).Should().Be(TerminalButton.Settings);
        input.SettingsOpen.Should().BeTrue();

        input.Press(TerminalButton.Settings, 1).Should().BeFalse();
        input.Release(TerminalButton.Settings, 1).Should().Be(TerminalButton.None);
        input.Press(TerminalButton.Settings, 0).Should().BeTrue();
        input.Release(TerminalButton.None, 0).Should().Be(TerminalButton.None);
        input.SettingsOpen.Should().BeTrue();

        input.Press(TerminalButton.Settings, 0).Should().BeTrue();
        input.SettingsOpen.Should().BeTrue();
        input.Release(TerminalButton.Settings, 0).Should().Be(TerminalButton.Settings);
        input.SettingsOpen.Should().BeFalse();
        input.Release(TerminalButton.Settings, 0).Should().Be(TerminalButton.None);
        input.SettingsOpen.Should().BeFalse();
    }

    [TestMethod]
    public void OnlyStyleAcceptsRightClickAndOtherButtonsCannotReleaseItsCapture()
    {
        var input = new TerminalButtonInput();
        input.Press(TerminalButton.Close, 1).Should().BeFalse();
        input.Release(TerminalButton.Close, 1).Should().Be(TerminalButton.None);
        input.Press(TerminalButton.Style, 1).Should().BeTrue();
        input.Press(TerminalButton.Style, 0).Should().BeFalse();
        input.Release(TerminalButton.Style, 0).Should().Be(TerminalButton.None);
        input.Release(TerminalButton.Style, 1).Should().Be(TerminalButton.Style);
    }
}
