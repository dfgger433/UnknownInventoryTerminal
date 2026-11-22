using System;

namespace UnknownInventoryTerminal
{
    // Top-left screen coordinates, shared by the overlay and the headless layout tests.
    internal static class TerminalItemTooltipLayout
    {
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
