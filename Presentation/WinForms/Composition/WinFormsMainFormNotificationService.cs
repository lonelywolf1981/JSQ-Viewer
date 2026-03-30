using System.Windows.Forms;
using JSQViewer.UI;

namespace JSQViewer.Presentation.WinForms.Composition
{
    public sealed class WinFormsMainFormNotificationService : IMainFormNotificationService
    {
        public void ShowError(Form owner, string title, string message)
        {
            MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void ShowInfoToast(Form owner, string message)
        {
            ToastNotification.Show(owner, message, false);
        }

        public void ShowErrorToast(Form owner, string message)
        {
            ToastNotification.Show(owner, message, true);
        }
    }
}
