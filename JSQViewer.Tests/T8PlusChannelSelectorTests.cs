using System.Collections.Generic;
using JSQViewer.Application.Charting;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class T8PlusChannelSelectorTests
    {
        [TestMethod]
        public void TryGetChannelNumber_ParsesPlainAndDecoratedNames()
        {
            int number;

            Assert.IsTrue(T8PlusChannelSelector.TryGetChannelNumber("T8", out number));
            Assert.AreEqual(8, number);

            Assert.IsTrue(T8PlusChannelSelector.TryGetChannelNumber("C:\\run::T10", out number));
            Assert.AreEqual(10, number);

            Assert.IsTrue(T8PlusChannelSelector.TryGetChannelNumber("T12#2", out number));
            Assert.AreEqual(12, number);

            Assert.IsFalse(T8PlusChannelSelector.TryGetChannelNumber("T-sie", out number));
            Assert.IsFalse(T8PlusChannelSelector.TryGetChannelNumber("W", out number));
            Assert.IsFalse(T8PlusChannelSelector.TryGetChannelNumber(null, out number));
        }

        [TestMethod]
        public void SelectColumns_TakesOnlyOwnSourceAndNumbersAtOrAboveThreshold()
        {
            var data = new TestData();
            data.SourceColumns["A"] = new[] { "T1", "T7", "T8", "T10", "T-sie", "W" };
            data.SourceColumns["B"] = new[] { "T9" };

            List<string> columns = T8PlusChannelSelector.SelectColumns(data, "A", 8);

            CollectionAssert.AreEqual(new[] { "T8", "T10" }, columns);
        }

        [TestMethod]
        public void SelectColumns_FallsBackToColumnNamesForSingleSource()
        {
            var data = new TestData();
            data.ColumnNames = new[] { "T7", "T8", "T9" };

            List<string> columns = T8PlusChannelSelector.SelectColumns(data, "unknown", 8);

            CollectionAssert.AreEqual(new[] { "T8", "T9" }, columns);
        }

        [TestMethod]
        public void SelectColumns_WithoutMatchingChannels_ReturnsEmpty()
        {
            var data = new TestData();
            data.SourceColumns["A"] = new[] { "T1", "T-sie" };

            Assert.AreEqual(0, T8PlusChannelSelector.SelectColumns(data, "A", 8).Count);
        }
    }
}
