using System;
using JSQViewer.Application.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingSummaryItemTests
    {
        [TestMethod]
        public void ToSourceString_BuildsRecordingSourceThatCanBeRecognized()
        {
            var item = new RecordingSummaryItem { Id = "recording-42" };

            string source = item.ToSourceString();

            Assert.AreEqual("jsqdb://recording/recording-42", source);
            Assert.IsTrue(RecordingSourceRef.IsRecordingSource(source));
        }

        [TestMethod]
        public void DurationHours_UsesStoppedAtWhenAvailable()
        {
            var item = new RecordingSummaryItem
            {
                StartedAt = new DateTime(2026, 8, 6, 9, 0, 0),
                StoppedAt = new DateTime(2026, 8, 6, 12, 30, 0)
            };

            Assert.AreEqual(3.5, item.DurationHours, 0.0001);
        }

        [TestMethod]
        public void DurationHours_ReturnsZeroWhenStartIsMissing()
        {
            var item = new RecordingSummaryItem
            {
                StoppedAt = new DateTime(2026, 8, 6, 12, 30, 0)
            };

            Assert.AreEqual(0.0, item.DurationHours, 0.0001);
        }

        [TestMethod]
        public void IsActive_IsTrueOnlyForRecordingStatus()
        {
            var item = new RecordingSummaryItem { Status = "recording" };
            Assert.IsTrue(item.IsActive);

            item.Status = "RECORDING";
            Assert.IsTrue(item.IsActive);

            item.Status = "stopped";
            Assert.IsFalse(item.IsActive);

            item.Status = null;
            Assert.IsFalse(item.IsActive);
        }
    }
}
