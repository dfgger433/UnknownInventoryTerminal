using System;

namespace UnknownInventoryTerminal
{
    // Horizontal anchors only: keep the game's authored row heights, pivots and placement.
    internal readonly struct SettingsRowColumns
    {
        internal const float Inset = 9f, Gap = 6f;
        internal float SplitAnchor { get; }
        internal float ControlLeftOffset { get; }
        internal float LabelRightOffset => ControlLeftOffset - Gap;

        internal SettingsRowColumns(float prefabWidth, float controlWidth, bool checkbox)
        {
            SplitAnchor = checkbox ? 1f : 1f - Math.Min(0.5f, (controlWidth + Inset) / prefabWidth);
            ControlLeftOffset = checkbox ? -(controlWidth + Inset) : 0f;
        }
    }
}
