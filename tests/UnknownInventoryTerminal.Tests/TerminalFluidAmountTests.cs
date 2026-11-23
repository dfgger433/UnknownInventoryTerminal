using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests
{
    [TestClass]
    public sealed class TerminalFluidAmountTests
    {
        [TestMethod]
        [DataRow(600f, 125.9f, 125)]
        [DataRow(35.5f, 120f, 35)]
        [DataRow(1f, 1f, 1)]
        [DataRow(0.99f, 100f, 0)]
        [DataRow(100f, 0f, 0)]
        [DataRow(100f, -1f, 0)]
        [DataRow(float.NaN, 50f, 0)]
        [DataRow(50f, float.PositiveInfinity, 0)]
        [DataRow(float.MaxValue, float.MaxValue, 16777216)]
        public void MaximumStaysWithinBothNativeVolumes(float source, float space, int expected)
        {
            TerminalFluidAmount.MaximumMl(source, space).Should().Be(expected);
        }

        [TestMethod]
        [DataRow(75, 75, true)]
        [DataRow(75, 74, false)]
        [DataRow(1, 1, true)]
        [DataRow(0, 50, false)]
        [DataRow(-1, 50, false)]
        public void ConfirmRequiresPositiveAmountWithinCurrentMaximum(int request, int currentMaximum, bool expected)
        {
            TerminalFluidAmount.CanTransfer(request, currentMaximum).Should().Be(expected);
        }

        [TestMethod]
        [DataRow("1", true)]
        [DataRow("250", true)]
        [DataRow("251", false)]
        [DataRow("", false)]
        [DataRow("0", false)]
        [DataRow("-5", false)]
        [DataRow("+5", false)]
        [DataRow("2.5", false)]
        [DataRow("25 mL", false)]
        [DataRow("25\0", false)]
        [DataRow("3000000000", false)]
        public void InputAcceptsOnlyWholeMillilitresInRange(string text, bool expected)
        {
            TerminalFluidAmount.TryParse(text, 250, out var ml).Should().Be(expected);
            if (expected) ml.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be(text);
        }
    }
}
