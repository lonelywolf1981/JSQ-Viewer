using System.Collections.Generic;
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
            var fs = new FakeFileSystem();
            for (int i = 0; i < 7; i++)
            {
                fs.ExistingDirectories.Add("Folder" + i);
            }

            var service = CreateService(fs);
            string spec = string.Join(";", Enumerable.Range(0, 7).Select(i => "Folder" + i));

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

        private static WorkspaceLoadOrchestrationService CreateService(IFileSystem fileSystem)
        {
            return new WorkspaceLoadOrchestrationService(new WorkspaceFolderSpecParser(), fileSystem);
        }

        private sealed class FakeFileSystem : IFileSystem
        {
            public HashSet<string> ExistingDirectories { get; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            public bool DirectoryExists(string path) => ExistingDirectories.Contains(path);

            public bool FileExists(string path) => false;

            public void WriteAllBytes(string path, byte[] contents) { }

            public void CreateDirectory(string path) { }

            public void AppendAllText(string path, string contents, Encoding encoding) { }
        }
    }
}
