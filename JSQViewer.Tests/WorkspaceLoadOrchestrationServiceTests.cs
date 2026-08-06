using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Workspace;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class WorkspaceLoadOrchestrationServiceTests
    {
        [TestMethod]
        public void ParseSpec_ParsesAndDeduplicates()
        {
            var service = CreateService(new FakeFileSystem());

            IReadOnlyList<string> result = service.ParseSpec("A;B;A");

            CollectionAssert.AreEqual(new[] { "A", "B" }, result.ToArray());
        }

        [TestMethod]
        public void JoinSpec_JoinsFolders()
        {
            var service = CreateService(new FakeFileSystem());

            string result = service.JoinSpec(new[] { "A", "B" });

            Assert.AreEqual("A ; B", result);
        }

        [TestMethod]
        public void IsValidSpec_ReturnsFalse_WhenEmpty()
        {
            var service = CreateService(new FakeFileSystem());

            bool result = service.IsValidSpec("");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsValidSpec_ReturnsFalse_WhenTooManyFolders()
        {
            int tooMany = WorkspaceFolderSpecParser.MaxFolderCount + 1;
            var fs = new FakeFileSystem();
            for (int i = 0; i < tooMany; i++)
            {
                fs.ExistingDirectories.Add("Folder" + i);
            }

            var service = CreateService(fs);
            string spec = string.Join(";", Enumerable.Range(0, tooMany).Select(i => "Folder" + i));

            bool result = service.IsValidSpec(spec);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsValidSpec_ReturnsFalse_WhenDirectoryNotFound()
        {
            var fs = new FakeFileSystem();
            // "A" is NOT added to existing directories → DirectoryExists returns false

            var service = CreateService(fs);

            bool result = service.IsValidSpec("A");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsValidSpec_ReturnsTrue_WhenFoldersExist()
        {
            var fs = new FakeFileSystem();
            fs.ExistingDirectories.Add("A");
            fs.ExistingDirectories.Add("B");

            var service = CreateService(fs);

            bool result = service.IsValidSpec("A;B");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsValidSpec_ReturnsTrue_WhenExportedProtocolFileExists()
        {
            var fs = new FakeFileSystem();
            fs.ExistingFiles.Add("A.xlsx");

            var service = CreateService(fs);

            bool result = service.IsValidSpec("A.xlsx");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsValidSpec_ReturnsFalse_WhenExportedProtocolFileMissing()
        {
            var service = CreateService(new FakeFileSystem());

            bool result = service.IsValidSpec("A.xlsx");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ResolveSelectedFolderSource_ReturnsFolder_WhenDatExists()
        {
            var fs = new FakeFileSystem();
            fs.ExistingDirectories.Add(@"C:\Data\Test");
            fs.FilesByPattern[MakeKey(@"C:\Data\Test", "*.dat", SearchOption.TopDirectoryOnly)] =
                new[] { @"C:\Data\Test\Prova001.dat" };
            fs.FilesByPattern[MakeKey(@"C:\Data\Test", "*.xlsx", SearchOption.TopDirectoryOnly)] =
                new[] { @"C:\Data\Test\Protocol.xlsx" };
            var service = CreateService(fs);

            string result = service.ResolveSelectedFolderSource(@"C:\Data\Test");

            Assert.AreEqual(@"C:\Data\Test", result);
        }

        [TestMethod]
        public void ResolveSelectedFolderSource_ReturnsNewestXlsx_WhenFolderHasNoDat()
        {
            var fs = new FakeFileSystem();
            fs.ExistingDirectories.Add(@"C:\Data\Test");
            fs.FilesByPattern[MakeKey(@"C:\Data\Test", "*.xlsx", SearchOption.TopDirectoryOnly)] =
                new[] { @"C:\Data\Test\Old.xlsx", @"C:\Data\Test\New.xlsx" };
            fs.LastWriteTimes[@"C:\Data\Test\Old.xlsx"] = new System.DateTime(2026, 5, 1, 10, 0, 0);
            fs.LastWriteTimes[@"C:\Data\Test\New.xlsx"] = new System.DateTime(2026, 5, 2, 10, 0, 0);
            var service = CreateService(fs);

            string result = service.ResolveSelectedFolderSource(@"C:\Data\Test");

            Assert.AreEqual(@"C:\Data\Test\New.xlsx", result);
        }

        [TestMethod]
        public void ResolveSelectedFolderSource_ReturnsFolder_WhenFolderHasNoDatOrXlsx()
        {
            var fs = new FakeFileSystem();
            fs.ExistingDirectories.Add(@"C:\Data\Test");
            var service = CreateService(fs);

            string result = service.ResolveSelectedFolderSource(@"C:\Data\Test");

            Assert.AreEqual(@"C:\Data\Test", result);
        }

        [TestMethod]
        public void CreateLoadRequest_SetsSpecAndSplitTrue()
        {
            var service = CreateService(new FakeFileSystem());

            WorkspaceLoadRequest request = service.CreateLoadRequest("A ; B");

            Assert.AreEqual("A ; B", request.FolderSpec);
            Assert.IsTrue(request.SplitOverlappingCodes);
        }

        [TestMethod]
        public void BuildWorkspaceKey_IsDeterministicAndOrderIndependent()
        {
            var service = CreateService(new FakeFileSystem());

            string keyAB = service.BuildWorkspaceKey(new[] { "A", "B" });
            string keyBA = service.BuildWorkspaceKey(new[] { "B", "A" });

            Assert.IsNotNull(keyAB);
            Assert.AreEqual(keyAB, keyBA);
        }

        [TestMethod]
        public void AddSources_MixedWorkspace_AppendsSelectedKeepingOrder()
        {
            WorkspaceLoadOrchestrationService service = CreateService(new FakeFileSystem());

            WorkspaceSourceAdditionResult result = service.AddSources(
                @"C:\Data\Test ; C:\Data\Protocol.xlsx",
                new[] { "jsqdb://recording/new" });

            Assert.AreEqual(WorkspaceSourceAdditionStatus.Success, result.Status);
            CollectionAssert.AreEqual(
                new[] { @"C:\Data\Test", @"C:\Data\Protocol.xlsx", "jsqdb://recording/new" },
                result.Sources.ToArray());
            Assert.AreEqual(
                @"C:\Data\Test ; C:\Data\Protocol.xlsx ; jsqdb://recording/new",
                result.FolderSpec);
        }

        [TestMethod]
        public void AddSources_DuplicateOfCurrentSource_IsIgnoredCaseInsensitively()
        {
            WorkspaceLoadOrchestrationService service = CreateService(new FakeFileSystem());

            WorkspaceSourceAdditionResult result = service.AddSources(
                "jsqdb://recording/old",
                new[] { "JSQDB://RECORDING/OLD", "jsqdb://recording/new" });

            Assert.AreEqual(WorkspaceSourceAdditionStatus.Success, result.Status);
            CollectionAssert.AreEqual(
                new[] { "jsqdb://recording/old", "jsqdb://recording/new" },
                result.Sources.ToArray());
        }

        [TestMethod]
        public void AddSources_OnlyDuplicates_ReturnsNoNewSources()
        {
            WorkspaceLoadOrchestrationService service = CreateService(new FakeFileSystem());

            WorkspaceSourceAdditionResult result = service.AddSources(
                "jsqdb://recording/old",
                new[] { "JSQDB://RECORDING/OLD", "  jsqdb://recording/old  " });

            Assert.AreEqual(WorkspaceSourceAdditionStatus.NoNewSources, result.Status);
            CollectionAssert.AreEqual(new[] { "jsqdb://recording/old" }, result.Sources.ToArray());
            Assert.AreEqual("jsqdb://recording/old", result.FolderSpec);
        }

        [TestMethod]
        public void AddSources_FiveCurrentDuplicateAndNew_AddsOnlyNewToSix()
        {
            WorkspaceLoadOrchestrationService service = CreateService(new FakeFileSystem());
            string current = @"C:\a ; C:\b ; C:\c ; C:\d ; jsqdb://recording/old";

            WorkspaceSourceAdditionResult result = service.AddSources(
                current,
                new[] { "JSQDB://RECORDING/OLD", "jsqdb://recording/new" });

            Assert.AreEqual(WorkspaceSourceAdditionStatus.Success, result.Status);
            Assert.AreEqual(6, result.Sources.Count);
            Assert.AreEqual("jsqdb://recording/new", result.Sources[5]);
        }

        [TestMethod]
        public void AddSources_FiveCurrentAndTwoNew_RejectsWholeOperation()
        {
            WorkspaceLoadOrchestrationService service = CreateService(new FakeFileSystem());
            string current = @"C:\a ; C:\b ; C:\c ; C:\d ; C:\e";

            WorkspaceSourceAdditionResult result = service.AddSources(
                current,
                new[] { "jsqdb://recording/one", "jsqdb://recording/two" });

            Assert.AreEqual(WorkspaceSourceAdditionStatus.LimitExceeded, result.Status);
            CollectionAssert.AreEqual(
                new[] { @"C:\a", @"C:\b", @"C:\c", @"C:\d", @"C:\e" },
                result.Sources.ToArray());
            Assert.AreEqual(@"C:\a ; C:\b ; C:\c ; C:\d ; C:\e", result.FolderSpec);
        }

        [TestMethod]
        public void AddSources_FullWorkspace_ReturnsLimitExceeded()
        {
            WorkspaceLoadOrchestrationService service = CreateService(new FakeFileSystem());
            string current = @"C:\a ; C:\b ; C:\c ; C:\d ; C:\e ; C:\f";

            WorkspaceSourceAdditionResult result = service.AddSources(
                current,
                new[] { "jsqdb://recording/one" });

            Assert.AreEqual(WorkspaceSourceAdditionStatus.LimitExceeded, result.Status);
            Assert.AreEqual(6, result.Sources.Count);
            Assert.AreEqual(current, result.FolderSpec);
        }

        [TestMethod]
        public void AddSources_DifferentRecordingUris_StayTwoSeparateSources()
        {
            WorkspaceLoadOrchestrationService service = CreateService(new FakeFileSystem());

            WorkspaceSourceAdditionResult result = service.AddSources(
                "jsqdb://recording/a",
                new[] { "jsqdb://recording/b" });

            Assert.AreEqual(WorkspaceSourceAdditionStatus.Success, result.Status);
            CollectionAssert.AreEqual(
                new[] { "jsqdb://recording/a", "jsqdb://recording/b" },
                result.Sources.ToArray());
        }

        [TestMethod]
        public void AddSources_EmptySelection_ReturnsNoNewSources()
        {
            WorkspaceLoadOrchestrationService service = CreateService(new FakeFileSystem());

            WorkspaceSourceAdditionResult result = service.AddSources(
                "jsqdb://recording/a",
                null);

            Assert.AreEqual(WorkspaceSourceAdditionStatus.NoNewSources, result.Status);
            CollectionAssert.AreEqual(new[] { "jsqdb://recording/a" }, result.Sources.ToArray());
        }

        private static WorkspaceLoadOrchestrationService CreateService(IFileSystem fileSystem)
        {
            return new WorkspaceLoadOrchestrationService(new WorkspaceFolderSpecParser(), fileSystem);
        }

        private static string MakeKey(string path, string pattern, SearchOption searchOption)
        {
            return path + "|" + pattern + "|" + searchOption;
        }

        private sealed class FakeFileSystem : IFileSystem
        {
            public HashSet<string> ExistingDirectories { get; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            public HashSet<string> ExistingFiles { get; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, string[]> FilesByPattern { get; } =
                new Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, System.DateTime> LastWriteTimes { get; } =
                new Dictionary<string, System.DateTime>(System.StringComparer.OrdinalIgnoreCase);

            public bool DirectoryExists(string path) => ExistingDirectories.Contains(path);

            public bool FileExists(string path) => ExistingFiles.Contains(path);

            public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
            {
                string[] files;
                return FilesByPattern.TryGetValue(MakeKey(path, searchPattern, searchOption), out files)
                    ? files
                    : new string[0];
            }

            public System.DateTime GetLastWriteTime(string path)
            {
                System.DateTime time;
                return LastWriteTimes.TryGetValue(path, out time) ? time : System.DateTime.MinValue;
            }

            public void WriteAllBytes(string path, byte[] contents) { }

            public void CreateDirectory(string path) { }

            public void AppendAllText(string path, string contents, Encoding encoding) { }
        }
    }
}
