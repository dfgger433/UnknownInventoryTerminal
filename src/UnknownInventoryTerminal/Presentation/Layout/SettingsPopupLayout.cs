using System;

namespace UnknownInventoryTerminal
{
    internal readonly struct SettingsPopupLayout
    {
        internal bool OpenAbove { get; }
        internal float Height { get; }

        internal SettingsPopupLayout(float itemHeight, int count, float templateLimit, float below, float above)
        {
            float wanted = Math.Min(templateLimit, itemHeight * count);
            OpenAbove = below < wanted && above > below;
            Height = Math.Max(0f, Math.Min(wanted, OpenAbove ? above : below));
        }
    }
}
