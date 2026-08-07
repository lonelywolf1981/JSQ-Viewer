using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Linq;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Channels;
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
using JSQViewer.Presentation.WinForms.ViewModels;
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
        public void LoadFolder_WhenAddingSource_PreservesExistingSourceLayoutState()
        {
            var dataReader = new FakeTestDataSourceReader();
            dataReader.SetSource(@"C:\tests\source-1", "A-01", "A-02");
            dataReader.SetSource(@"C:\tests\source-2", "B-01");
            dataReader.SetSource(@"C:\tests\source-3", "C-01");

            using (TestMainFormHarness harness = TestMainFormHarness.Create(dataSourceReader: dataReader))
            {
                harness.AllowExistingDirectories(3);
                harness.LoadFolderSpec(harness.BuildFolderSpec(2));
                harness.SetAllSortModes("User");
                harness.SetSourceLayoutState(@"C:\tests\source-1", "src-a-order", new[] { "A-02", "A-01" });
                harness.LoadFolderSpec(harness.BuildFolderSpec(3), true, true);

                harness.WaitForSessionFolder(harness.BuildFolderSpec(3));
                Assert.AreEqual("src-a-order", harness.GetSourceSelectedOrderKey(@"C:\tests\source-1"));
                CollectionAssert.AreEqual(
                    new[] { "A-02", "A-01" },
                    harness.GetSourceWindowItemCodes(@"C:\tests\source-1"));
                Assert.AreEqual(string.Empty, harness.GetSourceSelectedOrderKey(@"C:\tests\source-3"));
            }
        }

        [TestMethod]
        public void BindLoadedData_UsesLegacyMainOrderWhenWorkspaceLayoutIsAbsent()
        {
            var orderRepository = new FakeOrderRepository
            {
                LegacyOrder = new List<string> { "B-01", "A-01" }
            };
            var workspaceLayoutRepository = new FakeWorkspaceLayoutRepository
            {
                ExistsResult = false
            };

            using (TestMainFormHarness harness = TestMainFormHarness.Create(
                orderRepository: orderRepository,
                workspaceLayoutRepository: workspaceLayoutRepository))
            {
                harness.LoadMultiSourceSession();

                CollectionAssert.AreEqual(
                    new[] { "B-01", "A-01" },
                    harness.GetMainOrder());
            }
        }

        [TestMethod]
        public void SaveOrderFromSource_DoesNotMaterializeBlankMainSelection()
        {
            var workspaceLayoutRepository = new FakeWorkspaceLayoutRepository();

            using (TestMainFormHarness harness = TestMainFormHarness.Create(
                orderRepository: new FakeOrderRepository(),
                workspaceLayoutRepository: workspaceLayoutRepository))
            {
                harness.LoadMultiSourceSession();

                Assert.AreEqual(string.Empty, harness.GetMainSelectedOrderKey());

                harness.InvokeSaveOrderFromSource(@"C:\tests\source-a\", "Source A Saved");

                Assert.AreEqual(string.Empty, harness.GetMainSelectedOrderKey());
                Assert.IsNotNull(workspaceLayoutRepository.LastSavedState);
                Assert.AreEqual(string.Empty, workspaceLayoutRepository.LastSavedState.MainSelectedOrderKey);
            }
        }

        [TestMethod]
        public void DeleteOrderFromSource_ClearsDanglingSelectedKeyBeforeLayoutSave()
        {
            var orderRepository = new FakeOrderRepository();
            ChannelOrderModel saved = orderRepository.Save("Source A Saved", new[] { "A-01" });
            var workspaceLayoutRepository = new FakeWorkspaceLayoutRepository();

            using (TestMainFormHarness harness = TestMainFormHarness.Create(
                orderRepository: orderRepository,
                workspaceLayoutRepository: workspaceLayoutRepository))
            {
                harness.LoadMultiSourceSession();
                harness.SetDeleteOrderConfirmationResult(true);
                harness.SetSourceSelectedOrderKey(@"C:\tests\source-a\", saved.key);
                harness.SelectSourceOrderKey(@"C:\tests\source-a\", saved.key);

                harness.InvokeDeleteOrderFromSource(@"C:\tests\source-a\");
                harness.InvokeSaveCurrentWorkspaceLayout();

                Assert.AreEqual(string.Empty, harness.GetSourceSelectedOrderKey(@"C:\tests\source-a\"));
                Assert.IsNotNull(workspaceLayoutRepository.LastSavedState);
                Assert.IsTrue(workspaceLayoutRepository.LastSavedState.Sources.ContainsKey(@"C:\tests\source-a\"));
                Assert.AreEqual(
                    string.Empty,
                    workspaceLayoutRepository.LastSavedState.Sources[@"C:\tests\source-a\"].SelectedOrderKey);
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

        public static TestMainFormHarness Create(
            FakeOrderRepository orderRepository = null,
            FakeWorkspaceLayoutRepository workspaceLayoutRepository = null,
            FakeTestDataSourceReader dataSourceReader = null)
        {
            var localization = new FakeLocalizationService();
            Loc.Initialize(localization);
            var fileSystem = new FakeFileSystem();
            var logger = new FakeLogger();
            var notificationService = new FakeNotificationService();
            var session = new ViewerSession(new MemorySeriesSliceCache());
            orderRepository = orderRepository ?? new FakeOrderRepository();
            workspaceLayoutRepository = workspaceLayoutRepository ?? new FakeWorkspaceLayoutRepository();
            dataSourceReader = dataSourceReader ?? new FakeTestDataSourceReader();
            var form = new MainForm(
                new FakeAppPaths(),
                fileSystem,
                logger,
                notificationService,
                new FakeExternalProcessLauncher(),
                new FakeRecentFoldersRepository(),
                new FakeUiStateRepository(),
                new FakePresetRepository(),
                orderRepository,
                workspaceLayoutRepository,
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
                    dataSourceReader,
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

        public void LoadFolderSpec(string spec, bool preserveSelection = false, bool preserveSourceWindowsLayout = false)
        {
            InvokePrivate(_form, "LoadFolder", spec, false, preserveSelection, null, preserveSourceWindowsLayout);
            WaitForSessionFolder(spec);
        }

        public void SetFolderPickerResult(string pickedFolder)
        {
            SetPrivateField(_form, "_folderPickerOverrideForTests", new Func<string, string>(delegate { return pickedFolder; }));
        }

        public void SetDeleteOrderConfirmationResult(bool confirmed)
        {
            SetPrivateField(_form, "_confirmDeleteOrderOverrideForTests", new Func<string, string, bool>(delegate { return confirmed; }));
        }

        public void SetSourceLayoutState(string sourceRoot, string selectedOrderKey, IList<string> order)
        {
            ChannelWorkspacePresenter presenter = GetPrivateField<ChannelWorkspacePresenter>(_form, "_channelWorkspacePresenter");
            presenter.SetSourceSelectedOrderKey(sourceRoot, selectedOrderKey);
            presenter.ApplySourceOrder(sourceRoot, order);
        }

        public void SetAllSortModes(string sortMode)
        {
            ChannelWorkspacePresenter presenter = GetPrivateField<ChannelWorkspacePresenter>(_form, "_channelWorkspacePresenter");
            presenter.SetAllSortModes(sortMode);
        }

        public string GetSourceSelectedOrderKey(string sourceRoot)
        {
            ChannelWorkspacePresenter presenter = GetPrivateField<ChannelWorkspacePresenter>(_form, "_channelWorkspacePresenter");
            return presenter.GetSourceSelectedOrderKey(sourceRoot);
        }

        public string[] GetSourceOrder(string sourceRoot)
        {
            ChannelWorkspacePresenter presenter = GetPrivateField<ChannelWorkspacePresenter>(_form, "_channelWorkspacePresenter");
            return presenter.GetCurrentOrderForSource(sourceRoot).ToArray();
        }

        public string[] GetSourceWindowItemCodes(string sourceRoot)
        {
            ChannelWorkspacePresenter presenter = GetPrivateField<ChannelWorkspacePresenter>(_form, "_channelWorkspacePresenter");
            SourceChannelWindowViewModel window = presenter.GetSourceWindow(sourceRoot);
            return window.Items.Select(item => item.Code).ToArray();
        }

        public string[] GetMainOrder()
        {
            ChannelWorkspacePresenter presenter = GetPrivateField<ChannelWorkspacePresenter>(_form, "_channelWorkspacePresenter");
            return presenter.GetCurrentOrder().ToArray();
        }

        public string GetMainSelectedOrderKey()
        {
            ChannelWorkspacePresenter presenter = GetPrivateField<ChannelWorkspacePresenter>(_form, "_channelWorkspacePresenter");
            return presenter.GetMainSelectedOrderKey();
        }

        public void InvokeSaveOrderFromSource(string sourceRoot, string orderName)
        {
            object state = GetSourceWindowState(sourceRoot);
            SetSourceStateTextBox(state, "OrderNameBox", orderName);
            InvokePrivate(_form, "SaveOrderFromSource", state);
            System.Windows.Forms.Application.DoEvents();
        }

        public void SetSourceSelectedOrderKey(string sourceRoot, string selectedOrderKey)
        {
            ChannelWorkspacePresenter presenter = GetPrivateField<ChannelWorkspacePresenter>(_form, "_channelWorkspacePresenter");
            presenter.SetSourceSelectedOrderKey(sourceRoot, selectedOrderKey);
            InvokePrivate(_form, "BindOrderControlsForSource", GetSourceWindowState(sourceRoot));
        }

        public void SelectSourceOrderKey(string sourceRoot, string orderKey)
        {
            object state = GetSourceWindowState(sourceRoot);
            ComboBox ordersBox = GetSourceStateValue<ComboBox>(state, "OrdersBox");
            SelectOrderByKey(ordersBox, orderKey);
        }

        public void InvokeDeleteOrderFromSource(string sourceRoot)
        {
            InvokePrivate(_form, "DeleteOrderFromSource", GetSourceWindowState(sourceRoot));
            System.Windows.Forms.Application.DoEvents();
        }

        public void InvokeSaveCurrentWorkspaceLayout()
        {
            InvokePrivate(_form, "SaveCurrentWorkspaceLayout");
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

        private object GetSourceWindowState(string sourceRoot)
        {
            object dictionary = GetPrivateField<object>(_form, "_sourceWindows");
            PropertyInfo itemProperty = dictionary.GetType().GetProperty("Item");
            Assert.IsNotNull(itemProperty, "Missing Item property on _sourceWindows.");
            return itemProperty.GetValue(dictionary, new object[] { sourceRoot });
        }

        private static T GetSourceStateValue<T>(object state, string propertyName) where T : class
        {
            PropertyInfo property = state.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "Missing property: " + propertyName);
            return property.GetValue(state, null) as T;
        }

        private static void SetSourceStateTextBox(object state, string propertyName, string text)
        {
            TextBox box = GetSourceStateValue<TextBox>(state, propertyName);
            Assert.IsNotNull(box, "Missing textbox: " + propertyName);
            box.Text = text;
        }

        private static void SelectOrderByKey(ComboBox box, string orderKey)
        {
            Assert.IsNotNull(box);
            box.SelectedIndex = -1;
            for (int i = 0; i < box.Items.Count; i++)
            {
                object item = box.Items[i];
                PropertyInfo keyProperty = item.GetType().GetProperty("Key", BindingFlags.Instance | BindingFlags.Public);
                Assert.IsNotNull(keyProperty, "Order item is missing Key.");
                string currentKey = keyProperty.GetValue(item, null) as string;
                if (string.Equals(currentKey, orderKey, StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedIndex = i;
                    return;
                }
            }
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + fieldName);
            return field.GetValue(target) as T;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + fieldName);
            field.SetValue(target, value);
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
        private readonly List<ChannelOrderModel> _orders = new List<ChannelOrderModel>();

        public List<string> LegacyOrder { get; set; } = new List<string>();

        public List<ChannelOrderModel> List() { return _orders.Select(Clone).ToList(); }

        public ChannelOrderModel Load(string keyOrName)
        {
            ChannelOrderModel model = _orders.FirstOrDefault(order =>
                string.Equals(order.key, keyOrName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(order.name, keyOrName, StringComparison.OrdinalIgnoreCase));
            return model == null ? null : Clone(model);
        }

        public bool Exists(string keyOrName)
        {
            return _orders.Any(order =>
                string.Equals(order.key, keyOrName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(order.name, keyOrName, StringComparison.OrdinalIgnoreCase));
        }

        public ChannelOrderModel Save(string name, IList<string> order)
        {
            string key = BuildKey(name);
            ChannelOrderModel existing = _orders.FirstOrDefault(item => string.Equals(item.key, key, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new ChannelOrderModel();
                _orders.Add(existing);
            }

            existing.key = key;
            existing.name = name;
            existing.order = order == null ? new List<string>() : order.ToList();
            return Clone(existing);
        }

        public bool Delete(string keyOrName)
        {
            ChannelOrderModel existing = _orders.FirstOrDefault(order =>
                string.Equals(order.key, keyOrName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(order.name, keyOrName, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                return false;
            }

            _orders.Remove(existing);
            return true;
        }

        public List<string> LoadLegacyOrder() { return LegacyOrder == null ? new List<string>() : new List<string>(LegacyOrder); }
        public bool SaveLegacyOrder(IList<string> order) { return true; }

        private static string BuildKey(string name)
        {
            string value = (name ?? string.Empty).Trim().ToLowerInvariant();
            var chars = value
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray();
            return new string(chars).Trim('-');
        }

        private static ChannelOrderModel Clone(ChannelOrderModel model)
        {
            return new ChannelOrderModel
            {
                key = model.key,
                name = model.name,
                order = model.order == null ? new List<string>() : new List<string>(model.order)
            };
        }
    }

    internal sealed class FakeWorkspaceLayoutRepository : IWorkspaceLayoutRepository
    {
        public bool ExistsResult { get; set; } = true;

        public WorkspaceLayoutState LoadedState { get; set; } = new WorkspaceLayoutState();

        public WorkspaceLayoutState LastSavedState { get; private set; }

        public bool Exists(string workspaceKey)
        {
            return ExistsResult;
        }

        public WorkspaceLayoutState Load(string workspaceKey)
        {
            return LoadedState == null ? new WorkspaceLayoutState() : LoadedState.Clone();
        }

        public bool Save(string workspaceKey, WorkspaceLayoutState state)
        {
            LastSavedState = state == null ? new WorkspaceLayoutState() : state.Clone();
            return true;
        }
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
        private readonly Dictionary<string, string[]> _sourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        public void SetSource(string root, params string[] columns)
        {
            _sourceColumns[root] = columns ?? Array.Empty<string>();
        }

        public TestData Read(string root, Dictionary<string, ChannelInfo> channels, Dictionary<string, string> metadata)
        {
            string[] columns;
            if (!_sourceColumns.TryGetValue(root, out columns))
            {
                columns = new[] { "A-01" };
            }

            var data = new TestData
            {
                Root = root,
                RowCount = 1,
                TimestampsMs = new[] { 0L },
                ColumnNames = columns,
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = columns
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

            for (int i = 0; i < columns.Length; i++)
            {
                string code = columns[i];
                data.Columns[code] = new double?[] { i + 1d };
                data.Channels[code] = new ChannelInfo { Code = code, Name = code, Unit = "u" };
                data.CodeSources[code] = root;
            }

            return data;
        }
    }
}
