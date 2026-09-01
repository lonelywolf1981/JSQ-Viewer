using JSQViewer.Application.Charting;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class T8PlusSeriesBuilderTests
    {
        private static TestData BuildData()
        {
            var data = new TestData();
            data.TimestampsMs = new[] { 1000L, 2000L, 3000L };
            data.RowCount = 3;
            data.SourceColumns["A"] = new[] { "T1", "T8", "T9", "T10" };
            data.Columns["T1"] = new double?[] { 100.0, 100.0, 100.0 };
            data.Columns["T8"] = new double?[] { 10.0, null, -95.0 };
            data.Columns["T9"] = new double?[] { 20.0, 4.0, -95.0 };
            data.Columns["T10"] = new double?[] { 30.0, 6.0, null };
            return data;
        }

        [TestMethod]
        public void Build_ComputesMinimumAverageAndMaximumAcrossChannels()
        {
            T8PlusSeries series = new T8PlusSeriesBuilder().Build(BuildData(), "A");

            Assert.IsTrue(series.HasChannels);
            Assert.AreEqual(10.0, series.Minimum[0].Value, 1e-9);
            Assert.AreEqual(20.0, series.Average[0].Value, 1e-9);
            Assert.AreEqual(30.0, series.Maximum[0].Value, 1e-9);
        }

        [TestMethod]
        public void Build_IgnoresMissingValuesWhenAveraging()
        {
            T8PlusSeries series = new T8PlusSeriesBuilder().Build(BuildData(), "A");

            // На втором отсчёте T8 пуст, среднее считается по T9 и T10.
            Assert.AreEqual(4.0, series.Minimum[1].Value, 1e-9);
            Assert.AreEqual(5.0, series.Average[1].Value, 1e-9);
            Assert.AreEqual(6.0, series.Maximum[1].Value, 1e-9);
        }

        [TestMethod]
        public void Build_WhenSampleHasNoValidValues_YieldsNullInAllThreeSeries()
        {
            T8PlusSeries series = new T8PlusSeriesBuilder().Build(BuildData(), "A");

            // На третьем отсчёте T8 и T9 ниже порога валидности, T10 пуст.
            Assert.IsFalse(series.Minimum[2].HasValue);
            Assert.IsFalse(series.Average[2].HasValue);
            Assert.IsFalse(series.Maximum[2].HasValue);
        }

        [TestMethod]
        public void Build_IgnoresChannelsOfOtherSources()
        {
            TestData data = BuildData();
            data.SourceColumns["B"] = new[] { "T20" };
            data.Columns["T20"] = new double?[] { -50.0, -50.0, -50.0 };

            T8PlusSeries series = new T8PlusSeriesBuilder().Build(data, "A");

            Assert.AreEqual(10.0, series.Minimum[0].Value, 1e-9);
        }

        [TestMethod]
        public void Build_WithoutT8Channels_ReturnsEmptySeries()
        {
            var data = new TestData();
            data.TimestampsMs = new[] { 1000L };
            data.RowCount = 1;
            data.SourceColumns["A"] = new[] { "T1", "T-sie" };

            T8PlusSeries series = new T8PlusSeriesBuilder().Build(data, "A");

            Assert.IsFalse(series.HasChannels);
            Assert.AreEqual(0, series.Average.Length);
        }
    }
}
