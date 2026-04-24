using System.Collections.Generic;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.UiState;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class UiShellStateServiceTests
    {
        [TestMethod]
        public void LoadRecentFolders_ReturnsRepositoryContents()
        {
            var recentRepo = new FakeRecentFoldersRepository
            {
                LoadedFolders = new List<string> { @"C:\data\rec1", @"C:\data\rec2", string.Empty, "  " }
            };
            var uiRepo = new FakeUiStateRepository();
            var service = new UiShellStateService(recentRepo, uiRepo);

            List<string> result = service.LoadRecentFolders();

            Assert.AreEqual(1, recentRepo.LoadCalls);
            CollectionAssert.AreEqual(new[] { @"C:\data\rec1", @"C:\data\rec2" }, result);
        }

        [TestMethod]
        public void AddRecentFolder_AddsToFront_DedupsCaseInsensitive_MaxTwelve()
        {
            var recentRepo = new FakeRecentFoldersRepository();
            var uiRepo = new FakeUiStateRepository();
            var service = new UiShellStateService(recentRepo, uiRepo);

            var current = new List<string>
            {
                @"C:\data\B",
                @"C:\data\C",
                @"C:\data\D",
                @"C:\data\E",
                @"C:\data\F",
                @"C:\data\G",
                @"C:\data\H",
                @"C:\data\I",
                @"C:\data\J",
                @"C:\data\K",
                @"C:\data\L",
                @"C:\data\M",
                @"C:\data\N"
            };

            // Adding a new folder — should push oldest off, max 12
            List<string> result = service.AddRecentFolder(current, @"C:\data\A");

            Assert.AreEqual(@"C:\data\A", result[0]);
            Assert.AreEqual(12, result.Count);
            Assert.AreEqual(1, recentRepo.SaveCalls);

            // Adding a duplicate (case insensitive) — should move to front, no duplicate
            List<string> result2 = service.AddRecentFolder(result, @"c:\data\d");

            Assert.AreEqual(@"c:\data\d", result2[0]);
            Assert.IsFalse(result2.Contains(@"C:\data\D"), "Original casing should be removed");
            Assert.AreEqual(12, result2.Count);
        }

        [TestMethod]
        public void AddRecentFolder_EmptyInput_IsIgnored()
        {
            var recentRepo = new FakeRecentFoldersRepository();
            var uiRepo = new FakeUiStateRepository();
            var service = new UiShellStateService(recentRepo, uiRepo);

            var current = new List<string> { @"C:\data\A" };

            List<string> resultNull = service.AddRecentFolder(current, null);
            List<string> resultEmpty = service.AddRecentFolder(current, "   ");

            CollectionAssert.AreEqual(current, resultNull);
            CollectionAssert.AreEqual(current, resultEmpty);
            Assert.AreEqual(0, recentRepo.SaveCalls);
        }

        [TestMethod]
        public void LoadUiState_ReturnsRepositoryModel()
        {
            var recentRepo = new FakeRecentFoldersRepository();
            var model = new UiStateModel { folder = @"C:\data\test" };
            var uiRepo = new FakeUiStateRepository { LoadedModel = model };
            var service = new UiShellStateService(recentRepo, uiRepo);

            UiStateModel result = service.LoadUiState();

            Assert.AreEqual(1, uiRepo.LoadCalls);
            Assert.AreSame(model, result);
        }

        [TestMethod]
        public void SaveUiState_DelegatesToRepository()
        {
            var recentRepo = new FakeRecentFoldersRepository();
            var uiRepo = new FakeUiStateRepository();
            var service = new UiShellStateService(recentRepo, uiRepo);
            var model = new UiStateModel { folder = @"C:\data\save" };

            bool result = service.SaveUiState(model);

            Assert.IsTrue(result);
            Assert.AreEqual(1, uiRepo.SaveCalls);
            Assert.AreSame(model, uiRepo.SavedModel);
        }

        private sealed class FakeRecentFoldersRepository : IRecentFoldersRepository
        {
            public List<string> LoadedFolders { get; set; } = new List<string>();
            public List<string> SavedFolders { get; private set; }
            public int LoadCalls { get; private set; }
            public int SaveCalls { get; private set; }

            public List<string> Load()
            {
                LoadCalls++;
                return LoadedFolders;
            }

            public bool Save(IList<string> folders)
            {
                SaveCalls++;
                SavedFolders = new List<string>(folders);
                return true;
            }
        }

        private sealed class FakeUiStateRepository : IUiStateRepository
        {
            public UiStateModel LoadedModel { get; set; }
            public UiStateModel SavedModel { get; private set; }
            public int LoadCalls { get; private set; }
            public int SaveCalls { get; private set; }

            public UiStateModel Load()
            {
                LoadCalls++;
                return LoadedModel;
            }

            public bool Save(UiStateModel state)
            {
                SaveCalls++;
                SavedModel = state;
                return true;
            }
        }
    }
}
