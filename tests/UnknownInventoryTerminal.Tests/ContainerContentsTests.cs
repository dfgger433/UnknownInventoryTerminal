using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class ContainerContentsTests
{
    private sealed class Bag
    {
        public List<Thing> Children { get; } = new();
    }

    private sealed class Thing
    {
        public Bag? Parent { get; set; }
        public Bag? Container { get; init; }
    }

    private static (Thing Item, Bag Container)[] Scan(params Bag[] roots) => ContainerContents
        .Enumerate(roots, bag => bag.Children, item => item.Parent, item => item.Container).ToArray();

    [TestMethod]
    public void Enumerate_RejectsLooseItemsAndItemsBelongingToOtherContainers()
    {
        var carriedBag = new Bag();
        var foreignBag = new Bag();
        var stored = new Thing { Parent = carriedBag };
        var loose = new Thing();
        var foreign = new Thing { Parent = foreignBag };
        // Exercise stale/overbroad candidates, not just the happy path.
        carriedBag.Children.AddRange(new[] { loose, foreign, stored });
        foreignBag.Children.Add(foreign);

        var items = Scan(carriedBag);

        items.Should().ContainSingle().Which.Should().Be((stored, carriedBag));
    }

    [TestMethod]
    public void Enumerate_TraversesNestedContainerAndReturnsEachImmediateParentOnce()
    {
        var wornBag = new Bag();
        var pouch = new Bag();
        var pouchItem = new Thing { Parent = wornBag, Container = pouch };
        var stored = new Thing { Parent = pouch };
        wornBag.Children.AddRange(new[] { stored, pouchItem, pouchItem });
        pouch.Children.Add(stored);

        var items = Scan(wornBag, wornBag);

        items.Should().BeEquivalentTo(new[] { (pouchItem, wornBag), (stored, pouch) });
    }

    [TestMethod]
    public void Enumerate_TracksTransferOutOfContainerAndBackInOnNextScan()
    {
        var first = new Bag();
        var second = new Bag();
        var stored = new Thing { Parent = first };
        first.Children.Add(stored);
        Scan(first, second).Should().ContainSingle().Which.Should().Be((stored, first));

        stored.Parent = null;
        Scan(first, second).Should().BeEmpty();

        stored.Parent = second;
        second.Children.Add(stored);
        Scan(first, second).Should().ContainSingle().Which.Should().Be((stored, second));
    }
}
