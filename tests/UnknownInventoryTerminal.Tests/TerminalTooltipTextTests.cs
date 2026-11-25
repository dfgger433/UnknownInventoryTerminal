// SPDX-License-Identifier: GPL-3.0-only
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

/// <summary>Checks the NEI-style tooltip text adapter without launching Unity.</summary>
[TestClass]
public sealed class TerminalTooltipTextTests
{
    /// <summary>NEI's emphasis and nested colors survive, while shadows retain the original alpha.</summary>
    [TestMethod]
    public void BalancesRichTextAndPreservesOpacityInShadows()
    {
        var runs = TerminalTooltipText.Parse("说明 <color=#a2e8af><i>展开<alpha=#88>更多</i></color>数据", TerminalTooltipText.BodyColor);

        runs.Select(run => run.Text).Should().Equal("说明 ", "展开", "更多", "数据");
        runs.Select(run => run.Color).Should().Equal("BDB4E1FF", "A2E8AFFF", "A2E8AF88", "BDB4E1FF");
        TerminalTooltipText.Markup(runs[2], false).Should().Be("<color=#A2E8AF88><i>更多</i></color>");
        TerminalTooltipText.Markup(runs[2], true).Should().Be("<color=#19182788><i>更多</i></color>");
    }

    /// <summary>Game icons remain in reading order, including mid-line and untinted icons.</summary>
    [TestMethod]
    public void CapturesSpritesWithoutEmittingUnsupportedImGuiTags()
    {
        var runs = TerminalTooltipText.Parse("<sprite index=22>名称<br><color=#abcd><sprite index=11 tint=1>使用/<sprite=12>伤口 <size=120%>(<1%)</size>", TerminalTooltipText.TitleColor);

        runs.Select(run => run.Sprite).Should().Equal(22, -1, 11, -1, 12, -1);
        runs.Select(run => run.Tint).Should().Equal(false, false, true, false, false, false);
        runs[0].Color.Should().Be("F4F2FBFF");
        runs[2].Color.Should().Be("AABBCCDD");
        runs.Select(run => run.Text).Should().Equal("", "名称\n", "", "使用/", "", "伤口 (<1%)");
    }

    /// <summary>Mod icons referenced by name remain drawable and annotations stay literal.</summary>
    [TestMethod]
    public void RetainsNamedSpritesAndDoesNotParseTheGroupCaption()
    {
        var item = TerminalTooltipText.Parse("<sprite name=\"mod icon\" tint='1'>说明", TerminalTooltipText.BodyColor);
        var group = TerminalTooltipText.Parse("<b>3</b>", TerminalTooltipText.GroupColor, rich: false);

        item[0].IsSprite.Should().BeTrue();
        item[0].SpriteName.Should().Be("mod icon");
        item[0].Tint.Should().BeTrue();
        group.Single().Text.Should().Be("<b>3</b>");
    }

    /// <summary>Long mixed-language icon rows can wrap without splitting Latin or surrogate pairs.</summary>
    [TestMethod]
    public void KeepsWordsAndUnicodeElementsTogether()
    {
        TerminalTooltipText.WrapParts("mod item 耐久𠀀90%").Should().Equal("mod", " ", "item", " ", "耐", "久", "𠀀", "90%");
    }
}
