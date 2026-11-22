using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class TerminalPinsTests
{
    private sealed record Item(string Type);

    [TestMethod]
    public void LockAndUnlockOnlyAffectTheDraggedInstance()
    {
        var pins = new TerminalPins<object, Item>(6);
        var first = new Item("bandage");
        var second = new Item("bandage");
        pins.SetOwner(new object());

        pins.Toggle(first);
        pins.Contains(first).Should().BeTrue();
        pins.Contains(second).Should().BeFalse("the same type is not the same native item");
        pins.Toggle(second);
        pins.Toggle(first);
        pins.VisibleItems().Single().Should().BeSameAs(second);
        pins.Toggle(second);
        pins.PageCount.Should().Be(0);
    }

    [TestMethod]
    public void EveryPinCanBeReachedAndRemovingTheLastPageClampsThePage()
    {
        var pins = new TerminalPins<object, Item>(6);
        pins.SetOwner(new object());
        var items = Enumerable.Range(0, 13).Select(i => new Item(i.ToString())).ToArray();
        foreach (var item in items) pins.Toggle(item);

        pins.PageCount.Should().Be(3);
        pins.VisibleItems().Should().Equal(items.Skip(12));
        pins.MovePage(1);
        pins.VisibleItems().Should().Equal(items.Take(6));
        pins.MovePage(1);
        pins.VisibleItems().Should().Equal(items.Skip(6).Take(6));
        pins.MovePage(1);
        pins.Toggle(items[12]);
        pins.Page.Should().Be(1);
        pins.VisibleItems().Should().Equal(items.Skip(6).Take(6));
    }

    [TestMethod]
    public void ReturningToTheSameBodyRetainsPinsAndOwnershipChangesRemoveThem()
    {
        var pins = new TerminalPins<object, Item>(6);
        var body = new object();
        var kept = new Item("bandage");
        var dropped = new Item("bottle");
        pins.SetOwner(body);
        pins.Toggle(kept);
        pins.Toggle(dropped);

        pins.SetOwner(body);
        pins.Prune(item => ReferenceEquals(item, kept));
        pins.VisibleItems().Should().Equal(kept);
        pins.SetOwner(new object());
        pins.VisibleItems().Should().BeEmpty();
        pins.Page.Should().Be(0);
    }
}
