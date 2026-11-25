// SPDX-License-Identifier: GPL-3.0-only
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class SettingsRowColumnsTests
{
    [TestMethod]
    [DataRow(387.31f, false)] // placement dropdown
    [DataRow(212.72f, false)] // key binding
    [DataRow(32.8f, true)]    // toggles
    public void ResizingKeepsLabelsAndControlsInsideTheSameRow(float originalControlWidth, bool checkbox)
    {
        const float prefabWidth = 1058.2f;
        var columns = new SettingsRowColumns(prefabWidth, originalControlWidth, checkbox);
        foreach (float width in new[] { prefabWidth, 720f, 320f, 1400f })
        {
            float labelRight = width * columns.SplitAnchor + columns.LabelRightOffset;
            float fieldLeft = width * columns.SplitAnchor + columns.ControlLeftOffset;
            float fieldWidth = width - SettingsRowColumns.Inset - fieldLeft;
            labelRight.Should().BeGreaterThan(SettingsRowColumns.Inset);
            (fieldLeft - labelRight).Should().BeApproximately(SettingsRowColumns.Gap, 0.001f);
            fieldWidth.Should().BeGreaterThan(0f);
            if (checkbox || width == prefabWidth)
                fieldWidth.Should().BeApproximately(originalControlWidth, 0.001f);
        }
    }
}
