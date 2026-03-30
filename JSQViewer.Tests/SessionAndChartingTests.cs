using System;
using System.Collections.Generic;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Session;
using JSQViewer.Core;
using JSQViewer.Infrastructure.Cache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ViewerSessionTests
    {
        [TestMethod]
        public void SetData_UpdatesCurrentWorkspaceAndClearsSeriesSliceCache()
        {
            var cache = new RecordingSeriesSliceCache();
            var session = new ViewerSession(cache);
            var data = SessionAndChartingTestData.CreateData(new long[] { 10L, 20L, 30L });

            session.SetData("C:\\tests\\run-01", data);

            Assert.AreEqual("C:\\tests\\run-01", session.Folder);
            Assert.AreSame(data, session.Data);
            Assert.IsTrue(session.IsLoaded);
            Assert.AreEqual(1, session.DataVersion);
            Assert.AreEqual(1, cache.ClearCount);
        }

        [TestMethod]
        public void SetData_WithNullDataMarksSessionAsNotLoadedButStillBumpsVersion()
        {
            var cache = new RecordingSeriesSliceCache();
            var session = new ViewerSession(cache);

            session.SetData("C:\\tests\\run-01", SessionAndChartingTestData.CreateData(new long[] { 10L }));
            session.SetData(string.Empty, null);

            Assert.AreEqual(string.Empty, session.Folder);
            Assert.IsNull(session.Data);
            Assert.IsFalse(session.IsLoaded);
            Assert.AreEqual(2, session.DataVersion);
            Assert.AreEqual(2, cache.ClearCount);
        }
    }

    [TestClass]
    public class CompatibilityFacadeTests
    {
        [TestMethod]
        public void AppState_DelegatesToConfiguredViewerSession()
        {
            var cache = new RecordingSeriesSliceCache();
            var session = new ViewerSession(cache);
            AppState.Configure(session, new TimestampRangeService(), new DataSummaryService(new TimestampRangeService()));
            var data = SessionAndChartingTestData.CreateData(new long[] { 10L, 20L });

            AppState.SetData("C:\\tests\\compat", data);

            Assert.AreEqual("C:\\tests\\compat", AppState.Folder);
            Assert.AreSame(data, AppState.Data);
            Assert.IsTrue(AppState.IsLoaded);
            Assert.AreEqual(1, AppState.DataVersion);
        }

        [TestMethod]
        public void SeriesCache_DelegatesToConfiguredSeriesSliceService()
        {
            var cache = new MemorySeriesSliceCache();
            var service = new SeriesSliceService(cache, new TimestampRangeService());
            SeriesCache.Configure(service);
            var data = SessionAndChartingTestData.CreateData(
                new long[] { 0L, 100L, 200L },
                new Dictionary<string, double?[]>
                {
                    ["A-01"] = new double?[] { 1d, 2d, 3d }
                });

            SeriesSlice slice = SeriesCache.GetOrBuild(7, data, new[] { "A-01" }, 0L, 200L, 1);

            CollectionAssert.AreEqual(new long[] { 0L, 100L, 200L }, slice.Timestamps);
            CollectionAssert.AreEqual(new double?[] { 1d, 2d, 3d }, slice.Series["A-01"]);
        }
    }

    [TestClass]
    public class DataSummaryServiceTests
    {
        [TestMethod]
        public void BuildSummary_UsesFirstAndLastTimestamp()
        {
            var timestampRangeService = new TimestampRangeService();
            var summaryService = new DataSummaryService(timestampRangeService);
            var data = SessionAndChartingTestData.CreateData(new long[] { 1000L, 2500L, 9000L });

            DataSummary summary = summaryService.BuildSummary(data);

            Assert.AreEqual(3, summary.Points);
            Assert.AreEqual(1000L, summary.StartMs);
            Assert.AreEqual(9000L, summary.EndMs);
            Assert.AreEqual(timestampRangeService.UnixMsToLocalDateTime(1000L), summary.Start);
            Assert.AreEqual(timestampRangeService.UnixMsToLocalDateTime(9000L), summary.End);
        }
    }

    [TestClass]
    public class TimestampRangeServiceTests
    {
        [TestMethod]
        public void SliceByTime_SwapsBoundsAndReturnsHalfOpenRange()
        {
            var service = new TimestampRangeService();

            Tuple<int, int> range = service.SliceByTime(new long[] { 10L, 20L, 30L, 40L }, 35L, 15L);

            Assert.AreEqual(1, range.Item1);
            Assert.AreEqual(3, range.Item2);
        }
    }

    [TestClass]
    public class SeriesSliceServiceTests
    {
        [TestMethod]
        public void GetOrBuild_ReusesCachedSliceForSameVersionAndInvalidatesAcrossVersions()
        {
            var cache = new MemorySeriesSliceCache();
            var service = new SeriesSliceService(cache, new TimestampRangeService());
            var data = SessionAndChartingTestData.CreateData(
                new long[] { 0L, 100L, 200L, 300L, 400L },
                new Dictionary<string, double?[]>
                {
                    ["A-01"] = new double?[] { 1d, 2d, 3d, 4d, 5d }
                });

            SeriesSlice first = service.GetOrBuild(1, data, new[] { "A-01" }, 50L, 350L, 2);
            SeriesSlice second = service.GetOrBuild(1, data, new[] { "A-01" }, 50L, 350L, 2);
            SeriesSlice third = service.GetOrBuild(2, data, new[] { "A-01" }, 50L, 350L, 2);

            Assert.AreSame(first, second);
            Assert.AreNotSame(first, third);
            CollectionAssert.AreEqual(new long[] { 100L, 300L }, first.Timestamps);
            CollectionAssert.AreEqual(new double?[] { 2d, 4d }, first.Series["A-01"]);
        }
    }

    internal sealed class RecordingSeriesSliceCache : ISeriesSliceCache
    {
        public int ClearCount { get; private set; }

        public bool TryGet(string key, out SeriesSlice slice)
        {
            slice = null;
            return false;
        }

        public void Set(string key, SeriesSlice slice)
        {
        }

        public void Clear()
        {
            ClearCount++;
        }
    }

    internal static class SessionAndChartingTestData
    {
        public static TestData CreateData(long[] timestamps, Dictionary<string, double?[]> columns = null)
        {
            var data = new TestData
            {
                Root = "C:\\tests\\root",
                RowCount = timestamps == null ? 0 : timestamps.Length,
                TimestampsMs = timestamps ?? new long[0]
            };

            var sourceColumns = columns ?? new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["A-01"] = new double?[data.RowCount]
            };

            foreach (KeyValuePair<string, double?[]> pair in sourceColumns)
            {
                data.Columns[pair.Key] = pair.Value;
                data.Channels[pair.Key] = new ChannelInfo { Code = pair.Key, Name = pair.Key, Unit = "u" };
            }

            data.ColumnNames = new List<string>(sourceColumns.Keys).ToArray();
            return data;
        }
    }
}
