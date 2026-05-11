using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Recording;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class GetRecordingInfoUseCaseTests
    {
        private static readonly string Root = @"C:\Data\Test";

        private static TestData MakeData(string root, long startMs, long endMs,
            string columnName, double?[] values)
        {
            int count = values.Length;
            long[] timestamps = new long[count];
            for (int i = 0; i < count; i++)
                timestamps[i] = startMs + (count > 1 ? (long)((endMs - startMs) * i / (double)(count - 1)) : 0);

            return new TestData
            {
                RowCount = count,
                TimestampsMs = timestamps,
                ColumnNames = new[] { columnName },
                Columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    { [columnName] = values },
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                    { [root] = new[] { columnName } },
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                    { [root] = startMs },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                    { [root] = endMs },
                Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    { ["Модель"] = "KA140", ["Хладагент"] = "R600a" }
            };
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsCorrectMin()
        {
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { -20.0, -35.0, -38.4, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1Min);
            Assert.AreEqual(-38.4, r.T1Min.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsCorrectMinTime()
        {
            long startMs = 1_000_000_000L;
            long endMs   = 1_000_060_000L;
            var data = MakeData(Root, startMs, endMs, "T1",
                new double?[] { -20.0, -35.0, -38.4, -30.0 });
            var ts = new TimestampRangeService();
            var uc = new GetRecordingInfoUseCase(ts);

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1MinTime);
            DateTime expected = ts.UnixMsToLocalDateTime(startMs + 40_000);
            Assert.AreEqual(expected, r.T1MinTime.Value);
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsDropRate()
        {
            // first=-20, min=-38.4, duration=1 мин → rate = (-38.4 - (-20)) / 1 = -18.4
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { -20.0, -35.0, -38.4, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1DropRatePerMinute);
            Assert.AreEqual(-18.4, r.T1DropRatePerMinute.Value, 0.01);
        }

        [TestMethod]
        public void Execute_WithPrefixedT1_FindsChannel()
        {
            var data = MakeData(Root, 0, 60_000, "A-T1",
                new double?[] { -10.0, -25.0, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1Min, "A-T1 должен распознаваться как T1");
            Assert.AreEqual(-30.0, r.T1Min.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithNoT1Channel_ReturnsNullStats()
        {
            var data = MakeData(Root, 0, 60_000, "P1",
                new double?[] { 1.0, 2.0, 3.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNull(r.T1Min);
            Assert.IsNull(r.T1MinTime);
            Assert.IsNull(r.T1DropRatePerMinute);
        }

        [TestMethod]
        public void Execute_ReturnsMeta()
        {
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { -10.0, -20.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.Meta);
            Assert.IsTrue(r.Meta.Any(kv => kv.Key == "Модель" && kv.Value == "KA140"));
            Assert.IsTrue(r.Meta.Any(kv => kv.Key == "Хладагент" && kv.Value == "R600a"));
        }

        [TestMethod]
        public void Execute_SingleRow_DropRateIsNull()
        {
            var data = MakeData(Root, 0, 0, "T1",
                new double?[] { -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNull(r.T1DropRatePerMinute);
        }
    }
}
