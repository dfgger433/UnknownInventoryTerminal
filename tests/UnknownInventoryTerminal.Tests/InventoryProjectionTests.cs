using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class InventoryProjectionTests
{
    private static InventoryEntry Entry(string instance, string type, string name, string containerKey = "bag-1", string containerName = "Backpack", object? nativeItem = null) =>
        new(instance, type, name, containerKey, containerName, nativeItem ?? new object());

    [TestMethod]
    public void Build_DeduplicatesRepeatedInstanceKeys()
    {
        var item = new object();
        var first = Entry("instance-1", "Bandage", "Bandage", nativeItem: item);
        var duplicate = Entry("instance-1", "Bandage", "Bandage", nativeItem: new object());

        var rows = InventoryProjection.Build(new[] { first, duplicate }, InventoryViewMode.Merged, new SearchRegistry(), null);

        rows.Should().ContainSingle();
        rows[0].Count.Should().Be(1);
        rows[0].First.Should().BeSameAs(first);
        rows[0].First.NativeItem.Should().BeSameAs(item);
    }

    [TestMethod]
    public void Build_MergesSameTypeAndKeepsEveryRealReference()
    {
        var nativeA = new object();
        var nativeB = new object();
        var first = Entry("a", "Bandage", "Bandage", nativeItem: nativeA);
        var second = Entry("b", "Bandage", "Bandage", nativeItem: nativeB);

        var row = InventoryProjection.Build(new[] { first, second }, InventoryViewMode.Merged, new SearchRegistry(), null).Should().ContainSingle().Subject;

        row.Count.Should().Be(2);
        row.Instances.Should().ContainInOrder(first, second);
        row.Instances.Select(x => x.NativeItem).Should().ContainInOrder(nativeA, nativeB);
    }

    [TestMethod]
    public void Build_ByContainerDoesNotMergeSameNameFromDifferentContainerKeys()
    {
        var first = Entry("a", "Bandage", "Bandage", "bag-1", "Same backpack");
        var second = Entry("b", "Bandage", "Bandage", "bag-2", "Same backpack");

        var rows = InventoryProjection.Build(new[] { first, second }, InventoryViewMode.ByContainer, new SearchRegistry(), null);

        rows.Should().HaveCount(2);
        rows.Select(r => r.First.ContainerKey).Should().BeEquivalentTo("bag-1", "bag-2");
        rows.Should().OnlyContain(r => r.Count == 1);
    }

    [TestMethod]
    public void Build_UsesCaseSensitiveTypeIds()
    {
        var rows = InventoryProjection.Build(new[]
        {
            Entry("a", "Medical.Bandage", "Bandage"),
            Entry("b", "medical.bandage", "Bandage")
        }, InventoryViewMode.Merged, new SearchRegistry(), null);

        rows.Should().HaveCount(2);
        rows.Select(r => r.First.TypeId).Should().BeEquivalentTo("Medical.Bandage", "medical.bandage");
    }

    [TestMethod]
    public void Build_FiltersBeforeGrouping()
    {
        var matching = Entry("a", "Food", "Apple");
        var nonmatching = Entry("b", "Food", "Bread");

        var rows = InventoryProjection.Build(new[] { matching, nonmatching }, InventoryViewMode.Merged, new SearchRegistry(), "apple");

        rows.Should().ContainSingle();
        rows[0].Instances.Should().ContainSingle().Which.Should().BeSameAs(matching);
    }

    [TestMethod]
    public void Build_ThrowsForNullSourceAndRegistry()
    {
        var search = new SearchRegistry();
        Action nullSource = () => InventoryProjection.Build(null!, InventoryViewMode.Merged, search, null);
        Action nullSearch = () => InventoryProjection.Build(Array.Empty<InventoryEntry>(), InventoryViewMode.Merged, null!, null);

        nullSource.Should().Throw<ArgumentNullException>().WithParameterName("source");
        nullSearch.Should().Throw<ArgumentNullException>().WithParameterName("search");
    }

    [TestMethod]
    public void Search_NullOrWhitespaceQueryMatchesAll()
    {
        var entry = Entry("a", "food.apple", "Green Apple");
        var search = new SearchRegistry();

        search.Matches(entry, null).Should().BeTrue();
        search.Matches(entry, " \t\r\n ").Should().BeTrue();
    }

    [TestMethod]
    public void Search_IsCaseInsensitiveAndRequiresEveryWhitespaceSeparatedTerm()
    {
        var entry = Entry("a", "food.apple", "Green Apple", containerName: "Kitchen bag");
        var search = new SearchRegistry();

        search.Matches(entry, "gReEn APPLE").Should().BeTrue();
        search.Matches(entry, "green missing").Should().BeFalse();
        search.Matches(entry, "kitchen FOOD").Should().BeTrue();
    }

    [TestMethod]
    public void Search_AllowsOrBetweenAndGroupsAndRestrictsStarToTypeId()
    {
        var entry = Entry("a", "food.apple", "Green Apple", containerName: "Kitchen bag");
        var search = new SearchRegistry();

        search.Matches(entry, "red apple | green *FOOD").Should().BeTrue();
        search.Matches(entry, "green *kitchen | red apple").Should().BeFalse();
        search.Matches(entry, "missing | ").Should().BeFalse();
    }

    [TestMethod]
    public void Search_ExternalMatcherCanMatchTermsAndUnregistersOnDispose()
    {
        var entry = Entry("a", "item.42", "Unknown item");
        var search = new SearchRegistry();
        var calls = new List<string>();
        var registration = search.RegisterMatcher((_, term) =>
        {
            calls.Add(term);
            return term == "shang";
        });

        search.Matches(entry, "shang").Should().BeTrue();
        registration.Dispose();
        search.Matches(entry, "shang").Should().BeFalse();
        calls.Should().ContainSingle().Which.Should().Be("shang");
    }

    [TestMethod]
    public void Search_ExternalMatcherRunsPerTermAndParticipatesInAndMatching()
    {
        var entry = Entry("a", "item.42", "Unknown item");
        var search = new SearchRegistry();
        var calls = new List<string>();
        using var registration = search.RegisterMatcher((_, term) =>
        {
            calls.Add(term);
            return term == "pinyin";
        });

        search.Matches(entry, "Unknown pinyin").Should().BeTrue();
        search.Matches(entry, "pinyin absent").Should().BeFalse();
        calls.Should().Contain("pinyin").And.Contain("absent");
    }

    [TestMethod]
    public void Search_IsolatesAndDisablesThrowingProviderWhileBuiltInSearchStillWorks()
    {
        var failures = new List<Exception>();
        var search = new SearchRegistry(failures.Add);
        var calls = 0;
        using var registration = search.RegisterMatcher((_, _) =>
        {
            calls++;
            throw new InvalidOperationException("provider failure");
        });
        var entry = Entry("a", "food.apple", "Apple");

        search.Matches(entry, "unknown").Should().BeFalse();
        search.Matches(entry, "unknown").Should().BeFalse();
        search.Matches(entry, "apple").Should().BeTrue();
        calls.Should().Be(1);
        failures.Should().ContainSingle().Which.Should().BeOfType<InvalidOperationException>();
    }

    [TestMethod]
    public void Search_ThrowsForNullEntryAndMatcher()
    {
        var search = new SearchRegistry();
        Action nullEntry = () => search.Matches(null!, "x");
        Action nullMatcher = () => search.RegisterMatcher(null!);

        nullEntry.Should().Throw<ArgumentNullException>().WithParameterName("entry");
        nullMatcher.Should().Throw<ArgumentNullException>().WithParameterName("matcher");
    }

    [TestMethod]
    public void InventoryEntry_RejectsNullRequiredArguments()
    {
        var native = new object();
        Func<InventoryEntry>[] factories =
        {
            () => new InventoryEntry(null!, "id", "name", "container", "path", native),
            () => new InventoryEntry("instance", null!, "name", "container", "path", native),
            () => new InventoryEntry("instance", "id", "name", null!, "path", native),
            () => new InventoryEntry("instance", "id", "name", "container", null!, native),
            () => new InventoryEntry("instance", "id", "name", "container", "path", null!)
        };

        foreach (var factory in factories)
            ((Action)(() => factory())).Should().Throw<ArgumentException>();
    }
}
