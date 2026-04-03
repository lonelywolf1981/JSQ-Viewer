using System.Windows.Forms;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    public static class CheckedListSelectionPresenter
    {
        public static bool IsSelectedAfterItemCheck(CheckState newValue)
        {
            return newValue == CheckState.Checked || newValue == CheckState.Indeterminate;
        }
    }
}
