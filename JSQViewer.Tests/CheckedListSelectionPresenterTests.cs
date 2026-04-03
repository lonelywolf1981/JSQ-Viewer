using System.Windows.Forms;
using JSQViewer.Presentation.WinForms.Presenters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class CheckedListSelectionPresenterTests
    {
        [TestMethod]
        public void IsSelectedAfterItemCheck_UsesNewCheckedState()
        {
            Assert.IsTrue(CheckedListSelectionPresenter.IsSelectedAfterItemCheck(CheckState.Checked));
            Assert.IsTrue(CheckedListSelectionPresenter.IsSelectedAfterItemCheck(CheckState.Indeterminate));
            Assert.IsFalse(CheckedListSelectionPresenter.IsSelectedAfterItemCheck(CheckState.Unchecked));
        }
    }
}
