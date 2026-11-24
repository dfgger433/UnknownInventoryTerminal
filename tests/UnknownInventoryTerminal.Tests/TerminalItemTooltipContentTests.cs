using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

/// <summary>Tests that the terminal forwards native tooltip text unchanged and only falls back for invalid liquids.</summary>
[TestClass]
public sealed class TerminalItemTooltipContentTests
{
    /// <summary>Hands the same live item to the game and preserves native labels, colors and inline sprites verbatim.</summary>
    [TestMethod]
    public void ReturnsNativeTitleAndDescriptionWithoutReformatting()
    {
        Item? forwarded = null;
        const string title = "<sprite index=22>模组物品 <color=#77ff66>(<1%)</color>";
        const string body = "原版说明\n\n<color=#FFFFFF><sprite index=0 tint=1>重量: 2u\n<color=#cf99bf><sprite index=4 tint=1>5/11u\n";
        var item = new Item { NativeTooltip = live => { forwarded = live; return (title, body); } };

        var result = TerminalItemTooltipContent.Read(item);

        forwarded.Should().BeSameAs(item);
        result.Should().Be((title, body));
    }

    /// <summary>Re-reads the live item and validates mod-registered liquid data without rewriting native text.</summary>
    [TestMethod]
    public void UsesNativeFormatterForRegisteredLiquidAndCurrentState()
    {
        const string id = "tooltip-test:registered-liquid";
        Liquids.Registry[id] = new Liquids.Definition { localeFromItem = true };
        var stack = new LiquidStack { liquidId = id, amount = 7 };
        var water = new WaterContainerItem { Capacity = 10 };
        water.stack.Add(stack);
        var item = new Item { Liquid = water, NativeTooltip = live => ("Native title", $"<sprite index=21 tint=1>{live.Liquid!.stack[0]!.amount:0}mL") };

        var original = TerminalItemTooltipContent.Read(item);
        stack.amount = 3;
        var changed = TerminalItemTooltipContent.Read(item);

        original.Body.Should().Be("<sprite index=21 tint=1>7mL");
        changed.Body.Should().Be("<sprite index=21 tint=1>3mL");
    }

    /// <summary>For a missing liquid ID, reports capacity and weight without asking the game to price it.</summary>
    [TestMethod]
    public void MissingAndMalformedLiquidEntriesUseFallbackWithoutChangingTheStack()
    {
        var water = new WaterContainerItem { Capacity = 20 };
        water.stack.Add(new LiquidStack { liquidId = "tooltip-test:missing", amount = 5 });
        water.stack.Add(new LiquidStack { amount = 3 });
        water.stack.Add(null);
        var item = new Item { Liquid = water, fullName = "<sprite index=22>Container", totalWeight = 2 };

        var result = TerminalItemTooltipContent.Read(item);

        result.Title.Should().Be("<sprite index=22>Container");
        result.Body.Should().Contain("8/20mL").And.Contain("tooltip-test:missing (5mL)").And.Contain("weight: 2u");
        water.stack.Should().HaveCount(3);
        Liquids.Registry.ContainsKey("tooltip-test:missing").Should().BeFalse();
    }

    /// <summary>Invalid liquid content is not a reason to reveal an unidentified item.</summary>
    [TestMethod]
    public void FallbackDoesNotRevealUnrecognizedItem()
    {
        var water = new WaterContainerItem { Capacity = 20 };
        water.stack.Add(new LiquidStack { liquidId = "tooltip-test:secret", amount = 5 });
        var item = new Item
        {
            fullName = "Custom label",
            Stats = new ItemInfo { description = "Secret description", rec = new Recognition { recognizable = false } },
            Liquid = water
        };

        var result = TerminalItemTooltipContent.Read(item);

        result.Should().Be(("unknownobject", "unknownobjectdsc"));
    }
}
