// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using UnityEngine;

namespace UnknownInventoryTerminal
{
    internal static class TerminalItemTooltipContent
    {
        internal static (string Title, string Body) Read(Item item)
        {
            if (item.TryGetComponent<WaterContainerItem>(out var liquid))
            {
                // Vanilla's expanded trade value (and Together's cache stamp, even when collapsed)
                // indexes the liquid registry without a fallback. Don't mutate the stack to format it.
                foreach (var stack in liquid.stack)
                    if (stack == null || string.IsNullOrEmpty(stack.liquidId) || !Liquids.Registry.ContainsKey(stack.liquidId))
                        return ReadUnregisteredLiquid(item, liquid);
            }

            // The game's formatter reads Item.Stats/Item.GetItem, which CUCoreLib patches to resolve
            // registered definitions. It also owns localization, recognition, expansion and TMP markup.
            return PlayerCamera.ItemHoverDescription(item);
        }

        private static (string Title, string Body) ReadUnregisteredLiquid(Item item, WaterContainerItem liquid)
        {
            var stats = item.Stats;
            if (!stats.rec.recognizable) return (Locale.GetOther("unknownobject"), Locale.GetOther("unknownobjectdsc"));

            // A malformed save/absent liquid mod can still show volume and weight without querying trade value.
            var body = new StringBuilder();
            if (!string.IsNullOrEmpty(stats.description)) body.AppendLine(stats.description).AppendLine();
            var total = 0f;
            foreach (var stack in liquid.stack)
                if (stack != null) total += stack.amount;
            body.AppendLine($"{Mathf.RoundToInt(total)}/{Mathf.RoundToInt(liquid.Capacity)}mL");
            foreach (var stack in liquid.stack)
            {
                if (stack == null || string.IsNullOrEmpty(stack.liquidId)) continue;
                var name = Liquids.Registry.TryGetValue(stack.liquidId, out var registered)
                    ? (registered.localeFromItem ? Locale.GetItem(stack.liquidId) : Locale.GetOther(registered.localeName))
                    : stack.liquidId;
                body.AppendLine($"{name} ({Mathf.RoundToInt(stack.amount)}mL)");
            }
            body.Append($"{Locale.GetOther("weight")}: {item.totalWeight:0.##}u");
            return (item.fullName, body.ToString());
        }
    }
}
