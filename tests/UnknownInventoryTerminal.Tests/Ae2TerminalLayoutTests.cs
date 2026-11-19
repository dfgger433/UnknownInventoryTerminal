using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class Ae2TerminalLayoutTests
{
    [TestMethod]
    [DataRow(320, 240, 1, 3, 4)]
    [DataRow(640, 480, 2, 3, 4)]
    [DataRow(1280, 720, 3, 3, 4)]
    [DataRow(1920, 1080, 4, 5, 4)]
    [DataRow(3840, 2160, 4, 12, 4)]
    public void SixColumnBoardFitsUsingWholePixelTilesAndRows(int width, int height, int scale, int rows, int equipmentRows)
    {
        var layout = new Ae2TerminalLayout(width, height);

        Ae2TerminalLayout.Columns.Should().Be(6);
        layout.Scale.Should().Be(scale);
        layout.Rows.Should().Be(rows);
        layout.MainRows.Should().Be(1);
        layout.EquipmentRows.Should().Be(equipmentRows);
        layout.Width.Should().BeLessThanOrEqualTo(width);
        layout.Height.Should().BeLessThanOrEqualTo(height);
        layout.GridHeight.Should().Be(rows * 18 * scale);
        (layout.Width - layout.BoardLeft).Should().Be(141 * scale);
        Ae2TerminalLayout.ScrollbarLeft.Should().BeGreaterThanOrEqualTo(Ae2TerminalLayout.Inset + 6 * 18);
    }

    [TestMethod]
    [DataRow(320, 240, 19)]
    [DataRow(1921, 1081, 25)]
    [DataRow(2560, 1440, 120)]
    public void TerminalIsCenteredToTheNearestPixel(int width, int height, int equipmentSlots)
    {
        var layout = new Ae2TerminalLayout(width, height, 6, equipmentSlots);

        (width - 2 * layout.Left - layout.Width).Should().BeInRange(0, 1);
        (height - 2 * layout.Top - layout.Height).Should().BeInRange(0, 1);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    [DataRow(6, 1)]
    [DataRow(7, 2)]
    [DataRow(24, 4)]
    [DataRow(25, 5)]
    public void RowsKeepSixCellsIncludingUnboundTail(int slots, int rows)
    {
        Ae2TerminalLayout.ContentRows(slots).Should().Be(rows);
        Ae2TerminalLayout.PaddedCells(slots).Should().Be(rows * 6);
    }

    [TestMethod]
    public void EquipmentGrowsByOneFullRowWhenThePreviousRowFills()
    {
        var current = new Ae2TerminalLayout(1920, 1080, 6, 24);
        var expanded = new Ae2TerminalLayout(1920, 1080, 6, 25);

        expanded.EquipmentRows.Should().Be(current.EquipmentRows + 1);
        expanded.Width.Should().Be(current.Width);
        expanded.Height.Should().BeLessThanOrEqualTo(1080);
    }

    [TestMethod]
    public void ManyEquipmentSlotsPreserveMainAndStorageOnASmallScreen()
    {
        var layout = new Ae2TerminalLayout(320, 240, 6, 240);

        layout.Height.Should().BeLessThanOrEqualTo(240);
        layout.MainRows.Should().Be(1);
        layout.Rows.Should().BeGreaterThan(0);
        layout.EquipmentRows.Should().BeLessThan(Ae2TerminalLayout.ContentRows(240));
    }

    [TestMethod]
    public void CclSlotClaimsAppendWithoutMovingExistingCells()
    {
        var ids = new List<string>(Ae2EquipmentSlots.Vanilla);
        var vanillaCount = ids.Count;

        vanillaCount.Should().Be(19);
        Ae2EquipmentSlots.Append(ids, new[] { "HAT, utility", "  utility , , collar", "belt", null }).Should().BeTrue();
        Ae2EquipmentSlots.Append(ids, new[] { "utility", "collar", "lower-pouch" }).Should().BeTrue();
        Ae2EquipmentSlots.Append(ids, new[] { "lower-pouch" }).Should().BeFalse();
        ids.GetRange(0, vanillaCount).Should().Equal(Ae2EquipmentSlots.Vanilla);
        ids.GetRange(vanillaCount, 3).Should().Equal("utility", "collar", "lower-pouch");
    }

    [TestMethod]
    [DataRow("hat", "hat", true)]
    [DataRow("hat", "Hat", false)]
    [DataRow(" hat , UTILITY ", "utility", true)]
    [DataRow("utility, Utility", "Utility", false)]
    [DataRow("hat, utility", "belt", false)]
    [DataRow("", "belt", false)]
    public void EquipmentDropOnlyAcceptsAClaimedSlot(string declared, string cell, bool expected)
    {
        Ae2EquipmentSlots.ClaimsSlot(declared, cell).Should().Be(expected);
    }
}
