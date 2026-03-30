using System;
using System.Threading;
using System.Windows.Forms;
using JSQViewer.Application.Abstractions;
using JSQViewer.Infrastructure.Persistence;
using JSQViewer.Infrastructure.Platform;
using JSQViewer.Presentation.WinForms.Composition;
using JSQViewer.UI;
using WinFormsApplication = System.Windows.Forms.Application;

namespace JSQViewer
{
    internal static class Program
    {
        private static ILogger _logger;
        private static INotificationService _notificationService;
        private static IExternalProcessLauncher _externalProcessLauncher;
        private static IMainFormNotificationService _mainFormNotificationService;

        [STAThread]
        private static void Main()
        {
            IAppPaths appPaths = new ApplicationPaths();
            IFileSystem fileSystem = new FileSystemAdapter();
            ILocalizationService localizationService = new DictionaryLocalizationService();
            _logger = new FileSystemLogger(fileSystem, appPaths);
            _notificationService = new MessageBoxNotificationService();
            _externalProcessLauncher = new ShellExternalProcessLauncher();
            _mainFormNotificationService = new WinFormsMainFormNotificationService();
            IRecentFoldersRepository recentFoldersRepository = new FileRecentFoldersRepository(appPaths);
            IUiStateRepository uiStateRepository = new FileUiStateRepository(appPaths);
            IPresetRepository presetRepository = new FilePresetRepository(appPaths);
            IOrderRepository orderRepository = new FileOrderRepository(appPaths);
            IViewerSettingsRepository viewerSettingsRepository = new FileViewerSettingsRepository(appPaths);
            Loc.Initialize(localizationService);

            WinFormsApplication.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            WinFormsApplication.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            WinFormsApplication.EnableVisualStyles();
            WinFormsApplication.SetCompatibleTextRenderingDefault(false);
            WinFormsApplication.Run(new MainForm(
                appPaths,
                _logger,
                _mainFormNotificationService,
                _externalProcessLauncher,
                recentFoldersRepository,
                uiStateRepository,
                presetRepository,
                orderRepository,
                viewerSettingsRepository));
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
