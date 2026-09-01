using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Charting;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class T8PlusChartPipelineTests
    {
        private static ChartPipelineService CreateService()
        {
            return new ChartPipelineService(new SeriesSliceService(null, new TimestampRangeService()));
        }

        private static TestData BuildData()
        {
            var data = new TestData();
            data.Root = "A";
            data.TimestampsMs = new[] { 0L, 1000L, 2000L, 3000L };
            data.RowCount = 4;
            data.SourceOrder = new[] { "A" };
            data.SourceColumns["A"] = new[] { "T1", "T8", "T9" };
            data.SourceStartMs["A"] = 0L;
            data.SourceEndMs["A"] = 3000L;
            data.CodeSources["T1"] = "A";
            data.CodeSources["T8"] = "A";
            data.CodeSources["T9"] = "A";
            data.Columns["T1"] = new double?[] { 1.0, 1.0, 1.0, 1.0 };
            data.Columns["T8"] = new double?[] { 10.0, 8.0, 6.0, 4.0 };
            data.Columns["T9"] = new double?[] { 20.0, 18.0, 16.0, 14.0 };
            return data;
        }

        private static ChartPipelineRequest Request(TestData data, IReadOnlyList<T8PlusSeriesRequest> t8)
        {
            return ChartPipelineRequest.ForChart(
                data, new[] { "T1" }, false, 1, false, 1, 1000, 1,
                double.NaN, double.NaN, null, null, false, null, t8);
        }

        [TestMethod]
        public void Execute_WithAllThreeFlags_AddsThreeSeriesWithRolesAndSourceRoot()
        {
            TestData data = BuildData();
            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };

            ChartPipelineResult result = CreateService().Execute(Request(data, t8));

            List<ChartPipelineSeries> extra = result.Series
                .Where(s => s.Role != ChartSeriesRole.Channel)
                .ToList();

            Assert.AreEqual(3, extra.Count);
            CollectionAssert.AreEquivalent(
                new[] { ChartSeriesRole.T8Minimum, ChartSeriesRole.T8Average, ChartSeriesRole.T8Maximum },
                extra.Select(s => s.Role).ToArray());
            Assert.IsTrue(extra.All(s => s.SourceRoot == "A"));
            Assert.IsTrue(extra.All(s => s.SourceIndex == 0));
        }

        [TestMethod]
        public void Execute_WithSingleFlag_AddsOnlyThatSeriesWithExpectedValues()
        {
            TestData data = BuildData();
            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };

            ChartPipelineResult result = CreateService().Execute(Request(data, t8));

            ChartPipelineSeries average = result.Series.Single(s => s.Role == ChartSeriesRole.T8Average);

            Assert.AreEqual(4, average.YValues.Length);
            Assert.AreEqual(15.0, average.YValues[0], 1e-9);
            Assert.AreEqual(9.0, average.YValues[3], 1e-9);
        }

        [TestMethod]
        public void Execute_WithoutFlags_AddsNothing()
        {
            TestData data = BuildData();

            ChartPipelineResult result = CreateService().Execute(Request(data, null));

            Assert.IsTrue(result.Series.All(s => s.Role == ChartSeriesRole.Channel));
        }

        [TestMethod]
        public void Execute_WithoutT8Channels_AddsNothing()
        {
            var data = new TestData();
            data.Root = "A";
            data.TimestampsMs = new[] { 0L, 1000L };
            data.RowCount = 2;
            data.SourceOrder = new[] { "A" };
            data.SourceColumns["A"] = new[] { "T1" };
            data.CodeSources["T1"] = "A";
            data.Columns["T1"] = new double?[] { 1.0, 1.0 };
            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };

            ChartPipelineResult result = CreateService().Execute(Request(data, t8));

            Assert.IsTrue(result.Series.All(s => s.Role == ChartSeriesRole.Channel));
        }

        [TestMethod]
        public void Execute_RespectsDecimationStep()
        {
            TestData data = BuildData();
            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };
            ChartPipelineRequest request = ChartPipelineRequest.ForChart(
                data, new[] { "T1" }, false, 1, false, 2, 1000, 1,
                double.NaN, double.NaN, null, null, false, null, t8);

            ChartPipelineResult result = CreateService().Execute(request);

            ChartPipelineSeries average = result.Series.Single(s => s.Role == ChartSeriesRole.T8Average);
            ChartPipelineSeries channel = result.Series.Single(s => s.Role == ChartSeriesRole.Channel);

            Assert.AreEqual(2, result.Step);
            Assert.AreEqual(channel.XValues.Length, average.XValues.Length);
            Assert.AreEqual(15.0, average.YValues[0], 1e-9);
            Assert.AreEqual(11.0, average.YValues[1], 1e-9);
        }

        [TestMethod]
        public void Execute_WhenManyChannels_KeepsOnlyT8LinesInLegend()
        {
            var data = new TestData();
            data.Root = "A";
            data.TimestampsMs = new[] { 0L, 1000L };
            data.RowCount = 2;
            data.SourceOrder = new[] { "A" };
            var columns = new List<string>();
            for (int i = 1; i <= 30; i++)
            {
                string code = "C" + i.ToString();
                columns.Add(code);
                data.CodeSources[code] = "A";
                data.Columns[code] = new double?[] { i, i };
            }
            columns.Add("T8");
            data.CodeSources["T8"] = "A";
            data.Columns["T8"] = new double?[] { 5.0, 5.0 };
            data.SourceColumns["A"] = columns.ToArray();

            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };
            ChartPipelineResult result = CreateService().Execute(
                ChartPipelineRequest.ForChart(
                    data, columns, false, 1, false, 1, 1000, columns.Count,
                    double.NaN, double.NaN, null, null, false, null, t8));

            Assert.IsTrue(result.ShowLegend);
            Assert.IsTrue(result.Series.Where(s => s.Role == ChartSeriesRole.Channel).All(s => !s.IsVisibleInLegend));
            Assert.IsTrue(result.Series.Where(s => s.Role != ChartSeriesRole.Channel).All(s => s.IsVisibleInLegend));
        }
    }
}
