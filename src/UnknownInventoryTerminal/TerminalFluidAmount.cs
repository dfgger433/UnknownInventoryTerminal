using System;
using System.Globalization;

namespace UnknownInventoryTerminal
{
    internal static class TerminalFluidAmount
    {
        // The native API takes a float in mL. Every whole mL through this limit is exactly representable.
        private const int ExactFloatLimit = 1 << 24;

        internal static int MaximumMl(float sourceMl, float destinationSpaceMl)
        {
            if (!float.IsFinite(sourceMl) || !float.IsFinite(destinationSpaceMl)) return 0;
            var limit = Math.Min(sourceMl, destinationSpaceMl);
            return limit < 1f ? 0 : (int)Math.Min(limit, ExactFloatLimit);
        }

        internal static bool CanTransfer(int requestedMl, int maximumMl) => requestedMl > 0 && requestedMl <= maximumMl;

        internal static bool TryParse(string text, int maximumMl, out int ml)
        {
            ml = 0;
            if (text.Length is 0 or > 10) return false;
            foreach (var digit in text)
                if (digit is < '0' or > '9') return false;
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ml) && CanTransfer(ml, maximumMl);
        }
    }
}
