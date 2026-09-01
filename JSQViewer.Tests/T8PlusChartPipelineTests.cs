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

        private static ChartPipelineRequest Request(
            TestData data,
            IReadOnlyList<T8PlusSeriesRequest> t8,
            bool overlayMode = false,
            double rangeStart = double.NaN,
            double rangeEnd = double.NaN)
        {
            return ChartPipelineRequest.ForChart(
                data, new[] { "T1" }, overlayMode, 1, false, 1, 1000, 1,
                rangeStart, rangeEnd, null, null, false, null, t8);
        }

        [TestMethod]
        public void Execute_WithAllThreeFlags_ProducesThreeLevelsAtLastSample()
        {
            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };

            ChartPipelineResult result = CreateService().Execute(Request(BuildData(), t8));

            Assert.AreEqual(3, result.LevelLines.Count);
            // Последний отсчёт: T8=4, T9=14.
            Assert.AreEqual(4.0, result.LevelLines.Single(l => l.Role == ChartSeriesRole.T8Minimum).Value, 1e-9);
            Assert.AreEqual(9.0, result.LevelLines.Single(l => l.Role == ChartSeriesRole.T8Average).Value, 1e-9);
            Assert.AreEqual(14.0, result.LevelLines.Single(l => l.Role == ChartSeriesRole.T8Maximum).Value, 1e-9);
            Assert.IsTrue(result.LevelLines.All(l => l.SourceRoot == "A" && l.SourceIndex == 0));
        }

        [TestMethod]
        public void Execute_WithNarrowedRange_UsesLastSampleInsideRange()
        {
            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };

            ChartPipelineResult result = CreateService().Execute(
                Request(BuildData(), t8, false, 0d, 1500d));

            // Правый край 1500 мс: последний попавший отсчёт — 1000 мс, T8=8, T9=18.
            Assert.AreEqual(13.0, result.LevelLines.Single().Value, 1e-9);
        }

        [TestMethod]
        public void Execute_WhenEdgeSampleHasNoValue_StepsBackToNearestValidSample()
        {
            TestData data = BuildData();
            data.Columns["T8"] = new double?[] { 10.0, 8.0, 6.0, null };
            data.Columns["T9"] = new double?[] { 20.0, 18.0, 16.0, null };
            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };

            ChartPipelineResult result = CreateService().Execute(Request(data, t8));

            // На последнем отсчёте значений нет, берётся предыдущий: (6 + 16) / 2.
            Assert.AreEqual(11.0, result.LevelLines.Single().Value, 1e-9);
        }

        [TestMethod]
        public void Execute_WhenNoValidSampleInRange_ProducesNoLevels()
        {
            TestData data = BuildData();
            data.Columns["T8"] = new double?[] { null, null, null, null };
            data.Columns["T9"] = new double?[] { null, null, null, null };
            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };

            ChartPipelineResult result = CreateService().Execute(Request(data, t8));

            Assert.AreEqual(0, result.LevelLines.Count);
        }

        [TestMethod]
        public void Execute_WithoutFlagsOrWithoutT8Channels_ProducesNoLevels()
        {
            Assert.AreEqual(0, CreateService().Execute(Request(BuildData(), null)).LevelLines.Count);

            var data = new TestData();
            data.Root = "A";
            data.TimestampsMs = new[] { 0L, 1000L };
            data.RowCount = 2;
            data.SourceOrder = new[] { "A" };
            data.SourceColumns["A"] = new[] { "T1" };
            data.CodeSources["T1"] = "A";
            data.Columns["T1"] = new double?[] { 1.0, 1.0 };
            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };

            Assert.AreEqual(0, CreateService().Execute(Request(data, t8)).LevelLines.Count);
        }

        [TestMethod]
        public void Execute_InOverlayMode_ResolvesEdgePerSourceStart()
        {
            var data = new TestData();
            data.Root = "A";
            data.TimestampsMs = new[] { 0L, 1000L, 2000L, 3000L, 4000L };
            data.RowCount = 5;
            data.SourceOrder = new[] { "A", "B" };
            data.SourceColumns["A"] = new[] { "T8" };
            data.SourceColumns["B"] = new[] { "T9" };
            data.SourceStartMs["A"] = 0L;
            data.SourceEndMs["A"] = 4000L;
            data.SourceStartMs["B"] = 2000L;
            data.SourceEndMs["B"] = 4000L;
            data.CodeSources["T8"] = "A";
            data.CodeSources["T9"] = "B";
            data.Columns["T8"] = new double?[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
            data.Columns["T9"] = new double?[] { 11.0, 21.0, 31.0, 41.0, 51.0 };

            var t8 = new[]
            {
                new T8PlusSeriesRequest("A", false, true, false),
                new T8PlusSeriesRequest("B", false, true, false)
            };

            // В наложении ось — часы от начала своего прогона. Край в 1 час
            // для A это абсолютные 3600000 мс, для B — 2000 + 3600000 мс;
            // оба за пределами данных, поэтому берётся последний отсчёт каждого.
            ChartPipelineResult result = CreateService().Execute(
                Request(data, t8, true, 0d, 1d));

            ChartLevelLine a = result.LevelLines.Single(l => l.SourceRoot == "A");
            ChartLevelLine b = result.LevelLines.Single(l => l.SourceRoot == "B");

            Assert.AreEqual(0, a.SourceIndex);
            Assert.AreEqual(1, b.SourceIndex);
            Assert.AreEqual(50.0, a.Value, 1e-9);
            Assert.AreEqual(51.0, b.Value, 1e-9);
        }

        [TestMethod]
        public void Execute_WithSingleSource_LabelOmitsSourceName()
        {
            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };

            ChartPipelineResult result = CreateService().Execute(Request(BuildData(), t8));

            string label = result.LevelLines.Single().Label;
            Assert.IsTrue(label.Contains("T8+"), label);
            Assert.IsFalse(label.Contains("["), label);
        }

        [TestMethod]
        public void Execute_WithLevelsEnabled_DoesNotChangeSeriesOrLegend()
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

            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };
            ChartPipelineResult result = CreateService().Execute(
                ChartPipelineRequest.ForChart(
                    data, columns, false, 1, false, 1, 1000, columns.Count,
                    double.NaN, double.NaN, null, null, false, null, t8));

            // Уровни не являются сериями: набор серий и правило легенды не меняются.
            Assert.AreEqual(columns.Count, result.Series.Count);
            Assert.IsFalse(result.ShowLegend);
            Assert.AreEqual(3, result.LevelLines.Count);
        }
    }
}
