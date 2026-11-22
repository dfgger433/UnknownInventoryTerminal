using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CUCoreLib.Registries;
using UnityEngine;

namespace UnknownInventoryTerminal
{
    // Definitions and their translated metadata come from CCL. All changing values come from the hovered instance.
    internal static class TerminalItemTooltipContent
    {
        internal static (string Title, string Body) Read(Item item, bool expanded)
        {
            var stats = ItemRegistry.TryGetItemInfo(item.id, out var registered) ? registered : item.Stats;
            if (!stats.rec.recognizable)
            {
                var key = stats.category switch
                {
                    "medical" or "drug" or "tool" or "container" or "utility" or "water" or "food" => "unknown" + stats.category,
                    _ => "unknownobject"
                };
                return (Locale.GetOther(key), Locale.GetOther("unknownobjectdsc") + "\n\n" +
                    Colorize("fc037b", Locale.GetOther("unknownobjectwarning").Replace("<>", stats.rec.min.ToString())));
            }

            var title = string.IsNullOrEmpty(item.label) ? stats.fullName : item.label;
            if (string.IsNullOrEmpty(title)) title = item.id;
            if (item.isWet) title = Colorize("7d9ed1", Locale.GetOther("wet") + " " + title);
            if (item.favourited) title = "<sprite index=22>" + title;
            var condition = item.condition is > 0f and < .01f ? "<1" : Mathf.FloorToInt(item.condition * 100f).ToString();
            title += " (" + PlayerCamera.ConditionToColorCode(item.condition) + condition + "%" + PlayerCamera.EndColor() + ")";

            var text = new StringBuilder();
            if (!string.IsNullOrEmpty(stats.description)) text.Append(stats.description).Append("\n\n");
            if (item.TryGetComponent<BatteryItem>(out var battery)) text.AppendLine(battery.GetBatteryInfo());
            var canQuoteValue = !item.TryGetComponent<WaterContainerItem>(out var liquid) || AppendLiquid(text, liquid, expanded);
            Line(text, "ffffff", 0, $"{Locale.GetOther("weight")}: {item.totalWeight:0.##}u");
            if (expanded)
            {
                AppendExpanded(text, item, stats, canQuoteValue);
            }
            else
            {
                if (item.container) Line(text, "cf99bf", 4, $"{item.container.GetHoldingWeight():0.##}/{item.container.maxWeight:0.##}u");
                Line(text, "a2e8af", 2, "<i>" + Locale.GetOther("shifttoexpand") + "</i>");
            }
            if (item.favourited) text.AppendLine().Append("<sprite index=22>").Append(Colorize("EFE27F", Locale.GetOther("favouriteditem")));
            return (title, text.ToString().TrimEnd());
        }

        private static bool AppendLiquid(StringBuilder text, WaterContainerItem container, bool expanded)
        {
            var total = container.CurrentTotal;
            Line(text, ColorUtility.ToHtmlStringRGB(total > 0f ? container.AverageColor() : Color.white), 21,
                $"{Mathf.RoundToInt(total)}/{Mathf.RoundToInt(container.Capacity)}mL");
            var liquids = new List<string>();
            var allRegistered = true;
            foreach (var stack in container.stack)
            {
                if (!Liquids.Registry.TryGetValue(stack.liquidId, out var liquid))
                {
                    allRegistered = false;
                    liquids.Add($"{stack.liquidId} {Mathf.RoundToInt(stack.amount)}mL");
                    continue;
                }
                var name = liquid.localeFromItem ? Locale.GetItem(stack.liquidId) : Locale.GetOther(liquid.localeName);
                var line = Colorize(ColorUtility.ToHtmlStringRGB(liquid.color), $"{name} ({Mathf.FloorToInt(stack.amount)}mL)");
                if (expanded)
                {
                    var descKey = (liquid.localeFromItem ? stack.liquidId : liquid.localeName) + "dsc";
                    line += "\n" + Colorize(ColorUtility.ToHtmlStringRGB(Color.Lerp(liquid.color, Color.white, .5f)),
                        "<alpha=#88>" + Locale.GetOther(descKey) + "<alpha=#FF>");
                    if (liquid.qualities.Count > 0) line += "\n" + Qualities(liquid.GetScaledQualities(stack.amount));
                }
                liquids.Add(line);
            }
            if (liquids.Count > 0) text.AppendLine(string.Join(expanded ? "\n" : ", ", liquids));
            return allRegistered;
        }

        private static void AppendExpanded(StringBuilder text, Item item, ItemInfo stats, bool canQuoteValue)
        {
            var remaining = stats.rotSpeed > 0f ? item.TimeUntilDecayed() : TimeSpan.Zero;
            if (remaining.TotalSeconds > 0)
            {
                var decay = Locale.GetOther("decaysin").Replace("<>", PlayerCamera.TimeToString(remaining));
                var flags = (ItemInfo.DecayType)stats.decayInfo;
                var conditions = new List<string>();
                if (flags.HasFlag(ItemInfo.DecayType.NoDecayWhenNotWorn)) conditions.Add(Locale.GetOther("decayonlyworn"));
                if (flags.HasFlag(ItemInfo.DecayType.NoDecayWhenStill)) conditions.Add(Locale.GetOther("decayonlymoving"));
                if (flags.HasFlag(ItemInfo.DecayType.NoDecayWithoutContainerItem)) conditions.Add(Locale.GetOther("decayonlyhasitems"));
                if (conditions.Count > 0) decay += " (" + Locale.GetOther("decayonlyif") + string.Join(", ", conditions) + ")";
                Line(text, "a2dec2", 5, decay);
            }
            if (stats.onlyHoldInHands) Line(text, "ff8787", 6, Locale.GetOther("itemonlyinhands"));
            if (item.container is { } container && container)
            {
                Line(text, "cf99bf", 4, $"{Locale.GetOther("containermaxweight")} - {container.GetHoldingWeight():0.##}/{container.maxWeight:0.##}u");
                Line(text, "cf99bf", 10, $"{Locale.GetOther("containermaxweightperitem")} - {container.maxWeightPerItem:0.##}u");
                Line(text, "cf99bf", 0, $"{Locale.GetOther("containerencumberance")}: {(1f - container.encumberanceMult) * 100f:0.##}%");
            }
            if (stats.wearable)
            {
                Line(text, "91d1ff", 7, $"{Locale.GetOther("itemwearable")} \"{stats.wearSlotId}\"");
                if (stats.wearableArmor > 0f) Line(text, "91d1ff", 13, $"{Locale.GetOther("itemwearableprotection")} {Mathf.RoundToInt(stats.wearableArmor * 100f)}%");
                if (stats.wearableIsolation > 0f) Line(text, "91d1ff", 14, $"{Locale.GetOther("itemwearableisolation")} {stats.wearableIsolation * 100f:0.##}%");
            }
            var forWound = stats.ActuallyUsableOnLimb(item);
            if (stats.usable || forWound)
            {
                text.Append(Colorize("a3a3a3", Icon(11) + Locale.GetOther("itemusable")));
                if (stats.usable) text.Append(Colorize("a6ffaa", Icon(6) + Locale.GetOther(stats.usableWithLMB ? "itemusablehand" : "itemusableinventory")));
                if (stats.usable && forWound) text.Append('/');
                if (forWound) text.Append(Colorize("fffb91", Icon(12) + Locale.GetOther("itemusablewound")));
                text.AppendLine();
            }
            if (item.TryGetComponent<AmmoScript>(out var ammo) && ammo.itemType == AmmoScript.AmmoItemType.Magazine)
                Line(text, "ff8fb0", 8, $"{ammo.rounds}/{ammo.maxRounds} {Locale.GetOther("magazinerounds")}");
            if (stats.qualities.Count > 0) text.AppendLine(Qualities(stats.qualities));
            // The game prices liquids through a dictionary indexer, so an unknown ID has no usable price.
            if (!canQuoteValue) return;
            var value = stats.GetValue(item);
            if (value <= 0) return;
            if (item.TryGetComponent<BoughtItem>(out var bought))
                Line(text, "ff2b2b", 19, $"{Locale.GetOther("tradingblocked")} ({TimeSpan.FromSeconds(bought.time):m\\:ss})");
            else Line(text, "f6ff73", 9, Locale.GetOther("itemvalue") + value);
        }

        private static string Icon(int index) => $"<sprite index={index} tint=1>";
        private static string Colorize(string color, string value) => $"<color=#{color}>{value}</color>";
        private static void Line(StringBuilder text, string color, int icon, string value) =>
            text.AppendLine(Colorize(color, Icon(icon) + value));
        private static string Qualities(IEnumerable<CraftingQuality> qualities) => Colorize("ffc814", Icon(26) +
            Locale.GetOther("itemqualities") + string.Join(", ", qualities.Select(q => $"{q.LocaleName} ({q.amount:0.#})")));
    }
}
