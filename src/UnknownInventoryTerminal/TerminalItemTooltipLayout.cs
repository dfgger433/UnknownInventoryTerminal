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

        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
