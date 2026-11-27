// SPDX-License-Identifier: GPL-3.0-only
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

/// <summary>Verifies when a specific item drop takes precedence over terminal placement.</summary>
[TestClass]
public sealed class TerminalItemDropTests
{
    /// <summary>Self-drops, empty targets and missing sources are not item interactions.</summary>
    [TestMethod]
    public void RequiresTwoDistinctItems()
    {
        var item = new Item();
        var body = NeverCombine();

        TerminalItemDrop.Classify(body, item, item).Should().Be(TerminalItemInteraction.None);
        TerminalItemDrop.Classify(body, item, null).Should().Be(TerminalItemInteraction.None);
        TerminalItemDrop.Classify(body, null, item).Should().Be(TerminalItemInteraction.None);
    }

    /// <summary>Two liquid containers retain the terminal amount dialog even if another action could match.</summary>
    [TestMethod]
    public void LiquidPairRetainsPriority()
    {
        var source = new Item { Liquid = new(), Stats = new ItemInfo { Tags = { "tool" } } };
        var target = new Item { Liquid = new(), battery = new() };

        TerminalItemDrop.Classify(NeverCombine(), source, target).Should().Be(TerminalItemInteraction.Fluid);
    }

    /// <summary>Dragging a tool onto a powered container reaches the battery action before placement.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ToolDropOnlyUnloadsWhenABatteryIsPresent(bool loaded)
    {
        var source = new Item { Stats = new ItemInfo { Tags = { "tool" } } };
        var target = new Item { battery = new() { hasBattery = loaded }, Container = new() };

        TerminalItemDrop.Classify(NeverCombine(), source, target).Should().Be(TerminalItemInteraction.Battery);
        TerminalItemDrop.CanApplyBattery(source, target).Should().Be(loaded);
    }

    /// <summary>An insertion is eligible only for a native-sized battery and an empty device.</summary>
    [TestMethod]
    [DataRow(false, 49f, true)]
    [DataRow(false, 50f, true)]
    [DataRow(false, 51f, false)]
    [DataRow(true, 50f, false)]
    public void BatteryInsertionRespectsNativeCapacityAndOccupancy(bool loaded, float charge, bool expected)
    {
        var source = new Item { Stats = new BatteryInfo { Tags = { "battery" }, maxCharge = charge } };
        var target = new Item { battery = new() { hasBattery = loaded } };

        TerminalItemDrop.Classify(NeverCombine(), source, target).Should().Be(TerminalItemInteraction.Battery);
        TerminalItemDrop.CanApplyBattery(source, target).Should().Be(expected);
    }

    /// <summary>A battery-tagged item that vanilla would reject does not fall through to a move.</summary>
    [TestMethod]
    public void TagAloneCannotInsertABattery()
    {
        var source = new Item { Stats = new ItemInfo { Tags = { "battery" } } };
        var target = new Item { battery = new() };

        TerminalItemDrop.Classify(NeverCombine(), source, target).Should().Be(TerminalItemInteraction.Battery);
        TerminalItemDrop.CanApplyBattery(source, target).Should().BeFalse();
        TerminalItemDrop.CanApplyBattery(new Item { Stats = new DerivedBatteryInfo() }, target).Should().BeFalse();
        TerminalItemDrop.CanApplyBattery(source, new Item()).Should().BeFalse();
    }

    /// <summary>Other combinations defer to the native predicate with the receiver first.</summary>
    [TestMethod]
    public void AsksVanillaWhetherTheSpecificPairCanCombine()
    {
        var source = new Item();
        var target = new Item();
        var body = new Body { CanCombineItems = (first, second) => first == target && second == source };

        TerminalItemDrop.Classify(body, source, target).Should().Be(TerminalItemInteraction.Combine);
        TerminalItemDrop.Classify(body, target, source).Should().Be(TerminalItemInteraction.None);
        TerminalItemDrop.Classify(NeverCombine(), source, new Item { Container = new() })
            .Should().Be(TerminalItemInteraction.None, "ordinary containers retain terminal placement rules");
    }

    private sealed class DerivedBatteryInfo : BatteryInfo;

    private static Body NeverCombine() => new() { CanCombineItems = (_, _) => throw new AssertFailedException("Unexpected combine lookup.") };
}
