using System.Collections.Generic;
using JSQViewer.Core;
using JSQViewer.Presentation.WinForms.Presenters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class WorkspaceLoadRollbackCoordinatorTests
    {
        [TestMethod]
        public void RestoreAfterFailure_UnchangedSession_RestoresTextAndLiveRefresh()
        {
            var calls = new List<string>();
            var data = new TestData();
            var coordinator = new WorkspaceLoadRollbackCoordinator();

            coordinator.RestoreAfterFailure(
                false,
                true,
                data,
                "jsqdb://recording/a",
                data,
                "jsqdb://recording/a",
                () => calls.Add("text"),
                () => calls.Add("live"));

            CollectionAssert.AreEqual(new[] { "text", "live" }, calls);
        }

        [TestMethod]
        public void RestoreAfterFailure_SuccessfulLoad_DoesNotRestore()
        {
            var calls = new List<string>();
            var data = new TestData();
            var coordinator = new WorkspaceLoadRollbackCoordinator();

            coordinator.RestoreAfterFailure(
                true,
                true,
                data,
                "jsqdb://recording/a",
                data,
                "jsqdb://recording/a",
                () => calls.Add("text"),
                () => calls.Add("live"));

            Assert.AreEqual(0, calls.Count);
        }

        [TestMethod]
        public void RestoreAfterFailure_StaleGeneration_DoesNotRestore()
        {
            var calls = new List<string>();
            var data = new TestData();
            var coordinator = new WorkspaceLoadRollbackCoordinator();

            coordinator.RestoreAfterFailure(
                false,
                false,
                data,
                "jsqdb://recording/a",
                data,
                "jsqdb://recording/a",
                () => calls.Add("text"),
                () => calls.Add("live"));

            Assert.AreEqual(0, calls.Count);
        }

        [TestMethod]
        public void RestoreAfterFailure_SessionDataReplaced_DoesNotRestore()
        {
            var calls = new List<string>();
            var coordinator = new WorkspaceLoadRollbackCoordinator();

            coordinator.RestoreAfterFailure(
                false,
                true,
                new TestData(),
                "jsqdb://recording/a",
                new TestData(),
                "jsqdb://recording/a",
                () => calls.Add("text"),
                () => calls.Add("live"));

            Assert.AreEqual(0, calls.Count);
        }

        [TestMethod]
        public void RestoreAfterFailure_SessionFolderChanged_DoesNotRestore()
        {
            var calls = new List<string>();
            var data = new TestData();
            var coordinator = new WorkspaceLoadRollbackCoordinator();

            coordinator.RestoreAfterFailure(
                false,
                true,
                data,
                "jsqdb://recording/a",
                data,
                "jsqdb://recording/b",
                () => calls.Add("text"),
                () => calls.Add("live"));

            Assert.AreEqual(0, calls.Count);
        }

        [TestMethod]
        public void RestoreAfterFailure_NullCallbacks_AreIgnored()
        {
            var data = new TestData();
            var coordinator = new WorkspaceLoadRollbackCoordinator();

            coordinator.RestoreAfterFailure(
                false,
                true,
                data,
                null,
                data,
                null,
                null,
                null);
        }
    }
}
