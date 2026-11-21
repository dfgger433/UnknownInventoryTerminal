using CUCoreLib.Registries;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

/// <summary>Exercises tooltip content against representative live items and CCL definitions.</summary>
[TestClass]
public sealed class TerminalItemTooltipContentTests
{
    /// <summary>Registry text wins, but instance values are never shared between identical item IDs.</summary>
    [TestMethod]
    public void UsesRegistryDefinitionAndReadsEachLiveInstance()
    {
        const string id = "tooltip-test:registered";
        var fallback = new ItemInfo { fullName = "Fallback name", description = "Fallback description" };
        ItemRegistry.Definitions[id] = new ItemInfo { fullName = "Registered name", description = "Registered description" };
        var first = new Item { id = id, Stats = fallback, condition = .25f, totalWeight = 2f };
        var second = new Item { id = id, Stats = fallback, label = "Custom label", condition = .75f, totalWeight = 7f };

        var firstTip = TerminalItemTooltipContent.Read(first, expanded: false);
        var secondTip = TerminalItemTooltipContent.Read(second, expanded: false);

        firstTip.Title.Should().Contain("Registered name").And.Contain("25%");
        firstTip.Body.Should().Contain("Registered description").And.NotContain("Fallback description").And.Contain("2u");
        secondTip.Title.Should().Contain("Custom label").And.Contain("75%").And.NotContain("Registered name");
        secondTip.Body.Should().Contain("Registered description").And.Contain("7u").And.NotContain("2u");
    }

    /// <summary>An absent registry entry still uses the instance's vanilla item definition.</summary>
    [TestMethod]
    public void FallsBackToLiveStatsWhenRegistryDoesNotContainTheId()
    {
        var item = new Item
        {
            id = "tooltip-test:unregistered",
            Stats = new ItemInfo { fullName = "Vanilla name", description = "Vanilla description" },
            condition = .5f,
            totalWeight = 3f
        };

        var tip = TerminalItemTooltipContent.Read(item, expanded: false);

        tip.Title.Should().Contain("Vanilla name").And.Contain("50%");
        tip.Body.Should().Contain("Vanilla description").And.Contain("3u");
    }

    /// <summary>Neither the definition, the player label nor details leak before recognition.</summary>
    [TestMethod]
    public void UnrecognizedItemMasksLabelAndDetailsEvenWhenExpanded()
    {
        const string id = "tooltip-test:unrecognized";
        ItemRegistry.Definitions[id] = new ItemInfo
        {
            fullName = "Hidden item name",
            description = "Hidden item description",
            category = "tool",
            rec = new Recognition { recognizable = false, min = 17 }
        };
        var item = new Item
        {
            id = id,
            Stats = new ItemInfo { fullName = "Recognizable fallback" },
            label = "Hidden player label",
            condition = .5f,
            totalWeight = 3f,
            favourited = true,
            isWet = true
        };

        var tip = TerminalItemTooltipContent.Read(item, expanded: true);

        tip.Title.Should().Be("unknowntool");
        tip.Body.Should().Contain("unknownobjectdsc").And.Contain("unknownobjectwarning 17")
            .And.NotContain("Hidden").And.NotContain("Recognizable fallback").And.NotContain("weight")
            .And.NotContain("favouriteditem");
    }

    /// <summary>Liquid levels and battery text are evaluated again for the same hovered item.</summary>
    [TestMethod]
    public void ReReadsLiquidAndBatteryAfterRuntimeStateChanges()
    {
        var item = new Item { id = "tooltip-test:liquid-and-battery", condition = .001f };
        const string liquidId = "tooltip-test:liquid";
        Liquids.Registry[liquidId] = new Liquids.Definition { localeName = "localized-liquid" };
        var liquid = new WaterContainerItem { CurrentTotal = 40f, Capacity = 60f };
        liquid.stack.Add(new LiquidStack { liquidId = liquidId, amount = 40f });
        var battery = new BatteryItem { Info = "Battery ready" };
        item.Attach(liquid);
        item.Attach(battery);

        var before = TerminalItemTooltipContent.Read(item, expanded: false);
        liquid.CurrentTotal = liquid.stack[0].amount = 12f;
        battery.Info = "Battery empty";
        var after = TerminalItemTooltipContent.Read(item, expanded: false);

        before.Title.Should().Contain("<1%");
        before.Body.Should().Contain("Battery ready").And.Contain("40/60mL").And.Contain("localized-liquid (40mL)");
        after.Body.Should().Contain("Battery empty").And.Contain("12/60mL").And.Contain("localized-liquid (12mL)")
            .And.NotContain("40mL").And.NotContain("Battery ready");
    }

    /// <summary>Expanded mode adds statistics without dropping the basic live content.</summary>
    [TestMethod]
    public void ExpandedModeAddsDetailsWithoutDroppingBasicData()
    {
        var item = new Item
        {
            id = "tooltip-test:expandable",
            Stats = new ItemInfo
            {
                fullName = "Expandable pack",
                description = "A test pack.",
                onlyHoldInHands = true,
                qualities = { new CraftingQuality { LocaleName = "Test quality", amount = 2f } }
            },
            totalWeight = 6f,
            favourited = true,
            isWet = true
        };
        item.Attach(new Container { maxWeight = 11f, maxWeightPerItem = 4f, HoldingWeight = 5f });

        var collapsed = TerminalItemTooltipContent.Read(item, expanded: false);
        var expanded = TerminalItemTooltipContent.Read(item, expanded: true);

        collapsed.Title.Should().Be(expanded.Title).And.Contain("wet");
        collapsed.Body.Should().Contain("A test pack.").And.Contain("6u").And.Contain("5/11u")
            .And.Contain("favouriteditem").And.Contain("shifttoexpand").And.NotContain("Test quality")
            .And.NotContain("itemonlyinhands").And.NotContain("containermaxweightperitem");
        expanded.Body.Should().Contain("A test pack.").And.Contain("6u").And.Contain("5/11u")
            .And.Contain("favouriteditem").And.NotContain("shifttoexpand").And.Contain("Test quality")
            .And.Contain("itemonlyinhands").And.Contain("containermaxweightperitem");
    }
}
