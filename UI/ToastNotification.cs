using System;
using System.Drawing;
using System.Windows.Forms;

namespace JSQViewer.UI
{
    public static class ToastNotification
    {
        private static Form _current;
        private static Timer _currentTimer;

        public static void Show(Form owner, string text, bool isError)
        {
            try
            {
                CloseCurrentToast();

                var toast = new Form();
                _current = toast;
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
                timer.Tick += delegate
                {
                    CloseCurrentToast();
                };

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

        private static void CloseCurrentToast()
        {
            if (_currentTimer != null)
            {
                _currentTimer.Stop();
                _currentTimer.Dispose();
                _currentTimer = null;
            }
            if (_current != null && !_current.IsDisposed)
            {
                _current.Close();
            }
            _current = null;
        }
    }
}
