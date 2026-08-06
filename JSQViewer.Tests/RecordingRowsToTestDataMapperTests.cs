using System;
using System.Collections.Generic;
using JSQViewer.Application.Database;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingRowsToTestDataMapperTests
    {
        private const string Source = "jsqdb://recording/abc";

        private static RecordingAggregateRow Row(string channelId, long timestampMs, double value)
        {
            return new RecordingAggregateRow { ChannelId = channelId, TimestampMs = timestampMs, Value = value };
        }

        private static Dictionary<string, ChannelInfo> Channels()
        {
            return new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase)
            {
                { "T1", new ChannelInfo { Code = "T1", Name = "В морозилке", Unit = "C" } },
                { "Pe", new ChannelInfo { Code = "Pe", Name = "Давление всасывания", Unit = "bar" } }
            };
        }

        [TestMethod]
        public void TestData_InitializesSourceIdentityCollections()
        {
            var data = new TestData();

            Assert.IsNotNull(data.SourceDisplayNames);
            Assert.IsNotNull(data.SourceOrder);
            Assert.AreEqual(0, data.SourceOrder.Length);

            data.SourceDisplayNames["SOURCE"] = "Название";
            Assert.AreEqual("Название", data.SourceDisplayNames["source"]);
        }

        [TestMethod]
        public void Map_UsesTrimmedRecordingTitleAsSourceDisplayName()
        {
            TestData data = new RecordingRowsToTestDataMapper().Map(
                Source, "B", new List<RecordingAggregateRow>(), Channels(),
                new Dictionary<string, string> { { "Название", "  Прогон № 1  " } });

            Assert.AreEqual("Прогон № 1", data.SourceDisplayNames[Source]);
            CollectionAssert.AreEqual(new[] { Source }, data.SourceOrder);
        }

        [TestMethod]
        public void Map_BlankRecordingTitleFallsBackToRecordingId()
        {
            TestData data = new RecordingRowsToTestDataMapper().Map(
                Source, "B", new List<RecordingAggregateRow>(), Channels(),
                new Dictionary<string, string> { { "Название", "  " } });

            Assert.AreEqual("abc", data.SourceDisplayNames[Source]);
        }

        [TestMethod]
        public void StripPostPrefix_RemovesOnlyLeadingPostId()
        {
            Assert.AreEqual("T1", ChannelCodeNormalizer.StripPostPrefix("B-T1", "B"));
            Assert.AreEqual("MaxI", ChannelCodeNormalizer.StripPostPrefix("A-MaxI", "A"));
            Assert.AreEqual("T-avg", ChannelCodeNormalizer.StripPostPrefix("C-T-avg", "C"));
            Assert.AreEqual("T1", ChannelCodeNormalizer.StripPostPrefix("T1", "B"));
            Assert.AreEqual("A-T1", ChannelCodeNormalizer.StripPostPrefix("A-T1", "B"));
        }

        [TestMethod]
        public void Map_BuildsWideTableSortedByTimestamp()
        {
            var rows = new List<RecordingAggregateRow>
            {
                Row("B-T1", 2000, 10.5), Row("B-Pe", 2000, 1.4),
                Row("B-T1", 1000, 10.0), Row("B-Pe", 1000, 1.3)
            };

            TestData data = new RecordingRowsToTestDataMapper().Map(
                Source, "B", rows, Channels(), new Dictionary<string, string>());

            Assert.AreEqual(Source, data.Root);
            Assert.AreEqual(2, data.RowCount);
            CollectionAssert.AreEqual(new long[] { 1000, 2000 }, data.TimestampsMs);
            CollectionAssert.AreEqual(new double?[] { 10.0, 10.5 }, data.Columns["T1"]);
            CollectionAssert.AreEqual(new double?[] { 1.3, 1.4 }, data.Columns["Pe"]);
        }

        [TestMethod]
        public void Map_LeavesGapsForWindowsMissingFromResult()
        {
            var rows = new List<RecordingAggregateRow>
            {
                Row("B-T1", 1000, 10.0),
                Row("B-Pe", 1000, 1.3),
                Row("B-Pe", 2000, 1.4)
            };

            TestData data = new RecordingRowsToTestDataMapper().Map(
                Source, "B", rows, Channels(), new Dictionary<string, string>());

            Assert.AreEqual(2, data.RowCount);
            CollectionAssert.AreEqual(new double?[] { 10.0, null }, data.Columns["T1"]);
            CollectionAssert.AreEqual(new double?[] { 1.3, 1.4 }, data.Columns["Pe"]);
        }

        [TestMethod]
        public void Map_FillsSourceBoundsAndCodeSources()
        {
            var rows = new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-T1", 5000, 11.0) };

            TestData data = new RecordingRowsToTestDataMapper().Map(
                Source, "B", rows, Channels(), new Dictionary<string, string> { { "Модель", "LIDER" } });

            Assert.AreEqual(1000L, data.SourceStartMs[Source]);
            Assert.AreEqual(5000L, data.SourceEndMs[Source]);
            Assert.AreEqual(Source, data.CodeSources["T1"]);
            Assert.AreEqual("LIDER", data.Meta["Модель"]);
            CollectionAssert.Contains(data.SourceColumns[Source], "T1");
        }

        [TestMethod]
        public void Map_WithoutRows_ProducesEmptyButValidTestData()
        {
            TestData data = new RecordingRowsToTestDataMapper().Map(
                Source, "B", new List<RecordingAggregateRow>(), Channels(), new Dictionary<string, string>());

            Assert.AreEqual(0, data.RowCount);
            Assert.AreEqual(0, data.TimestampsMs.Length);
            Assert.AreEqual(0L, data.SourceStartMs[Source]);
        }

        [TestMethod]
        public void GetLastTimestampMs_ReturnsTailOrMinusOneWhenEmpty()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData filled = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-T1", 4000, 12.0) },
                Channels(), new Dictionary<string, string>());
            TestData empty = mapper.Map(Source, "B",
                new List<RecordingAggregateRow>(), Channels(), new Dictionary<string, string>());

            Assert.AreEqual(4000L, mapper.GetLastTimestampMs(filled));
            Assert.AreEqual(-1L, mapper.GetLastTimestampMs(empty));
        }

        [TestMethod]
        public void Append_AddsNewWindowsToTail()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-Pe", 1000, 1.3) },
                Channels(), new Dictionary<string, string>());

            TestData appended = mapper.Append(data, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 2000, 10.5), Row("B-Pe", 2000, 1.4) });

            Assert.AreEqual(2, appended.RowCount);
            CollectionAssert.AreEqual(new long[] { 1000, 2000 }, appended.TimestampsMs);
            CollectionAssert.AreEqual(new double?[] { 10.0, 10.5 }, appended.Columns["T1"]);
            Assert.AreEqual(2000L, appended.SourceEndMs[Source]);
        }

        [TestMethod]
        public void Append_WithNewRows_PreservesSourceIdentityMetadata()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0) },
                Channels(), new Dictionary<string, string> { { "Название", "Прогон" } });

            TestData appended = mapper.Append(data, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 2000, 10.5) });

            Assert.AreNotSame(data.SourceDisplayNames, appended.SourceDisplayNames);
            Assert.AreEqual("Прогон", appended.SourceDisplayNames[Source]);
            CollectionAssert.AreEqual(new[] { Source }, appended.SourceOrder);
        }

        [TestMethod]
        public void Append_IgnoresWindowsAlreadyLoaded()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-T1", 2000, 10.5) },
                Channels(), new Dictionary<string, string>());

            TestData appended = mapper.Append(data, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 2000, 99.9), Row("B-T1", 3000, 11.0) });

            Assert.AreEqual(3, appended.RowCount);
            CollectionAssert.AreEqual(new double?[] { 10.0, 10.5, 11.0 }, appended.Columns["T1"]);
        }

        [TestMethod]
        public void Append_WithoutNewRows_ReturnsSameInstance()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0) },
                Channels(), new Dictionary<string, string>());

            TestData appended = mapper.Append(data, "B", new List<RecordingAggregateRow>());

            Assert.AreSame(data, appended);
        }

        [TestMethod]
        public void Append_WithoutNewRows_PreservesSourceIdentityMetadata()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0) },
                Channels(), new Dictionary<string, string> { { "Название", "Прогон" } });
            Dictionary<string, string> displayNames = data.SourceDisplayNames;
            string[] sourceOrder = data.SourceOrder;

            TestData appended = mapper.Append(data, "B", new List<RecordingAggregateRow>());

            Assert.AreSame(data, appended);
            Assert.AreSame(displayNames, appended.SourceDisplayNames);
            Assert.AreSame(sourceOrder, appended.SourceOrder);
            Assert.AreEqual("Прогон", appended.SourceDisplayNames[Source]);
            CollectionAssert.AreEqual(new[] { Source }, appended.SourceOrder);
        }

        [TestMethod]
        public void Append_LeavesGapWhenChannelMissingFromNewWindow()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-Pe", 1000, 1.3) },
                Channels(), new Dictionary<string, string>());

            TestData appended = mapper.Append(data, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 2000, 10.5) });

            CollectionAssert.AreEqual(new double?[] { 10.0, 10.5 }, appended.Columns["T1"]);
            CollectionAssert.AreEqual(new double?[] { 1.3, null }, appended.Columns["Pe"]);
        }

        [TestMethod]
        public void Append_NewChannelGetsFullLengthColumnWithHistoricalGaps()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-T1", 2000, 10.5) },
                Channels(), new Dictionary<string, string>());

            TestData appended = mapper.Append(data, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 3000, 11.0), Row("B-Pe", 3000, 1.5) });

            Assert.AreEqual(appended.TimestampsMs.Length, appended.Columns["Pe"].Length);
            CollectionAssert.AreEqual(new double?[] { null, null, 1.5 }, appended.Columns["Pe"]);
        }

        [TestMethod]
        public void Append_DoesNotChangeSourceStartMs()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-T1", 2000, 10.5) },
                Channels(), new Dictionary<string, string>());

            TestData appended = mapper.Append(data, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 3000, 11.0) });

            Assert.AreEqual(1000L, appended.SourceStartMs[Source]);
            Assert.AreEqual(3000L, appended.SourceEndMs[Source]);
        }

        [TestMethod]
        public void Append_DropsWindowFallingInsideAlreadyLoadedRange()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-T1", 3000, 11.0) },
                Channels(), new Dictionary<string, string>());

            TestData appended = mapper.Append(data, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 2000, 99.9), Row("B-T1", 4000, 12.0) });

            Assert.AreEqual(3, appended.RowCount);
            CollectionAssert.AreEqual(new long[] { 1000, 3000, 4000 }, appended.TimestampsMs);
        }
    }
}
