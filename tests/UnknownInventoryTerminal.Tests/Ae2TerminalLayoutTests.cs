// SPDX-License-Identifier: GPL-3.0-only
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class Ae2TerminalLayoutTests
{
    [TestMethod]
    [DataRow(320, 240, 1, 3)]
    [DataRow(640, 480, 2, 3)]
    [DataRow(1280, 720, 4, 3)]
    [DataRow(1920, 1080, 4, 3)]
    // 2160 / 4 = 540 UI px leaves room for 23 rows; Small takes a quarter of them, as in AE2.
    [DataRow(3840, 2160, 4, 5)]
    public void SmallBoardIsTheDrawnPngAtAWholePixelScale(int width, int height, int scale, int rows)
    {
        var layout = new Ae2TerminalLayout(width, height);

        Ae2TerminalLayout.Columns.Should().Be(6);
        layout.Scale.Should().Be(scale);
        layout.Rows.Should().Be(rows);
        layout.EquipmentRows.Should().Be(3, "terminal.png draws three equipment rows");
        layout.MainRows.Should().Be(1, "terminal.png draws one main-inventory row");
        layout.Width.Should().BeLessThanOrEqualTo(width);
        layout.Height.Should().BeLessThanOrEqualTo(height);
        layout.GridHeight.Should().Be(rows * 18 * scale);
        (layout.Width - layout.BoardLeft).Should().Be(141 * scale);
        layout.Height.Should().Be((170 + (rows - 3) * 18) * scale, "three rows are the 170 px board drawn in terminal.png");
        (layout.Height - layout.PanelHeight).Should().Be(Ae2TerminalLayout.Header * scale);
    }

    [TestMethod]
    public void SectionsFollowTheGridsDrawnInTerminalPng()
    {
        var layout = new Ae2TerminalLayout(1280, 720);
        Ae2TerminalLayout.Header.Should().Be(17);
        layout.StatusTop.Should().Be(71);
        layout.EquipmentTop.Should().Be(85);
        layout.MainTop.Should().Be(143);
        (layout.MainTop + 18).Should().BeLessThan(layout.BoardHeight);
        Ae2TerminalLayout.GrooveLeft.Should().BeGreaterThan(Ae2TerminalLayout.Inset + Ae2TerminalLayout.GridWidth);
    }

    [TestMethod]
    [DataRow(1, 3)]
    [DataRow(2, 4)]
    [DataRow(3, 6)]
    [DataRow(4, 8)]
    public void TerminalStyleUsesAe2ShareOfTheRowsThatFit(int styleValue, int rows)
    {
        var style = (TerminalHeight)styleValue;
        // 1080 / 4 = 270 UI px: (270 - 2 * 4 margin - 17 header - 99 bottom) / 18 = 8 possible rows.
        var layout = new Ae2TerminalLayout(1920, 1080, style);

        layout.Scale.Should().Be(4, "the height style never changes the pixel scale");
        layout.Rows.Should().Be(rows);
        layout.Height.Should().Be((17 + rows * 18 + 99) * 4);
        layout.Height.Should().BeLessThanOrEqualTo(1080 - 2 * Ae2TerminalLayout.Margin * 4);
        layout.StatusTop.Should().Be(17 + rows * 18, "equipment and main inventory move down with the storage");
        layout.MainTop.Should().Be(layout.StatusTop + 72);
    }

    [TestMethod]
    [DataRow(1280, 720)]
    [DataRow(1366, 768)]
    public void ShortScreensKeepTheThreeRowBoardForEveryStyle(int width, int height)
    {
        foreach (TerminalHeight style in System.Enum.GetValues(typeof(TerminalHeight)))
            new Ae2TerminalLayout(width, height, style).Rows.Should().Be(Ae2TerminalLayout.MinRows);
    }

    [TestMethod]
    [DataRow(1, false, 2)]
    [DataRow(4, false, 1)]
    [DataRow(1, true, 4)]
    [DataRow(3, true, 2)]
    public void TerminalStyleButtonCyclesLikeAe2(int current, bool backwards, int next)
    {
        Ae2TerminalLayout.Next((TerminalHeight)current, backwards).Should().Be((TerminalHeight)next);
    }

    [TestMethod]
    [DataRow((int)TerminalHeight.Small, 52)]
    [DataRow((int)TerminalHeight.Full, 142)]
    public void SliderIsCenteredAndTravelsInsideThePaintedEndBorders(int styleValue, int trackHeight)
    {
        var layout = new Ae2TerminalLayout(1920, 1080, (TerminalHeight)styleValue);
        var left = Ae2TerminalLayout.ThumbLeft(Ae2TerminalLayout.DefaultThumbWidth);

        left.Should().Be(121);
        (left * 2 + Ae2TerminalLayout.DefaultThumbWidth).Should().Be(2 * Ae2TerminalLayout.GrooveLeft + Ae2TerminalLayout.GrooveWidth);
        (Ae2TerminalLayout.Header + Ae2TerminalLayout.GrooveBorder).Should().Be(18);
        layout.ScrollTrackTop().Should().Be(Ae2TerminalLayout.GrooveBorder);
        layout.ScrollTrackTop(hasPinnedPager: true).Should().Be(Ae2TerminalLayout.Pitch + Ae2TerminalLayout.GrooveBorder);
        layout.ScrollTrackHeight().Should().Be(trackHeight);
        layout.ScrollTrackHeight(hasPinnedPager: true).Should().Be(trackHeight - Ae2TerminalLayout.Pitch);
        (Ae2TerminalLayout.Header + layout.ScrollTrackTop() + trackHeight).Should().Be(layout.StatusTop - 1);
        (Ae2TerminalLayout.Header + layout.ScrollTrackTop(hasPinnedPager: true) +
            layout.ScrollTrackHeight(hasPinnedPager: true)).Should().Be(layout.StatusTop - 1);
    }

    [TestMethod]
    [DataRow(320, 240, (int)TerminalHeight.Small)]
    [DataRow(1920, 1080, (int)TerminalHeight.Full)]
    public void DragActionsPackOnlyEligibleFacesBelowTheToolbar(int width, int height, int style)
    {
        var layout = new Ae2TerminalLayout(width, height, (TerminalHeight)style);
        var scale = layout.Scale;
        var topInset = Ae2TerminalLayout.ButtonBarInsetY * scale;
        var pitch = Ae2TerminalLayout.ToolbarPitch * scale;
        layout.DragActionBackgroundBounds(TerminalDragAction.None).Should().Be((0, 0, 0, 0));

        for (var mask = 1; mask <= (int)AllDragActions; mask++)
        {
            var actions = (TerminalDragAction)mask;
            var bar = layout.DragActionBackgroundBounds(actions);
            bar.Y.Should().BeGreaterThan((Ae2TerminalLayout.ButtonBarY +
                Ae2TerminalLayout.ButtonBarHeight(Ae2TerminalLayout.ToolbarButtons)) * scale);
            bar.Width.Should().Be(Ae2TerminalLayout.ButtonBarWidth * scale);
            if (layout.Rows == Ae2TerminalLayout.MinRows) (bar.Y + bar.Height).Should().Be(layout.Height);
            else (bar.Y + bar.Height).Should().BeLessThan(layout.Height);

            var count = 0;
            foreach (var action in DragActions)
            {
                if ((actions & action) == 0) continue;
                var face = layout.DragActionBounds(actions, action);
                face.X.Should().Be(Ae2TerminalLayout.ToolbarX * scale);
                face.Y.Should().Be(bar.Y + topInset + count * pitch);
                (face.X + face.Width).Should().BeLessThan((Ae2TerminalLayout.Rail + Ae2TerminalLayout.Inset) * scale);
                count++;
            }
            bar.Height.Should().Be(Ae2TerminalLayout.ButtonBarHeight(count) * scale);
        }
    }

    private static readonly TerminalDragAction[] DragActions =
        { TerminalDragAction.Drain, TerminalDragAction.Use, TerminalDragAction.Pin };
    private const TerminalDragAction AllDragActions = TerminalDragAction.Drain | TerminalDragAction.Use | TerminalDragAction.Pin;

    [TestMethod]
    [DataRow(320, 240, (int)TerminalHeight.Small, 1, 3, 101)]
    [DataRow(640, 480, (int)TerminalHeight.Small, 2, 3, 101)]
    [DataRow(768, 534, (int)TerminalHeight.Full, 3, 3, 101)]
    [DataRow(1280, 720, (int)TerminalHeight.Full, 4, 3, 101)]
    [DataRow(1920, 1080, (int)TerminalHeight.Medium, 4, 4, 99)]
    [DataRow(1920, 1080, (int)TerminalHeight.Full, 4, 8, 170)]
    public void ArmingKeepsTheExactFaceInPlaceWhenItsSiblingsDisappear(int width, int height, int style, int scale, int rows, int drainY)
    {
        var layout = new Ae2TerminalLayout(width, height, (TerminalHeight)style);
        layout.Scale.Should().Be(scale);
        layout.Rows.Should().Be(rows);
        layout.DragActionBounds(AllDragActions, TerminalDragAction.Drain).Y.Should().Be(drainY * scale);

        foreach (var actions in new[] { AllDragActions, TerminalDragAction.Drain | TerminalDragAction.Pin,
                     TerminalDragAction.Use | TerminalDragAction.Pin, TerminalDragAction.Drain, TerminalDragAction.Use })
        foreach (var action in new[] { TerminalDragAction.Drain, TerminalDragAction.Use })
        {
            if ((actions & action) == 0) continue;
            var face = layout.DragActionBounds(actions, action);
            var bar = layout.DragActionBackgroundBounds(actions, armed: action);
            bar.Height.Should().Be(Ae2TerminalLayout.ButtonBarHeight(1) * scale);
            (face.Y - bar.Y).Should().Be(Ae2TerminalLayout.ButtonBarInsetY * scale);
            (bar.Y + bar.Height - face.Y - face.Height).Should().Be(5 * scale);
            layout.HitsDragAction(actions, action, layout.Left + face.X, layout.Top + face.Y).Should().BeTrue();
        }
    }

    [TestMethod]
    [DataRow(320, 240, (int)TerminalHeight.Small)]
    [DataRow(1281, 721, (int)TerminalHeight.Full)]
    public void DragActionHitsUseTheExactAbsoluteFaceRatherThanTheBackground(int width, int height, int style)
    {
        var layout = new Ae2TerminalLayout(width, height, (TerminalHeight)style);
        foreach (var actions in new[] { AllDragActions, TerminalDragAction.Drain | TerminalDragAction.Pin })
        foreach (var action in DragActions)
        {
            var (x, y, w, h) = layout.DragActionBounds(actions, action);
            var left = layout.Left + x;
            var top = layout.Top + y;
            if ((actions & action) == 0)
            {
                layout.HitsDragAction(actions, action, left, top).Should().BeFalse();
                continue;
            }
            layout.HitsDragAction(actions, action, left, top).Should().BeTrue();
            layout.HitsDragAction(actions, action, left + w - 1, top + h - 1).Should().BeTrue();
            layout.HitsDragAction(actions, action, left - 1, top).Should().BeFalse();
            layout.HitsDragAction(actions, action, left, top - 1).Should().BeFalse();
            layout.HitsDragAction(actions, action, left + w, top).Should().BeFalse();
            layout.HitsDragAction(actions, action, left, top + h).Should().BeFalse();
            foreach (var other in DragActions)
                if (other != action) layout.HitsDragAction(actions, other, left, top).Should().BeFalse();
        }
    }

    [TestMethod]
    public void ToolbarSitsOnTheVerticalButtonBarLikeAe2()
    {
        // AE2: the bar is 21 px wide and its right edge covers the board's 2 px left border.
        (Ae2TerminalLayout.ButtonBarX + Ae2TerminalLayout.ButtonBarWidth).Should().Be(Ae2TerminalLayout.Rail + 2);
        (Ae2TerminalLayout.ToolbarX + Ae2TerminalLayout.ButtonWidth).Should().Be(Ae2TerminalLayout.Rail + 2);
        Ae2TerminalLayout.ButtonBarX.Should().BeGreaterThanOrEqualTo(0, "the bar stays inside the window");
        Ae2TerminalLayout.ButtonBarY.Should().Be(0);
        Ae2TerminalLayout.ToolbarY(1).Should().Be(25, "AE2 places 16 px buttons 6 px apart, starting at y = 3");
        var barWithReturn = Ae2TerminalLayout.ButtonBarHeight(Ae2TerminalLayout.ToolbarButtons + 1);
        Ae2TerminalLayout.ToolbarButtons.Should().Be(4, "the close button is separate and settings is the fourth sidebar action");
        (Ae2TerminalLayout.ButtonBarY + barWithReturn).Should().BeLessThanOrEqualTo(Ae2TerminalLayout.BaseBoardHeight);
        (Ae2TerminalLayout.ToolbarY(Ae2TerminalLayout.ToolbarButtons) + Ae2TerminalLayout.ButtonHeight).Should().BeLessThanOrEqualTo(
            Ae2TerminalLayout.ButtonBarY + barWithReturn - Ae2TerminalLayout.ButtonBarBottomBorder);
    }

    [TestMethod]
    public void CloseRestoresTheOriginalRightInsetWithoutOverlappingSearchOrScroll()
    {
        (Ae2TerminalLayout.Rail + Ae2TerminalLayout.BoardWidth - Ae2TerminalLayout.CloseX - Ae2TerminalLayout.ButtonWidth)
            .Should().Be(4);
        (Ae2TerminalLayout.CloseY + Ae2TerminalLayout.ChromeTopPadding).Should().Be(0);
        (Ae2TerminalLayout.CloseY + Ae2TerminalLayout.ButtonHeight)
            .Should().BeLessThanOrEqualTo(Ae2TerminalLayout.Header + Ae2TerminalLayout.GrooveBorder);
        (Ae2TerminalLayout.SearchX + Ae2TerminalLayout.SearchWidth).Should().BeLessThan(Ae2TerminalLayout.CloseX);
    }

    [TestMethod]
    [DataRow(320, 240)]
    [DataRow(1921, 1081)]
    [DataRow(2560, 1440)]
    public void TerminalIsCenteredToTheNearestPixel(int width, int height)
    {
        var layout = new Ae2TerminalLayout(width, height);

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
