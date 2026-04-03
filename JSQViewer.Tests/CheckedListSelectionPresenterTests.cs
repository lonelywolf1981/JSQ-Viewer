using System.Windows.Forms;
using JSQViewer.Presentation.WinForms.Presenters;
using JSQViewer.Presentation.WinForms.ViewModels;
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

        [TestMethod]
        public void GetDeferredItemCode_ReadsCodeFromCurrentBoundItem()
        {
            var item = new ChannelListItemViewModel("C:\\srcC::C-01", "C-01", "u", false);

            Assert.AreEqual("C:\\srcC::C-01", CheckedListSelectionPresenter.GetDeferredItemCode(item));
            Assert.IsNull(CheckedListSelectionPresenter.GetDeferredItemCode(null));
            Assert.IsNull(CheckedListSelectionPresenter.GetDeferredItemCode("plain-string"));
        }

        [TestMethod]
        public void RequiresFullRebuildAfterSelectionChange_OnlyForSelectionDependentViews()
        {
            Assert.IsTrue(CheckedListSelectionPresenter.RequiresFullRebuildAfterSelectionChange(true, "User"));
            Assert.IsTrue(CheckedListSelectionPresenter.RequiresFullRebuildAfterSelectionChange(false, "Selected first"));
            Assert.IsFalse(CheckedListSelectionPresenter.RequiresFullRebuildAfterSelectionChange(false, "User"));
            Assert.IsFalse(CheckedListSelectionPresenter.RequiresFullRebuildAfterSelectionChange(false, "Code"));
            Assert.IsFalse(CheckedListSelectionPresenter.RequiresFullRebuildAfterSelectionChange(false, null));
        }
    }
}
