using System;
using System.Collections.Generic;
using System.Reflection;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Charting.UseCases;
using JSQViewer.Application.Exporting;
using JSQViewer.Application.Exporting.Ports;
using JSQViewer.Application.Session;
using JSQViewer.Application.Workspace;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Application.Workspace.UseCases;
using JSQViewer.Core;
using JSQViewer.Infrastructure.Cache;
using JSQViewer.Infrastructure.Platform;
using JSQViewer.Presentation.WinForms.Charting;
using JSQViewer.Presentation.WinForms.Composition;
using JSQViewer.Presentation.WinForms.Presenters;
using JSQViewer.Settings;
using JSQViewer.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ChartDisplayBehaviorTests
    {
        [TestMethod]
        public void ShowChartButtonOnClickMarksChartAsRequestedOnMainForm()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                harness.LoadSession();

                harness.InvokeShowChartButtonOnClick();

                Assert.IsTrue(harness.Form.IsChartRequestedForTests);
            }
        }

        [TestMethod]
        public void CloseAllButtonOnClickClearsRequestedStateOnMainForm()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                harness.LoadSession();
                harness.InvokeShowChartButtonOnClick();

                Assert.IsTrue(harness.Form.IsChartRequestedForTests);

                harness.InvokeCloseAllButtonOnClick();

                Assert.IsFalse(harness.Form.IsChartRequestedForTests);
            }
        }

        [TestMethod]
        public void ChartHostUserClosePathClearsRequestedStateOnMainForm()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                harness.LoadSession();
                harness.SelectChartChannel();
                harness.InvokeShowChartButtonOnClick();
                harness.InvokeEnsureChartHostForm();

                Assert.IsTrue(harness.Form.IsChartRequestedForTests);

                harness.InvokeChartHostUserClosing();

                Assert.IsFalse(harness.Form.IsChartRequestedForTests);
            }
        }
    }

    internal sealed class TestMainFormHarness : IDisposable
    {
        private readonly ViewerSession _viewerSession;
        private readonly MainForm _form;

        private TestMainFormHarness(ViewerSession viewerSession, MainForm form)
        {
            _viewerSession = viewerSession;
            _form = form;
        }

        public MainForm Form
        {
            get { return _form; }
        }

        public static TestMainFormHarness Create()
        {
            Loc.Initialize(new FakeLocalizationService());
            var fileSystem = new FakeFileSystem();
            var logger = new FakeLogger();
            var notificationService = new FakeNotificationService();
            var session = new ViewerSession(new MemorySeriesSliceCache());
            var form = new MainForm(
                new FakeAppPaths(),
                fileSystem,
                logger,
                notificationService,
                new FakeExternalProcessLauncher(),
                new FakeRecentFoldersRepository(),
                new FakeUiStateRepository(),
                new FakePresetRepository(),
                new FakeOrderRepository(),
                new FakeViewerSettingsRepository(),
                session,
                new TimestampRangeService(),
                new BuildChartViewUseCase(new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()))),
                new BuildWorkspaceSummaryUseCase(new DataSummaryService(new TimestampRangeService())),
                new ChartViewModelFactory(new TimestampRangeService()),
                new ChartRenderer(),
                new ExportTemplateUseCase(new ChartDisplayFakeTemplateExporter(), new ChartDisplayFakeTemplateExportValidator()),
                new ExportSettingsPresenter(),
                new ViewerSettingsSanitizer(),
                new WorkspaceFolderSpecParser(),
                new LoadWorkspaceDataUseCase(
                    new WorkspaceFolderSpecParser(),
                    new FakeTestRootLocator(),
                    new FakeTestMetadataReader(),
                    new FakeCanaliDefinitionReader(),
                    new FakeTestDataSourceReader(),
                    new MergeLoadedSourcesUseCase()));

            return new TestMainFormHarness(session, form);
        }

        public void LoadSession()
        {
            _viewerSession.SetData("C:\\tests\\run-01", SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L, 2000L }));
        }

        public void SelectChartChannel()
        {
            GetPrivateField<ChannelWorkspacePresenter>(_form, "_channelWorkspacePresenter").ApplyCheckedCodes(new[] { "A-01" });
        }

        public void InvokeShowChartButtonOnClick()
        {
            InvokePrivate(_form, "ShowChartButtonOnClick", _form, EventArgs.Empty);
        }

        public void InvokeCloseAllButtonOnClick()
        {
            InvokePrivate(_form, "CloseAllButtonOnClick", _form, EventArgs.Empty);
        }

        public void InvokeEnsureChartHostForm()
        {
            InvokePrivate(_form, "EnsureChartHostForm");
        }

        public void InvokeChartHostUserClosing()
        {
            Form host = GetPrivateField<Form>(_form, "_chartHostForm");
            Assert.IsNotNull(host);

            MethodInfo onFormClosing = typeof(Form).GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onFormClosing);

            var args = new FormClosingEventArgs(CloseReason.UserClosing, false);
            onFormClosing.Invoke(host, new object[] { args });
        }

        public void Dispose()
        {
            if (_form != null)
            {
                _form.Dispose();
            }
        }

        private static object InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing method: " + methodName);
            return method.Invoke(target, args);
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + fieldName);
            return field.GetValue(target) as T;
        }
    }

    internal sealed class FakeAppPaths : IAppPaths
    {
        public string ApplicationBaseDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public string ProjectRoot
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public string LogDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }
    }

    internal sealed class FakeFileSystem : IFileSystem
    {
        public bool FileExists(string path) { return false; }
        public bool DirectoryExists(string path) { return false; }
        public void WriteAllBytes(string path, byte[] contents) { }
        public void CreateDirectory(string path) { }
        public void AppendAllText(string path, string contents, System.Text.Encoding encoding) { }
    }

    internal sealed class FakeLogger : ILogger
    {
        public void LogInfo(string message) { }
        public void LogError(string message, Exception exception) { }
    }

    internal sealed class FakeNotificationService : IMainFormNotificationService
    {
        public void ShowError(Form owner, string title, string message) { }
        public void ShowInfoToast(Form owner, string message) { }
        public void ShowErrorToast(Form owner, string message) { }
    }

    internal sealed class FakeExternalProcessLauncher : IExternalProcessLauncher
    {
        public void Open(string path) { }
    }

    internal sealed class FakeLocalizationService : ILocalizationService
    {
        public AppLanguage CurrentLanguage { get; set; }

        public event Action LanguageChanged
        {
            add { }
            remove { }
        }

        public string Get(string key)
        {
            return key;
        }
    }

    internal sealed class FakeRecentFoldersRepository : IRecentFoldersRepository
    {
        public List<string> Load() { return new List<string>(); }
        public bool Save(IList<string> folders) { return true; }
    }

    internal sealed class FakeUiStateRepository : IUiStateRepository
    {
        public UiStateModel Load() { return new UiStateModel(); }
        public bool Save(UiStateModel state) { return true; }
    }

    internal sealed class FakePresetRepository : IPresetRepository
    {
        public List<ViewerPreset> List() { return new List<ViewerPreset>(); }
        public ViewerPreset Load(string keyOrName) { return null; }
        public bool Exists(string keyOrName) { return false; }
        public ViewerPreset Save(ViewerPreset preset) { return preset; }
        public bool Delete(string keyOrName) { return false; }
    }

    internal sealed class FakeOrderRepository : IOrderRepository
    {
        public List<ChannelOrderModel> List() { return new List<ChannelOrderModel>(); }
        public ChannelOrderModel Load(string keyOrName) { return null; }
        public bool Exists(string keyOrName) { return false; }
        public ChannelOrderModel Save(string name, IList<string> order) { return null; }
        public bool Delete(string keyOrName) { return false; }
        public List<string> LoadLegacyOrder() { return new List<string>(); }
        public bool SaveLegacyOrder(IList<string> order) { return true; }
    }

    internal sealed class FakeViewerSettingsRepository : IViewerSettingsRepository
    {
        public ViewerSettingsModel Load() { return ViewerSettingsModel.CreateDefault(); }
        public bool Save(ViewerSettingsModel settings) { return true; }
    }

    internal sealed class ChartDisplayFakeTemplateExporter : ITemplateExporter
    {
        public byte[] Export(ExportTemplateRequest request) { return new byte[0]; }
    }

    internal sealed class ChartDisplayFakeTemplateExportValidator : ITemplateExportValidator
    {
        public TemplateValidationResult Validate(byte[] xlsxBytes) { return new TemplateValidationResult { Ok = true, Message = string.Empty }; }
    }

    internal sealed class FakeTestRootLocator : ITestRootLocator
    {
        public string FindRoot(string folder) { return folder; }
    }

    internal sealed class FakeTestMetadataReader : ITestMetadataReader
    {
        public Dictionary<string, string> Read(string root) { return new Dictionary<string, string>(); }
    }

    internal sealed class FakeCanaliDefinitionReader : ICanaliDefinitionReader
    {
        public Dictionary<string, ChannelInfo> Read(string root) { return new Dictionary<string, ChannelInfo>(); }
    }

    internal sealed class FakeTestDataSourceReader : ITestDataSourceReader
    {
        public TestData Read(string root, Dictionary<string, ChannelInfo> channels, Dictionary<string, string> metadata)
        {
            return SessionAndChartingTestData.CreateData(new long[] { 0L });
        }
    }
}
