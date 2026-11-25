// SPDX-License-Identifier: GPL-3.0-only

using System;

namespace UnknownInventoryTerminal
{
    // Matches appeng.api.config.TerminalStyle: the storage gets a quarter, half, three quarters or all of the rows
    // that fit on the screen.
    internal enum TerminalHeight { Small = 1, Medium = 2, Tall = 3, Full = 4 }
    [Flags]
    internal enum TerminalDragAction { None = 0, Drain = 1, Use = 2, Pin = 4 }

    // Positions follow the board drawn in terminal.png. Like AE2's MEStorageScreen, the board is stacked from the
    // header, first, middle and last storage rows and the bottom part, each drawn as cut at an integer scale.
    internal readonly struct Ae2TerminalLayout
    {
        internal const int Columns = 6, Pitch = 18, Inset = 7, Rail = 24;
        internal const int BoardWidth = 141, Header = 17, Bottom = 99;
        internal const int GridWidth = Columns * Pitch;
        // terminal.png draws three storage rows; that board is also the smallest one, so the button only adds rows.
        internal const int MinRows = 3, EquipmentRowCount = 3, MainRowCount = 1;
        internal const int BaseBoardHeight = Header + MinRows * Pitch + Bottom;
        // Screen pixels (in UI units) kept free above and below the board.
        internal const int Margin = 4;
        // Offsets of the sections drawn in the bottom part, from its top edge.
        internal const int StatusHeight = 14, EquipmentOffset = StatusHeight, MainOffset = EquipmentOffset + EquipmentRowCount * Pitch + 4;
        // The scrollbar groove drawn in terminal.png; the thumb keeps its own sprite size and is centered on it.
        internal const int GrooveLeft = 124, GrooveWidth = 6, GrooveBorder = 1;
        internal const int DefaultThumbWidth = 12, DefaultThumbHeight = 15;
        // AE2 VerticalButtonBar with IconButton faces: each 18x20 face ends 2 px right of the board's left edge,
        // and faces are 2 px apart. Coordinates below are relative to the window, whose board starts at x = Rail.
        internal const int ButtonWidth = 18, ButtonHeight = 20, ButtonSpacing = 2;
        internal const int ToolbarX = Rail + 2 - ButtonWidth, ToolbarTop = 3, ToolbarPitch = ButtonHeight + ButtonSpacing;
        internal const int CloseRightInset = 4;
        internal const int CloseX = Rail + BoardWidth - CloseRightInset - ButtonWidth;
        // The old right inset is preserved. With the shorter painted header, leave the thumb's top edge unobstructed.
        internal const int CloseY = Header + GrooveBorder - ButtonHeight;
        internal const int ChromeTopPadding = -CloseY;
        internal const int SearchX = Rail + 55, SearchY = 3, SearchHeight = 13;
        internal const int SearchWidth = CloseX - SearchX - 2;
        internal const int ToolbarButtons = 4;
        // The background covers the board's dark outline and light bevel at the join.
        internal const int ButtonBarInsetX = 3, ButtonBarInsetY = 3;
        internal const int ButtonBarX = ToolbarX - ButtonBarInsetX, ButtonBarY = ToolbarTop - ButtonBarInsetY, ButtonBarWidth = 21;
        internal const int ButtonBarTopBorder = 2, ButtonBarBottomBorder = 4;
        internal const int ButtonBarJoinWidth = ButtonBarX + ButtonBarWidth - Rail;
        internal const int ButtonBarPatchCount = 4;

        // The first row can meet the board's own top outline as-is. Further down, stop that dark
        // row at the bevel so it turns the corner instead of striking through the white border.
        internal static (int Y, int Width, int Height) ButtonBarPatch(int height, int patch, bool flushWithBoardTop) => patch switch
        {
            0 => (0, ButtonBarWidth - (flushWithBoardTop ? 0 : ButtonBarJoinWidth - 1), 1),
            1 => (1, ButtonBarWidth, ButtonBarTopBorder - 1),
            2 => (ButtonBarTopBorder, ButtonBarWidth, height - ButtonBarTopBorder - ButtonBarBottomBorder),
            _ => (height - ButtonBarBottomBorder, ButtonBarWidth, ButtonBarBottomBorder)
        };
        internal int BoardLeft => Rail * Scale;
        internal int Scale { get; }
        internal TerminalHeight Style { get; }
        internal int Rows { get; }
        internal int MainRows => MainRowCount;
        internal int EquipmentRows => EquipmentRowCount;
        internal int GridHeight => Rows * Pitch * Scale;
        internal int StorageRows(bool hasPinnedRow) => Rows - (hasPinnedRow ? 1 : 0);
        // Board-relative top edges, in UI units.
        internal int StatusTop => Header + Rows * Pitch;
        internal int EquipmentTop => StatusTop + EquipmentOffset;
        internal int MainTop => StatusTop + MainOffset;
        internal int BoardHeight => Header + Rows * Pitch + Bottom;
        internal int PanelHeight => (BoardHeight - Header) * Scale;
        internal int Width => (Rail + BoardWidth) * Scale;
        internal int Height => BoardHeight * Scale;
        private readonly int _screenWidth, _screenHeight;
        internal int Left => (_screenWidth - Width) / 2;
        internal int Top => (_screenHeight - Height) / 2;

        internal Ae2TerminalLayout(int screenWidth, int screenHeight, TerminalHeight style = TerminalHeight.Small)
        {
            // The scale depends only on the smallest board, so changing the height never changes the pixel size.
            Scale = 1;
            for (var scale = 2; scale <= 4; scale++)
                if ((Rail + BoardWidth + 8) * scale <= screenWidth && (BaseBoardHeight + 2 * Margin) * scale <= screenHeight) Scale = scale;
            Style = Enum.IsDefined(typeof(TerminalHeight), style) ? style : TerminalHeight.Small;
            Rows = Math.Max(MinRows, PossibleRows(screenHeight / Scale) * (int)Style / 4);
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
        }

        // AE2 TerminalStyle.getPossibleRows: rows that fit between the header and the bottom part.
        internal static int PossibleRows(int availableHeight) =>
            Math.Max(0, availableHeight - 2 * Margin - Header - Bottom) / Pitch;

        internal static TerminalHeight Next(TerminalHeight style, bool backwards) =>
            (TerminalHeight)(((int)style - 1 + (backwards ? 3 : 1)) % 4 + 1);

        internal static int ToolbarY(int index) => ToolbarTop + index * ToolbarPitch;
        internal static int ThumbLeft(int thumbWidth) => GrooveLeft + (GrooveWidth - thumbWidth) / 2;
        private static int ActionCount(TerminalDragAction actions) =>
            ((actions & TerminalDragAction.Drain) != 0 ? 1 : 0) +
            ((actions & TerminalDragAction.Use) != 0 ? 1 : 0) +
            ((actions & TerminalDragAction.Pin) != 0 ? 1 : 0);

        // Keep the rail against the short board's bottom; center it on the equipment rows otherwise.
        private int FirstDragActionY(int count) => Rows == MinRows
            ? BoardHeight - ButtonBarHeight(count) + ButtonBarInsetY
            : Math.Max(EquipmentTop + (EquipmentRowCount * Pitch - (count - 1) * ToolbarPitch - ButtonHeight) / 2,
                ButtonBarY + ButtonBarHeight(ToolbarButtons) + 5);

        // Lower bits are earlier slots (Drain, Use, Pin); count only the ones present.
        private static int DragActionIndex(TerminalDragAction actions, TerminalDragAction action) =>
            ActionCount(actions & (TerminalDragAction)((int)action - 1));

        internal (int X, int Y, int Width, int Height) DragActionBounds(TerminalDragAction actions, TerminalDragAction action)
        {
            var y = FirstDragActionY(ActionCount(actions)) + DragActionIndex(actions, action) * ToolbarPitch;
            return (ToolbarX * Scale, y * Scale, ButtonWidth * Scale, ButtonHeight * Scale);
        }

        internal (int X, int Y, int Width, int Height) DragActionBackgroundBounds(TerminalDragAction actions,
            TerminalDragAction armed = TerminalDragAction.None)
        {
            var count = ActionCount(actions);
            if (count == 0) return default;
            var y = FirstDragActionY(count) - ButtonBarInsetY;
            if (armed != TerminalDragAction.None)
            {
                y += DragActionIndex(actions, armed) * ToolbarPitch;
                count = 1;
            }
            return (ButtonBarX * Scale, y * Scale, ButtonBarWidth * Scale, ButtonBarHeight(count) * Scale);
        }

        // Hover art can move by one pixel without changing the hit rectangle.
        internal bool HitsDragAction(TerminalDragAction actions, TerminalDragAction action, float screenX, float screenY)
        {
            if ((actions & action) == 0) return false;
            var (x, y, width, height) = DragActionBounds(actions, action);
            var left = Left + x;
            var top = Top + y;
            return screenX >= left && screenX < left + width && screenY >= top && screenY < top + height;
        }
        // A pinned row with multiple pages reserves the first storage row for the pager, but a single page
        // leaves the scrollbar alongside the pinned row so its thumb still starts at the top of the groove.
        internal int ScrollTrackTop(bool hasPinnedPager = false) => (hasPinnedPager ? Pitch : 0) + GrooveBorder;
        // Like AE2, the thumb travels between the two end borders of the painted groove.
        internal int ScrollTrackHeight(bool hasPinnedPager = false) =>
            (hasPinnedPager ? StorageRows(hasPinnedRow: true) : Rows) * Pitch - 2 * GrooveBorder;
        // Height of vertical_buttons_bg.png behind the given number of visible buttons.
        internal static int ButtonBarHeight(int buttons) => buttons * ToolbarPitch + 6;
        internal static int ContentRows(int tiles) => (Math.Max(0, tiles) + Columns - 1) / Columns;
        internal static int PaddedCells(int slots) => ContentRows(slots) * Columns;
    }
}
