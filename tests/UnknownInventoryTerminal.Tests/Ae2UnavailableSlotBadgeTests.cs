// SPDX-License-Identifier: GPL-3.0-only
using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class Ae2UnavailableSlotBadgeTests
{
    [TestMethod]
    public void BadgeUsesTheCleanStatesAtlasCrop()
    {
        var style = Ae2ScreenStyle.Parse(System.IO.File.ReadAllText(
            TerminalAssets.ScreenPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "assets", "unknowninventoryterminal"))));
        style.TryGet(Ae2Image.UnavailableSlotBadge, out var region).Should().BeTrue();
        (region.Texture, region.X, region.Y, region.Width, region.Height).Should().Be(("gui/terminal/terminal_controls.png", 16, 97, 16, 16));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void BadgeKeepsTheVisibleIconAspectRatioWhenCentered(int scale)
    {
        var visible = (Width: 10, Height: 11);
        var (x, y, width, height) = Ae2UnavailableSlotBadge.Bounds(visible, scale);
        var cell = Ae2TerminalLayout.Pitch * scale;

        width.Should().Be(10 * scale);
        height.Should().Be(11 * scale);
        width.Should().BeLessThan(cell);
        height.Should().BeLessThan(cell);
        (x * 2 + width).Should().Be(cell);
        Math.Abs(y * 2 + height - cell).Should().BeLessThanOrEqualTo(1);
    }
}
