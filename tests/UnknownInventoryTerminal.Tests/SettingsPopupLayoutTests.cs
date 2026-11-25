// SPDX-License-Identifier: GPL-3.0-only
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class SettingsPopupLayoutTests
{
    [TestMethod]
    [DataRow(150f, 240f, 3, false, 150f)] // fits exactly below
    [DataRow(20f, 200f, 3, true, 150f)]   // last row on the page
    [DataRow(60f, 80f, 3, true, 80f)]     // short viewport: keep real scrolling
    [DataRow(90f, 60f, 3, false, 90f)]
    [DataRow(300f, 200f, 6, false, 193f)] // honor the popup template's limit
    public void PopupFitsVisibleSpaceWithoutAddingEmptyRows(float below, float above, int options, bool opensAbove, float height)
    {
        var popup = new SettingsPopupLayout(50f, options, 193f, below, above);
        popup.OpenAbove.Should().Be(opensAbove);
        popup.Height.Should().Be(height);
    }
}
