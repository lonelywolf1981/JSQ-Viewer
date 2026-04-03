using System;
using System.Collections.Generic;
using System.IO;
using JSQViewer.Application.Channels;
using JSQViewer.Application.Workspace;
using JSQViewer.Infrastructure.Persistence;
using JSQViewer.Infrastructure.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class WorkspaceLayoutStateTests
    {
        [TestMethod]
        public void BuildWorkspaceKey_IsOrderInsensitiveAndNormalizesPaths()
        {
            var parser = new WorkspaceFolderSpecParser();

            string keyA = parser.BuildWorkspaceKey(new[] { "C:/Data/A/", "D:\\Data\\B" });
            string keyB = parser.BuildWorkspaceKey(new[] { "d:/data/b\\", "c:\\data\\a" });

            Assert.AreEqual(keyA, keyB);
        }

        [TestMethod]
        public void SaveAndLoad_RoundTripsWorkspaceScopedOrderSelections()
        {
            string root = Path.Combine(Path.GetTempPath(), "jsqviewer-layout-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var repository = new FileWorkspaceLayoutRepository(new ApplicationPaths(root));
                var state = new WorkspaceLayoutState
                {
                    MainSelectedOrderKey = "main-order",
                    SourceSelectedOrderKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["C:\\srcA"] = "order-a",
                        ["C:\\srcB"] = "order-b"
                    }
                };

                Assert.IsTrue(repository.Save("workspace-key", state));

                WorkspaceLayoutState loaded = repository.Load("workspace-key");

                Assert.AreEqual("main-order", loaded.MainSelectedOrderKey);
                Assert.AreEqual("order-a", loaded.SourceSelectedOrderKeys["C:\\srcA"]);
                Assert.AreEqual("order-b", loaded.SourceSelectedOrderKeys["C:\\srcB"]);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }

    [TestClass]
    public class ChannelWorkspaceSourceOrderTests
    {
        [TestMethod]
        public void ApplyOrderToSource_ReordersOnlyRequestedSource()
        {
            var workspace = new ChannelWorkspaceModel();
            workspace.Load(ChannelWorkspaceTestData.CreateMultiSourceData(), null, null);

            workspace.ApplyOrderToSource("C:\\srcA", new[] { "C:\\srcA::A-02", "C:\\srcA::A-01" });

            CollectionAssert.AreEqual(
                new[] { "C:\\srcA::A-02", "C:\\srcA::A-01", "C:\\srcB::B-01" },
                ChannelWorkspaceTestHarness.ToCodeList(workspace.GetCurrentOrderForSource("C:\\srcA")));
            CollectionAssert.AreEqual(
                new[] { "C:\\srcB::B-01", "C:\\srcA::A-01", "C:\\srcA::A-02" },
                ChannelWorkspaceTestHarness.ToCodeList(workspace.GetCurrentOrderForSource("C:\\srcB")));
            CollectionAssert.AreEqual(
                new[] { "C:\\srcA::A-01", "C:\\srcA::A-02", "C:\\srcB::B-01" },
                ChannelWorkspaceTestHarness.ToCodeList(workspace.GetCurrentOrder()));
        }
    }
}
