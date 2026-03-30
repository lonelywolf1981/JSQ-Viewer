using System.Windows.Forms;
using JSQViewer.Application.Abstractions;

namespace JSQViewer.Infrastructure.Platform
{
    public sealed class MessageBoxNotificationService : INotificationService
    {
        public void ShowError(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
