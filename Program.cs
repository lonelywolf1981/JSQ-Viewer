using System;
using System.Threading;
using System.Windows.Forms;
using JSQViewer.Application.Abstractions;
using JSQViewer.Infrastructure.Platform;
using JSQViewer.UI;
using WinFormsApplication = System.Windows.Forms.Application;

namespace JSQViewer
{
    internal static class Program
    {
        private static ILogger _logger;
        private static INotificationService _notificationService;

        [STAThread]
        private static void Main()
        {
            IAppPaths appPaths = new ApplicationPaths();
            IFileSystem fileSystem = new FileSystemAdapter();
            ILocalizationService localizationService = new DictionaryLocalizationService();
            _logger = new FileSystemLogger(fileSystem, appPaths);
            _notificationService = new MessageBoxNotificationService();
            Loc.Initialize(localizationService);

            WinFormsApplication.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            WinFormsApplication.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            WinFormsApplication.EnableVisualStyles();
            WinFormsApplication.SetCompatibleTextRenderingDefault(false);
            WinFormsApplication.Run(new MainForm(appPaths));
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            try
            {
                _logger.LogError("Unhandled UI exception.", e.Exception);
                _notificationService.ShowError(
                    "JSQViewer Error",
                    "An unexpected error occurred:\n" + e.Exception.Message);
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
                _logger.LogError("Unhandled fatal exception.", ex);
                _notificationService.ShowError(
                    "JSQViewer Fatal Error",
                    "A fatal error occurred:\n" + (ex != null ? ex.Message : "Unknown error"));
            }
            catch
            {
            }
        }
    }
}
