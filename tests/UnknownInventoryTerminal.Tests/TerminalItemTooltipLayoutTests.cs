using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

/// <summary>Verifies the non-interactive tooltip stays on screen near the pointer.</summary>
[TestClass]
public sealed class TerminalItemTooltipLayoutTests
{
    /// <summary>Chooses the near side where possible, flips at the edges and clamps oversized placements.</summary>
    [TestMethod]
    [DataRow(80f, 60f, 140f, 70f, 90f, 70f)]
    [DataRow(310f, 200f, 140f, 70f, 160f, 120f)]
    [DataRow(165f, 120f, 160f, 130f, 4f, 4f)]
    public void AnchorsAndClampsToTheScreen(float pointerX, float pointerY, float width, float height, float x, float y)
    {
        var position = TerminalItemTooltipLayout.Place(pointerX, pointerY, width, height, 320, 240, 10, 4);

        position.Should().Be((x, y));
    }

    /// <summary>Small windows maintain a readable starting size; larger displays do not scale unboundedly.</summary>
    [TestMethod]
    [DataRow(480f, .9f)]
    [DataRow(720f, 1f)]
    [DataRow(1080f, 1.5f)]
    [DataRow(2160f, 2f)]
    public void PicksReadableTextScaleIndependentlyOfTheSpriteScale(float screenHeight, float expected)
    {
        TerminalItemTooltipLayout.ReadableScale(screenHeight).Should().Be(expected);
    }

    /// <summary>Bounds wrap long text, without making a panel wider than a narrow screen.</summary>
    [TestMethod]
    [DataRow(8f, 700f, 44f)]
    [DataRow(87.3f, 700f, 88f)]
    [DataRow(680f, 700f, 366f)]
    [DataRow(680f, 255f, 255f)]
    [DataRow(8f, 20f, 20f)]
    public void WrapsInsideTheContentWidth(float wanted, float available, float expected)
    {
        TerminalItemTooltipLayout.TextWidth(wanted, available).Should().Be(expected);
    }

    /// <summary>Expanded content only shrinks when all lines cannot fit at the preferred size.</summary>
    [TestMethod]
    [DataRow(1.5f, 750f, 420f, 1.5f)]
    [DataRow(1.5f, 750f, 600f, 1.25f)]
    [DataRow(1.5f, 750f, 0f, 1.5f)]
    public void ShrinksOnlyWhenTheContentExceedsTheScreen(float preferred, float available, float height, float expected)
    {
        TerminalItemTooltipLayout.FitScale(preferred, available, height).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(20f, 60f, 38f, 80f, false, 41f, 60f)] // Left toolbar: float over the board, not within the control.
    [DataRow(302f, 20f, 320f, 40f, false, 149f, 20f)]
    [DataRow(280f, 10f, 310f, 22f, true, 166f, 25f)]
    [DataRow(280f, 208f, 310f, 228f, true, 166f, 155f)]
    [DataRow(20f, 214f, 38f, 234f, false, 41f, 186f)]
    [DataRow(133f, 106f, 145f, 106f, false, 148f, 106f)] // Scroll track: align to cursor Y and clear the thumb.
    [DataRow(285f, 230f, 297f, 230f, false, 132f, 186f)]
    public void ControlHintsStayInsideTheScreen(float left, float top, float right, float bottom, bool below, float x, float y)
    {
        var position = TerminalItemTooltipLayout.PlaceControl(left, top, right, bottom, below,
            width: 150, height: 50, screenWidth: 320, screenHeight: 240, gap: 3, margin: 4);

        position.Should().Be((x, y));
    }
}
