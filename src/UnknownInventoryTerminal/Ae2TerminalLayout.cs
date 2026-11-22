using System;

namespace UnknownInventoryTerminal
{
    // Matches appeng.api.config.TerminalStyle: the storage gets a quarter, half, three quarters or all of the rows
    // that fit on the screen.
    internal enum TerminalHeight { Small = 1, Medium = 2, Tall = 3, Full = 4 }
    internal enum TerminalDragAction { Drain, Use }

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
        // A separate two-slot group uses the same left rail, below even the expanded toolbar.
        // Keep both slot origins fixed when a single action is offered or armed.
        internal (int X, int Y, int Width, int Height) DragActionBounds(TerminalDragAction action)
        {
            var y = Math.Max(EquipmentTop + (EquipmentRowCount * Pitch - ToolbarPitch - ButtonHeight) / 2,
                ButtonBarY + ButtonBarHeight(ToolbarButtons + 1) + 5);
            if (action == TerminalDragAction.Use) y += ToolbarPitch;
            return (ToolbarX * Scale, y * Scale, ButtonWidth * Scale, ButtonHeight * Scale);
        }

        internal (int X, int Y, int Width, int Height) DragActionBackgroundBounds(bool drain, bool use)
        {
            var (_, y, _, _) = DragActionBounds(drain ? TerminalDragAction.Drain : TerminalDragAction.Use);
            var height = ButtonBarHeight(drain && use ? 2 : 1);
            return (ButtonBarX * Scale, y - ButtonBarInsetY * Scale, ButtonBarWidth * Scale, height * Scale);
        }

        // Like the toolbar, poll the fixed face rectangle. Hover art moves by one pixel;
        // neither that offset nor the transparent background changes the hit area.
        internal bool HitsDragAction(TerminalDragAction action, float screenX, float screenY)
        {
            var (x, y, width, height) = DragActionBounds(action);
            var left = Left + x;
            var top = Top + y;
            return screenX >= left && screenX < left + width && screenY >= top && screenY < top + height;
        }
        // Like AE2, the thumb travels between the two end borders of the painted groove.
        internal int ScrollTrackHeight => Rows * Pitch - 2 * GrooveBorder;
        // Height of vertical_buttons_bg.png behind the given number of visible buttons.
        internal static int ButtonBarHeight(int buttons) => buttons * ToolbarPitch + 6;
        internal static int ContentRows(int tiles) => (Math.Max(0, tiles) + Columns - 1) / Columns;
        internal static int PaddedCells(int slots) => ContentRows(slots) * Columns;
    }
}
