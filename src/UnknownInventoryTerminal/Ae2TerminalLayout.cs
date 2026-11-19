using System;

namespace UnknownInventoryTerminal
{
    internal readonly struct Ae2TerminalLayout
    {
        internal const int Columns = 6, Pitch = 18, Inset = 7, Rail = 24, Header = 22;
        internal const int GridWidth = Columns * Pitch, ScrollbarLeft = Inset + GridWidth + 5;
        internal const int BoardWidth = GridWidth + 33, AtlasWidth = 195, AtlasRemovedWidth = 3 * Pitch;
        internal const int DetailHeight = 24, EquipmentHeader = 12, Footer = 16;
        private const int FixedHeight = Header + Footer + DetailHeight + EquipmentHeader;
        internal int BoardLeft => Rail * Scale;
        internal int Scale { get; }
        internal int Rows { get; }
        internal int MainRows { get; }
        internal int EquipmentRows { get; }
        internal int GridHeight => Rows * Pitch * Scale;
        internal int LoadoutHeight => (DetailHeight + EquipmentHeader + (MainRows + EquipmentRows) * Pitch) * Scale;
        internal int PanelHeight => GridHeight + LoadoutHeight;
        internal int Width => (Rail + BoardWidth) * Scale;
        internal int Height => (Header + Footer) * Scale + PanelHeight;
        private readonly int _screenWidth, _screenHeight;
        internal int Left => (_screenWidth - Width) / 2;
        internal int Top => (_screenHeight - Height) / 2;

        internal Ae2TerminalLayout(int screenWidth, int screenHeight, int mainSlots = Columns, int equipmentSlots = 19)
        {
            var requestedMainRows = Math.Max(1, ContentRows(mainSlots));
            var requestedEquipmentRows = Math.Max(1, ContentRows(equipmentSlots));
            Scale = 1;
            for (var scale = 2; scale <= 4; scale++)
            {
                if ((BoardWidth + Rail + 8) * scale <= screenWidth &&
                    (FixedHeight + (requestedMainRows + Math.Min(4, requestedEquipmentRows) + 3) * Pitch + 8) * scale <= screenHeight)
                    Scale = scale;
            }
            var availableRows = Math.Max(3, (screenHeight / Scale - FixedHeight - 8) / Pitch);
            MainRows = Math.Min(requestedMainRows, availableRows - 2);
            EquipmentRows = Math.Min(requestedEquipmentRows, Math.Max(1, availableRows - MainRows - Math.Min(3, availableRows - MainRows - 1)));
            Rows = Math.Min(12, availableRows - MainRows - EquipmentRows);
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
        }

        internal static int ContentRows(int tiles) => (Math.Max(0, tiles) + Columns - 1) / Columns;
        internal static int PaddedCells(int slots) => ContentRows(slots) * Columns;
    }
}
