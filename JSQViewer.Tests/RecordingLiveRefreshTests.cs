using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Application.Workspace;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;
using JSQViewer.UI;
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
            Assert.AreEqual("Прогон ABC", secondRefresh.SourceDisplayNames[initial.Root]);
            CollectionAssert.AreEqual(new[] { initial.Root }, secondRefresh.SourceOrder);
        }

        [TestMethod]
        public void RecordingSourceRef_RecognizesOnlyTheExactRecordingSource()
        {
            string recordingId;

            Assert.IsTrue(RecordingSourceRef.TryParse("jsqdb://recording/abc", out recordingId));
            Assert.AreEqual("abc", recordingId);
            Assert.IsFalse(RecordingSourceRef.IsRecordingSource(@"C:\data ; jsqdb://recording/abc"));
        }

        [TestMethod]
        public void TryGetSingleLiveRecordingId_SingleCanonicalRecordingSource_ReturnsId()
        {
            string recordingId;

            bool result = InvokeTryGetSingleLiveRecordingId("jsqdb://recording/abc", out recordingId);

            Assert.IsTrue(result);
            Assert.AreEqual("abc", recordingId);
        }

        [TestMethod]
        public void TryGetSingleLiveRecordingId_TwoRecordingSources_ReturnsFalse()
        {
            string recordingId;

            bool result = InvokeTryGetSingleLiveRecordingId(
                "jsqdb://recording/abc ; jsqdb://recording/def",
                out recordingId);

            Assert.IsFalse(result);
            Assert.IsNull(recordingId);
        }

        [TestMethod]
        public void TryGetSingleLiveRecordingId_FolderAndRecordingSource_ReturnsFalse()
        {
            string recordingId;

            bool result = InvokeTryGetSingleLiveRecordingId(
                @"C:\data ; jsqdb://recording/abc",
                out recordingId);

            Assert.IsFalse(result);
            Assert.IsNull(recordingId);
        }

        [TestMethod]
        public void TryGetSingleLiveRecordingId_NonCanonicalSingleSource_ReturnsFalse()
        {
            string recordingId;

            bool result = InvokeTryGetSingleLiveRecordingId(@"C:\data", out recordingId);

            Assert.IsFalse(result);
            Assert.IsNull(recordingId);
        }

        private static bool InvokeTryGetSingleLiveRecordingId(string spec, out string recordingId)
        {
            MethodInfo method = typeof(MainForm).GetMethod(
                "TryGetSingleLiveRecordingId",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "TryGetSingleLiveRecordingId должен быть private static helper MainForm.");

            var service = new WorkspaceLoadOrchestrationService(
                new WorkspaceFolderSpecParser(),
                new NullFileSystem());
            object[] args = { service, spec, null };
            bool result = (bool)method.Invoke(null, args);
            recordingId = (string)args[2];
            return result;
        }

        private sealed class NullFileSystem : IFileSystem
        {
            public bool DirectoryExists(string path) => false;

            public bool FileExists(string path) => false;

            public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => new string[0];

            public System.DateTime GetLastWriteTime(string path) => System.DateTime.MinValue;

            public void WriteAllBytes(string path, byte[] contents) { }

            public void CreateDirectory(string path) { }

            public void AppendAllText(string path, string contents, Encoding encoding) { }
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
                    new Dictionary<string, string>
                    {
                        { "Название", "Прогон ABC" }
                    });
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
