// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace UnknownInventoryTerminal
{
    internal static class LiquidContentsSnapshot
    {
        internal static string? CaptureKey(Item item)
        {
            if (!item.TryGetComponent<WaterContainerItem>(out var liquid)) return null;
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            if (liquid.stack != null)
                foreach (var stack in liquid.stack)
                    if (stack != null && stack.amount > 0f && !float.IsInfinity(stack.amount) &&
                        !string.IsNullOrEmpty(stack.liquidId)) ids.Add(stack.liquidId);

            // Amount and stack order do not affect grouping. Length prefixes avoid collisions with modded IDs.
            return string.Concat(ids.Select(id => id.Length.ToString(CultureInfo.InvariantCulture) + ":" + id));
        }
    }
}
