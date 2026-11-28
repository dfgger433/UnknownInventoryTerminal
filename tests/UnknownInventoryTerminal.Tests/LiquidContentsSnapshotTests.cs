// SPDX-License-Identifier: GPL-3.0-only
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class LiquidContentsSnapshotTests
{
    private static Item Bag(params (string Id, float Amount)[] contents)
    {
        var liquid = new WaterContainerItem();
        foreach (var (id, amount) in contents)
            liquid.stack.Add(new LiquidStack { liquidId = id, amount = amount });
        return new Item { Liquid = liquid };
    }

    private static InventoryEntry Capture(string key, Item item, string type = "ivbag", string parent = "pack") =>
        new(key, type, "IV bag", parent, parent, item, null, LiquidContentsSnapshot.CaptureKey(item));

    [TestMethod]
    public void Build_GroupsOnlyMatchingContentsWithinAnItemType()
    {
        var waterA = Bag(("water", 100));
        var waterB = Bag(("water", 20));
        var entries = new[]
        {
            Capture("z", waterB, parent: "other-pack"),
            Capture("b", Bag(("saline", 100))),
            Capture("c", Bag()),
            Capture("a", waterA),
            Capture("d", Bag(("water", 100)), type: "bottle"),
            Capture("e", Bag(("water", 100), ("saline", 10)))
        };

        var rows = InventoryProjection.Build(entries, InventoryViewMode.Merged, new SearchRegistry(), null);

        rows.Select(row => row.First.InstanceKey).Should().Equal("d", "c", "a", "b", "e");
        rows.Single(row => row.Count == 2).Instances.Select(entry => entry.NativeItem).Should().Equal(waterA, waterB);
        InventoryProjection.Build(entries, InventoryViewMode.ByContainer, new SearchRegistry(), null)
            .Should().OnlyContain(row => row.Count == 1);
    }

    [TestMethod]
    public void CaptureKey_NormalizesMixturesWithoutDependingOnTheRegistryOrVolume()
    {
        var first = Bag(("mod:blue", 40), ("water", 15), ("water", 20));
        var second = Bag(("water", 1), ("mod:blue", 500), ("saline", 0));

        LiquidContentsSnapshot.CaptureKey(first).Should().Be(LiquidContentsSnapshot.CaptureKey(second));
        LiquidContentsSnapshot.CaptureKey(first).Should().NotBe(LiquidContentsSnapshot.CaptureKey(Bag(("water", 35))));
        LiquidContentsSnapshot.CaptureKey(first).Should().NotBe(LiquidContentsSnapshot.CaptureKey(Bag(("MOD:blue", 40), ("water", 35))));
        LiquidContentsSnapshot.CaptureKey(Bag(("a::b", 1))).Should().NotBe(LiquidContentsSnapshot.CaptureKey(Bag(("a:", 1), ("b", 1))));
    }

    [TestMethod]
    public void CaptureKey_DistinguishesEmptyContainersAndUpdatesOnTheNextSnapshot()
    {
        var item = Bag(("water", 20));
        item.Liquid!.stack.Add(null);
        item.Liquid.stack.Add(new LiquidStack { amount = 5 });
        item.Liquid.stack.Add(new LiquidStack { liquidId = "invalid", amount = float.NaN });
        var beforeDrain = Capture("a", item);

        item.Liquid.stack[0]!.amount = 0;
        var afterDrain = Capture("a", item);

        beforeDrain.LiquidContentsKey.Should().NotBeNullOrEmpty();
        afterDrain.LiquidContentsKey.Should().BeEmpty();
        LiquidContentsSnapshot.CaptureKey(new Item()).Should().BeNull();
    }
}
