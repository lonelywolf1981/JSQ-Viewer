using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Charting;
using JSQViewer.Core;
using JSQViewer.Infrastructure.Cache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ChartPipelineTests
    {
        [TestMethod]
        public void ResolveStep_UsesManualValueWhenAutoStepIsDisabled()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var request = ChartPipelineRequest.ForChart(
                SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L, 2000L, 3000L }),
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 3,
                targetPoints: 5000,
                selectedChannelCount: 1);

            ChartPipelineResult result = service.Execute(request);

            Assert.AreEqual(3, result.Step);
        }

        [TestMethod]
        public void ResolveStep_CapsTargetPointsByChannelCount()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var request = ChartPipelineRequest.ForChart(
                SessionAndChartingTestData.CreateData(Enumerable.Range(0, 50000).Select(i => (long)i).ToArray()),
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: true,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 12);

            ChartPipelineResult result = service.Execute(request);

            Assert.AreEqual(12, result.Step);
        }

        [TestMethod]
        public void ResolveStep_ForcesStepOneForMultiSourceSelections()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var data = SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L, 2000L, 3000L });
            data.SourceColumns["source-a"] = new[] { "A-01" };
            data.SourceColumns["source-b"] = new[] { "B-01" };
            data.CodeSources["A-01"] = "C:\\tests\\source-a\\";
            data.CodeSources["B-01"] = "C:\\tests\\source-b\\";
            data.SourceStartMs["C:\\tests\\source-a\\"] = 0L;
            data.SourceEndMs["C:\\tests\\source-a\\"] = 3000L;
            data.SourceStartMs["C:\\tests\\source-b\\"] = 0L;
            data.SourceEndMs["C:\\tests\\source-b\\"] = 3000L;

            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01", "B-01" },
                overlayMode: true,
                dataVersion: 1,
                autoStepEnabled: true,
                manualStep: 5,
                targetPoints: 5000,
                selectedChannelCount: 2);

            ChartPipelineResult result = service.Execute(request);

            Assert.AreEqual(1, result.Step);
        }

        [TestMethod]
        public void ResolveLegendText_UsesSourceNameWhenMultipleSourcesAreLoaded()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var data = SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L });
            data.SourceColumns["source-a"] = new[] { "A-01" };
            data.SourceColumns["source-b"] = new[] { "B-01" };
            data.CodeSources["A-01"] = "C:\\tests\\source-a\\";

            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1);

            ChartPipelineResult result = service.Execute(request);

            Assert.AreEqual("[source-a] A-01", result.Series.Single().LegendText);
        }

        [TestMethod]
        public void Execute_UsesCachedSeriesSliceService()
        {
            var cache = new CountingSeriesSliceCache();
            var sliceService = new SeriesSliceService(cache, new TimestampRangeService());
            var service = new ChartPipelineService(sliceService);
            var data = SessionAndChartingTestData.CreateData(
                new long[] { 0L, 1000L, 2000L },
                new Dictionary<string, double?[]>
                {
                    ["A-01"] = new double?[] { 1d, 2d, 3d }
                });
            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 7,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1);

            ChartPipelineResult first = service.Execute(request);
            ChartPipelineResult second = service.Execute(request);

            Assert.AreEqual(0d, first.Series.Single().XValues[0]);
            Assert.AreEqual(first.Series.Single().XValues[0], second.Series.Single().XValues[0]);
            Assert.AreEqual(1, cache.SetCount);
            Assert.IsTrue(cache.HitCount >= 1);
        }
    }

    internal sealed class CountingSeriesSliceCache : ISeriesSliceCache
    {
        private readonly Dictionary<string, SeriesSlice> _items = new Dictionary<string, SeriesSlice>(StringComparer.OrdinalIgnoreCase);

        public int HitCount { get; private set; }

        public int SetCount { get; private set; }

        public bool TryGet(string key, out SeriesSlice slice)
        {
            bool found = _items.TryGetValue(key, out slice);
            if (found)
            {
                HitCount++;
            }

            return found;
        }

        public void Set(string key, SeriesSlice slice)
        {
            _items[key] = slice;
            SetCount++;
        }

        public void Clear()
        {
            _items.Clear();
        }
    }
}
