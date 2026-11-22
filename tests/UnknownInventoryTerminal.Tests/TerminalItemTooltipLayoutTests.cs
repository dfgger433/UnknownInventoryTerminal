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
}
