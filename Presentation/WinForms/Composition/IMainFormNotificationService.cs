using System.Windows.Forms;

namespace JSQViewer.Presentation.WinForms.Composition
{
    public interface IMainFormNotificationService
    {
        void ShowError(Form owner, string title, string message);

        void ShowInfoToast(Form owner, string message);

        void ShowErrorToast(Form owner, string message);
    }
}
