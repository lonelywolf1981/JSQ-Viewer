using JSQViewer.Presentation.WinForms.Presenters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ChartDisplayBehaviorTests
    {
        [TestMethod]
        public void RequestOpen_MarksChartAsRequested()
        {
            var presenter = new ChartDisplayPresenter();

            presenter.RequestOpen();

            Assert.IsTrue(presenter.IsChartRequested);
            Assert.IsTrue(presenter.ShouldOpenHostForCurrentRedraw());
        }

        [TestMethod]
        public void SelectionChange_RendersOnlyWhenChartWasRequested()
        {
            var presenter = new ChartDisplayPresenter();

            Assert.IsFalse(presenter.ShouldRenderAfterSelectionChange());
            presenter.RequestOpen();
            Assert.IsTrue(presenter.ShouldRenderAfterSelectionChange());
        }

        [TestMethod]
        public void WorkspaceReload_RendersOnlyWhenChartWasRequested()
        {
            var presenter = new ChartDisplayPresenter();

            Assert.IsFalse(presenter.ShouldRenderAfterWorkspaceReload());
            presenter.RequestOpen();
            Assert.IsTrue(presenter.ShouldRenderAfterWorkspaceReload());
            Assert.IsTrue(presenter.IsChartRequested);
        }

        [TestMethod]
        public void Close_ClearsRequestedState()
        {
            var presenter = new ChartDisplayPresenter();
            presenter.RequestOpen();

            presenter.Close();

            Assert.IsFalse(presenter.IsChartRequested);
            Assert.IsFalse(presenter.ShouldRenderAfterSelectionChange());
            Assert.IsFalse(presenter.ShouldOpenHostForCurrentRedraw());
        }
    }
}
