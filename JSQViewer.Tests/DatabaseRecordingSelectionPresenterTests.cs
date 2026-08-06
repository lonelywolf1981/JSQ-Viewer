using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Workspace;
using JSQViewer.Presentation.WinForms.Presenters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class DatabaseRecordingSelectionPresenterTests
    {
        [TestMethod]
        public void ApplySelection_NewSource_LoadsCombinedSpecWithoutNotification()
        {
            var loadedSpecs = new List<string>();
            var notificationKeys = new List<string>();
            var presenter = new DatabaseRecordingSelectionPresenter(CreateWorkspaceService());

            presenter.ApplySelection(
                "jsqdb://recording/one",
                new[] { "jsqdb://recording/two" },
                loadedSpecs.Add,
                notificationKeys.Add);

            CollectionAssert.AreEqual(
                new[] { "jsqdb://recording/one ; jsqdb://recording/two" },
                loadedSpecs);
            Assert.AreEqual(0, notificationKeys.Count);
        }

        [TestMethod]
        public void ApplySelection_OnlyDuplicates_NotifiesWithoutLoading()
        {
            var loadedSpecs = new List<string>();
            var notificationKeys = new List<string>();
            var presenter = new DatabaseRecordingSelectionPresenter(CreateWorkspaceService());

            presenter.ApplySelection(
                "jsqdb://recording/one",
                new[] { "JSQDB://RECORDING/ONE" },
                loadedSpecs.Add,
                notificationKeys.Add);

            Assert.AreEqual(0, loadedSpecs.Count);
            CollectionAssert.AreEqual(new[] { "SourceAlreadyAdded" }, notificationKeys);
        }

        [TestMethod]
        public void ApplySelection_LimitExceeded_NotifiesWithoutLoading()
        {
            var loadedSpecs = new List<string>();
            var notificationKeys = new List<string>();
            var presenter = new DatabaseRecordingSelectionPresenter(CreateWorkspaceService());

            presenter.ApplySelection(
                @"C:\a ; C:\b ; C:\c ; C:\d ; C:\e",
                new[] { "jsqdb://recording/one", "jsqdb://recording/two" },
                loadedSpecs.Add,
                notificationKeys.Add);

            Assert.AreEqual(0, loadedSpecs.Count);
            CollectionAssert.AreEqual(new[] { "TooManyFolders" }, notificationKeys);
        }

        [TestMethod]
        public void ApplySelection_FiveCurrentWithDuplicateAndNew_LoadsSixSources()
        {
            var loadedSpecs = new List<string>();
            var notificationKeys = new List<string>();
            var presenter = new DatabaseRecordingSelectionPresenter(CreateWorkspaceService());

            presenter.ApplySelection(
                @"C:\a ; C:\b ; C:\c ; C:\d ; jsqdb://recording/old",
                new[] { "JSQDB://RECORDING/OLD", "jsqdb://recording/new" },
                loadedSpecs.Add,
                notificationKeys.Add);

            Assert.AreEqual(0, notificationKeys.Count);
            CollectionAssert.AreEqual(
                new[] { @"C:\a ; C:\b ; C:\c ; C:\d ; jsqdb://recording/old ; jsqdb://recording/new" },
                loadedSpecs);
        }

        [TestMethod]
        public void ApplySelection_RequiresCallbacks()
        {
            var presenter = new DatabaseRecordingSelectionPresenter(CreateWorkspaceService());

            Assert.ThrowsException<ArgumentNullException>(() => presenter.ApplySelection(
                "jsqdb://recording/one",
                new[] { "jsqdb://recording/two" },
                null,
                key => { }));
            Assert.ThrowsException<ArgumentNullException>(() => presenter.ApplySelection(
                "jsqdb://recording/one",
                new[] { "jsqdb://recording/two" },
                spec => { },
                null));
        }

        private static WorkspaceLoadOrchestrationService CreateWorkspaceService()
        {
            return new WorkspaceLoadOrchestrationService(
                new WorkspaceFolderSpecParser(),
                new NullFileSystem());
        }

        private sealed class NullFileSystem : IFileSystem
        {
            public bool DirectoryExists(string path) => false;

            public bool FileExists(string path) => false;

            public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => new string[0];

            public DateTime GetLastWriteTime(string path) => DateTime.MinValue;

            public void WriteAllBytes(string path, byte[] contents) { }

            public void CreateDirectory(string path) { }

            public void AppendAllText(string path, string contents, Encoding encoding) { }
        }
    }
}
