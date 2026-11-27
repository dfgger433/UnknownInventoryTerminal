// SPDX-License-Identifier: GPL-3.0-only

namespace UnknownInventoryTerminal
{
    internal enum TerminalItemInteraction { None, Fluid, Battery, Combine }

    internal static class TerminalItemDrop
    {
        internal static TerminalItemInteraction Classify(Body body, Item? source, Item? target)
        {
            if (source == null || target == null || source == target) return TerminalItemInteraction.None;
            // Keep the terminal's amount dialog in place of the vanilla liquid UI.
            if (source.TryGetComponent<WaterContainerItem>(out _) && target.TryGetComponent<WaterContainerItem>(out _))
                return TerminalItemInteraction.Fluid;
            if (target.battery && (source.Stats.HasTag("tool") || source.Stats.HasTag("battery")))
                return TerminalItemInteraction.Battery;
            // Vanilla handles a container before it checks for a combination. Do not bypass
            // the terminal's container-placement rules by treating that drop as a combination.
            return !target.TryGetComponent<Container>(out _) && body.CanCombine(target, source)
                ? TerminalItemInteraction.Combine : TerminalItemInteraction.None;
        }

        internal static bool CanApplyBattery(Item source, Item target)
        {
            // Vanilla consumes even no-op battery drops. Together can fall through to a
            // slot/container move or announce a load before the native capacity check.
            var battery = target.battery;
            if (battery == null) return false;
            var stats = source.Stats;
            if (stats.HasTag("tool")) return battery.hasBattery;
            return !battery.hasBattery && stats is BatteryInfo info &&
                info.GetType() == typeof(BatteryInfo) && info.maxCharge <= battery.maxAllowedCharge;
        }
    }
}
