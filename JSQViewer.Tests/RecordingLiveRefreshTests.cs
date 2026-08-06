using System.Collections.Generic;
using JSQViewer.Application.Database;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingLiveRefreshTests
    {
        [TestMethod]
        public void GrowingRecordingReader_AppendsNewWindowsWithoutReplacingExistingRows()
        {
            var reader = new GrowingRecordingReader();

            TestData initial = reader.ReadRecording("abc");
            TestData firstRefresh = reader.AppendNewWindows(initial, "abc");
            TestData secondRefresh = reader.AppendNewWindows(firstRefresh, "abc");

            Assert.AreEqual(1, initial.RowCount);
            Assert.AreEqual(2, firstRefresh.RowCount);
            Assert.AreEqual(3, secondRefresh.RowCount);
            CollectionAssert.AreEqual(new long[] { 1000L, 2000L, 3000L }, secondRefresh.TimestampsMs);
            CollectionAssert.AreEqual(
                new double?[] { 10.0, 11.0, 11.0 },
                secondRefresh.Columns["T1"]);
        }

        [TestMethod]
        public void RecordingSourceRef_RecognizesOnlyTheExactRecordingSource()
        {
            string recordingId;

            Assert.IsTrue(RecordingSourceRef.TryParse("jsqdb://recording/abc", out recordingId));
            Assert.AreEqual("abc", recordingId);
            Assert.IsFalse(RecordingSourceRef.IsRecordingSource(@"C:\data ; jsqdb://recording/abc"));
        }

        private sealed class GrowingRecordingReader : IRecordingDataReader
        {
            private readonly RecordingRowsToTestDataMapper _mapper = new RecordingRowsToTestDataMapper();
            private int _appendCount;

            public TestData ReadRecording(string recordingId)
            {
                return _mapper.Map(
                    RecordingSourceRef.Build(recordingId),
                    "post-1",
                    new List<RecordingAggregateRow>
                    {
                        NewRow(1000L, 10.0)
                    },
                    new Dictionary<string, ChannelInfo>(),
                    new Dictionary<string, string>());
            }

            public TestData AppendNewWindows(TestData existing, string recordingId)
            {
                _appendCount++;
                long timestamp = _appendCount == 1 ? 2000L : 3000L;
                return _mapper.Append(
                    existing,
                    "post-1",
                    new List<RecordingAggregateRow>
                    {
                        NewRow(timestamp, 11.0)
                    });
            }

            private static RecordingAggregateRow NewRow(long timestampMs, double value)
            {
                return new RecordingAggregateRow
                {
                    ChannelId = "T1",
                    TimestampMs = timestampMs,
                    Value = value
                };
            }
        }
    }
}
