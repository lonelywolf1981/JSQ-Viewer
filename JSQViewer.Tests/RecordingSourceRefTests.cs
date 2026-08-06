using JSQViewer.Application.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingSourceRefTests
    {
        [TestMethod]
        public void Build_ProducesCanonicalSource()
        {
            Assert.AreEqual("jsqdb://recording/21edc519ba594f94", RecordingSourceRef.Build("21edc519ba594f94"));
        }

        [TestMethod]
        public void TryParse_ExtractsRecordingId()
        {
            string recordingId;

            Assert.IsTrue(RecordingSourceRef.TryParse("jsqdb://recording/21edc519ba594f94", out recordingId));
            Assert.AreEqual("21edc519ba594f94", recordingId);
        }

        [TestMethod]
        public void TryParse_IgnoresSurroundingWhitespaceAndSchemeCase()
        {
            string recordingId;

            Assert.IsTrue(RecordingSourceRef.TryParse("  JSQDB://RECORDING/abc123  ", out recordingId));
            Assert.AreEqual("abc123", recordingId);
        }

        [TestMethod]
        public void TryParse_RejectsFoldersProtocolsAndEmptyIds()
        {
            string recordingId;

            Assert.IsFalse(RecordingSourceRef.TryParse(@"C:\data\test", out recordingId));
            Assert.IsFalse(RecordingSourceRef.TryParse(@"C:\data\protocol.xlsx", out recordingId));
            Assert.IsFalse(RecordingSourceRef.TryParse("jsqdb://recording/", out recordingId));
            Assert.IsFalse(RecordingSourceRef.TryParse(null, out recordingId));
            Assert.IsFalse(RecordingSourceRef.TryParse("jsqdb://session/abc", out recordingId));
        }

        [TestMethod]
        public void IsRecordingSource_MatchesTryParse()
        {
            Assert.IsTrue(RecordingSourceRef.IsRecordingSource("jsqdb://recording/abc"));
            Assert.IsFalse(RecordingSourceRef.IsRecordingSource(@"C:\data\test"));
        }
    }
}
