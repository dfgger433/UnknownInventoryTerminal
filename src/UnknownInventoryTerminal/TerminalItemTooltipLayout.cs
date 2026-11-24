using System;

namespace UnknownInventoryTerminal
{
    // Top-left screen coordinates, shared by the overlay and the headless layout tests.
    internal static class TerminalItemTooltipLayout
    {
        // NEI's logical text size and 380-unit width cap; leave the terminal's atlas scale independent.
        internal const int TitleSize = 13, DetailSize = 12;
        internal const float Padding = 7f, DetailGap = 3f, GroupGap = 6f;
        internal static float ReadableScale(float screenWidth, float screenHeight)
        {
            // Same default zoom and viewport limits as NEI (default left/right panel widths).
            var centerLimit = screenWidth * ((2560f - 20f - 741f - 719f) / 2560f) / 255f;
            return Math.Max(.01f, Math.Min(Clamp(screenHeight / 720f, .9f, 2f), Math.Min(centerLimit, screenHeight / 440f)));
        }
        internal static float TextWidth(float preferredWidth, float availableWidth) =>
            Math.Min(availableWidth, Clamp((float)Math.Ceiling(preferredWidth), 58f - 2 * Padding, 380f - 2 * Padding));
        internal static float FitScale(float preferredScale, float availableHeight, float contentHeight) =>
            contentHeight > 0f ? Math.Min(preferredScale, availableHeight / contentHeight) : preferredScale;

        internal static (float X, float Y) Place(float x, float y, float width, float height,
            float screenWidth, float screenHeight, float gap, float margin)
        {
            var right = screenWidth - margin;
            var bottom = screenHeight - margin;
            var left = x + gap + width <= right ? x + gap : x - gap - width;
            var top = y + gap + height <= bottom ? y + gap : y - gap - height;
            return (Clamp(left, margin, right - width), Clamp(top, margin, bottom - height));
        }

        internal static (float X, float Y) PlaceControl(float left, float top, float right, float bottom, bool below,
            float width, float height, float screenWidth, float screenHeight, float gap, float margin)
        {
            var maxX = screenWidth - margin - width;
            var maxY = screenHeight - margin - height;
            if (below)
            {
                var y = bottom + gap <= maxY ? bottom + gap : top - gap - height;
                return (Clamp(left, margin, maxX), Clamp(y, margin, maxY));
            }
            var x = right + gap <= maxX ? right + gap : left - gap - width;
            return (Clamp(x, margin, maxX), Clamp(top, margin, maxY));
        }

        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
