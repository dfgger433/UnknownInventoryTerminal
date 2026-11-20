using System;

namespace UnknownInventoryTerminal
{
    // Positions follow the board drawn in terminal.png. The board is shown as one image at an
    // integer scale, so its rows and sections are fixed rather than tiled to fit the screen.
    internal readonly struct Ae2TerminalLayout
    {
        internal const int Columns = 6, Pitch = 18, Inset = 7, Rail = 24;
        internal const int BoardWidth = 141, BoardHeight = 170, Header = 17;
        internal const int GridWidth = Columns * Pitch;
        internal const int StorageRows = 3, EquipmentRowCount = 3, MainRowCount = 1;
        // Board-relative top edges of the sections drawn in terminal.png.
        internal const int StatusTop = Header + StorageRows * Pitch, StatusHeight = 14;
        internal const int EquipmentTop = StatusTop + StatusHeight, MainTop = EquipmentTop + EquipmentRowCount * Pitch + 4;
        // The scrollbar groove drawn in terminal.png; the thumb keeps its own sprite size and is centered on it.
        internal const int GrooveLeft = 124, GrooveWidth = 6, DefaultThumbWidth = 16, DefaultThumbHeight = 16;
        // Toolbar buttons sit in the transparent rail left of the board.
        internal const int ToolbarX = 3, ToolbarPitch = 21, ButtonWidth = 18, ButtonHeight = 20;
        internal int BoardLeft => Rail * Scale;
        internal int Scale { get; }
        internal int Rows => StorageRows;
        internal int MainRows => MainRowCount;
        internal int EquipmentRows => EquipmentRowCount;
        internal int GridHeight => Rows * Pitch * Scale;
        internal int PanelHeight => (BoardHeight - Header) * Scale;
        internal int Width => (Rail + BoardWidth) * Scale;
        internal int Height => BoardHeight * Scale;
        private readonly int _screenWidth, _screenHeight;
        internal int Left => (_screenWidth - Width) / 2;
        internal int Top => (_screenHeight - Height) / 2;

        internal Ae2TerminalLayout(int screenWidth, int screenHeight)
        {
            Scale = 1;
            for (var scale = 2; scale <= 4; scale++)
                if ((Rail + BoardWidth + 8) * scale <= screenWidth && (BoardHeight + 8) * scale <= screenHeight) Scale = scale;
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
        }

        internal static int ToolbarY(int index) => index * ToolbarPitch;
        internal static int ContentRows(int tiles) => (Math.Max(0, tiles) + Columns - 1) / Columns;
        internal static int PaddedCells(int slots) => ContentRows(slots) * Columns;
    }
}
