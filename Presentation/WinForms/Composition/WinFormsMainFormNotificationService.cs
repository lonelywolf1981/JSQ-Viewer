using System.Windows.Forms;
using System.Drawing;

namespace JSQViewer.Presentation.WinForms.Composition
{
    public sealed class WinFormsMainFormNotificationService : IMainFormNotificationService
    {
        private Form _currentToast;
        private Timer _currentTimer;

        public void ShowError(Form owner, string title, string message)
        {
            MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void ShowInfoToast(Form owner, string message)
        {
            ShowToast(owner, message, false);
        }

        public void ShowErrorToast(Form owner, string message)
        {
            ShowToast(owner, message, true);
        }

        private void ShowToast(Form owner, string text, bool isError)
        {
            try
            {
                CloseCurrentToast();

                var toast = new Form();
                _currentToast = toast;
                toast.FormBorderStyle = FormBorderStyle.None;
                toast.ShowInTaskbar = false;
                toast.StartPosition = FormStartPosition.Manual;
                toast.TopMost = true;
                toast.BackColor = isError ? Color.FromArgb(198, 52, 52) : Color.FromArgb(44, 132, 96);
                toast.Size = new Size(340, 56);

                var label = new Label();
                label.Dock = DockStyle.Fill;
                label.ForeColor = Color.White;
                label.TextAlign = ContentAlignment.MiddleCenter;
                label.Text = text ?? string.Empty;
                toast.Controls.Add(label);

                Rectangle bounds = owner != null ? owner.Bounds : Screen.PrimaryScreen.WorkingArea;
                toast.Location = new Point(bounds.Right - toast.Width - 24, bounds.Top + 24);

                var timer = new Timer();
                _currentTimer = timer;
                timer.Interval = 2200;
                timer.Tick += delegate { CloseCurrentToast(); };
                toast.FormClosed += delegate
                {
                    if (_currentTimer == timer)
                    {
                        _currentTimer = null;
                    }

                    timer.Stop();
                    timer.Dispose();
                };
                toast.Shown += delegate { timer.Start(); };
                toast.Show(owner);
            }
            catch
            {
            }
        }

        private void CloseCurrentToast()
        {
            if (_currentTimer != null)
            {
                _currentTimer.Stop();
                _currentTimer.Dispose();
                _currentTimer = null;
            }

            if (_currentToast != null && !_currentToast.IsDisposed)
            {
                _currentToast.Close();
            }

            _currentToast = null;
        }
    }
}
