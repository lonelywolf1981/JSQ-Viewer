using System;
using System.Threading;
using System.Windows.Forms;
using LeMuReViewer.Core;
using LeMuReViewer.UI;

namespace LeMuReViewer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            try
            {
                AppLogger.LogError(AppDomain.CurrentDomain.BaseDirectory, "Unhandled UI exception.", e.Exception);
                MessageBox.Show(
                    "An unexpected error occurred:\n" + e.Exception.Message,
                    "LeMuReViewer Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception ex = e.ExceptionObject as Exception;
                AppLogger.LogError(AppDomain.CurrentDomain.BaseDirectory, "Unhandled fatal exception.", ex);
                MessageBox.Show(
                    "A fatal error occurred:\n" + (ex != null ? ex.Message : "Unknown error"),
                    "LeMuReViewer Fatal Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }
}
