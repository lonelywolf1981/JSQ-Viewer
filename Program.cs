using System;
using System.Threading;
using System.Windows.Forms;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Charting.UseCases;
using JSQViewer.Application.Exporting;
using JSQViewer.Application.Session;
using JSQViewer.Application.Workspace;
using JSQViewer.Core;
using JSQViewer.Infrastructure.Composition;
using JSQViewer.Infrastructure.Cache;
using JSQViewer.Infrastructure.Exporting;
using JSQViewer.Infrastructure.Persistence;
using JSQViewer.Infrastructure.Platform;
using JSQViewer.Presentation.WinForms.Charting;
using JSQViewer.Presentation.WinForms.Composition;
using JSQViewer.Presentation.WinForms.Presenters;
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
            IWorkspaceLayoutRepository workspaceLayoutRepository = new FileWorkspaceLayoutRepository(appPaths);
            IPresetRepository presetRepository = new FilePresetRepository(appPaths);
            IOrderRepository orderRepository = new FileOrderRepository(appPaths);
            IViewerSettingsRepository viewerSettingsRepository = new FileViewerSettingsRepository(appPaths);
            ISeriesSliceCache seriesSliceCache = new MemorySeriesSliceCache();
            var timestampRangeService = new TimestampRangeService();
            var dataSummaryService = new DataSummaryService(timestampRangeService);
            var seriesSliceService = new SeriesSliceService(seriesSliceCache, timestampRangeService);
            var chartPipelineService = new ChartPipelineService(seriesSliceService);
            var buildChartViewUseCase = new BuildChartViewUseCase(chartPipelineService);
            var buildWorkspaceSummaryUseCase = new BuildWorkspaceSummaryUseCase(dataSummaryService);
            var chartViewModelFactory = new ChartViewModelFactory(timestampRangeService);
            var chartRenderer = new ChartRenderer();
            var exportTemplateUseCase = new ExportTemplateUseCase(new OpenXmlTemplateExporter(appPaths), new OpenXmlTemplateExportValidator());
            var exportSettingsPresenter = new ExportSettingsPresenter();
            var viewerSettingsSanitizer = new ViewerSettingsSanitizer();
            IViewerSession viewerSession = new ViewerSession(seriesSliceCache);
            AppState.Configure(viewerSession, timestampRangeService, dataSummaryService);
            SeriesCache.Configure(seriesSliceService);
            WorkspaceFolderSpecParser workspaceFolderSpecParser = WorkspaceLoadingComposition.CreateFolderSpecParser();
            var loadWorkspaceDataUseCase = WorkspaceLoadingComposition.CreateLoadWorkspaceDataUseCase(workspaceFolderSpecParser);
            Loc.Initialize(localizationService);

            WinFormsApplication.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            WinFormsApplication.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            WinFormsApplication.EnableVisualStyles();
            WinFormsApplication.SetCompatibleTextRenderingDefault(false);
            WinFormsApplication.Run(new MainForm(
                appPaths,
                fileSystem,
                _logger,
                _mainFormNotificationService,
                _externalProcessLauncher,
                recentFoldersRepository,
                uiStateRepository,
                workspaceLayoutRepository,
                presetRepository,
                orderRepository,
                viewerSettingsRepository,
                viewerSession,
                timestampRangeService,
                buildChartViewUseCase,
                buildWorkspaceSummaryUseCase,
                chartViewModelFactory,
                chartRenderer,
                exportTemplateUseCase,
                exportSettingsPresenter,
                viewerSettingsSanitizer,
                workspaceFolderSpecParser,
                loadWorkspaceDataUseCase));
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
