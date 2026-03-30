using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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
using System.Windows.Forms.DataVisualization.Charting;
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

        [TestMethod]
        public void EnsureChartHostForm_KeepsRenderedAxisSettingsOnSharedChartInstance()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                Chart chart = harness.GetChart();
                var renderer = new ChartRenderer();
                renderer.Render(chart, new Presentation.WinForms.ViewModels.ChartViewModel
                {
                    HasData = true,
                    OverlayMode = true,
                    ShowLegend = true,
                    XAxisSettings = new Presentation.WinForms.ViewModels.ChartAxisSettingsViewModel
                    {
                        IsManualEnabled = true,
                        Minimum = 0.25,
                        Maximum = 1.75,
                        Step = 0.5
                    },
                    YAxisSettings = new Presentation.WinForms.ViewModels.ChartAxisSettingsViewModel
                    {
                        IsManualEnabled = true,
                        Minimum = -10d,
                        Maximum = 30d,
                        Step = 5d
                    },
                    Series = new[]
                    {
                        new Presentation.WinForms.ViewModels.ChartSeriesViewModel
                        {
                            Code = "A-01",
                            LegendText = "A-01",
                            XValues = new[] { 0.0, 1.0, 2.0 },
                            YValues = new[] { 1d, 2d, 3d },
                            BorderWidth = 2,
                            IsVisibleInLegend = true
                        }
                    }
                });

                Assert.AreEqual(0.25, chart.ChartAreas[0].AxisX.Minimum);
                Assert.AreEqual(1.75, chart.ChartAreas[0].AxisX.Maximum);
                Assert.AreEqual(0.5, chart.ChartAreas[0].AxisX.Interval);
                Assert.AreEqual(-10d, chart.ChartAreas[0].AxisY.Minimum);
                Assert.AreEqual(30d, chart.ChartAreas[0].AxisY.Maximum);
                Assert.AreEqual(5d, chart.ChartAreas[0].AxisY.Interval);

                harness.InvokeEnsureChartHostForm();

                Chart chartAfterEnsure = harness.GetChart();
                Assert.AreSame(chart, chartAfterEnsure);
                Assert.AreEqual(0.25, chartAfterEnsure.ChartAreas[0].AxisX.Minimum);
                Assert.AreEqual(1.75, chartAfterEnsure.ChartAreas[0].AxisX.Maximum);
                Assert.AreEqual(0.5, chartAfterEnsure.ChartAreas[0].AxisX.Interval);
                Assert.AreEqual(-10d, chartAfterEnsure.ChartAreas[0].AxisY.Minimum);
                Assert.AreEqual(30d, chartAfterEnsure.ChartAreas[0].AxisY.Maximum);
                Assert.AreEqual(5d, chartAfterEnsure.ChartAreas[0].AxisY.Interval);
            }
        }

        [TestMethod]
        public void CompareOverlayToggle_ClearsManualXAxisState()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                harness.LoadMultiSourceSession();
                harness.SetManualXAxis("0", "1000", "100");

                Assert.IsTrue(harness.GetManualXAxisCheckBox().Checked);

                harness.SetCompareOverlayChecked(true);

                Assert.IsFalse(harness.GetManualXAxisCheckBox().Checked);
                Assert.IsFalse(harness.GetManualXAxisCheckBox().Enabled);
                Assert.AreEqual(string.Empty, harness.GetXAxisMinimumBox().Text);
                Assert.AreEqual(string.Empty, harness.GetXAxisMaximumBox().Text);
                Assert.AreEqual(string.Empty, harness.GetXAxisStepBox().Text);
                Assert.IsFalse(harness.GetXAxisMinimumBox().Enabled);
                Assert.IsFalse(harness.GetXAxisMaximumBox().Enabled);
                Assert.IsFalse(harness.GetXAxisStepBox().Enabled);

                harness.SetCompareOverlayChecked(false);

                Assert.IsFalse(harness.GetManualXAxisCheckBox().Checked);
                Assert.IsTrue(harness.GetManualXAxisCheckBox().Enabled);
                Assert.IsFalse(harness.GetXAxisMinimumBox().Enabled);
                Assert.IsFalse(harness.GetXAxisMaximumBox().Enabled);
                Assert.IsFalse(harness.GetXAxisStepBox().Enabled);
            }
        }

        [TestMethod]
        public void RangeChange_DoesNotOverrideManualXAxis()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                harness.LoadSession();
                harness.SelectChartChannel();
                harness.InvokeShowChartButtonOnClick();
                harness.SetManualXAxis("0", "2000", "1000");

                Chart chart = harness.GetChart();
                Assert.AreEqual(new TimestampRangeService().UnixMsToLocalDateTime(0L).ToOADate(), chart.ChartAreas[0].AxisX.Minimum);
                Assert.AreEqual(new TimestampRangeService().UnixMsToLocalDateTime(2000L).ToOADate(), chart.ChartAreas[0].AxisX.Maximum);

                harness.SetRange(0.1, 0.2);

                Assert.IsFalse(harness.GetRangeTrackBar().Enabled);
                Assert.AreEqual(new TimestampRangeService().UnixMsToLocalDateTime(0L).ToOADate(), chart.ChartAreas[0].AxisX.Minimum);
                Assert.AreEqual(new TimestampRangeService().UnixMsToLocalDateTime(2000L).ToOADate(), chart.ChartAreas[0].AxisX.Maximum);
            }
        }

        [TestMethod]
        public void InvalidManualXAxisInput_RevertsManualModeOff()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                harness.LoadSession();
                harness.SelectChartChannel();
                harness.InvokeShowChartButtonOnClick();

                harness.SetManualXAxis("10", "1", "bad");

                Assert.IsFalse(harness.GetManualXAxisCheckBox().Checked);
                Assert.IsTrue(harness.GetRangeTrackBar().Enabled);
            }
        }

        [TestMethod]
        public void AddDataButtonOnClick_RejectsSixFoldersBeforePicker()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                string spec = harness.BuildFolderSpec(6);
                harness.AllowExistingDirectories(6);
                harness.SetFolderBoxText(spec);

                harness.InvokeAddDataButtonOnClick();

                Assert.AreEqual(spec, harness.GetFolderBoxText());
                Assert.AreEqual(harness.Localization.Get("TooManyFolders"), harness.NotificationService.LastErrorToastMessage);
            }
        }

        [TestMethod]
        public void IsValidFolderSpec_AllowsFourSixAndRejectsSevenFolders()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                harness.AllowExistingDirectories(4);
                Assert.IsTrue(harness.InvokeIsValidFolderSpec(harness.BuildFolderSpec(4)));

                harness.AllowExistingDirectories(6);
                Assert.IsTrue(harness.InvokeIsValidFolderSpec(harness.BuildFolderSpec(6)));

                harness.AllowExistingDirectories(7);
                Assert.IsFalse(harness.InvokeIsValidFolderSpec(harness.BuildFolderSpec(7)));
            }
        }

        [TestMethod]
        public void RecentFoldersBoxOnSelectedIndexChanged_LoadsFourFolders()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                string spec = harness.BuildFolderSpec(4);
                harness.AllowExistingDirectories(4);
                harness.SetRecentFolderItems(spec);

                harness.InvokeRecentFoldersBoxOnSelectedIndexChanged();

                harness.WaitForSessionFolder(spec);
                Assert.AreEqual(spec, harness.SessionFolder);
                Assert.IsTrue(string.IsNullOrEmpty(harness.NotificationService.LastErrorToastMessage));
            }
        }

        [TestMethod]
        public void RecentFoldersBoxOnSelectedIndexChanged_LoadsSixFolders()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                string spec = harness.BuildFolderSpec(6);
                harness.AllowExistingDirectories(6);
                harness.SetRecentFolderItems(spec);

                harness.InvokeRecentFoldersBoxOnSelectedIndexChanged();

                harness.WaitForSessionFolder(spec);
                Assert.AreEqual(spec, harness.SessionFolder);
                Assert.IsTrue(string.IsNullOrEmpty(harness.NotificationService.LastErrorToastMessage));
            }
        }

        [TestMethod]
        public void RecentFoldersBoxOnSelectedIndexChanged_DoesNotLoadSevenFolders()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                string spec = harness.BuildFolderSpec(7);
                harness.AllowExistingDirectories(7);
                harness.SetRecentFolderItems(spec);

                harness.InvokeRecentFoldersBoxOnSelectedIndexChanged();

                Assert.IsFalse(harness.InvokeIsValidFolderSpec(spec));
                Assert.AreEqual(string.Empty, harness.SessionFolder);
                Assert.IsTrue(string.IsNullOrEmpty(harness.NotificationService.LastErrorToastMessage));
            }
        }

        [TestMethod]
        public void TryAutoLoadLastFolder_LoadsFourFolders()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                string spec = harness.BuildFolderSpec(4);
                harness.AllowExistingDirectories(4);
                harness.SetRecentFolderItems(spec);

                harness.InvokeTryAutoLoadLastFolder();

                harness.WaitForSessionFolder(spec);
                Assert.AreEqual(spec, harness.SessionFolder);
                Assert.IsTrue(string.IsNullOrEmpty(harness.NotificationService.LastErrorToastMessage));
            }
        }

        [TestMethod]
        public void TryAutoLoadLastFolder_LoadsSixFolders()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                string spec = harness.BuildFolderSpec(6);
                harness.AllowExistingDirectories(6);
                harness.SetRecentFolderItems(spec);

                harness.InvokeTryAutoLoadLastFolder();

                harness.WaitForSessionFolder(spec);
                Assert.AreEqual(spec, harness.SessionFolder);
                Assert.IsTrue(string.IsNullOrEmpty(harness.NotificationService.LastErrorToastMessage));
            }
        }

        [TestMethod]
        public void TryAutoLoadLastFolder_DoesNotLoadSevenFolders()
        {
            using (TestMainFormHarness harness = TestMainFormHarness.Create())
            {
                string spec = harness.BuildFolderSpec(7);
                harness.AllowExistingDirectories(7);
                harness.SetRecentFolderItems(spec);

                harness.InvokeTryAutoLoadLastFolder();

                Assert.IsFalse(harness.InvokeIsValidFolderSpec(spec));
                Assert.AreEqual(string.Empty, harness.SessionFolder);
                Assert.IsTrue(string.IsNullOrEmpty(harness.NotificationService.LastErrorToastMessage));
            }
        }
    }

    internal sealed class TestMainFormHarness : IDisposable
    {
        private readonly ViewerSession _viewerSession;
        private readonly MainForm _form;
        private readonly FakeFileSystem _fileSystem;
        private readonly FakeNotificationService _notificationService;
        private readonly FakeLocalizationService _localization;

        private TestMainFormHarness(ViewerSession viewerSession, MainForm form, FakeFileSystem fileSystem, FakeNotificationService notificationService, FakeLocalizationService localization)
        {
            _viewerSession = viewerSession;
            _form = form;
            _fileSystem = fileSystem;
            _notificationService = notificationService;
            _localization = localization;
        }

        public MainForm Form
        {
            get { return _form; }
        }

        public string SessionFolder
        {
            get { return _viewerSession.Folder; }
        }

        public FakeNotificationService NotificationService
        {
            get { return _notificationService; }
        }

        public FakeLocalizationService Localization
        {
            get { return _localization; }
        }

        public static TestMainFormHarness Create()
        {
            var localization = new FakeLocalizationService();
            Loc.Initialize(localization);
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

            return new TestMainFormHarness(session, form, fileSystem, notificationService, localization);
        }

        public void LoadSession()
        {
            LoadSession(SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L, 2000L }));
        }

        public void LoadMultiSourceSession()
        {
            var data = SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L, 2000L });
            data.SourceColumns["C:\\tests\\source-a\\"] = new[] { "A-01" };
            data.SourceColumns["C:\\tests\\source-b\\"] = new[] { "B-01" };
            data.CodeSources["A-01"] = "C:\\tests\\source-a\\";
            data.CodeSources["B-01"] = "C:\\tests\\source-b\\";
            data.SourceStartMs["C:\\tests\\source-a\\"] = 0L;
            data.SourceEndMs["C:\\tests\\source-a\\"] = 2000L;
            data.SourceStartMs["C:\\tests\\source-b\\"] = 0L;
            data.SourceEndMs["C:\\tests\\source-b\\"] = 2000L;
            data.Columns["B-01"] = new double?[] { 2d, 3d, 4d };
            data.Channels["B-01"] = new ChannelInfo { Code = "B-01", Name = "B-01", Unit = "u" };
            data.ColumnNames = new[] { "A-01", "B-01" };
            LoadSession(data);
        }

        public void SetManualXAxis(string minimum, string maximum, string step)
        {
            GetXAxisMinimumBox().Text = minimum;
            GetXAxisMaximumBox().Text = maximum;
            GetXAxisStepBox().Text = step;
            CheckBox checkBox = GetManualXAxisCheckBox();
            checkBox.Checked = true;
            InvokePrivate(_form, "AxisValueInputOnLeave", GetXAxisStepBox(), EventArgs.Empty);
            System.Windows.Forms.Application.DoEvents();
        }

        public void SetCompareOverlayChecked(bool value)
        {
            GetPrivateField<CheckBox>(_form, "_compareOverlayCheck").Checked = value;
            System.Windows.Forms.Application.DoEvents();
        }

        public void SetRange(double lowerValue, double upperValue)
        {
            RangeTrackBar trackBar = GetRangeTrackBar();
            trackBar.LowerValue = lowerValue;
            trackBar.UpperValue = upperValue;
            InvokePrivate(_form, "RangeTrackBarOnRangeChanged", trackBar, EventArgs.Empty);
            System.Windows.Forms.Application.DoEvents();
        }

        public RangeTrackBar GetRangeTrackBar()
        {
            return GetPrivateField<RangeTrackBar>(_form, "_rangeTrackBar");
        }

        public CheckBox GetManualXAxisCheckBox()
        {
            return GetPrivateField<CheckBox>(_form, "_manualAxisXCheck");
        }

        public TextBox GetXAxisMinimumBox()
        {
            return GetPrivateField<TextBox>(_form, "_axisXMinimumBox");
        }

        public TextBox GetXAxisMaximumBox()
        {
            return GetPrivateField<TextBox>(_form, "_axisXMaximumBox");
        }

        public TextBox GetXAxisStepBox()
        {
            return GetPrivateField<TextBox>(_form, "_axisXStepBox");
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

        public void AllowExistingDirectories(int count)
        {
            _fileSystem.SetExistingDirectories(BuildFolderSpec(count).Split(new[] { " ; " }, StringSplitOptions.None));
        }

        public string BuildFolderSpec(int count)
        {
            var folders = new List<string>(count);
            for (int i = 1; i <= count; i++)
            {
                folders.Add(@"C:\tests\source-" + i);
            }

            return string.Join(" ; ", folders);
        }

        public bool InvokeIsValidFolderSpec(string spec)
        {
            return (bool)InvokePrivate(_form, "IsValidFolderSpec", spec);
        }

        public void SetRecentFolderItems(string spec)
        {
            var comboBox = new ComboBox();
            FieldInfo recentFoldersField = typeof(MainForm).GetField("_recentFoldersBox", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(recentFoldersField, "Missing field: _recentFoldersBox");
            recentFoldersField.SetValue(_form, comboBox);
            comboBox.Items.Clear();
            comboBox.Items.Add(spec);
            comboBox.SelectedIndex = 0;
        }

        public void SetFolderBoxText(string spec)
        {
            GetPrivateField<TextBox>(_form, "_folderBox").Text = spec;
        }

        public string GetFolderBoxText()
        {
            return GetPrivateField<TextBox>(_form, "_folderBox").Text;
        }

        public void InvokeRecentFoldersBoxOnSelectedIndexChanged()
        {
            InvokePrivate(_form, "RecentFoldersBoxOnSelectedIndexChanged", _form, EventArgs.Empty);
            System.Windows.Forms.Application.DoEvents();
        }

        public void InvokeTryAutoLoadLastFolder()
        {
            InvokePrivate(_form, "TryAutoLoadLastFolder");
            System.Windows.Forms.Application.DoEvents();
        }

        public void InvokeAddDataButtonOnClick()
        {
            InvokePrivate(_form, "AddDataButtonOnClick", _form, EventArgs.Empty);
            System.Windows.Forms.Application.DoEvents();
        }

        public void WaitForSessionFolder(string expectedFolder)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                if (string.Equals(_viewerSession.Folder, expectedFolder, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }
        }

        public Chart GetChart()
        {
            return GetPrivateField<Chart>(_form, "_chart");
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

        private void LoadSession(TestData data)
        {
            _viewerSession.SetData("C:\\tests\\run-01", data);
            InvokePrivate(_form, "BindLoadedData", data, false);
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
        private readonly HashSet<string> _existingDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool FileExists(string path) { return false; }

        public bool DirectoryExists(string path)
        {
            return _existingDirectories.Contains(path);
        }

        public void SetExistingDirectories(IEnumerable<string> directories)
        {
            _existingDirectories.Clear();
            foreach (string directory in directories)
            {
                _existingDirectories.Add(directory);
            }
        }

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
        public string LastErrorToastMessage { get; private set; }

        public void ShowError(Form owner, string title, string message) { }
        public void ShowInfoToast(Form owner, string message) { }
        public void ShowErrorToast(Form owner, string message) { LastErrorToastMessage = message; }
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
            var data = new TestData
            {
                Root = root,
                RowCount = 1,
                TimestampsMs = new[] { 0L },
                ColumnNames = new[] { "A-01" },
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = new[] { "A-01" }
                },
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = 0L
                },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = 0L
                }
            };

            data.Columns["A-01"] = new double?[] { 1d };
            data.Channels["A-01"] = new ChannelInfo { Code = "A-01", Name = "A-01", Unit = "u" };
            data.CodeSources["A-01"] = root;
            return data;
        }
    }
}
