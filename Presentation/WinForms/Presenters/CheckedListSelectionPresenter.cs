using System.Windows.Forms;
using JSQViewer.Presentation.WinForms.ViewModels;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    public static class CheckedListSelectionPresenter
    {
        public static bool IsSelectedAfterItemCheck(CheckState newValue)
        {
            return newValue == CheckState.Checked || newValue == CheckState.Indeterminate;
        }

        public static string GetDeferredItemCode(object item)
        {
            var channelItem = item as ChannelListItemViewModel;
            return channelItem == null ? null : channelItem.Code;
        }

        public static bool RequiresFullRebuildAfterSelectionChange(bool selectedOnly, string sortMode)
        {
            if (selectedOnly)
            {
                return true;
            }

            return string.Equals((sortMode ?? string.Empty).Trim(), "Selected first", System.StringComparison.Ordinal);
        }
    }
}
