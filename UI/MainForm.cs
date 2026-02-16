using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using LeMuReViewer.Core;
using LeMuReViewer.Export;
using LeMuReViewer.Settings;

namespace LeMuReViewer.UI
{
    public sealed class MainForm : Form
    {
        private readonly TextBox _folderBox;
        private readonly Button _browseButton;
        private readonly Button _loadButton;
        private readonly Button _copyPathButton;
        private readonly ComboBox _recentFoldersBox;
        private readonly Label _summaryLabel;
        private readonly Label _selectionInfoLabel;
        private readonly TextBox _channelFilterBox;
        private readonly ComboBox _sortModeBox;
        private readonly CheckBox _selectedOnlyCheck;
        private readonly Button _selectAllChannelsButton;
        private readonly Button _clearChannelsButton;
        private readonly CheckedListBox _channelsList;
        private readonly CheckBox _autoStepCheck;
        private readonly ComboBox _targetPointsBox;
        private readonly NumericUpDown _manualStepUpDown;
        private readonly CheckBox _includeExtraCheck;
        private readonly ComboBox _refrigerantBox;
        private readonly Button _exportTemplateButton;
        private readonly Button _settingsButton;
        private readonly Button _langButton;
        private readonly Label _recentLabel;
        private readonly Label _targetLabel;
        private readonly Label _manualLabel;
        private readonly Label _channelsHeader;
        private readonly Label _refrigLabel;
        private readonly TextBox _presetNameBox;
        private readonly Button _savePresetButton;
        private readonly ComboBox _presetsBox;
        private readonly Button _loadPresetButton;
        private readonly Button _deletePresetButton;
        private readonly TextBox _orderNameBox;
        private readonly Button _saveOrderButton;
        private readonly ComboBox _ordersBox;
        private readonly Button _loadOrderButton;
        private readonly Button _deleteOrderButton;
        private readonly Label _statusLabel;
        private readonly Chart _chart;
        private readonly Panel _busyPanel;
        private readonly Label _busyLabel;
        private readonly ProgressBar _busyProgress;
        private readonly SplitContainer _splitMain;
        private readonly ToolTip _toolTip;

        private int _dragIndex = -1;
        private readonly List<ChannelItem> _allChannels = new List<ChannelItem>();
        private readonly HashSet<string> _checkedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _pendingCheckedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _projectRoot;
        private readonly string _orderFilePath;
        private readonly string _recentFoldersFilePath;
        private readonly string _viewerSettingsFilePath;
        private readonly string _uiStateFilePath;
        private ViewerSettingsModel _viewerSettings;
        private static readonly Regex NaturalSplitRegex = new Regex("(\\d+)", RegexOptions.Compiled);

        public MainForm()
        {
            _projectRoot = ResolveProjectRoot();
            _orderFilePath = Path.Combine(_projectRoot, "channel_order.json");
            _recentFoldersFilePath = Path.Combine(_projectRoot, "recent_folders.json");
            _viewerSettingsFilePath = Path.Combine(_projectRoot, "viewer_settings.json");
            _uiStateFilePath = Path.Combine(_projectRoot, "ui_state.json");
            _viewerSettings = ViewerSettingsStore.Load(_viewerSettingsFilePath);

            Text = Loc.Get("AppTitle");
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                Icon = Icon.ExtractAssociatedIcon(exePath);
            }
            catch { }
            Width = 1420;
            Height = 900;
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            KeyPreview = true;

            _splitMain = new SplitContainer();
            _splitMain.Dock = DockStyle.Fill;
            _splitMain.Orientation = Orientation.Vertical;
            _splitMain.SplitterDistance = 520;
            Controls.Add(_splitMain);

            var left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.ColumnCount = 1;
            left.RowCount = 13;
            for (int i = 0; i < 13; i++) left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles[6] = new RowStyle(SizeType.Percent, 100);
            _splitMain.Panel1.Controls.Add(left);

            var folderRow = NewRow(); left.Controls.Add(folderRow, 0, 0);
            _folderBox = new TextBox(); _folderBox.Width = 220; folderRow.Controls.Add(_folderBox);
            _browseButton = new Button(); _browseButton.Text = Loc.Get("Browse"); _browseButton.Click += BrowseButtonOnClick; folderRow.Controls.Add(_browseButton);
            _loadButton = new Button(); _loadButton.Text = Loc.Get("Load"); _loadButton.Click += LoadButtonOnClick; folderRow.Controls.Add(_loadButton);
            _copyPathButton = new Button(); _copyPathButton.Text = Loc.Get("CopyPath"); _copyPathButton.Click += CopyPathButtonOnClick; folderRow.Controls.Add(_copyPathButton);
            _langButton = new Button(); _langButton.Text = Loc.Get("Language"); _langButton.Width = 36; _langButton.Click += LangButtonOnClick; folderRow.Controls.Add(_langButton);

            var recentRow = NewRow(); left.Controls.Add(recentRow, 0, 1);
            _recentLabel = new Label(); _recentLabel.Text = Loc.Get("Recent"); _recentLabel.AutoSize = true; _recentLabel.Padding = new Padding(0, 6, 4, 0); recentRow.Controls.Add(_recentLabel);
            _recentFoldersBox = new ComboBox(); _recentFoldersBox.Width = 360; _recentFoldersBox.DropDownStyle = ComboBoxStyle.DropDownList; _recentFoldersBox.SelectedIndexChanged += RecentFoldersBoxOnSelectedIndexChanged; recentRow.Controls.Add(_recentFoldersBox);

            _summaryLabel = new Label(); _summaryLabel.AutoSize = true; _summaryLabel.Padding = new Padding(4, 6, 4, 4); _summaryLabel.Text = Loc.Get("NoTestLoaded"); left.Controls.Add(_summaryLabel, 0, 2);
            _selectionInfoLabel = new Label(); _selectionInfoLabel.AutoSize = true; _selectionInfoLabel.Padding = new Padding(4, 2, 4, 6); _selectionInfoLabel.Text = Loc.Get("Selected"); left.Controls.Add(_selectionInfoLabel, 0, 3);

            var stepRow = NewRow(); left.Controls.Add(stepRow, 0, 4);
            _autoStepCheck = new CheckBox(); _autoStepCheck.Text = Loc.Get("AutoStep"); _autoStepCheck.Checked = true; _autoStepCheck.AutoSize = true; _autoStepCheck.CheckedChanged += StepControlsOnChanged; stepRow.Controls.Add(_autoStepCheck);
            _targetLabel = new Label(); _targetLabel.Text = Loc.Get("Target"); _targetLabel.AutoSize = true; _targetLabel.Padding = new Padding(8, 5, 2, 0); stepRow.Controls.Add(_targetLabel);
            _targetPointsBox = new ComboBox(); _targetPointsBox.DropDownStyle = ComboBoxStyle.DropDownList; _targetPointsBox.Width = 90; _targetPointsBox.Items.Add("1000"); _targetPointsBox.Items.Add("2000"); _targetPointsBox.Items.Add("5000"); _targetPointsBox.Items.Add("10000"); _targetPointsBox.Items.Add("20000"); _targetPointsBox.SelectedIndex = 2; _targetPointsBox.SelectedIndexChanged += StepControlsOnChanged; stepRow.Controls.Add(_targetPointsBox);
            _manualLabel = new Label(); _manualLabel.Text = Loc.Get("Manual"); _manualLabel.AutoSize = true; _manualLabel.Padding = new Padding(8, 5, 2, 0); stepRow.Controls.Add(_manualLabel);
            _manualStepUpDown = new NumericUpDown(); _manualStepUpDown.Width = 70; _manualStepUpDown.Minimum = 1; _manualStepUpDown.Maximum = 100000; _manualStepUpDown.Value = 1; _manualStepUpDown.Enabled = false; _manualStepUpDown.ValueChanged += StepControlsOnChanged; stepRow.Controls.Add(_manualStepUpDown);

            var channelsHeaderRow = NewRow(); left.Controls.Add(channelsHeaderRow, 0, 5);
            _channelsHeader = new Label(); _channelsHeader.Text = Loc.Get("Channels"); _channelsHeader.AutoSize = true; _channelsHeader.Padding = new Padding(4, 6, 2, 0); channelsHeaderRow.Controls.Add(_channelsHeader);
            _channelFilterBox = new TextBox(); _channelFilterBox.Width = 120; _channelFilterBox.TextChanged += ChannelViewOptionsChanged; channelsHeaderRow.Controls.Add(_channelFilterBox);
            _sortModeBox = new ComboBox(); _sortModeBox.Width = 140; _sortModeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _sortModeBox.Items.Add(new SortModeItem("User", "SortUser"));
            _sortModeBox.Items.Add(new SortModeItem("Code", "SortCode"));
            _sortModeBox.Items.Add(new SortModeItem("Natural code", "SortNaturalCode"));
            _sortModeBox.Items.Add(new SortModeItem("Label", "SortLabel"));
            _sortModeBox.Items.Add(new SortModeItem("Unit", "SortUnit"));
            _sortModeBox.Items.Add(new SortModeItem("Priority A/C", "SortPriorityAC"));
            _sortModeBox.Items.Add(new SortModeItem("Selected first", "SortSelectedFirst"));
            _sortModeBox.SelectedIndex = 5; _sortModeBox.SelectedIndexChanged += ChannelViewOptionsChanged; channelsHeaderRow.Controls.Add(_sortModeBox);
            _selectedOnlyCheck = new CheckBox(); _selectedOnlyCheck.Text = Loc.Get("SelectedOnly"); _selectedOnlyCheck.AutoSize = true; _selectedOnlyCheck.CheckedChanged += ChannelViewOptionsChanged; channelsHeaderRow.Controls.Add(_selectedOnlyCheck);
            _selectAllChannelsButton = new Button(); _selectAllChannelsButton.Text = Loc.Get("SelectAll"); _selectAllChannelsButton.Width = 90; _selectAllChannelsButton.Click += SelectAllChannelsButtonOnClick; channelsHeaderRow.Controls.Add(_selectAllChannelsButton);
            _clearChannelsButton = new Button(); _clearChannelsButton.Text = Loc.Get("Clear"); _clearChannelsButton.Width = 72; _clearChannelsButton.Click += ClearChannelsButtonOnClick; channelsHeaderRow.Controls.Add(_clearChannelsButton);
            _channelsList = new CheckedListBox(); _channelsList.Dock = DockStyle.Fill; _channelsList.CheckOnClick = true; _channelsList.AllowDrop = true; _channelsList.ItemCheck += ChannelsListOnItemCheck; _channelsList.MouseDown += ChannelsListOnMouseDown; _channelsList.DragOver += ChannelsListOnDragOver; _channelsList.DragDrop += ChannelsListOnDragDrop; left.Controls.Add(_channelsList, 0, 6);
            _channelsList.IntegralHeight = false;

            var templateOptionsRow = NewRow(); left.Controls.Add(templateOptionsRow, 0, 7);
            _includeExtraCheck = new CheckBox(); _includeExtraCheck.Text = Loc.Get("IncludeExtra"); _includeExtraCheck.Checked = true; _includeExtraCheck.AutoSize = true; templateOptionsRow.Controls.Add(_includeExtraCheck);
            _refrigLabel = new Label(); _refrigLabel.Text = Loc.Get("Refrigerant"); _refrigLabel.AutoSize = true; _refrigLabel.Padding = new Padding(8, 5, 2, 0); templateOptionsRow.Controls.Add(_refrigLabel);
            _refrigerantBox = new ComboBox(); _refrigerantBox.DropDownStyle = ComboBoxStyle.DropDownList; _refrigerantBox.Width = 90; _refrigerantBox.Items.Add("R290"); _refrigerantBox.Items.Add("R600a"); _refrigerantBox.SelectedIndex = 0; templateOptionsRow.Controls.Add(_refrigerantBox);

            var exportButtonsRow = NewRow(); left.Controls.Add(exportButtonsRow, 0, 8);
            _exportTemplateButton = new Button(); _exportTemplateButton.Text = Loc.Get("ExportTemplate"); _exportTemplateButton.Enabled = false; _exportTemplateButton.Click += ExportTemplateButtonOnClick; exportButtonsRow.Controls.Add(_exportTemplateButton);
            _settingsButton = new Button(); _settingsButton.Text = Loc.Get("Styles"); _settingsButton.Click += SettingsButtonOnClick; exportButtonsRow.Controls.Add(_settingsButton);

            var presetSaveRow = NewRow(); left.Controls.Add(presetSaveRow, 0, 9);
            _presetNameBox = new TextBox(); _presetNameBox.Width = 180; _presetNameBox.Text = Loc.Get("PresetName"); presetSaveRow.Controls.Add(_presetNameBox);
            _savePresetButton = new Button(); _savePresetButton.Text = Loc.Get("SavePreset"); _savePresetButton.Enabled = false; _savePresetButton.Click += SavePresetButtonOnClick; presetSaveRow.Controls.Add(_savePresetButton);

            var presetLoadRow = NewRow(); left.Controls.Add(presetLoadRow, 0, 10);
            _presetsBox = new ComboBox(); _presetsBox.Width = 220; _presetsBox.DropDownStyle = ComboBoxStyle.DropDownList; presetLoadRow.Controls.Add(_presetsBox);
            _loadPresetButton = new Button(); _loadPresetButton.Text = Loc.Get("Load"); _loadPresetButton.Enabled = false; _loadPresetButton.Click += LoadPresetButtonOnClick; presetLoadRow.Controls.Add(_loadPresetButton);
            _deletePresetButton = new Button(); _deletePresetButton.Text = Loc.Get("Delete"); _deletePresetButton.Enabled = false; _deletePresetButton.Click += DeletePresetButtonOnClick; presetLoadRow.Controls.Add(_deletePresetButton);

            var orderRow = NewRow(); left.Controls.Add(orderRow, 0, 11);
            _orderNameBox = new TextBox(); _orderNameBox.Width = 130; _orderNameBox.Text = Loc.Get("OrderName"); orderRow.Controls.Add(_orderNameBox);
            _saveOrderButton = new Button(); _saveOrderButton.Text = Loc.Get("SaveOrder"); _saveOrderButton.Enabled = false; _saveOrderButton.Click += SaveOrderButtonOnClick; orderRow.Controls.Add(_saveOrderButton);
            _ordersBox = new ComboBox(); _ordersBox.Width = 180; _ordersBox.DropDownStyle = ComboBoxStyle.DropDownList; orderRow.Controls.Add(_ordersBox);
            _loadOrderButton = new Button(); _loadOrderButton.Text = Loc.Get("Load"); _loadOrderButton.Enabled = false; _loadOrderButton.Click += LoadOrderButtonOnClick; orderRow.Controls.Add(_loadOrderButton);
            _deleteOrderButton = new Button(); _deleteOrderButton.Text = Loc.Get("Delete"); _deleteOrderButton.Enabled = false; _deleteOrderButton.Click += DeleteOrderButtonOnClick; orderRow.Controls.Add(_deleteOrderButton);

            _statusLabel = new Label(); _statusLabel.AutoSize = true; _statusLabel.Padding = new Padding(4, 6, 4, 8); _statusLabel.Text = Loc.Get("Ready"); left.Controls.Add(_statusLabel, 0, 12);

            _chart = BuildChart();
            _splitMain.Panel2.Controls.Add(_chart);

            _busyPanel = new Panel();
            _busyPanel.Dock = DockStyle.Fill;
            _busyPanel.BackColor = Color.FromArgb(180, 230, 230, 230);
            _busyPanel.Visible = false;
            Controls.Add(_busyPanel);
            _busyPanel.BringToFront();

            var busyBox = new Panel();
            busyBox.Size = new Size(300, 90);
            busyBox.BackColor = Color.White;
            busyBox.BorderStyle = BorderStyle.FixedSingle;
            _busyPanel.Controls.Add(busyBox);

            _busyLabel = new Label();
            _busyLabel.AutoSize = false;
            _busyLabel.TextAlign = ContentAlignment.MiddleCenter;
            _busyLabel.Dock = DockStyle.Top;
            _busyLabel.Height = 38;
            _busyLabel.Text = Loc.Get("Working");
            busyBox.Controls.Add(_busyLabel);

            _busyProgress = new ProgressBar();
            _busyProgress.Dock = DockStyle.Top;
            _busyProgress.Style = ProgressBarStyle.Marquee;
            _busyProgress.MarqueeAnimationSpeed = 25;
            _busyProgress.Height = 16;
            _busyProgress.Top = 44;
            busyBox.Controls.Add(_busyProgress);

            EventHandler centerBusyBox = delegate
            {
                busyBox.Left = (_busyPanel.ClientSize.Width - busyBox.Width) / 2;
                busyBox.Top = (_busyPanel.ClientSize.Height - busyBox.Height) / 2;
            };
            _busyPanel.Resize += centerBusyBox;
            _busyPanel.VisibleChanged += delegate { if (_busyPanel.Visible) centerBusyBox(null, EventArgs.Empty); };

            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 10000;
            _toolTip.InitialDelay = 400;
            _toolTip.ReshowDelay = 200;
            ApplyTooltips();

            LoadRecentFolders();
            ReloadPresets();
            ReloadOrders();
            FormClosing += OnFormClosingSaveOrder;
            FormClosing += OnFormClosingSaveUiState;
            KeyDown += MainFormOnKeyDown;
            Loc.LanguageChanged += ApplyLocalization;
            LoadUiState();
            StepControlsOnChanged(this, EventArgs.Empty);
        }

        private void LangButtonOnClick(object sender, EventArgs e)
        {
            Loc.Current = Loc.Current == Lang.Ru ? Lang.En : Lang.Ru;
        }

        private void ApplyLocalization()
        {
            Text = Loc.Get("AppTitle");
            _browseButton.Text = Loc.Get("Browse");
            _loadButton.Text = Loc.Get("Load");
            _copyPathButton.Text = Loc.Get("CopyPath");
            _langButton.Text = Loc.Get("Language");
            _recentLabel.Text = Loc.Get("Recent");
            _autoStepCheck.Text = Loc.Get("AutoStep");
            _targetLabel.Text = Loc.Get("Target");
            _manualLabel.Text = Loc.Get("Manual");
            _channelsHeader.Text = Loc.Get("Channels");
            _selectedOnlyCheck.Text = Loc.Get("SelectedOnly");
            _selectAllChannelsButton.Text = Loc.Get("SelectAll");
            _clearChannelsButton.Text = Loc.Get("Clear");
            _includeExtraCheck.Text = Loc.Get("IncludeExtra");
            _refrigLabel.Text = Loc.Get("Refrigerant");
            _exportTemplateButton.Text = Loc.Get("ExportTemplate");
            _settingsButton.Text = Loc.Get("Styles");
            _savePresetButton.Text = Loc.Get("SavePreset");
            _loadPresetButton.Text = Loc.Get("Load");
            _deletePresetButton.Text = Loc.Get("Delete");
            _saveOrderButton.Text = Loc.Get("SaveOrder");
            _loadOrderButton.Text = Loc.Get("Load");
            _deleteOrderButton.Text = Loc.Get("Delete");
            // Force sort combo to re-read ToString() of items
            int selIdx = _sortModeBox.SelectedIndex;
            typeof(ComboBox).GetMethod("RefreshItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(_sortModeBox, null);
            if (selIdx >= 0 && selIdx < _sortModeBox.Items.Count) _sortModeBox.SelectedIndex = selIdx;
            ApplyTooltips();
            UpdateSelectionInfo();
        }

        private void ApplyTooltips()
        {
            _toolTip.SetToolTip(_folderBox, Loc.Get("TipFolder"));
            _toolTip.SetToolTip(_browseButton, Loc.Get("TipBrowse"));
            _toolTip.SetToolTip(_loadButton, Loc.Get("TipLoad"));
            _toolTip.SetToolTip(_copyPathButton, Loc.Get("TipCopyPath"));
            _toolTip.SetToolTip(_langButton, Loc.Get("TipLang"));
            _toolTip.SetToolTip(_recentFoldersBox, Loc.Get("TipRecent"));
            _toolTip.SetToolTip(_autoStepCheck, Loc.Get("TipAutoStep"));
            _toolTip.SetToolTip(_targetPointsBox, Loc.Get("TipTarget"));
            _toolTip.SetToolTip(_manualStepUpDown, Loc.Get("TipManualStep"));
            _toolTip.SetToolTip(_channelFilterBox, Loc.Get("TipFilter"));
            _toolTip.SetToolTip(_sortModeBox, Loc.Get("TipSort"));
            _toolTip.SetToolTip(_selectedOnlyCheck, Loc.Get("TipSelectedOnly"));
            _toolTip.SetToolTip(_selectAllChannelsButton, Loc.Get("TipSelectAll"));
            _toolTip.SetToolTip(_clearChannelsButton, Loc.Get("TipClear"));
            _toolTip.SetToolTip(_includeExtraCheck, Loc.Get("TipIncludeExtra"));
            _toolTip.SetToolTip(_refrigerantBox, Loc.Get("TipRefrigerant"));
            _toolTip.SetToolTip(_exportTemplateButton, Loc.Get("TipExportTemplate"));
            _toolTip.SetToolTip(_settingsButton, Loc.Get("TipStyles"));
            _toolTip.SetToolTip(_presetNameBox, Loc.Get("TipPresetName"));
            _toolTip.SetToolTip(_savePresetButton, Loc.Get("TipSavePreset"));
            _toolTip.SetToolTip(_presetsBox, Loc.Get("TipPresets"));
            _toolTip.SetToolTip(_loadPresetButton, Loc.Get("TipLoadPreset"));
            _toolTip.SetToolTip(_deletePresetButton, Loc.Get("TipDeletePreset"));
            _toolTip.SetToolTip(_orderNameBox, Loc.Get("TipOrderName"));
            _toolTip.SetToolTip(_saveOrderButton, Loc.Get("TipSaveOrder"));
            _toolTip.SetToolTip(_ordersBox, Loc.Get("TipOrders"));
            _toolTip.SetToolTip(_loadOrderButton, Loc.Get("TipLoadOrder"));
            _toolTip.SetToolTip(_deleteOrderButton, Loc.Get("TipDeleteOrder"));
        }

        private static FlowLayoutPanel NewRow()
        {
            var row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.AutoSize = true;
            row.WrapContents = false;
            return row;
        }

        private static Chart BuildChart()
        {
            var chart = new Chart(); chart.Dock = DockStyle.Fill;
            var area = new ChartArea("main");
            area.AxisX.LabelStyle.Format = "HH:mm\ndd.MM";
            area.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            area.AxisX.MajorGrid.LineColor = Color.Gainsboro;
            area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
            area.CursorX.IsUserEnabled = true;
            area.CursorX.IsUserSelectionEnabled = true;
            area.AxisX.ScrollBar.Enabled = true;
            area.AxisX.ScaleView.Zoomable = true;
            chart.ChartAreas.Add(area);
            var legend = new Legend("legend"); legend.Docking = Docking.Top; legend.Alignment = StringAlignment.Center; chart.Legends.Add(legend);
            return chart;
        }

        private void BrowseButtonOnClick(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = Directory.Exists(_folderBox.Text) ? _folderBox.Text : string.Empty;
                if (dialog.ShowDialog(this) == DialogResult.OK) _folderBox.Text = dialog.SelectedPath;
            }
        }

        private void CopyPathButtonOnClick(object sender, EventArgs e)
        {
            try { if (!string.IsNullOrWhiteSpace(_folderBox.Text)) { Clipboard.SetText(_folderBox.Text); NotifySuccess(Loc.Get("PathCopied")); } }
            catch (Exception ex) { AppLogger.LogError(_projectRoot, "Copy path failed.", ex); NotifyError(Loc.Get("CopyPathFailed")); }
        }

        private void RecentFoldersBoxOnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_recentFoldersBox.SelectedItem != null) _folderBox.Text = _recentFoldersBox.SelectedItem.ToString();
        }

        private void TryAutoLoadLastFolder()
        {
            try
            {
                if (_recentFoldersBox.Items.Count <= 0 || _recentFoldersBox.SelectedItem == null)
                {
                    return;
                }

                string folder = _recentFoldersBox.SelectedItem.ToString();
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                {
                    LoadFolder(folder, false);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(_projectRoot, "Auto-load recent folder failed.", ex);
            }
        }

        private void MainFormOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAllChannelsButtonOnClick(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Escape)
            {
                ClearChannelsButtonOnClick(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.E)
            {
                ExportTemplateButtonOnClick(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
            }
        }

        private void LoadButtonOnClick(object sender, EventArgs e)
        {
            string folder = (_folderBox.Text ?? string.Empty).Trim();
            if (folder.Length == 0) { NotifyError(Loc.Get("SelectFolder")); return; }
            LoadFolder(folder, true);
        }

        private void LoadFolder(string folder, bool addToRecent)
        {
            try
            {
                SetBusy(true, Loc.Get("LoadingData"));
                Cursor = Cursors.WaitCursor;
                TestData data = TestLoader.LoadTest(folder);
                AppState.SetData(folder, data);
                BindLoadedData(data);
                if (addToRecent)
                {
                    AddRecentFolder(folder);
                }
                NotifySuccess(string.Format(Loc.Get("LoadedTest"), data.RowCount));
            }
            catch (Exception ex) { AppLogger.LogError(_projectRoot, "Load test failed.", ex); MessageBox.Show(this, ex.Message, Loc.Get("LoadFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error); NotifyError(Loc.Get("LoadFailed")); }
            finally { Cursor = Cursors.Default; SetBusy(false, null); }
        }

        private void BindLoadedData(TestData data)
        {
            _allChannels.Clear();
            _checkedCodes.Clear();
            if (_pendingCheckedCodes.Count > 0)
            {
                foreach (string code in _pendingCheckedCodes)
                {
                    _checkedCodes.Add(code);
                }
                _pendingCheckedCodes.Clear();
            }
            string[] orderedColumns = ApplySavedOrder(data.ColumnNames);
            for (int i = 0; i < orderedColumns.Length; i++)
            {
                string code = orderedColumns[i];
                ChannelInfo ch;
                string label = data.Channels.TryGetValue(code, out ch) ? ch.Label : code;
                string unit = data.Channels.TryGetValue(code, out ch) ? (ch.Unit ?? string.Empty) : string.Empty;
                _allChannels.Add(new ChannelItem(code, label, unit));
            }
            RebuildChannelList();
            DataSummary summary = AppState.BuildSummary(data);
            _summaryLabel.Text = string.Format(Loc.Get("Points"), summary.Points, summary.Start, summary.End);
            _exportTemplateButton.Enabled = _savePresetButton.Enabled = true;
            _loadPresetButton.Enabled = _deletePresetButton.Enabled = _presetsBox.Items.Count > 0;
            _saveOrderButton.Enabled = true;
            _loadOrderButton.Enabled = _deleteOrderButton.Enabled = _ordersBox.Items.Count > 0;
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void StepControlsOnChanged(object sender, EventArgs e)
        {
            _manualStepUpDown.Enabled = !_autoStepCheck.Checked;
            _targetPointsBox.Enabled = _autoStepCheck.Checked;
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void ChannelViewOptionsChanged(object sender, EventArgs e)
        {
            RebuildChannelList();
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void ChannelsListOnItemCheck(object sender, ItemCheckEventArgs e)
        {
            BeginInvoke((Action)(delegate
            {
                SyncCheckedFromVisibleList();
                UpdateSelectionInfo();
                RedrawChart();
            }));
        }

        private void SelectAllChannelsButtonOnClick(object sender, EventArgs e)
        {
            for (int i = 0; i < _allChannels.Count; i++)
            {
                _checkedCodes.Add(_allChannels[i].Code);
            }
            for (int i = 0; i < _channelsList.Items.Count; i++)
            {
                _channelsList.SetItemChecked(i, true);
            }
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void ClearChannelsButtonOnClick(object sender, EventArgs e)
        {
            _checkedCodes.Clear();
            for (int i = 0; i < _channelsList.Items.Count; i++)
            {
                _channelsList.SetItemChecked(i, false);
            }
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void ChannelsListOnMouseDown(object sender, MouseEventArgs e)
        {
            _dragIndex = _channelsList.IndexFromPoint(e.Location);
            if (_dragIndex >= 0 && e.Button == MouseButtons.Left) _channelsList.DoDragDrop(_channelsList.Items[_dragIndex], DragDropEffects.Move);
        }

        private void ChannelsListOnDragOver(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(ChannelItem)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void ChannelsListOnDragDrop(object sender, DragEventArgs e)
        {
            if (_dragIndex < 0 || !e.Data.GetDataPresent(typeof(ChannelItem))) return;
            if (_selectedOnlyCheck.Checked || !string.IsNullOrWhiteSpace(_channelFilterBox.Text) || GetSelectedSortKey() != "User") return;
            Point p = _channelsList.PointToClient(new Point(e.X, e.Y));
            int targetIndex = _channelsList.IndexFromPoint(p);
            if (targetIndex < 0) targetIndex = _channelsList.Items.Count - 1;
            if (targetIndex == _dragIndex) return;
            object dragged = _channelsList.Items[_dragIndex];
            bool draggedChecked = _channelsList.GetItemChecked(_dragIndex);
            _channelsList.Items.RemoveAt(_dragIndex);
            _channelsList.Items.Insert(targetIndex, dragged);
            _channelsList.SetItemChecked(targetIndex, draggedChecked);
            _channelsList.SelectedIndex = targetIndex;
            RebuildAllChannelsFromVisibleList();
            SyncCheckedFromVisibleList();
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void RedrawChart()
        {
            _chart.Series.Clear();
            TestData data = AppState.Data;
            if (data == null || data.RowCount == 0) return;
            List<string> selectedCodes = GetSelectedCodes();
            if (selectedCodes.Count == 0) return;
            int step = ResolveStep(data.TimestampsMs.Length);
            SeriesSlice slice = SeriesCache.GetOrBuild(AppState.DataVersion, data, selectedCodes, data.TimestampsMs[0], data.TimestampsMs[data.TimestampsMs.Length - 1], step);

            // Pre-convert timestamps to OADate once for all channels
            long[] ts = slice.Timestamps;
            double[] oaDates = new double[ts.Length];
            for (int i = 0; i < ts.Length; i++)
                oaDates[i] = AppState.UnixMsToLocalDateTime(ts[i]).ToOADate();

            _chart.SuspendLayout();
            try
            {
                foreach (string code in selectedCodes)
                {
                    double?[] values;
                    if (!slice.Series.TryGetValue(code, out values)) continue;
                    var series = new Series(code); series.ChartType = SeriesChartType.FastLine; series.XValueType = ChartValueType.DateTime; series.BorderWidth = 2;
                    int n = Math.Min(oaDates.Length, values.Length);

                    // Collect non-null points into arrays for batch binding
                    var xList = new List<double>(n);
                    var yList = new List<double>(n);
                    for (int i = 0; i < n; i++)
                    {
                        if (values[i].HasValue)
                        {
                            xList.Add(oaDates[i]);
                            yList.Add(values[i].Value);
                        }
                    }
                    if (xList.Count > 0)
                        series.Points.DataBindXY(xList, yList);

                    _chart.Series.Add(series);
                }
            }
            finally
            {
                _chart.ResumeLayout();
            }
        }

        private int ResolveStep(int totalPoints)
        {
            if (!_autoStepCheck.Checked) return Math.Max(1, (int)_manualStepUpDown.Value);
            int target = 5000;
            if (_targetPointsBox.SelectedItem != null)
            {
                int parsed; if (int.TryParse(_targetPointsBox.SelectedItem.ToString(), out parsed)) target = Math.Max(1, parsed);
            }
            return Math.Max(1, totalPoints / target);
        }

        private void ExportTemplateButtonOnClick(object sender, EventArgs e)
        {
            TestData data = AppState.Data; if (data == null) return;
            string templatePath = Path.Combine(_projectRoot, "template.xlsx");
            if (!File.Exists(templatePath)) { NotifyError(Loc.Get("TemplateNotFound")); return; }
            List<string> selectedCodes = GetSelectedCodes();
            string refrig = _refrigerantBox.SelectedItem == null ? "R290" : _refrigerantBox.SelectedItem.ToString();
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel files (*.xlsx)|*.xlsx"; dialog.FileName = "template_filled_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    SetBusy(true, Loc.Get("ExportingTemplate"));
                    byte[] payload = TemplateExporter.Export(templatePath, AppState.Folder, data, selectedCodes, _includeExtraCheck.Checked, refrig, _viewerSettings);
                    File.WriteAllBytes(dialog.FileName, payload);
                    TemplateValidationResult vr = TemplateExportValidator.Validate(payload);
                    if (vr.Ok)
                    {
                        NotifySuccess(Loc.Get("TemplateExported"));
                    }
                    else
                    {
                        AppLogger.LogError(_projectRoot, "Template export validation warning: " + vr.Message, null);
                        NotifyError(Loc.Get("TemplateExportedWarning"));
                    }
                }
                catch (Exception ex) { AppLogger.LogError(_projectRoot, "Template export failed.", ex); MessageBox.Show(this, ex.Message, Loc.Get("TemplateExportFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error); NotifyError(Loc.Get("TemplateExportFailed")); }
                finally { SetBusy(false, null); }
            }
        }

        private void SettingsButtonOnClick(object sender, EventArgs e)
        {
            using (var dlg = new SettingsDialog(_viewerSettings))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _viewerSettings = dlg.Result ?? ViewerSettingsModel.CreateDefault();
                SetBusy(true, Loc.Get("SavingStyles"));
                bool ok = ViewerSettingsStore.Save(_viewerSettingsFilePath, _viewerSettings);
                if (ok) NotifySuccess(Loc.Get("StylesSaved"));
                else NotifyError(Loc.Get("StylesSaveFailed"));
                if (!ok) AppLogger.LogError(_projectRoot, "Style settings save failed.", null);
                SetBusy(false, null);
            }
        }

        private void SavePresetButtonOnClick(object sender, EventArgs e)
        {
            List<string> selected = GetSelectedCodes();
            if (selected.Count == 0) { NotifyError(Loc.Get("SelectChannel")); return; }
            string name = (_presetNameBox.Text ?? string.Empty).Trim();
            if (name.Length == 0 || string.Equals(name, Loc.Get("PresetName"), StringComparison.OrdinalIgnoreCase)) { NotifyError(Loc.Get("EnterPresetName")); return; }
            int targetPoints = 5000;
            int parsedTarget;
            if (_targetPointsBox.SelectedItem != null && int.TryParse(_targetPointsBox.SelectedItem.ToString(), out parsedTarget))
            {
                targetPoints = parsedTarget;
            }
            ViewerPreset payload = new ViewerPreset();
            payload.name = name;
            payload.channels = selected;
            payload.sort_mode = GetSelectedSortKey();
            payload.auto_step = _autoStepCheck.Checked;
            payload.target_points = targetPoints;
            payload.manual_step = (int)_manualStepUpDown.Value;
            payload.include_extra = _includeExtraCheck.Checked;
            payload.refrigerant = _refrigerantBox.SelectedItem == null ? "R290" : _refrigerantBox.SelectedItem.ToString();

            bool existed = PresetStore.Exists(_projectRoot, payload.name);
            ViewerPreset preset = PresetStore.Save(_projectRoot, payload);
            ReloadPresets(); SelectPresetByKey(preset.key); NotifySuccess(string.Format(existed ? Loc.Get("PresetUpdated") : Loc.Get("PresetSaved"), preset.name));
        }

        private void LoadPresetButtonOnClick(object sender, EventArgs e)
        {
            var item = _presetsBox.SelectedItem as PresetItem; if (item == null) return;
            ViewerPreset preset = PresetStore.Load(_projectRoot, item.Key);
            if (preset == null || preset.channels == null) { NotifyError(Loc.Get("PresetInvalid")); return; }
            if (!string.IsNullOrWhiteSpace(preset.sort_mode))
            {
                SelectSortModeByKey(preset.sort_mode);
            }
            if (preset.auto_step.HasValue)
            {
                _autoStepCheck.Checked = preset.auto_step.Value;
            }
            if (preset.target_points.HasValue)
            {
                SelectComboItem(_targetPointsBox, preset.target_points.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (preset.manual_step.HasValue)
            {
                int m = Math.Max((int)_manualStepUpDown.Minimum, Math.Min((int)_manualStepUpDown.Maximum, preset.manual_step.Value));
                _manualStepUpDown.Value = m;
            }
            if (preset.include_extra.HasValue)
            {
                _includeExtraCheck.Checked = preset.include_extra.Value;
            }
            if (!string.IsNullOrWhiteSpace(preset.refrigerant))
            {
                SelectComboItem(_refrigerantBox, preset.refrigerant);
            }
            ApplyChannelChecks(preset.channels); NotifySuccess(string.Format(Loc.Get("PresetLoaded"), preset.name ?? item.Key));
        }

        private void DeletePresetButtonOnClick(object sender, EventArgs e)
        {
            var item = _presetsBox.SelectedItem as PresetItem; if (item == null) return;
            if (MessageBox.Show(this, string.Format(Loc.Get("DeletePresetQ"), item.Name), Loc.Get("DeletePresetTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            bool ok = PresetStore.Delete(_projectRoot, item.Key);
            ReloadPresets();
            if (ok) NotifySuccess(string.Format(Loc.Get("PresetDeleted"), item.Name));
            else NotifyError(Loc.Get("PresetDeleteFailed"));
        }

        private void SaveOrderButtonOnClick(object sender, EventArgs e)
        {
            string name = (_orderNameBox.Text ?? string.Empty).Trim();
            if (name.Length == 0 || string.Equals(name, Loc.Get("OrderName"), StringComparison.OrdinalIgnoreCase))
            {
                NotifyError(Loc.Get("EnterOrderName"));
                return;
            }
            List<string> order = BuildCurrentOrder();
            if (order.Count == 0)
            {
                NotifyError(Loc.Get("NoChannelsToSave"));
                return;
            }
            bool existed = OrderStore.Exists(_projectRoot, name);
            ChannelOrderModel saved = OrderStore.Save(_projectRoot, name, order);
            ReloadOrders();
            SelectOrderByKey(saved.key);
            NotifySuccess(string.Format(existed ? Loc.Get("OrderUpdated") : Loc.Get("OrderSaved"), saved.name));
        }

        private void LoadOrderButtonOnClick(object sender, EventArgs e)
        {
            var item = _ordersBox.SelectedItem as OrderItem;
            if (item == null) return;
            ChannelOrderModel order = OrderStore.Load(_projectRoot, item.Key);
            if (order == null || order.order == null)
            {
                NotifyError(Loc.Get("OrderInvalid"));
                return;
            }
            ApplyOrder(order.order);
            NotifySuccess(string.Format(Loc.Get("OrderLoaded"), order.name ?? item.Key));
        }

        private void DeleteOrderButtonOnClick(object sender, EventArgs e)
        {
            var item = _ordersBox.SelectedItem as OrderItem;
            if (item == null) return;
            if (MessageBox.Show(this, string.Format(Loc.Get("DeleteOrderQ"), item.Name), Loc.Get("DeleteOrderTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            bool ok = OrderStore.Delete(_projectRoot, item.Key);
            ReloadOrders();
            if (ok) NotifySuccess(string.Format(Loc.Get("OrderDeleted"), item.Name));
            else NotifyError(Loc.Get("OrderDeleteFailed"));
        }

        private void ApplyChannelChecks(IList<string> checkedCodes)
        {
            _checkedCodes.Clear();
            if (checkedCodes != null) for (int i = 0; i < checkedCodes.Count; i++) _checkedCodes.Add(checkedCodes[i]);
            RebuildChannelList();
            UpdateSelectionInfo();
            RedrawChart();
        }

        private List<string> GetSelectedCodes()
        {
            SyncCheckedFromVisibleList();
            var result = new List<string>();
            for (int i = 0; i < _allChannels.Count; i++)
            {
                ChannelItem item = _allChannels[i];
                if (_checkedCodes.Contains(item.Code)) result.Add(item.Code);
            }
            return result;
        }

        private void RebuildChannelList()
        {
            SyncCheckedFromVisibleList();
            IEnumerable<ChannelItem> items = _allChannels;

            string filter = (_channelFilterBox.Text ?? string.Empty).Trim();
            if (filter.Length > 0)
            {
                string f = filter.ToLowerInvariant();
                items = items.Where(delegate(ChannelItem c)
                {
                    return (c.Code ?? string.Empty).ToLowerInvariant().Contains(f)
                        || (c.Label ?? string.Empty).ToLowerInvariant().Contains(f);
                });
            }

            if (_selectedOnlyCheck.Checked)
            {
                items = items.Where(c => _checkedCodes.Contains(c.Code));
            }

            string mode = GetSelectedSortKey();
            if (mode == "Code")
            {
                items = items.OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase);
            }
            else if (mode == "Natural code")
            {
                items = items.OrderBy(c => c.Code, new NaturalStringComparer());
            }
            else if (mode == "Label")
            {
                items = items.OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase);
            }
            else if (mode == "Unit")
            {
                items = items.OrderBy(c => c.Unit, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase);
            }
            else if (mode == "Priority A/C")
            {
                items = items.OrderBy(c => PrefixPriority(c.Code)).ThenBy(c => c.Code, new NaturalStringComparer());
            }
            else if (mode == "Selected first")
            {
                items = items.OrderByDescending(c => _checkedCodes.Contains(c.Code)).ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase);
            }

            _channelsList.Items.Clear();
            foreach (ChannelItem c in items)
            {
                int idx = _channelsList.Items.Add(c, _checkedCodes.Contains(c.Code));
                if (idx >= 0) { }
            }
        }

        private void SyncCheckedFromVisibleList()
        {
            for (int i = 0; i < _channelsList.Items.Count; i++)
            {
                ChannelItem item = _channelsList.Items[i] as ChannelItem;
                if (item == null) continue;
                bool isChecked = _channelsList.GetItemChecked(i);
                if (isChecked) _checkedCodes.Add(item.Code);
                else _checkedCodes.Remove(item.Code);
            }
        }

        private void RebuildAllChannelsFromVisibleList()
        {
            if (GetSelectedSortKey() != "User")
            {
                return;
            }
            if (_selectedOnlyCheck.Checked || !string.IsNullOrWhiteSpace(_channelFilterBox.Text))
            {
                return;
            }
            _allChannels.Clear();
            for (int i = 0; i < _channelsList.Items.Count; i++)
            {
                ChannelItem item = _channelsList.Items[i] as ChannelItem;
                if (item != null) _allChannels.Add(item);
            }
        }

        private void ReloadPresets()
        {
            _presetsBox.Items.Clear();
            List<ViewerPreset> presets = PresetStore.List(_projectRoot);
            for (int i = 0; i < presets.Count; i++)
            {
                ViewerPreset p = presets[i];
                _presetsBox.Items.Add(new PresetItem(p.key, p.name, p.channels == null ? 0 : p.channels.Count));
            }
            if (_presetsBox.Items.Count > 0) _presetsBox.SelectedIndex = 0;
            _loadPresetButton.Enabled = _deletePresetButton.Enabled = _presetsBox.Items.Count > 0;
        }

        private void ReloadOrders()
        {
            _ordersBox.Items.Clear();
            List<ChannelOrderModel> orders = OrderStore.List(_projectRoot);
            for (int i = 0; i < orders.Count; i++)
            {
                ChannelOrderModel o = orders[i];
                _ordersBox.Items.Add(new OrderItem(o.key, o.name, o.order == null ? 0 : o.order.Count));
            }
            if (_ordersBox.Items.Count > 0)
            {
                _ordersBox.SelectedIndex = 0;
            }
            _loadOrderButton.Enabled = _deleteOrderButton.Enabled = _ordersBox.Items.Count > 0;
        }

        private void SelectPresetByKey(string key)
        {
            for (int i = 0; i < _presetsBox.Items.Count; i++)
            {
                var item = _presetsBox.Items[i] as PresetItem;
                if (item != null && string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) { _presetsBox.SelectedIndex = i; return; }
            }
        }

        private void SelectOrderByKey(string key)
        {
            for (int i = 0; i < _ordersBox.Items.Count; i++)
            {
                var item = _ordersBox.Items[i] as OrderItem;
                if (item != null && string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) { _ordersBox.SelectedIndex = i; return; }
            }
        }

        private void SelectSortModeByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            for (int i = 0; i < _sortModeBox.Items.Count; i++)
            {
                var item = _sortModeBox.Items[i] as SortModeItem;
                if (item != null && string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    _sortModeBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private static void SelectComboItem(ComboBox combo, string value)
        {
            if (combo == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            for (int i = 0; i < combo.Items.Count; i++)
            {
                object item = combo.Items[i];
                if (item != null && string.Equals(item.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private void UpdateSelectionInfo()
        {
            SyncCheckedFromVisibleList();
            int selected = _checkedCodes.Count;
            int total = _allChannels.Count;
            string stepInfo = string.Empty;
            TestData data = AppState.Data;
            if (data != null && data.TimestampsMs != null && data.TimestampsMs.Length > 0)
            {
                int step = ResolveStep(data.TimestampsMs.Length);
                int approx = Math.Max(1, data.TimestampsMs.Length / step);
                stepInfo = string.Format(Loc.Get("StepInfo"), step, approx);
            }
            _selectionInfoLabel.Text = string.Format(Loc.Get("SelectedInfo"), selected, total, stepInfo);
        }

        private void SetStatus(string text) { _statusLabel.Text = text ?? string.Empty; }

        private void NotifySuccess(string text)
        {
            SetStatus(text);
            AppLogger.LogInfo(_projectRoot, text);
            ToastNotification.Show(this, text, false);
        }

        private void NotifyError(string text)
        {
            SetStatus(text);
            ToastNotification.Show(this, text, true);
        }

        private void SetBusy(bool busy, string text)
        {
            if (_busyPanel == null)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(text))
            {
                _busyLabel.Text = text;
            }
            _busyPanel.Visible = busy;
            _busyPanel.BringToFront();

            _loadButton.Enabled = !busy;
            _exportTemplateButton.Enabled = !busy && AppState.IsLoaded;
            _savePresetButton.Enabled = !busy && AppState.IsLoaded;
            _saveOrderButton.Enabled = !busy && AppState.IsLoaded;
            _settingsButton.Enabled = !busy;

            Update();
        }

        private string[] ApplySavedOrder(string[] columns)
        {
            var source = new List<string>(columns ?? new string[0]);
            var saved = LoadSavedOrder();
            if (saved.Count == 0) return source.ToArray();
            var result = new List<string>(source.Count);
            var inSource = new HashSet<string>(source, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < saved.Count; i++) if (inSource.Contains(saved[i]) && !result.Contains(saved[i], StringComparer.OrdinalIgnoreCase)) result.Add(saved[i]);
            for (int i = 0; i < source.Count; i++) if (!result.Contains(source[i], StringComparer.OrdinalIgnoreCase)) result.Add(source[i]);
            return result.ToArray();
        }

        private List<string> BuildCurrentOrder()
        {
            RebuildAllChannelsFromVisibleList();
            var order = new List<string>();
            for (int i = 0; i < _allChannels.Count; i++)
            {
                order.Add(_allChannels[i].Code);
            }
            return order;
        }

        private void ApplyOrder(IList<string> order)
        {
            if (order == null || order.Count == 0)
            {
                return;
            }
            var map = new Dictionary<string, ChannelItem>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _allChannels.Count; i++)
            {
                ChannelItem c = _allChannels[i];
                map[c.Code] = c;
            }

            var reordered = new List<ChannelItem>(_allChannels.Count);
            for (int i = 0; i < order.Count; i++)
            {
                ChannelItem c;
                if (map.TryGetValue(order[i], out c))
                {
                    reordered.Add(c);
                    map.Remove(order[i]);
                }
            }
            foreach (ChannelItem rest in _allChannels)
            {
                if (map.ContainsKey(rest.Code))
                {
                    reordered.Add(rest);
                    map.Remove(rest.Code);
                }
            }
            _allChannels.Clear();
            _allChannels.AddRange(reordered);
            RebuildChannelList();
        }

        private List<string> LoadSavedOrder()
        {
            try
            {
                if (!File.Exists(_orderFilePath)) return new List<string>();
                var payload = JsonHelper.LoadFromFile(_orderFilePath, new OrderPayload());
                return payload != null && payload.order != null ? payload.order : new List<string>();
            }
            catch { return new List<string>(); }
        }

        private void LoadRecentFolders()
        {
            _recentFoldersBox.Items.Clear();
            var payload = JsonHelper.LoadFromFile(_recentFoldersFilePath, new RecentFoldersPayload());
            if (payload == null || payload.folders == null) return;
            for (int i = 0; i < payload.folders.Count; i++) if (!string.IsNullOrWhiteSpace(payload.folders[i])) _recentFoldersBox.Items.Add(payload.folders[i]);
            if (_recentFoldersBox.Items.Count > 0) _recentFoldersBox.SelectedIndex = 0;
        }

        private void AddRecentFolder(string folder)
        {
            string path = (folder ?? string.Empty).Trim();
            if (path.Length == 0) return;
            var folders = new List<string>(); folders.Add(path);
            for (int i = 0; i < _recentFoldersBox.Items.Count; i++)
            {
                string existing = _recentFoldersBox.Items[i].ToString();
                if (!string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)) folders.Add(existing);
            }
            while (folders.Count > 12) folders.RemoveAt(folders.Count - 1);
            _recentFoldersBox.Items.Clear();
            for (int i = 0; i < folders.Count; i++) _recentFoldersBox.Items.Add(folders[i]);
            if (_recentFoldersBox.Items.Count > 0) _recentFoldersBox.SelectedIndex = 0;
            JsonHelper.SaveToFile(_recentFoldersFilePath, new RecentFoldersPayload { folders = folders });
        }

        private void OnFormClosingSaveOrder(object sender, FormClosingEventArgs e)
        {
            try
            {
                var order = new List<string>();
                RebuildAllChannelsFromVisibleList();
                for (int i = 0; i < _allChannels.Count; i++)
                {
                    order.Add(_allChannels[i].Code);
                }
                JsonHelper.SaveToFile(_orderFilePath, new OrderPayload { order = order });
            }
            catch { }
        }

        private void OnFormClosingSaveUiState(object sender, FormClosingEventArgs e)
        {
            try
            {
                SyncCheckedFromVisibleList();
                var state = new UiStatePayload();
                state.folder = _folderBox.Text;
                state.auto_step = _autoStepCheck.Checked;
                state.target_points = _targetPointsBox.SelectedItem == null ? "5000" : _targetPointsBox.SelectedItem.ToString();
                state.manual_step = (int)_manualStepUpDown.Value;
                state.sort_mode = GetSelectedSortKey();
                state.selected_only = _selectedOnlyCheck.Checked;
                state.channel_filter = _channelFilterBox.Text;
                state.include_extra = _includeExtraCheck.Checked;
                state.refrigerant = _refrigerantBox.SelectedItem == null ? "R290" : _refrigerantBox.SelectedItem.ToString();
                state.splitter_distance = _splitMain.SplitterDistance;
                state.checked_channels = _checkedCodes.ToList();
                JsonHelper.SaveToFile(_uiStateFilePath, state);
            }
            catch { }
        }

        private void LoadUiState()
        {
            try
            {
                UiStatePayload state = JsonHelper.LoadFromFile(_uiStateFilePath, new UiStatePayload());
                if (state == null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(state.folder))
                {
                    _folderBox.Text = state.folder;
                }
                if (!string.IsNullOrWhiteSpace(state.target_points))
                {
                    SelectComboItem(_targetPointsBox, state.target_points);
                }
                if (!string.IsNullOrWhiteSpace(state.sort_mode))
                {
                    SelectSortModeByKey(state.sort_mode);
                }
                if (!string.IsNullOrWhiteSpace(state.refrigerant))
                {
                    SelectComboItem(_refrigerantBox, state.refrigerant);
                }

                _autoStepCheck.Checked = state.auto_step.GetValueOrDefault(true);
                if (state.manual_step.HasValue)
                {
                    int m = Math.Max((int)_manualStepUpDown.Minimum, Math.Min((int)_manualStepUpDown.Maximum, state.manual_step.Value));
                    _manualStepUpDown.Value = m;
                }
                _selectedOnlyCheck.Checked = state.selected_only.GetValueOrDefault(false);
                _channelFilterBox.Text = state.channel_filter ?? string.Empty;
                _includeExtraCheck.Checked = state.include_extra.GetValueOrDefault(true);
                if (state.splitter_distance.HasValue && state.splitter_distance.Value > 220)
                {
                    _splitMain.SplitterDistance = state.splitter_distance.Value;
                }

                _pendingCheckedCodes.Clear();
                if (state.checked_channels != null)
                {
                    foreach (string code in state.checked_channels)
                    {
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            _pendingCheckedCodes.Add(code);
                        }
                    }
                }
            }
            catch { }
        }

        private static string ResolveProjectRoot()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo dir = new DirectoryInfo(baseDir);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir.FullName, "template.xlsx"))) return dir.FullName;
                dir = dir.Parent;
            }
            return baseDir;
        }

        private static int PrefixPriority(string code)
        {
            string c = code ?? string.Empty;
            if (c.StartsWith("A-", StringComparison.OrdinalIgnoreCase)) return 0;
            if (c.StartsWith("C-", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        private sealed class ChannelItem
        {
            public string Code { get; private set; }
            public string Label { get; private set; }
            public string Unit { get; private set; }
            public ChannelItem(string code, string label, string unit) { Code = code; Label = label; Unit = unit ?? string.Empty; }
            public override string ToString() { return Label; }
        }

        private sealed class PresetItem
        {
            public string Key { get; private set; }
            public string Name { get; private set; }
            private readonly string _label;
            public PresetItem(string key, string name, int count) { Key = key; Name = name; _label = string.Format("{0} ({1})", name, count); }
            public override string ToString() { return _label; }
        }

        private sealed class OrderItem
        {
            public string Key { get; private set; }
            public string Name { get; private set; }
            private readonly string _label;
            public OrderItem(string key, string name, int count) { Key = key; Name = name; _label = string.Format("{0} ({1})", name, count); }
            public override string ToString() { return _label; }
        }

        private sealed class OrderPayload { public List<string> order { get; set; } }
        private sealed class RecentFoldersPayload { public List<string> folders { get; set; } }
        private sealed class UiStatePayload
        {
            public string folder { get; set; }
            public bool? auto_step { get; set; }
            public string target_points { get; set; }
            public int? manual_step { get; set; }
            public string sort_mode { get; set; }
            public bool? selected_only { get; set; }
            public string channel_filter { get; set; }
            public bool? include_extra { get; set; }
            public string refrigerant { get; set; }
            public int? splitter_distance { get; set; }
            public List<string> checked_channels { get; set; }
        }

        private sealed class SortModeItem
        {
            public string Key { get; private set; }
            public string LocKey { get; private set; }
            public SortModeItem(string key, string locKey) { Key = key; LocKey = locKey; }
            public override string ToString() { return Loc.Get(LocKey); }
        }

        private string GetSelectedSortKey()
        {
            var item = _sortModeBox.SelectedItem as SortModeItem;
            return item != null ? item.Key : "User";
        }

        private sealed class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                string[] a = NaturalSplitRegex.Split(x ?? string.Empty);
                string[] b = NaturalSplitRegex.Split(y ?? string.Empty);
                int n = Math.Max(a.Length, b.Length);
                for (int i = 0; i < n; i++)
                {
                    if (i >= a.Length) return -1;
                    if (i >= b.Length) return 1;

                    int ai, bi;
                    bool an = int.TryParse(a[i], out ai);
                    bool bn = int.TryParse(b[i], out bi);
                    if (an && bn)
                    {
                        int cmpN = ai.CompareTo(bi);
                        if (cmpN != 0) return cmpN;
                    }
                    else
                    {
                        int cmp = string.Compare(a[i], b[i], StringComparison.OrdinalIgnoreCase);
                        if (cmp != 0) return cmp;
                    }
                }
                return 0;
            }
        }
    }
}
