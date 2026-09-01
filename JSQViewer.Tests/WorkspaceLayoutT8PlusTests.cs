using System.Collections.Generic;
using System.Web.Script.Serialization;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Channels;
using JSQViewer.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class WorkspaceLayoutT8PlusTests
    {
        private sealed class FakeLayoutRepository : IWorkspaceLayoutRepository
        {
            public Dictionary<string, WorkspaceLayoutState> Saved =
                new Dictionary<string, WorkspaceLayoutState>();

            public WorkspaceLayoutState Load(string workspaceKey)
            {
                WorkspaceLayoutState state;
                return Saved.TryGetValue(workspaceKey, out state) ? state : null;
            }

            public bool Save(string workspaceKey, WorkspaceLayoutState state)
            {
                Saved[workspaceKey] = state;
                return true;
            }
        }

        private sealed class FakeOrderRepository : IOrderRepository
        {
            public List<ChannelOrderModel> List() { return new List<ChannelOrderModel>(); }
            public ChannelOrderModel Load(string keyOrName) { return null; }
            public bool Exists(string keyOrName) { return false; }
            public ChannelOrderModel Save(string name, IList<string> order) { return null; }
            public bool Delete(string keyOrName) { return true; }
            public List<string> LoadLegacyOrder() { return new List<string>(); }
            public bool SaveLegacyOrder(IList<string> order) { return true; }
        }

        [TestMethod]
        public void SaveSourceT8PlusLines_RoundTripsThroughRepository()
        {
            var repository = new FakeLayoutRepository();
            var service = new WorkspaceLayoutStateService(repository, new FakeOrderRepository());

            WorkspaceLayoutState state = service.SaveSourceT8PlusLines(
                "ws", new WorkspaceLayoutState(), "C:\\runs\\A\\",
                new T8PlusLineSelection(true, false, true));

            Assert.IsTrue(repository.Saved.ContainsKey("ws"));

            T8PlusLineSelection restored = service.GetSourceT8PlusLines(state, "C:\\runs\\A");

            Assert.IsTrue(restored.ShowMinimum);
            Assert.IsFalse(restored.ShowAverage);
            Assert.IsTrue(restored.ShowMaximum);
        }

        [TestMethod]
        public void GetSourceT8PlusLines_ForUnknownSource_ReturnsNone()
        {
            var service = new WorkspaceLayoutStateService(new FakeLayoutRepository(), new FakeOrderRepository());

            T8PlusLineSelection restored = service.GetSourceT8PlusLines(new WorkspaceLayoutState(), "C:\\runs\\Z");

            Assert.IsFalse(restored.HasAny);
        }

        [TestMethod]
        public void SaveSourceT8PlusLines_WithNothingSelected_DropsTheEntry()
        {
            var service = new WorkspaceLayoutStateService(new FakeLayoutRepository(), new FakeOrderRepository());

            WorkspaceLayoutState state = service.SaveSourceT8PlusLines(
                "ws", new WorkspaceLayoutState(), "C:\\runs\\A",
                new T8PlusLineSelection(true, true, true));
            state = service.SaveSourceT8PlusLines("ws", state, "C:\\runs\\A", T8PlusLineSelection.None);

            Assert.AreEqual(0, state.SourceT8PlusLines.Count);
        }

        [TestMethod]
        public void None_ReturnsFreshInstanceEachAccess()
        {
            T8PlusLineSelection first = T8PlusLineSelection.None;
            first.ShowMinimum = true;

            Assert.IsFalse(T8PlusLineSelection.None.ShowMinimum);
        }

        [TestMethod]
        public void SourceT8PlusLines_RoundTripsThroughJavaScriptSerializer()
        {
            var state = new WorkspaceLayoutState();
            state.SourceT8PlusLines["C:\\runs\\A"] = new T8PlusLineSelection(true, false, true);
            state.EnsureInitialized();

            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(state);
            WorkspaceLayoutState restoredState = serializer.Deserialize<WorkspaceLayoutState>(json);
            restoredState.EnsureInitialized();

            string queryRoot = WorkspaceLayoutState.NormalizeSourceRoot("c:\\runs\\a\\");
            T8PlusLineSelection restored;
            bool found = restoredState.SourceT8PlusLines.TryGetValue(queryRoot, out restored);

            Assert.IsTrue(found);
            Assert.IsTrue(restored.ShowMinimum);
            Assert.IsFalse(restored.ShowAverage);
            Assert.IsTrue(restored.ShowMaximum);
        }
    }
}
