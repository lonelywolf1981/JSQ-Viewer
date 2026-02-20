using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Threading.Tasks;
using JSQViewer.Core;
using JSQViewer.Export;
using JSQViewer.Settings;

namespace JSQViewer.UI
{
    public sealed class MainForm : Form
    {
        private readonly TextBox _folderBox;
        private readonly Button _browseButton;
        private readonly Button _addDataButton;
        private readonly Button _refreshButton;
        private readonly Button _closeAllButton;
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
        private readonly CheckBox _compareOverlayCheck;
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
        private readonly ContextMenuStrip _chartContextMenu;
        private readonly ToolStripMenuItem _crosshairMenuItem;
        private bool _crosshairEnabled = true;
        private int? _mainCrosshairPixelX;
        private readonly RangeTrackBar _rangeTrackBar;
        private readonly FlowLayoutPanel _rangePanel;
        private readonly Label _rangeLabel;
        private readonly Button _resetRangeButton;
        private double _rangeStartOa = double.NaN;
        private double _rangeEndOa = double.NaN;
        private readonly List<RangeTrackBar> _detachedRangeBars = new List<RangeTrackBar>();
        private bool _syncingRange;
        private static readonly DateTime OverlayBaseLocalDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local);

        private int _dragIndex = -1;
        private Point _dragStartPoint = Point.Empty;
        private bool _dragInitiated;
        private readonly List<ChannelItem> _allChannels = new List<ChannelItem>();
        private readonly HashSet<string> _checkedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _lastSelectedCodes = new List<string>();
        private readonly HashSet<string> _pendingCheckedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Form> _sourceChannelForms = new List<Form>();
        private readonly Dictionary<string, CheckedListBox> _sourceChannelLists = new Dictionary<string, CheckedListBox>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SourceWindowState> _sourceWindows = new Dictionary<string, SourceWindowState>(StringComparer.OrdinalIgnoreCase);
        private Form _chartHostForm;
        private bool _syncingSourceChannelSelection;
        private bool _closingSourceChannelWindows;
        private int _fixedControlPanelHeight = 430;
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

            Font = new Font("Microsoft Sans Serif", 10f);
            Text = Loc.Get("AppTitle");
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                Icon = Icon.ExtractAssociatedIcon(exePath);
            }
            catch { }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Width = wa.Width;
            Height = _fixedControlPanelHeight;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(wa.Left, wa.Top);
            MaximizeBox = true;
            MinimumSize = new Size(980, 260);
            KeyPreview = true;

            _splitMain = new SplitContainer();
            _splitMain.Dock = DockStyle.Fill;
            _splitMain.Orientation = Orientation.Vertical;
            _splitMain.SplitterDistance = 520;
            Controls.Add(_splitMain);

            var left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.ColumnCount = 1;
            left.RowCount = 14;
            for (int i = 0; i < 14; i++) left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles[7] = new RowStyle(SizeType.Percent, 100);
            _splitMain.Panel1.Controls.Add(left);

            var smallFont = new Font("Microsoft Sans Serif", 8.25f);

            var folderRow = NewRow(); left.Controls.Add(folderRow, 0, 0);
            _folderBox = new TextBox(); _folderBox.Width = 600; folderRow.Controls.Add(_folderBox);

            var folderButtonsRow = NewRow(); left.Controls.Add(folderButtonsRow, 0, 1);
            _browseButton = new Button(); _browseButton.Text = Loc.Get("Browse"); _browseButton.AutoSize = true; _browseButton.Click += BrowseButtonOnClick; folderButtonsRow.Controls.Add(_browseButton);
            _addDataButton = new Button(); _addDataButton.Text = Loc.Get("AddData"); _addDataButton.AutoSize = true; _addDataButton.Click += AddDataButtonOnClick; folderButtonsRow.Controls.Add(_addDataButton);
            _refreshButton = new Button(); _refreshButton.Text = Loc.Get("Refresh"); _refreshButton.AutoSize = true; _refreshButton.Click += RefreshButtonOnClick; folderButtonsRow.Controls.Add(_refreshButton);
            _closeAllButton = new Button(); _closeAllButton.Text = Loc.Get("CloseAll"); _closeAllButton.AutoSize = true; _closeAllButton.Click += CloseAllButtonOnClick; folderButtonsRow.Controls.Add(_closeAllButton);
            _langButton = new Button(); _langButton.Text = Loc.Get("Language"); _langButton.Width = 40; _langButton.Click += LangButtonOnClick; folderButtonsRow.Controls.Add(_langButton);

            var recentRow = NewRow(); left.Controls.Add(recentRow, 0, 2);
            _recentLabel = new Label(); _recentLabel.Text = Loc.Get("Recent"); _recentLabel.AutoSize = true; _recentLabel.Padding = new Padding(0, 6, 4, 0); recentRow.Controls.Add(_recentLabel);
            _recentFoldersBox = new ComboBox(); _recentFoldersBox.Width = 600; _recentFoldersBox.DropDownStyle = ComboBoxStyle.DropDownList; _recentFoldersBox.SelectedIndexChanged += RecentFoldersBoxOnSelectedIndexChanged; recentRow.Controls.Add(_recentFoldersBox);

            _summaryLabel = new Label(); _summaryLabel.Font = smallFont; _summaryLabel.AutoSize = true; _summaryLabel.Padding = new Padding(4, 6, 4, 4); _summaryLabel.Text = Loc.Get("NoTestLoaded"); left.Controls.Add(_summaryLabel, 0, 3);
            _selectionInfoLabel = new Label(); _selectionInfoLabel.Font = smallFont; _selectionInfoLabel.AutoSize = true; _selectionInfoLabel.Padding = new Padding(4, 2, 4, 6); _selectionInfoLabel.Text = Loc.Get("Selected"); left.Controls.Add(_selectionInfoLabel, 0, 4);

            var stepRow = NewRow(); left.Controls.Add(stepRow, 0, 5);
            _autoStepCheck = new CheckBox(); _autoStepCheck.Text = Loc.Get("AutoStep"); _autoStepCheck.Checked = true; _autoStepCheck.AutoSize = true; _autoStepCheck.CheckedChanged += StepControlsOnChanged; stepRow.Controls.Add(_autoStepCheck);
            _targetLabel = new Label(); _targetLabel.Text = Loc.Get("Target"); _targetLabel.AutoSize = true; _targetLabel.Padding = new Padding(8, 5, 2, 0); stepRow.Controls.Add(_targetLabel);
            _targetPointsBox = new ComboBox(); _targetPointsBox.DropDownStyle = ComboBoxStyle.DropDownList; _targetPointsBox.Width = 90; _targetPointsBox.Items.Add("1000"); _targetPointsBox.Items.Add("2000"); _targetPointsBox.Items.Add("5000"); _targetPointsBox.Items.Add("10000"); _targetPointsBox.Items.Add("20000"); _targetPointsBox.SelectedIndex = 2; _targetPointsBox.SelectedIndexChanged += StepControlsOnChanged; stepRow.Controls.Add(_targetPointsBox);
            _manualLabel = new Label(); _manualLabel.Text = Loc.Get("Manual"); _manualLabel.AutoSize = true; _manualLabel.Padding = new Padding(8, 5, 2, 0); stepRow.Controls.Add(_manualLabel);
            _manualStepUpDown = new NumericUpDown(); _manualStepUpDown.Width = 70; _manualStepUpDown.Minimum = 1; _manualStepUpDown.Maximum = 100000; _manualStepUpDown.Value = 1; _manualStepUpDown.Enabled = false; _manualStepUpDown.ValueChanged += StepControlsOnChanged; stepRow.Controls.Add(_manualStepUpDown);
            _compareOverlayCheck = new CheckBox(); _compareOverlayCheck.Text = Loc.Get("CompareOverlayMode"); _compareOverlayCheck.AutoSize = true; _compareOverlayCheck.Padding = new Padding(12, 2, 0, 0); _compareOverlayCheck.Enabled = false; _compareOverlayCheck.CheckedChanged += CompareOverlayCheckOnCheckedChanged; stepRow.Controls.Add(_compareOverlayCheck);

            var channelsHeaderRow = NewRow(); left.Controls.Add(channelsHeaderRow, 0, 6);
            _channelsHeader = new Label(); _channelsHeader.Text = Loc.Get("Channels"); _channelsHeader.AutoSize = true; _channelsHeader.Padding = new Padding(4, 6, 2, 0); channelsHeaderRow.Controls.Add(_channelsHeader);
            _channelFilterBox = new TextBox(); _channelFilterBox.Width = 130; _channelFilterBox.TextChanged += ChannelViewOptionsChanged; channelsHeaderRow.Controls.Add(_channelFilterBox);
            _sortModeBox = new ComboBox(); _sortModeBox.Width = 160; _sortModeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            PopulateSortModeBox(_sortModeBox);
            _sortModeBox.SelectedIndex = 5; _sortModeBox.SelectedIndexChanged += ChannelViewOptionsChanged; channelsHeaderRow.Controls.Add(_sortModeBox);
            _selectedOnlyCheck = new CheckBox(); _selectedOnlyCheck.Text = Loc.Get("SelectedOnly"); _selectedOnlyCheck.AutoSize = true; _selectedOnlyCheck.CheckedChanged += ChannelViewOptionsChanged; channelsHeaderRow.Controls.Add(_selectedOnlyCheck);
            _selectAllChannelsButton = new Button(); _selectAllChannelsButton.Text = Loc.Get("SelectAll"); _selectAllChannelsButton.AutoSize = true; _selectAllChannelsButton.Click += SelectAllChannelsButtonOnClick; channelsHeaderRow.Controls.Add(_selectAllChannelsButton);
            _clearChannelsButton = new Button(); _clearChannelsButton.Text = Loc.Get("Clear"); _clearChannelsButton.AutoSize = true; _clearChannelsButton.Click += ClearChannelsButtonOnClick; channelsHeaderRow.Controls.Add(_clearChannelsButton);
            _channelsList = new CheckedListBox(); _channelsList.Dock = DockStyle.Fill; _channelsList.CheckOnClick = true; _channelsList.AllowDrop = true; _channelsList.ItemCheck += ChannelsListOnItemCheck; _channelsList.MouseDown += ChannelsListOnMouseDown; _channelsList.MouseMove += ChannelsListOnMouseMove; _channelsList.DragOver += ChannelsListOnDragOver; _channelsList.DragDrop += ChannelsListOnDragDrop; left.Controls.Add(_channelsList, 0, 7);
            _channelsList.IntegralHeight = false;

            var templateOptionsRow = NewRow(); left.Controls.Add(templateOptionsRow, 0, 8);
            _includeExtraCheck = new CheckBox(); _includeExtraCheck.Text = Loc.Get("IncludeExtra"); _includeExtraCheck.Checked = true; _includeExtraCheck.AutoSize = true; templateOptionsRow.Controls.Add(_includeExtraCheck);
            _refrigLabel = new Label(); _refrigLabel.Text = Loc.Get("Refrigerant"); _refrigLabel.AutoSize = true; _refrigLabel.Padding = new Padding(8, 5, 2, 0); templateOptionsRow.Controls.Add(_refrigLabel);
            _refrigerantBox = new ComboBox(); _refrigerantBox.DropDownStyle = ComboBoxStyle.DropDownList; _refrigerantBox.Width = 90; _refrigerantBox.Items.Add("R290"); _refrigerantBox.Items.Add("R600a"); _refrigerantBox.SelectedIndex = 0; templateOptionsRow.Controls.Add(_refrigerantBox);

            var exportButtonsRow = NewRow(); left.Controls.Add(exportButtonsRow, 0, 9);
            _exportTemplateButton = new Button(); _exportTemplateButton.Text = Loc.Get("ExportTemplate"); _exportTemplateButton.AutoSize = true; _exportTemplateButton.Enabled = false; _exportTemplateButton.Click += ExportTemplateButtonOnClick; exportButtonsRow.Controls.Add(_exportTemplateButton);
            _settingsButton = new Button(); _settingsButton.Text = Loc.Get("Styles"); _settingsButton.AutoSize = true; _settingsButton.Click += SettingsButtonOnClick; exportButtonsRow.Controls.Add(_settingsButton);

            var presetSaveRow = NewRow(); left.Controls.Add(presetSaveRow, 0, 10);
            _presetNameBox = new TextBox(); _presetNameBox.Width = 200; _presetNameBox.Text = Loc.Get("PresetName"); presetSaveRow.Controls.Add(_presetNameBox);
            _savePresetButton = new Button(); _savePresetButton.Text = Loc.Get("SavePreset"); _savePresetButton.AutoSize = true; _savePresetButton.Enabled = false; _savePresetButton.Click += SavePresetButtonOnClick; presetSaveRow.Controls.Add(_savePresetButton);

            var presetLoadRow = NewRow(); left.Controls.Add(presetLoadRow, 0, 11);
            _presetsBox = new ComboBox(); _presetsBox.Width = 240; _presetsBox.DropDownStyle = ComboBoxStyle.DropDownList; presetLoadRow.Controls.Add(_presetsBox);
            _loadPresetButton = new Button(); _loadPresetButton.Text = Loc.Get("Load"); _loadPresetButton.AutoSize = true; _loadPresetButton.Enabled = false; _loadPresetButton.Click += LoadPresetButtonOnClick; presetLoadRow.Controls.Add(_loadPresetButton);
            _deletePresetButton = new Button(); _deletePresetButton.Text = Loc.Get("Delete"); _deletePresetButton.AutoSize = true; _deletePresetButton.Enabled = false; _deletePresetButton.Click += DeletePresetButtonOnClick; presetLoadRow.Controls.Add(_deletePresetButton);

            var orderRow = NewRow(); left.Controls.Add(orderRow, 0, 12);
            _orderNameBox = new TextBox(); _orderNameBox.Width = 150; _orderNameBox.Text = Loc.Get("OrderName"); orderRow.Controls.Add(_orderNameBox);
            _saveOrderButton = new Button(); _saveOrderButton.Text = Loc.Get("SaveOrder"); _saveOrderButton.AutoSize = true; _saveOrderButton.Enabled = false; _saveOrderButton.Click += SaveOrderButtonOnClick; orderRow.Controls.Add(_saveOrderButton);
            _ordersBox = new ComboBox(); _ordersBox.Width = 200; _ordersBox.DropDownStyle = ComboBoxStyle.DropDownList; orderRow.Controls.Add(_ordersBox);
            _loadOrderButton = new Button(); _loadOrderButton.Text = Loc.Get("Load"); _loadOrderButton.AutoSize = true; _loadOrderButton.Enabled = false; _loadOrderButton.Click += LoadOrderButtonOnClick; orderRow.Controls.Add(_loadOrderButton);
            _deleteOrderButton = new Button(); _deleteOrderButton.Text = Loc.Get("Delete"); _deleteOrderButton.AutoSize = true; _deleteOrderButton.Enabled = false; _deleteOrderButton.Click += DeleteOrderButtonOnClick; orderRow.Controls.Add(_deleteOrderButton);

            _statusLabel = new Label(); _statusLabel.Font = smallFont; _statusLabel.AutoSize = true; _statusLabel.Padding = new Padding(4, 6, 4, 8); _statusLabel.Text = Loc.Get("Ready"); left.Controls.Add(_statusLabel, 0, 13);

            _chart = BuildChart();
            _chart.MouseMove += ChartOnMouseMove;
            _chart.MouseLeave += ChartOnMouseLeave;
            _chart.MouseWheel += ChartOnMouseWheel;
            _chart.MouseDoubleClick += ChartOnMouseDoubleClick;
            _chart.Paint += ChartOnPaint;

            _chartContextMenu = new ContextMenuStrip();
            _crosshairMenuItem = new ToolStripMenuItem(Loc.Get("ChartCrosshair")) { Checked = _crosshairEnabled, CheckOnClick = true };
            _crosshairMenuItem.Click += CrosshairMenuItemOnClick;
            _chartContextMenu.Items.Add(_crosshairMenuItem);
            _chartContextMenu.Items.Add(new ToolStripSeparator());
            var resetZoomItem = new ToolStripMenuItem(Loc.Get("ChartResetZoom"));
            resetZoomItem.Click += ResetZoomMenuItemOnClick;
            _chartContextMenu.Items.Add(resetZoomItem);
            var showAllItem = new ToolStripMenuItem(Loc.Get("ChartShowAll"));
            showAllItem.Click += ShowAllMenuItemOnClick;
            _chartContextMenu.Items.Add(showAllItem);
            var resetRangeItem = new ToolStripMenuItem(Loc.Get("ResetRange"));
            resetRangeItem.Click += delegate { ResetRangeButtonOnClick(null, EventArgs.Empty); };
            _chartContextMenu.Items.Add(resetRangeItem);
            _chartContextMenu.Items.Add(new ToolStripSeparator());
            var detachItem = new ToolStripMenuItem(Loc.Get("ChartDetach"));
            detachItem.Click += DetachChartMenuItemOnClick;
            _chartContextMenu.Items.Add(detachItem);
            _chartContextMenu.Items.Add(new ToolStripSeparator());
            var saveImageItem = new ToolStripMenuItem(Loc.Get("ChartSaveImage")) { ShortcutKeys = Keys.Control | Keys.S };
            saveImageItem.Click += SaveImageMenuItemOnClick;
            _chartContextMenu.Items.Add(saveImageItem);
            var copyImageItem = new ToolStripMenuItem(Loc.Get("ChartCopyImage")) { ShortcutKeys = Keys.Control | Keys.C };
            copyImageItem.Click += CopyImageMenuItemOnClick;
            _chartContextMenu.Items.Add(copyImageItem);
            _chart.ContextMenuStrip = _chartContextMenu;

            _rangePanel = new FlowLayoutPanel();
            _rangePanel.Dock = DockStyle.Bottom;
            _rangePanel.AutoSize = true;
            _rangePanel.WrapContents = false;
            _rangePanel.Padding = new Padding(4, 2, 4, 2);
            _rangeLabel = new Label();
            _rangeLabel.AutoSize = true;
            _rangeLabel.Padding = new Padding(0, 4, 4, 0);
            _rangeLabel.Text = Loc.Get("RangeAll");
            _rangePanel.Controls.Add(_rangeLabel);
            _resetRangeButton = new Button();
            _resetRangeButton.Text = Loc.Get("ResetRange");
            _resetRangeButton.AutoSize = true;
            _resetRangeButton.Visible = false;
            _resetRangeButton.Click += ResetRangeButtonOnClick;
            _rangePanel.Controls.Add(_resetRangeButton);

            _rangeTrackBar = new RangeTrackBar();
            _rangeTrackBar.Dock = DockStyle.Bottom;
            _rangeTrackBar.Height = 48;
            _rangeTrackBar.RangeChanged += RangeTrackBarOnRangeChanged;
            _splitMain.Panel2.Controls.Add(_chart);
            _splitMain.Panel2.Controls.Add(_rangeTrackBar);
            _splitMain.Panel2.Controls.Add(_rangePanel);
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
            _toolTip.AutoPopDelay = 600000;
            _toolTip.InitialDelay = 400;
            _toolTip.ReshowDelay = 200;
            ApplyTooltips();

            LoadRecentFolders();
            ReloadPresets();
            ReloadOrders();
            FormClosing += OnFormClosingSaveOrder;
            FormClosing += OnFormClosingSaveUiState;
            FormClosing += delegate { CloseSourceChannelWindows(); if (_chartHostForm != null && !_chartHostForm.IsDisposed) _chartHostForm.Close(); };
            KeyDown += MainFormOnKeyDown;
            Loc.LanguageChanged += ApplyLocalization;
            LoadUiState();
            StepControlsOnChanged(this, EventArgs.Empty);
            ConfigureMainAsControlPanel();
            EnsureChartHostForm();
            _chartHostForm.Hide();
            Resize += MainFormOnResizeFixHeight;
        }

        private void LangButtonOnClick(object sender, EventArgs e)
        {
            Loc.Current = Loc.Current == Lang.Ru ? Lang.En : Lang.Ru;
        }

        private void ApplyLocalization()
        {
            Text = Loc.Get("AppTitle");
            _browseButton.Text = Loc.Get("Browse");
            _addDataButton.Text = Loc.Get("AddData");
            _refreshButton.Text = Loc.Get("Refresh");
            _closeAllButton.Text = Loc.Get("CloseAll");
            _langButton.Text = Loc.Get("Language");
            _recentLabel.Text = Loc.Get("Recent");
            _autoStepCheck.Text = Loc.Get("AutoStep");
            _targetLabel.Text = Loc.Get("Target");
            _manualLabel.Text = Loc.Get("Manual");
            _compareOverlayCheck.Text = Loc.Get("CompareOverlayMode");
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
            // Context menu: [0]=crosshair [1]=sep [2]=resetZoom [3]=showAll [4]=resetRange [5]=sep [6]=detach [7]=sep [8]=saveImage [9]=copyImage
            _crosshairMenuItem.Text = Loc.Get("ChartCrosshair");
            ((ToolStripMenuItem)_chartContextMenu.Items[2]).Text = Loc.Get("ChartResetZoom");
            ((ToolStripMenuItem)_chartContextMenu.Items[3]).Text = Loc.Get("ChartShowAll");
            ((ToolStripMenuItem)_chartContextMenu.Items[4]).Text = Loc.Get("ResetRange");
            ((ToolStripMenuItem)_chartContextMenu.Items[6]).Text = Loc.Get("ChartDetach");
            ((ToolStripMenuItem)_chartContextMenu.Items[8]).Text = Loc.Get("ChartSaveImage");
            ((ToolStripMenuItem)_chartContextMenu.Items[9]).Text = Loc.Get("ChartCopyImage");
            _rangeLabel.Text = BuildRangeLabelText(_rangeStartOa, _rangeEndOa);
            _resetRangeButton.Text = Loc.Get("ResetRange");
            if (_chartHostForm != null && !_chartHostForm.IsDisposed)
            {
                _chartHostForm.Text = string.Format(Loc.Get("ChartWindowTitle"), AppState.Folder ?? Loc.Get("AppTitle"));
            }
            foreach (var kv in _sourceWindows)
            {
                SourceWindowState sw = kv.Value;
                if (sw == null) continue;
                if (sw.Form != null && !sw.Form.IsDisposed)
                {
                    sw.Form.Text = string.Format(Loc.Get("ChannelsForSource"), Path.GetFileName(sw.SourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                }
                if (sw.SelectedOnlyCheck != null) sw.SelectedOnlyCheck.Text = Loc.Get("SelectedOnly");
                if (sw.SelectAllButton != null) sw.SelectAllButton.Text = Loc.Get("SelectAll");
                if (sw.ClearButton != null) sw.ClearButton.Text = Loc.Get("Clear");
                if (sw.SavePresetButton != null) sw.SavePresetButton.Text = Loc.Get("SavePreset");
                if (sw.LoadPresetButton != null) sw.LoadPresetButton.Text = Loc.Get("Load");
                if (sw.DeletePresetButton != null) sw.DeletePresetButton.Text = Loc.Get("Delete");
                if (sw.SaveOrderButton != null) sw.SaveOrderButton.Text = Loc.Get("SaveOrder");
                if (sw.LoadOrderButton != null) sw.LoadOrderButton.Text = Loc.Get("Load");
                if (sw.DeleteOrderButton != null) sw.DeleteOrderButton.Text = Loc.Get("Delete");
                if (sw.SortModeBox != null)
                {
                    typeof(ComboBox).GetMethod("RefreshItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(sw.SortModeBox, null);
                }
            }

            // Force sort combo to re-read ToString() of items
            int selIdx = _sortModeBox.SelectedIndex;
            typeof(ComboBox).GetMethod("RefreshItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(_sortModeBox, null);
            if (selIdx >= 0 && selIdx < _sortModeBox.Items.Count) _sortModeBox.SelectedIndex = selIdx;
            ApplyTooltips();
            UpdateSelectionInfo();
            AdjustControlPanelWidth();
        }

        private void ApplyTooltips()
        {
            _toolTip.SetToolTip(_folderBox, Loc.Get("TipFolder"));
            _toolTip.SetToolTip(_browseButton, Loc.Get("TipBrowse"));
            _toolTip.SetToolTip(_addDataButton, Loc.Get("TipAddData"));
            _toolTip.SetToolTip(_refreshButton, Loc.Get("TipRefresh"));
            _toolTip.SetToolTip(_closeAllButton, Loc.Get("TipCloseAll"));
            _toolTip.SetToolTip(_langButton, Loc.Get("TipLang"));
            _toolTip.SetToolTip(_recentFoldersBox, Loc.Get("TipRecent"));
            _toolTip.SetToolTip(_autoStepCheck, Loc.Get("TipAutoStep"));
            _toolTip.SetToolTip(_targetPointsBox, Loc.Get("TipTarget"));
            _toolTip.SetToolTip(_manualStepUpDown, Loc.Get("TipManualStep"));
            _toolTip.SetToolTip(_compareOverlayCheck, Loc.Get("TipCompareOverlayMode"));
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

        private void ConfigureMainAsControlPanel()
        {
            _splitMain.Panel2Collapsed = true;
            _channelsHeader.Visible = false;
            _channelFilterBox.Visible = false;
            _sortModeBox.Visible = false;
            _selectedOnlyCheck.Visible = false;
            _selectAllChannelsButton.Visible = false;
            _clearChannelsButton.Visible = false;
            _channelsList.Visible = false;
            _presetNameBox.Visible = false;
            _savePresetButton.Visible = false;
            _presetsBox.Visible = false;
            _loadPresetButton.Visible = false;
            _deletePresetButton.Visible = false;
            _orderNameBox.Visible = false;
            _saveOrderButton.Visible = false;
            _ordersBox.Visible = false;
            _loadOrderButton.Visible = false;
            _deleteOrderButton.Visible = false;
            _statusLabel.Visible = false;
            _splitMain.SplitterDistance = ClientSize.Width - 8;
            TableLayoutPanel left = _channelsList.Parent as TableLayoutPanel;
            if (left != null && left.RowStyles.Count > 7)
            {
                left.RowStyles[7] = new RowStyle(SizeType.AutoSize);
            }
            AdjustControlPanelWidth();
        }

        private void MainFormOnResizeFixHeight(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized) return;
            bool changed = false;
            if (Height != _fixedControlPanelHeight)
            {
                Height = _fixedControlPanelHeight;
                changed = true;
            }
            int minWidth = Math.Max(980, ResolveControlPanelPreferredWidth());
            if (Width < minWidth)
            {
                Width = minWidth;
                changed = true;
            }
            if (changed)
            {
                BeginInvoke((Action)AdjustControlPanelWidth);
            }
        }

        private int ResolveControlPanelPreferredWidth()
        {
            if (_splitMain == null || _splitMain.Panel1.Controls.Count == 0) return 980;
            Control content = _splitMain.Panel1.Controls[0];
            Size pref = content.GetPreferredSize(new Size(10000, 0));
            int frameWidth = Width - ClientSize.Width;
            return pref.Width + frameWidth + 20;
        }

        private void AdjustControlPanelWidth()
        {
            if (WindowState == FormWindowState.Minimized) return;
            _fixedControlPanelHeight = ResolveControlPanelPreferredHeight();
            if (Height != _fixedControlPanelHeight)
            {
                Height = _fixedControlPanelHeight;
            }
            MinimumSize = new Size(980, _fixedControlPanelHeight);
            int minWidth = Math.Max(980, ResolveControlPanelPreferredWidth());
            if (Width < minWidth)
            {
                Width = minWidth;
            }
        }

        private int ResolveControlPanelPreferredHeight()
        {
            if (_splitMain == null || _splitMain.Panel1.Controls.Count == 0) return 430;
            Control content = _splitMain.Panel1.Controls[0];
            int targetClientHeight = content.GetPreferredSize(new Size(Math.Max(860, ClientSize.Width - 24), 0)).Height + 16;
            int frameHeight = Height - ClientSize.Height;
            int calculated = targetClientHeight + frameHeight;
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            return Math.Max(260, Math.Min(calculated, wa.Height));
        }

        private void EnsureChartHostForm()
        {
            if (_chartHostForm != null && !_chartHostForm.IsDisposed) return;
            _chartHostForm = new Form();
            _chartHostForm.Text = string.Format(Loc.Get("ChartWindowTitle"), Loc.Get("AppTitle"));
            _chartHostForm.Width = 1220;
            _chartHostForm.Height = 760;
            _chartHostForm.StartPosition = FormStartPosition.Manual;
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int hostX = wa.Left + Math.Max(20, (wa.Width - _chartHostForm.Width) / 2);
            int hostY = Bottom + 12;
            int maxY = wa.Bottom - _chartHostForm.Height - 20;
            if (hostY > maxY) hostY = Math.Max(wa.Top + 20, maxY);
            _chartHostForm.Location = new Point(hostX, hostY);
            _chartHostForm.FormBorderStyle = FormBorderStyle.Sizable;
            _chartHostForm.FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    _chartHostForm.Hide();
                }
            };
            try { _chartHostForm.Icon = Icon; } catch { }

            if (_chart.Parent != null) _chart.Parent.Controls.Remove(_chart);
            if (_rangeTrackBar.Parent != null) _rangeTrackBar.Parent.Controls.Remove(_rangeTrackBar);
            if (_rangePanel.Parent != null) _rangePanel.Parent.Controls.Remove(_rangePanel);
            _chartHostForm.Controls.Add(_chart);
            _chartHostForm.Controls.Add(_rangeTrackBar);
            _chartHostForm.Controls.Add(_rangePanel);
        }

        private void ShowChartHost()
        {
            EnsureChartHostForm();
            if (!_chartHostForm.Visible)
            {
                _chartHostForm.Show(this);
            }
            if (_chartHostForm.WindowState == FormWindowState.Minimized)
            {
                _chartHostForm.WindowState = FormWindowState.Normal;
            }
            _chartHostForm.BringToFront();
            _chartHostForm.Activate();
        }

        private void HideChartHost()
        {
            _mainCrosshairPixelX = null;
            _toolTip.Hide(_chart);
            if (_chartHostForm != null && !_chartHostForm.IsDisposed && _chartHostForm.Visible)
            {
                _chartHostForm.Hide();
            }
        }

        private void CloseAllButtonOnClick(object sender, EventArgs e)
        {
            _folderBox.Text = string.Empty;
            _checkedCodes.Clear();
            _lastSelectedCodes.Clear();
            _allChannels.Clear();
            _pendingCheckedCodes.Clear();
            AppState.SetData(string.Empty, null);
            _chart.Series.Clear();
            _summaryLabel.Text = Loc.Get("NoTestLoaded");
            _selectionInfoLabel.Text = Loc.Get("Selected");
            _exportTemplateButton.Enabled = _savePresetButton.Enabled = false;
            _saveOrderButton.Enabled = false;
            _loadPresetButton.Enabled = _deletePresetButton.Enabled = _presetsBox.Items.Count > 0;
            _loadOrderButton.Enabled = _deleteOrderButton.Enabled = _ordersBox.Items.Count > 0;
            _compareOverlayCheck.Checked = false;
            _compareOverlayCheck.Enabled = false;
            CloseSourceChannelWindows();
            HideChartHost();
            NotifySuccess(Loc.Get("ClearedAll"));
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
            area.CursorX.IsUserEnabled = false;
            area.CursorX.IsUserSelectionEnabled = false;
            area.CursorX.LineColor = Color.Black;
            area.CursorX.LineDashStyle = ChartDashStyle.Dash;
            area.CursorX.LineWidth = 2;
            area.CursorY.IsUserEnabled = false;
            area.CursorY.LineColor = Color.Transparent;
            area.CursorY.LineDashStyle = ChartDashStyle.Dash;
            area.CursorY.LineWidth = 0;
            area.AxisX.ScrollBar.Enabled = false;
            area.AxisX.ScaleView.Zoomable = false;
            area.AxisY.ScaleView.Zoomable = false;

            chart.ChartAreas.Add(area);
            var legend = new Legend("legend"); legend.Docking = Docking.Top; legend.Alignment = StringAlignment.Center; chart.Legends.Add(legend);
            return chart;
        }

        // ──── Chart interactivity ────

        private void RangeTrackBarOnRangeChanged(object sender, EventArgs e)
        {
            if (_syncingRange) return;
            var trackBar = sender as RangeTrackBar;
            if (trackBar == null) return;

            double lo = trackBar.LowerValue;
            double hi = trackBar.UpperValue;

            bool isFullRange = Math.Abs(lo - trackBar.Minimum) < 1e-10 && Math.Abs(hi - trackBar.Maximum) < 1e-10;

            if (isFullRange)
            {
                _rangeStartOa = double.NaN;
                _rangeEndOa = double.NaN;
                _rangeLabel.Text = BuildRangeLabelText(_rangeStartOa, _rangeEndOa);
                _resetRangeButton.Visible = false;
                if (_chart.ChartAreas.Count > 0)
                {
                    _chart.ChartAreas[0].AxisX.Minimum = double.NaN;
                    _chart.ChartAreas[0].AxisX.Maximum = double.NaN;
                }
            }
            else
            {
                _rangeStartOa = lo;
                _rangeEndOa = hi;
                _rangeLabel.Text = BuildRangeLabelText(lo, hi);
                _resetRangeButton.Visible = true;
                if (_chart.ChartAreas.Count > 0)
                {
                    _chart.ChartAreas[0].AxisX.Minimum = lo;
                    _chart.ChartAreas[0].AxisX.Maximum = hi;
                }
            }

            // Sync all range bars (main + detached)
            SyncAllRangeBars(trackBar, lo, hi);
        }

        private void SyncAllRangeBars(RangeTrackBar source, double lo, double hi)
        {
            _syncingRange = true;
            try
            {
                // Sync main trackbar if source is a detached one
                if (source != _rangeTrackBar)
                {
                    _rangeTrackBar.LowerValue = lo;
                    _rangeTrackBar.UpperValue = hi;
                }
                // Sync all detached trackbars
                for (int i = _detachedRangeBars.Count - 1; i >= 0; i--)
                {
                    RangeTrackBar bar = _detachedRangeBars[i];
                    if (bar.IsDisposed) { _detachedRangeBars.RemoveAt(i); continue; }
                    if (bar == source) continue;
                    bar.LowerValue = lo;
                    bar.UpperValue = hi;
                }
            }
            finally
            {
                _syncingRange = false;
            }
        }

        private void ChartOnMouseMove(object sender, MouseEventArgs e)
        {
            if (_chart.ChartAreas.Count == 0 || _chart.Series.Count == 0) return;
            var area = _chart.ChartAreas[0];
            try
            {
                double xVal = area.AxisX.PixelPositionToValue(e.X);

                if (_crosshairEnabled)
                {
                    _mainCrosshairPixelX = e.X;
                    _chart.Invalidate();
                        BuildCrosshairTooltip(_chart, _toolTip, xVal, e.Location, IsOverlayCompareModeActive());
                }
            }
            catch { }
        }

        private void ChartOnMouseLeave(object sender, EventArgs e)
        {
            _mainCrosshairPixelX = null;
            _chart.Invalidate();
            _toolTip.Hide(_chart);
        }

        private void ChartOnPaint(object sender, PaintEventArgs e)
        {
            if (!_crosshairEnabled || !_mainCrosshairPixelX.HasValue || _chart.ChartAreas.Count == 0) return;
            ChartArea area = _chart.ChartAreas[0];

            RectangleF areaRect = new RectangleF(
                _chart.ClientSize.Width * area.Position.X / 100f,
                _chart.ClientSize.Height * area.Position.Y / 100f,
                _chart.ClientSize.Width * area.Position.Width / 100f,
                _chart.ClientSize.Height * area.Position.Height / 100f);

            int x = _mainCrosshairPixelX.Value;
            if (x < areaRect.Left || x > areaRect.Right) return;

            using (var pen = new Pen(Color.Black, 1f))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                e.Graphics.DrawLine(pen, x, areaRect.Top, x, areaRect.Bottom);
            }
        }

        private static void BuildCrosshairTooltip(Chart chart, ToolTip toolTip, double xVal, Point mousePoint, bool overlayMode)
        {
            if (chart.Series.Count == 0)
            {
                if ((chart.Tag as string) != string.Empty)
                {
                    chart.Tag = string.Empty;
                    toolTip.Hide(chart);
                }
                return;
            }

            var sb = new System.Text.StringBuilder();
            if (overlayMode)
            {
                sb.Append(FormatOverlayElapsed(xVal));
            }
            else
            {
                DateTime cursorTime = DateTime.FromOADate(xVal);
                sb.AppendFormat("{0:yyyy-MM-dd HH:mm:ss}", cursorTime);
            }

            Series closestSeries = null;
            double closestDist = double.MaxValue;

            foreach (Series s in chart.Series)
            {
                if (s.Points.Count == 0) continue;
                int idx = FindNearestPointIndex(s.Points, xVal);
                if (idx < 0) continue;
                DataPoint dp = s.Points[idx];
                sb.AppendFormat("\n{0}: {1:F2}", s.Name, dp.YValues[0]);

                double dist = Math.Abs(dp.XValue - xVal);
                if (dist < closestDist) { closestDist = dist; closestSeries = s; }
            }

            foreach (Series s in chart.Series)
            {
                s.BorderWidth = s == closestSeries ? 3 : 2;
            }

            string text = sb.ToString();
            string prev = chart.Tag as string;
            if (!string.Equals(prev, text, StringComparison.Ordinal))
            {
                chart.Tag = text;
                // Show tooltip a bit to the right of the cursor and only on content change to avoid flicker.
                toolTip.Show(text, chart, mousePoint.X + 24, mousePoint.Y + 8);
            }
        }

        private static int FindNearestPointIndex(DataPointCollection points, double xValue)
        {
            int lo = 0;
            int hi = points.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                double midX = points[mid].XValue;
                if (midX < xValue) lo = mid + 1;
                else if (midX > xValue) hi = mid - 1;
                else return mid;
            }
            if (lo >= points.Count) return hi;
            if (hi < 0) return lo;
            return Math.Abs(points[lo].XValue - xValue) < Math.Abs(points[hi].XValue - xValue) ? lo : hi;
        }

        private void ChartOnMouseWheel(object sender, MouseEventArgs e)
        {
            if (_chart.ChartAreas.Count == 0) return;
            var area = _chart.ChartAreas[0];
            try
            {
                bool zoomY = Control.ModifierKeys.HasFlag(Keys.Control);
                Axis axis = zoomY ? area.AxisY : area.AxisX;

                double viewMin = axis.ScaleView.ViewMinimum;
                double viewMax = axis.ScaleView.ViewMaximum;
                double range = viewMax - viewMin;
                if (range <= 0) return;

                double mousePos;
                try
                {
                    mousePos = zoomY
                        ? area.AxisY.PixelPositionToValue(e.Y)
                        : area.AxisX.PixelPositionToValue(e.X);
                }
                catch { return; }

                double ratio = (mousePos - viewMin) / range;
                double factor = e.Delta > 0 ? 0.8 : 1.25;
                double newRange = range * factor;

                // Limit zoom range
                double fullMin = axis.Minimum;
                double fullMax = axis.Maximum;
                double fullRange = fullMax - fullMin;
                if (double.IsNaN(fullRange) || fullRange <= 0) return;
                if (newRange > fullRange) { axis.ScaleView.ZoomReset(0); return; }
                if (newRange < fullRange * 0.001) return;

                double newMin = mousePos - newRange * ratio;
                double newMax = newMin + newRange;

                if (newMin < fullMin) { newMin = fullMin; newMax = fullMin + newRange; }
                if (newMax > fullMax) { newMax = fullMax; newMin = fullMax - newRange; }

                axis.ScaleView.Zoom(newMin, newMax);
            }
            catch { }
        }

        private void ChartOnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            ResetChartZoom();
        }

        private void ResetChartZoom()
        {
            if (_chart.ChartAreas.Count == 0) return;
            var area = _chart.ChartAreas[0];
            area.AxisX.ScaleView.ZoomReset(0);
            area.AxisY.ScaleView.ZoomReset(0);
        }

        private void CrosshairMenuItemOnClick(object sender, EventArgs e)
        {
            _crosshairEnabled = _crosshairMenuItem.Checked;
            if (!_crosshairEnabled && _chart.ChartAreas.Count > 0)
            {
                _mainCrosshairPixelX = null;
                _chart.Invalidate();
                _toolTip.Hide(_chart);
            }
        }

        private void ResetZoomMenuItemOnClick(object sender, EventArgs e)
        {
            ResetChartZoom();
        }

        private void ShowAllMenuItemOnClick(object sender, EventArgs e)
        {
            if (_chart.ChartAreas.Count == 0) return;
            var area = _chart.ChartAreas[0];
            area.AxisX.ScaleView.ZoomReset(0);
            area.AxisY.ScaleView.ZoomReset(0);
            area.AxisX.Minimum = double.NaN;
            area.AxisX.Maximum = double.NaN;
            area.AxisY.Minimum = double.NaN;
            area.AxisY.Maximum = double.NaN;
            ResetRangeButtonOnClick(null, EventArgs.Empty);
        }

        private void ResetRangeButtonOnClick(object sender, EventArgs e)
        {
            _rangeStartOa = double.NaN;
            _rangeEndOa = double.NaN;
            _rangeTrackBar.LowerValue = _rangeTrackBar.Minimum;
            _rangeTrackBar.UpperValue = _rangeTrackBar.Maximum;
            _rangeLabel.Text = BuildRangeLabelText(_rangeStartOa, _rangeEndOa);
            _resetRangeButton.Visible = false;
            if (_chart.ChartAreas.Count > 0)
            {
                _chart.ChartAreas[0].AxisX.Minimum = double.NaN;
                _chart.ChartAreas[0].AxisX.Maximum = double.NaN;
            }
        }

        private void SaveImageMenuItemOnClick(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = Loc.Get("ChartImageFilter");
                dialog.FileName = "chart_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ChartImageFormat format = ChartImageFormat.Png;
                    string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg") format = ChartImageFormat.Jpeg;
                    else if (ext == ".bmp") format = ChartImageFormat.Bmp;
                    _chart.SaveImage(dialog.FileName, format);
                    NotifySuccess(Loc.Get("ChartImageSaved"));
                }
                catch (Exception ex) { AppLogger.LogError(_projectRoot, "Save chart image failed.", ex); NotifyError(ex.Message); }
            }
        }

        private void CopyImageMenuItemOnClick(object sender, EventArgs e)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    _chart.SaveImage(ms, ChartImageFormat.Png);
                    ms.Position = 0;
                    using (var bmp = new Bitmap(ms))
                    {
                        Clipboard.SetImage(bmp);
                    }
                }
                NotifySuccess(Loc.Get("ChartImageCopied"));
            }
            catch (Exception ex) { AppLogger.LogError(_projectRoot, "Copy chart image failed.", ex); NotifyError(ex.Message); }
        }

        private void DetachChartMenuItemOnClick(object sender, EventArgs e)
        {
            if (_chart.Series.Count == 0) return;
            bool overlayMode = IsOverlayCompareModeActive();

            var form = new Form();
            form.Text = string.Format(Loc.Get("ChartWindowTitle"), AppState.Folder ?? Loc.Get("AppTitle"));
            form.Width = 1200;
            form.Height = 700;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.KeyPreview = true;
            try { form.Icon = Icon; } catch { }

            var detachedChart = BuildChart();
            if (detachedChart.ChartAreas.Count > 0)
            {
                ChartArea detachedArea = detachedChart.ChartAreas[0];
                detachedArea.AxisX.LabelStyle.Format = overlayMode ? "HH:mm:ss" : "HH:mm\ndd.MM";
                detachedArea.AxisX.Title = overlayMode ? Loc.Get("OverlayXAxisTitle") : string.Empty;
            }
            detachedChart.SuspendLayout();
            try
            {
                foreach (Series src in _chart.Series)
                {
                    var s = new Series(src.Name);
                    s.ChartType = src.ChartType;
                    s.XValueType = src.XValueType;
                    s.BorderWidth = src.BorderWidth;
                    s.Color = src.Color;
                    foreach (DataPoint dp in src.Points)
                    {
                        s.Points.AddXY(dp.XValue, dp.YValues[0]);
                    }
                    detachedChart.Series.Add(s);
                }
            }
            finally
            {
                detachedChart.ResumeLayout();
            }

            // Add a range track bar to the detached window
            var detachedRangeBar = new RangeTrackBar();
            detachedRangeBar.Dock = DockStyle.Bottom;
            detachedRangeBar.Height = 48;
            detachedRangeBar.Minimum = _rangeTrackBar.Minimum;
            detachedRangeBar.Maximum = _rangeTrackBar.Maximum;
            detachedRangeBar.LowerValue = _rangeTrackBar.LowerValue;
            detachedRangeBar.UpperValue = _rangeTrackBar.UpperValue;

            // Use shared handler for sync; also update detached chart axis
            detachedRangeBar.RangeChanged += RangeTrackBarOnRangeChanged;
            detachedRangeBar.RangeChanged += delegate
            {
                if (_syncingRange) return;
                if (detachedChart.ChartAreas.Count == 0) return;
                bool full = Math.Abs(detachedRangeBar.LowerValue - detachedRangeBar.Minimum) < 1e-10
                         && Math.Abs(detachedRangeBar.UpperValue - detachedRangeBar.Maximum) < 1e-10;
                detachedChart.ChartAreas[0].AxisX.Minimum = full ? double.NaN : detachedRangeBar.LowerValue;
                detachedChart.ChartAreas[0].AxisX.Maximum = full ? double.NaN : detachedRangeBar.UpperValue;
            };

            // Also update detached chart when main trackbar changes (via sync)
            _rangeTrackBar.RangeChanged += delegate
            {
                if (detachedRangeBar.IsDisposed || detachedChart.IsDisposed) return;
                if (detachedChart.ChartAreas.Count == 0) return;
                bool full = Math.Abs(_rangeTrackBar.LowerValue - _rangeTrackBar.Minimum) < 1e-10
                         && Math.Abs(_rangeTrackBar.UpperValue - _rangeTrackBar.Maximum) < 1e-10;
                detachedChart.ChartAreas[0].AxisX.Minimum = full ? double.NaN : _rangeTrackBar.LowerValue;
                detachedChart.ChartAreas[0].AxisX.Maximum = full ? double.NaN : _rangeTrackBar.UpperValue;
            };

            _detachedRangeBars.Add(detachedRangeBar);
            form.FormClosed += delegate { _detachedRangeBars.Remove(detachedRangeBar); };

            // Apply current range to detached chart
            if (!double.IsNaN(_rangeStartOa) && !double.IsNaN(_rangeEndOa) && detachedChart.ChartAreas.Count > 0)
            {
                detachedChart.ChartAreas[0].AxisX.Minimum = _rangeStartOa;
                detachedChart.ChartAreas[0].AxisX.Maximum = _rangeEndOa;
            }

            AttachChartInteractivity(detachedChart, form, overlayMode);
            form.Controls.Add(detachedChart);
            form.Controls.Add(detachedRangeBar);
            form.Show(this);
        }

        private void AttachChartInteractivity(Chart chart, Form ownerForm, bool overlayMode)
        {
            var tip = new ToolTip { AutoPopDelay = 600000, InitialDelay = 400, ReshowDelay = 200 };
            bool crosshair = true;
            int? crosshairPixelX = null;

            chart.MouseMove += delegate(object s, MouseEventArgs ev)
            {
                if (chart.ChartAreas.Count == 0 || chart.Series.Count == 0) return;
                var area = chart.ChartAreas[0];
                try
                {
                    double xVal = area.AxisX.PixelPositionToValue(ev.X);
                    if (crosshair)
                    {
                        crosshairPixelX = ev.X;
                        chart.Invalidate();
                        BuildCrosshairTooltip(chart, tip, xVal, ev.Location, overlayMode);
                    }
                }
                catch { }
            };
            chart.MouseLeave += delegate
            {
                crosshairPixelX = null;
                chart.Invalidate();
                tip.Hide(chart);
            };
            chart.Paint += delegate(object s, PaintEventArgs ev)
            {
                if (!crosshair || !crosshairPixelX.HasValue || chart.ChartAreas.Count == 0) return;
                ChartArea area = chart.ChartAreas[0];
                RectangleF areaRect = new RectangleF(
                    chart.ClientSize.Width * area.Position.X / 100f,
                    chart.ClientSize.Height * area.Position.Y / 100f,
                    chart.ClientSize.Width * area.Position.Width / 100f,
                    chart.ClientSize.Height * area.Position.Height / 100f);
                int x = crosshairPixelX.Value;
                if (x < areaRect.Left || x > areaRect.Right) return;
                using (var pen = new Pen(Color.Black, 1f))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    ev.Graphics.DrawLine(pen, x, areaRect.Top, x, areaRect.Bottom);
                }
            };

            chart.MouseWheel += delegate(object s, MouseEventArgs ev)
            {
                if (chart.ChartAreas.Count == 0) return;
                var area = chart.ChartAreas[0];
                try
                {
                    bool zoomY = Control.ModifierKeys.HasFlag(Keys.Control);
                    Axis axis = zoomY ? area.AxisY : area.AxisX;
                    double viewMin = axis.ScaleView.ViewMinimum;
                    double viewMax = axis.ScaleView.ViewMaximum;
                    double range = viewMax - viewMin;
                    if (range <= 0) return;
                    double mousePos;
                    try { mousePos = zoomY ? area.AxisY.PixelPositionToValue(ev.Y) : area.AxisX.PixelPositionToValue(ev.X); }
                    catch { return; }
                    double ratio = (mousePos - viewMin) / range;
                    double factor = ev.Delta > 0 ? 0.8 : 1.25;
                    double newRange = range * factor;
                    double fullMin = axis.Minimum;
                    double fullMax = axis.Maximum;
                    double fullRange = fullMax - fullMin;
                    if (double.IsNaN(fullRange) || fullRange <= 0) return;
                    if (newRange > fullRange) { axis.ScaleView.ZoomReset(0); return; }
                    if (newRange < fullRange * 0.001) return;
                    double newMin = mousePos - newRange * ratio;
                    double newMax = newMin + newRange;
                    if (newMin < fullMin) { newMin = fullMin; newMax = fullMin + newRange; }
                    if (newMax > fullMax) { newMax = fullMax; newMin = fullMax - newRange; }
                    axis.ScaleView.Zoom(newMin, newMax);
                }
                catch { }
            };

            chart.MouseDoubleClick += delegate
            {
                if (chart.ChartAreas.Count == 0) return;
                chart.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);
                chart.ChartAreas[0].AxisY.ScaleView.ZoomReset(0);
            };

            // Context menu
            var ctx = new ContextMenuStrip();

            var crosshairItem = new ToolStripMenuItem(Loc.Get("ChartCrosshair")) { Checked = true, CheckOnClick = true };
            crosshairItem.Click += delegate
            {
                crosshair = crosshairItem.Checked;
                if (!crosshair && chart.ChartAreas.Count > 0)
                {
                    crosshairPixelX = null;
                    chart.Invalidate();
                    tip.Hide(chart);
                }
            };
            ctx.Items.Add(crosshairItem);
            ctx.Items.Add(new ToolStripSeparator());

            var resetItem = new ToolStripMenuItem(Loc.Get("ChartResetZoom"));
            resetItem.Click += delegate
            {
                if (chart.ChartAreas.Count == 0) return;
                chart.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);
                chart.ChartAreas[0].AxisY.ScaleView.ZoomReset(0);
            };
            ctx.Items.Add(resetItem);

            var showAllItem = new ToolStripMenuItem(Loc.Get("ChartShowAll"));
            showAllItem.Click += delegate
            {
                if (chart.ChartAreas.Count == 0) return;
                var a = chart.ChartAreas[0];
                a.AxisX.ScaleView.ZoomReset(0); a.AxisY.ScaleView.ZoomReset(0);
                a.AxisX.Minimum = double.NaN; a.AxisX.Maximum = double.NaN;
                a.AxisY.Minimum = double.NaN; a.AxisY.Maximum = double.NaN;
            };
            ctx.Items.Add(showAllItem);
            ctx.Items.Add(new ToolStripSeparator());

            var saveItem = new ToolStripMenuItem(Loc.Get("ChartSaveImage")) { ShortcutKeys = Keys.Control | Keys.S };
            saveItem.Click += delegate
            {
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = Loc.Get("ChartImageFilter");
                    dlg.FileName = "chart_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    if (dlg.ShowDialog(ownerForm) != DialogResult.OK) return;
                    try
                    {
                        ChartImageFormat fmt = ChartImageFormat.Png;
                        string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                        if (ext == ".jpg" || ext == ".jpeg") fmt = ChartImageFormat.Jpeg;
                        else if (ext == ".bmp") fmt = ChartImageFormat.Bmp;
                        chart.SaveImage(dlg.FileName, fmt);
                    }
                    catch { }
                }
            };
            ctx.Items.Add(saveItem);

            var copyItem = new ToolStripMenuItem(Loc.Get("ChartCopyImage")) { ShortcutKeys = Keys.Control | Keys.C };
            copyItem.Click += delegate
            {
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        chart.SaveImage(ms, ChartImageFormat.Png);
                        ms.Position = 0;
                        using (var bmp = new Bitmap(ms)) { Clipboard.SetImage(bmp); }
                    }
                }
                catch { }
            };
            ctx.Items.Add(copyItem);

            chart.ContextMenuStrip = ctx;

            // Keyboard shortcuts in the owner form
            if (ownerForm != null)
            {
                ownerForm.KeyDown += delegate(object s, KeyEventArgs ev)
                {
                    if (ev.Control && ev.KeyCode == Keys.S)
                    {
                        saveItem.PerformClick(); ev.SuppressKeyPress = true;
                    }
                    else if (ev.Control && ev.KeyCode == Keys.C)
                    {
                        copyItem.PerformClick(); ev.SuppressKeyPress = true;
                    }
                    else if (ev.KeyCode == Keys.Home)
                    {
                        resetItem.PerformClick(); ev.SuppressKeyPress = true;
                    }
                };
            }
        }

        private void BrowseButtonOnClick(object sender, EventArgs e)
        {
            List<string> current = ParseFolderSpec(_folderBox.Text);
            string initial = current.Count > 0 && Directory.Exists(current[0]) ? current[0] : string.Empty;
            string picked = SelectSingleFolder(initial);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                string spec = JoinFolderSpec(new List<string> { picked });
                _folderBox.Text = spec;
                LoadFolder(spec, true);
            }
        }

        private void AddDataButtonOnClick(object sender, EventArgs e)
        {
            try
            {
                List<string> current = ParseFolderSpec(_folderBox.Text);
                if (current.Count == 0)
                {
                    NotifyError(Loc.Get("SelectFolder"));
                    return;
                }
                if (current.Count >= 3)
                {
                    NotifyError(Loc.Get("TooManyFolders"));
                    return;
                }
                string initial = Directory.Exists(current[current.Count - 1]) ? current[current.Count - 1] : string.Empty;
                string picked = SelectSingleFolder(initial);
                if (string.IsNullOrWhiteSpace(picked)) return;
                if (current.Any(f => string.Equals(f, picked, StringComparison.OrdinalIgnoreCase)))
                {
                    NotifyError(Loc.Get("FolderAlreadyAdded"));
                    return;
                }
                current.Add(picked);
                string spec = JoinFolderSpec(current);
                _folderBox.Text = spec;
                LoadFolder(spec, true);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(_projectRoot, "Add data folder failed.", ex);
                NotifyError(Loc.Get("LoadFailed"));
            }
        }

        private void RefreshButtonOnClick(object sender, EventArgs e)
        {
            bool overlayMode = _compareOverlayCheck.Checked;
            LoadFolder(_folderBox.Text, false, true, overlayMode, true);
        }

        private string SelectSingleFolder(string initial)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = initial ?? string.Empty;
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedPath : string.Empty;
            }
        }

        private void RecentFoldersBoxOnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_recentFoldersBox.SelectedItem == null) return;
            string folder = _recentFoldersBox.SelectedItem.ToString();
            _folderBox.Text = folder;
            if (IsValidFolderSpec(folder))
            {
                LoadFolder(folder, false);
            }
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
                if (IsValidFolderSpec(folder))
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
                return;
            }
            if (e.Control && e.KeyCode == Keys.S)
            {
                SaveImageMenuItemOnClick(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.C && _chart.Focused)
            {
                CopyImageMenuItemOnClick(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Home)
            {
                ResetChartZoom();
                e.SuppressKeyPress = true;
            }
        }

        private async void LoadFolder(string folder, bool addToRecent, bool preserveSelection = false, bool? preferredOverlayMode = null, bool preserveSourceWindowsLayout = false)
        {
            try
            {
                List<string> folders = ParseFolderSpec(folder);
                if (folders.Count == 0)
                {
                    NotifyError(Loc.Get("SelectFolder"));
                    return;
                }
                if (folders.Count > 3)
                {
                    NotifyError(Loc.Get("TooManyFolders"));
                    return;
                }
                for (int i = 0; i < folders.Count; i++)
                {
                    if (!Directory.Exists(folders[i]))
                    {
                        throw new DirectoryNotFoundException("Folder not found: " + folders[i]);
                    }
                }

                string spec = JoinFolderSpec(folders);
                _folderBox.Text = spec;
                if (preserveSelection)
                {
                    List<string> currentSelectedCodes = GetSelectedCodes();
                    _pendingCheckedCodes.Clear();
                    for (int i = 0; i < currentSelectedCodes.Count; i++)
                    {
                        _pendingCheckedCodes.Add(currentSelectedCodes[i]);
                    }
                }
                SetBusy(true, Loc.Get("LoadingData"));
                Cursor = Cursors.WaitCursor;
                TestData data;
                if (folders.Count == 1)
                {
                    data = await Task.Run(() => TestLoader.LoadTest(folders[0]));
                }
                else
                {
                    List<TestData> loaded = await Task.Run(() => TestLoader.LoadTests(folders));
                    data = TestLoader.MergeLoadedTests(loaded, true);
                }
                AppState.SetData(spec, data);
                BindLoadedData(data, preserveSourceWindowsLayout);
                if (preferredOverlayMode.HasValue)
                {
                    if (_compareOverlayCheck.Enabled)
                    {
                        _compareOverlayCheck.Checked = preferredOverlayMode.Value;
                    }
                    else
                    {
                        _compareOverlayCheck.Checked = false;
                    }
                }
                if (addToRecent)
                {
                    AddRecentFolder(spec);
                }
                NotifySuccess(string.Format(Loc.Get("LoadedTest"), data.RowCount));
            }
            catch (Exception ex) { AppLogger.LogError(_projectRoot, "Load test failed.", ex); MessageBox.Show(this, ex.Message, Loc.Get("LoadFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error); NotifyError(Loc.Get("LoadFailed")); }
            finally { Cursor = Cursors.Default; SetBusy(false, null); }
        }

        private static List<string> ParseFolderSpec(string spec)
        {
            var list = new List<string>();
            string raw = spec ?? string.Empty;
            string[] parts = raw.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim().Trim('"');
                if (p.Length == 0) continue;
                if (!list.Any(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(p);
                }
            }
            return list;
        }

        private static string JoinFolderSpec(List<string> folders)
        {
            return string.Join(" ; ", folders ?? new List<string>());
        }

        private static bool IsValidFolderSpec(string spec)
        {
            List<string> folders = ParseFolderSpec(spec);
            if (folders.Count == 0 || folders.Count > 3) return false;
            for (int i = 0; i < folders.Count; i++)
            {
                if (!Directory.Exists(folders[i])) return false;
            }
            return true;
        }

        private static string NormalizeChannelCodeForDisplay(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            int sep = code.IndexOf("::", StringComparison.Ordinal);
            string result = sep >= 0 ? code.Substring(sep + 2) : code;
            int hash = result.IndexOf('#');
            if (hash > 0)
            {
                result = result.Substring(0, hash);
            }
            return result;
        }

        private static string BuildDisplayLabel(string displayCode, ChannelInfo channel)
        {
            string unitPart = channel == null || string.IsNullOrEmpty(channel.Unit) ? string.Empty : " (" + channel.Unit + ")";
            string namePart = channel == null || string.IsNullOrEmpty(channel.Name) ? string.Empty : " - " + channel.Name;
            return (displayCode ?? string.Empty) + namePart + unitPart;
        }

        private void BindLoadedData(TestData data, bool preserveSourceWindowsLayout)
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
            bool canOverlay = IsOverlayCompareModeAvailable(data);
            _compareOverlayCheck.Enabled = canOverlay;
            if (!canOverlay)
            {
                _compareOverlayCheck.Checked = false;
            }
            if (data.SourceColumns != null && data.SourceColumns.Count > 0)
            {
                _folderBox.Text = JoinFolderSpec(data.SourceColumns.Keys.ToList());
            }
            bool refreshedInPlace = false;
            if (preserveSourceWindowsLayout)
            {
                refreshedInPlace = TryRefreshSourceChannelWindows(data);
            }
            if (!refreshedInPlace)
            {
                RebuildSourceChannelWindows(data);
            }
            DataSummary summary = AppState.BuildSummary(data);
            _summaryLabel.Text = string.Format(Loc.Get("Points"), summary.Points, summary.Start, summary.End);
            if (_chartHostForm != null && !_chartHostForm.IsDisposed)
            {
                _chartHostForm.Text = string.Format(Loc.Get("ChartWindowTitle"), AppState.Folder ?? Loc.Get("AppTitle"));
            }
            _exportTemplateButton.Enabled = _savePresetButton.Enabled = true;
            _loadPresetButton.Enabled = _deletePresetButton.Enabled = _presetsBox.Items.Count > 0;
            _saveOrderButton.Enabled = true;
            _loadOrderButton.Enabled = _deleteOrderButton.Enabled = _ordersBox.Items.Count > 0;
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void RebuildSourceChannelWindows(TestData data)
        {
            CloseSourceChannelWindows();
            if (data == null || data.SourceColumns == null || data.SourceColumns.Count == 0)
            {
                return;
            }

            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int baseX = wa.Left + 12;
            int baseY = Bottom + 10;
            int col = 0;
            string[] globalOrdered = ApplySavedOrder(data.ColumnNames);

            foreach (var kv in data.SourceColumns)
            {
                string sourceRoot = kv.Key;
                string[] sourceCols = kv.Value ?? new string[0];
                var set = new HashSet<string>(sourceCols, StringComparer.OrdinalIgnoreCase);
                string[] ordered = globalOrdered.Where(set.Contains).ToArray();

                var form = new Form();
                form.Text = string.Format(Loc.Get("ChannelsForSource"), Path.GetFileName(sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                form.Width = 560;
                form.Height = 640;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(baseX + (col * 570), baseY);
                form.ShowInTaskbar = false;
                form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
                var top = new FlowLayoutPanel();
                top.Dock = DockStyle.Top;
                top.Height = 32;
                top.WrapContents = false;
                top.Padding = new Padding(4, 4, 4, 2);

                var filterBox = new TextBox();
                filterBox.Width = 130;
                filterBox.Text = _channelFilterBox.Text;
                top.Controls.Add(filterBox);

                var sortBox = new ComboBox();
                sortBox.DropDownStyle = ComboBoxStyle.DropDownList;
                sortBox.Width = 150;
                PopulateSortModeBox(sortBox);
                SelectSortModeByKey(sortBox, GetSelectedSortKey());
                top.Controls.Add(sortBox);

                var selectedOnly = new CheckBox();
                selectedOnly.Text = Loc.Get("SelectedOnly");
                selectedOnly.AutoSize = true;
                selectedOnly.Checked = _selectedOnlyCheck.Checked;
                top.Controls.Add(selectedOnly);

                var selectAll = new Button();
                selectAll.Text = Loc.Get("SelectAll");
                selectAll.AutoSize = true;
                top.Controls.Add(selectAll);

                var clear = new Button();
                clear.Text = Loc.Get("Clear");
                clear.AutoSize = true;
                top.Controls.Add(clear);

                var list = new CheckedListBox();
                list.Dock = DockStyle.Fill;
                list.CheckOnClick = true;
                list.IntegralHeight = false;
                list.Tag = sourceRoot;
                list.ItemCheck += SourceListOnItemCheck;

                var bottom = new TableLayoutPanel();
                bottom.Dock = DockStyle.Bottom;
                bottom.AutoSize = true;
                bottom.ColumnCount = 1;
                bottom.RowCount = 4;

                var presetSaveRow = NewRow();
                var presetNameBox = new TextBox(); presetNameBox.Width = 190; presetNameBox.Text = _presetNameBox.Text;
                var savePresetButton = new Button(); savePresetButton.Text = Loc.Get("SavePreset"); savePresetButton.AutoSize = true;
                presetSaveRow.Controls.Add(presetNameBox);
                presetSaveRow.Controls.Add(savePresetButton);
                bottom.Controls.Add(presetSaveRow, 0, 0);

                var presetLoadRow = NewRow();
                var presetsBox = new ComboBox(); presetsBox.Width = 220; presetsBox.DropDownStyle = ComboBoxStyle.DropDownList;
                var loadPresetButton = new Button(); loadPresetButton.Text = Loc.Get("Load"); loadPresetButton.AutoSize = true;
                var deletePresetButton = new Button(); deletePresetButton.Text = Loc.Get("Delete"); deletePresetButton.AutoSize = true;
                presetLoadRow.Controls.Add(presetsBox);
                presetLoadRow.Controls.Add(loadPresetButton);
                presetLoadRow.Controls.Add(deletePresetButton);
                bottom.Controls.Add(presetLoadRow, 0, 1);

                var orderRow = NewRow();
                var orderNameBox = new TextBox(); orderNameBox.Width = 150; orderNameBox.Text = _orderNameBox.Text;
                var saveOrderButton = new Button(); saveOrderButton.Text = Loc.Get("SaveOrder"); saveOrderButton.AutoSize = true;
                var ordersBox = new ComboBox(); ordersBox.Width = 180; ordersBox.DropDownStyle = ComboBoxStyle.DropDownList;
                var loadOrderButton = new Button(); loadOrderButton.Text = Loc.Get("Load"); loadOrderButton.AutoSize = true;
                var deleteOrderButton = new Button(); deleteOrderButton.Text = Loc.Get("Delete"); deleteOrderButton.AutoSize = true;
                orderRow.Controls.Add(orderNameBox);
                orderRow.Controls.Add(saveOrderButton);
                orderRow.Controls.Add(ordersBox);
                orderRow.Controls.Add(loadOrderButton);
                orderRow.Controls.Add(deleteOrderButton);
                bottom.Controls.Add(orderRow, 0, 2);

                var status = new Label();
                status.AutoSize = true;
                status.Padding = new Padding(4, 4, 4, 6);
                status.Text = _statusLabel.Text;
                bottom.Controls.Add(status, 0, 3);

                var state = new SourceWindowState
                {
                    SourceRoot = sourceRoot,
                    Form = form,
                    FilterBox = filterBox,
                    SortModeBox = sortBox,
                    SelectedOnlyCheck = selectedOnly,
                    SelectAllButton = selectAll,
                    ClearButton = clear,
                    PresetNameBox = presetNameBox,
                    SavePresetButton = savePresetButton,
                    PresetsBox = presetsBox,
                    LoadPresetButton = loadPresetButton,
                    DeletePresetButton = deletePresetButton,
                    OrderNameBox = orderNameBox,
                    SaveOrderButton = saveOrderButton,
                    OrdersBox = ordersBox,
                    LoadOrderButton = loadOrderButton,
                    DeleteOrderButton = deleteOrderButton,
                    StatusLabel = status,
                    List = list,
                    Items = ordered.Select(code =>
                    {
                        ChannelInfo ch;
                        string displayCode = NormalizeChannelCodeForDisplay(code);
                        string label = data.Channels.TryGetValue(code, out ch)
                            ? BuildDisplayLabel(displayCode, ch)
                            : displayCode;
                        string unit = data.Channels.TryGetValue(code, out ch) ? (ch.Unit ?? string.Empty) : string.Empty;
                        return new ChannelItem(code, label, unit);
                    }).ToList()
                };

                filterBox.TextChanged += delegate { SourceWindowOptionsChanged(state); };
                sortBox.SelectedIndexChanged += delegate { SourceWindowOptionsChanged(state); };
                selectedOnly.CheckedChanged += delegate { SourceWindowOptionsChanged(state); };
                selectAll.Click += delegate { SelectAllInSource(state); };
                clear.Click += delegate { ClearAllInSource(state); };
                savePresetButton.Click += delegate { SavePresetFromSource(state); };
                loadPresetButton.Click += delegate { LoadPresetFromSource(state); };
                deletePresetButton.Click += delegate { DeletePresetFromSource(state); };
                saveOrderButton.Click += delegate { SaveOrderFromSource(state); };
                loadOrderButton.Click += delegate { LoadOrderFromSource(state); };
                deleteOrderButton.Click += delegate { DeleteOrderFromSource(state); };

                form.Controls.Add(list);
                form.Controls.Add(bottom);
                form.Controls.Add(top);
                form.FormClosed += delegate
                {
                    if (!_closingSourceChannelWindows)
                    {
                        SourceWindowState closedState;
                        if (_sourceWindows.TryGetValue(sourceRoot, out closedState) && closedState != null && closedState.Items != null)
                        {
                            for (int i = 0; i < closedState.Items.Count; i++)
                            {
                                _checkedCodes.Remove(closedState.Items[i].Code);
                            }
                        }
                        var folders = ParseFolderSpec(_folderBox.Text);
                        folders = folders.Where(f => !string.Equals(f, sourceRoot, StringComparison.OrdinalIgnoreCase)).ToList();
                        _folderBox.Text = JoinFolderSpec(folders);
                        BeginInvoke((Action)(delegate
                        {
                            if (folders.Count == 0)
                            {
                                CloseAllButtonOnClick(null, EventArgs.Empty);
                            }
                            else
                            {
                                LoadFolder(_folderBox.Text, false);
                            }
                        }));
                    }
                    _sourceChannelForms.Remove(form);
                    _sourceChannelLists.Remove(sourceRoot);
                    _sourceWindows.Remove(sourceRoot);
                };

                _sourceChannelForms.Add(form);
                _sourceChannelLists[sourceRoot] = list;
                _sourceWindows[sourceRoot] = state;
                BindPresetControlsForSource(state);
                BindOrderControlsForSource(state);
                RebuildSourceWindowList(state);
                form.Show(this);

                col++;
            }
        }

        private bool TryRefreshSourceChannelWindows(TestData data)
        {
            if (data == null || data.SourceColumns == null || data.SourceColumns.Count == 0)
            {
                return false;
            }
            if (_sourceWindows == null || _sourceWindows.Count == 0)
            {
                return false;
            }

            var existingRoots = new HashSet<string>(_sourceWindows.Keys, StringComparer.OrdinalIgnoreCase);
            var incomingRoots = new HashSet<string>(data.SourceColumns.Keys, StringComparer.OrdinalIgnoreCase);
            if (!existingRoots.SetEquals(incomingRoots))
            {
                return false;
            }

            string[] globalOrdered = ApplySavedOrder(data.ColumnNames);
            foreach (var kv in data.SourceColumns)
            {
                string sourceRoot = kv.Key;
                SourceWindowState state;
                if (!_sourceWindows.TryGetValue(sourceRoot, out state) || state == null)
                {
                    return false;
                }
                if (state.Form == null || state.Form.IsDisposed || state.List == null || state.List.IsDisposed)
                {
                    return false;
                }

                string[] sourceCols = kv.Value ?? new string[0];
                var set = new HashSet<string>(sourceCols, StringComparer.OrdinalIgnoreCase);
                string[] ordered = globalOrdered.Where(set.Contains).ToArray();
                state.Items = ordered.Select(code =>
                {
                    ChannelInfo ch;
                    string displayCode = NormalizeChannelCodeForDisplay(code);
                    string label = data.Channels.TryGetValue(code, out ch)
                        ? BuildDisplayLabel(displayCode, ch)
                        : displayCode;
                    string unit = data.Channels.TryGetValue(code, out ch) ? (ch.Unit ?? string.Empty) : string.Empty;
                    return new ChannelItem(code, label, unit);
                }).ToList();

                state.Form.Text = string.Format(Loc.Get("ChannelsForSource"), Path.GetFileName(sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                RebuildSourceWindowList(state);
            }

            return true;
        }

        private void CloseSourceChannelWindows()
        {
            _closingSourceChannelWindows = true;
            try
            {
                for (int i = _sourceChannelForms.Count - 1; i >= 0; i--)
                {
                    Form f = _sourceChannelForms[i];
                    if (f == null) continue;
                    try
                    {
                        if (!f.IsDisposed) f.Close();
                    }
                    catch { }
                }
            }
            finally
            {
                _closingSourceChannelWindows = false;
            }
            _sourceChannelForms.Clear();
            _sourceChannelLists.Clear();
            _sourceWindows.Clear();
            HideChartHost();
        }

        private void SyncSourceWindowsChecksFromSelectedCodes()
        {
            if (_syncingSourceChannelSelection) return;
            _syncingSourceChannelSelection = true;
            try
            {
                foreach (var kv in _sourceWindows)
                {
                    CheckedListBox list = kv.Value.List;
                    if (list == null || list.IsDisposed) continue;
                    for (int i = 0; i < list.Items.Count; i++)
                    {
                        ChannelItem item = list.Items[i] as ChannelItem;
                        if (item == null) continue;
                        bool shouldCheck = _checkedCodes.Contains(item.Code);
                        if (list.GetItemChecked(i) != shouldCheck)
                        {
                            list.SetItemChecked(i, shouldCheck);
                        }
                    }
                }
            }
            finally
            {
                _syncingSourceChannelSelection = false;
            }
        }

        private void SourceListOnItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_syncingSourceChannelSelection) return;
            var list = sender as CheckedListBox;
            if (list == null) return;

            BeginInvoke((Action)(delegate
            {
                if (_syncingSourceChannelSelection) return;
                _syncingSourceChannelSelection = true;
                try
                {
                    for (int i = 0; i < list.Items.Count; i++)
                    {
                        ChannelItem item = list.Items[i] as ChannelItem;
                        if (item == null) continue;
                        bool checkedNow = list.GetItemChecked(i);
                        if (checkedNow) _checkedCodes.Add(item.Code);
                        else _checkedCodes.Remove(item.Code);
                    }
                }
                finally
                {
                    _syncingSourceChannelSelection = false;
                }

                RebuildChannelList();
                SyncSourceWindowsChecksFromSelectedCodes();
                UpdateSelectionInfo();
                RedrawChart();
            }));
        }

        private void StepControlsOnChanged(object sender, EventArgs e)
        {
            _manualStepUpDown.Enabled = !_autoStepCheck.Checked;
            _targetPointsBox.Enabled = _autoStepCheck.Checked;
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void CompareOverlayCheckOnCheckedChanged(object sender, EventArgs e)
        {
            TestData data = AppState.Data;
            if (data == null)
            {
                return;
            }
            if (!IsOverlayCompareModeAvailable(data))
            {
                if (_compareOverlayCheck.Checked)
                {
                    _compareOverlayCheck.Checked = false;
                    return;
                }
                return;
            }

            BeginInvoke((Action)(delegate
            {
                AppLogger.LogInfo(_projectRoot, string.Format(
                    "COMPARE_TOGGLE begin checked={0} dataRows={1} sourceWindows={2} checkedCodes={3}",
                    _compareOverlayCheck.Checked ? "1" : "0",
                    data.RowCount,
                    _sourceWindows.Count,
                    string.Join(",", _checkedCodes.Take(20).ToArray())));

                _rangeStartOa = double.NaN;
                _rangeEndOa = double.NaN;
                _rangeLabel.Text = BuildRangeLabelText(_rangeStartOa, _rangeEndOa);
                _resetRangeButton.Visible = false;
                List<string> previousSelection = _checkedCodes.ToList();
                RefreshCheckedCodesFromSourceWindows();
                if (_checkedCodes.Count == 0 && previousSelection.Count > 0)
                {
                    for (int i = 0; i < previousSelection.Count; i++)
                    {
                        _checkedCodes.Add(previousSelection[i]);
                    }
                }
                if (_checkedCodes.Count == 0 && _lastSelectedCodes.Count > 0)
                {
                    for (int i = 0; i < _lastSelectedCodes.Count; i++)
                    {
                        _checkedCodes.Add(_lastSelectedCodes[i]);
                    }
                }
                if (_chart.ChartAreas.Count > 0)
                {
                    _chart.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);
                    _chart.ChartAreas[0].AxisY.ScaleView.ZoomReset(0);
                    _chart.ChartAreas[0].AxisX.Minimum = double.NaN;
                    _chart.ChartAreas[0].AxisX.Maximum = double.NaN;
                    _chart.ChartAreas[0].AxisY.Minimum = double.NaN;
                    _chart.ChartAreas[0].AxisY.Maximum = double.NaN;
                }
                RunWithoutRangeSync(delegate
                {
                    if (!double.IsNaN(_rangeTrackBar.Minimum) && !double.IsNaN(_rangeTrackBar.Maximum) && _rangeTrackBar.Minimum < _rangeTrackBar.Maximum)
                    {
                        _rangeTrackBar.LowerValue = _rangeTrackBar.Minimum;
                        _rangeTrackBar.UpperValue = _rangeTrackBar.Maximum;
                    }
                });
                ApplyChannelChecks(_checkedCodes.ToList());
                _chart.Invalidate();
                AppLogger.LogInfo(_projectRoot, string.Format(
                    "COMPARE_TOGGLE end checked={0} checkedCodes={1} axisX=[{2};{3}] range=[{4};{5}] series={6}",
                    _compareOverlayCheck.Checked ? "1" : "0",
                    string.Join(",", _checkedCodes.Take(20).ToArray()),
                    _chart.ChartAreas.Count > 0 ? _chart.ChartAreas[0].AxisX.Minimum.ToString(CultureInfo.InvariantCulture) : "na",
                    _chart.ChartAreas.Count > 0 ? _chart.ChartAreas[0].AxisX.Maximum.ToString(CultureInfo.InvariantCulture) : "na",
                    _rangeTrackBar.LowerValue.ToString(CultureInfo.InvariantCulture),
                    _rangeTrackBar.UpperValue.ToString(CultureInfo.InvariantCulture),
                    _chart.Series.Count));
            }));
        }

        private void ChannelViewOptionsChanged(object sender, EventArgs e)
        {
            RebuildChannelList();
            foreach (var kv in _sourceWindows)
            {
                RebuildSourceWindowList(kv.Value);
            }
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void ChannelsListOnItemCheck(object sender, ItemCheckEventArgs e)
        {
            BeginInvoke((Action)(delegate
            {
                SyncCheckedFromVisibleList();
                SyncSourceWindowsChecksFromSelectedCodes();
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
            SyncSourceWindowsChecksFromSelectedCodes();
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
            SyncSourceWindowsChecksFromSelectedCodes();
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void ChannelsListOnMouseDown(object sender, MouseEventArgs e)
        {
            _dragIndex = _channelsList.IndexFromPoint(e.Location);
            _dragStartPoint = e.Button == MouseButtons.Left && _dragIndex >= 0 ? e.Location : Point.Empty;
            _dragInitiated = false;
        }

        private void ChannelsListOnMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragInitiated || _dragIndex < 0 || _dragStartPoint == Point.Empty || e.Button != MouseButtons.Left) return;
            Size threshold = SystemInformation.DragSize;
            if (Math.Abs(e.X - _dragStartPoint.X) > threshold.Width || Math.Abs(e.Y - _dragStartPoint.Y) > threshold.Height)
            {
                _dragInitiated = true;
                if (_dragIndex < _channelsList.Items.Count)
                    _channelsList.DoDragDrop(_channelsList.Items[_dragIndex], DragDropEffects.Move);
            }
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
            if (data == null || data.RowCount == 0)
            {
                HideChartHost();
                return;
            }
            bool overlayMode = IsOverlayCompareModeActive();
            ApplyAxisXMode(overlayMode);
            List<string> selectedCodes = GetSelectedCodes();
            if (selectedCodes.Count == 0 && _lastSelectedCodes.Count > 0)
            {
                selectedCodes = _lastSelectedCodes.ToList();
            }
            if (selectedCodes.Count == 0)
            {
                HideChartHost();
                return;
            }
            if (overlayMode)
            {
                AppLogger.LogInfo(_projectRoot, string.Format(
                    "REDRAW overlay=1 selected={0} codes={1} dataRows={2}",
                    selectedCodes.Count,
                    string.Join(",", selectedCodes.Take(20).ToArray()),
                    data.RowCount));
            }
            _lastSelectedCodes.Clear();
            _lastSelectedCodes.AddRange(selectedCodes);
            ShowChartHost();
            int step = ResolveStep(data.TimestampsMs.Length);
            if (step > 1 && ShouldForceStepOneForMultiSource(data, selectedCodes))
            {
                AppLogger.LogInfo(_projectRoot, string.Format(
                    "REDRAW step override from {0} to 1 for multi-source selected channels.",
                    step));
                step = 1;
            }
            SeriesSlice slice = SeriesCache.GetOrBuild(AppState.DataVersion, data, selectedCodes, data.TimestampsMs[0], data.TimestampsMs[data.TimestampsMs.Length - 1], step);

            // Pre-convert timestamps to OADate once for all channels in normal mode
            long[] ts = slice.Timestamps;
            double[] oaDates = null;
            if (!overlayMode)
            {
                oaDates = new double[ts.Length];
                for (int i = 0; i < ts.Length; i++)
                    oaDates[i] = AppState.UnixMsToLocalDateTime(ts[i]).ToOADate();
            }

            // Hide legend when too many series (major perf killer)
            bool showLegend = selectedCodes.Count <= 20;
            if (_chart.Legends.Count > 0)
                _chart.Legends[0].Enabled = showLegend;

            _chart.BeginInit();
            _chart.SuspendLayout();
            _chart.AntiAliasing = selectedCodes.Count > 10 ? AntiAliasingStyles.None : AntiAliasingStyles.All;
            try
            {
                long maxOverlayDurationMs = 0L;
                int builtSeries = 0;
                foreach (string code in selectedCodes)
                {
                    double?[] values;
                    if (!slice.Series.TryGetValue(code, out values)) continue;
                    var series = new Series(code);
                    series.ChartType = SeriesChartType.FastLine;
                    series.XValueType = ChartValueType.DateTime;
                    series.BorderWidth = selectedCodes.Count > 20 ? 1 : 2;
                    series.IsVisibleInLegend = showLegend;
                    int n = Math.Min(ts.Length, values.Length);
                    int firstValueIndex = -1;
                    for (int i = 0; i < n; i++)
                    {
                        if (values[i].HasValue)
                        {
                            firstValueIndex = i;
                            break;
                        }
                    }
                    long seriesBaseMs = firstValueIndex >= 0 ? ts[firstValueIndex] : (n > 0 ? ts[0] : 0L);

                    // Count non-null first to allocate exact size
                    int count = 0;
                    for (int i = 0; i < n; i++) if (values[i].HasValue) count++;
                    if (count > 0)
                    {
                        double[] xArr = new double[count];
                        double[] yArr = new double[count];
                        int w = 0;
                        for (int i = 0; i < n; i++)
                        {
                            if (values[i].HasValue)
                            {
                                long relativeMs = Math.Max(0L, ts[i] - seriesBaseMs);
                                if (relativeMs > 864000000L) relativeMs = 864000000L; // cap to 10 days for stable axis rendering
                                xArr[w] = overlayMode
                                    ? OverlayBaseLocalDate.AddMilliseconds(relativeMs).ToOADate()
                                    : oaDates[i];
                                yArr[w] = values[i].Value;
                                if (overlayMode && relativeMs > maxOverlayDurationMs) maxOverlayDurationMs = relativeMs;
                                w++;
                            }
                        }
                        series.Points.DataBindXY(xArr, yArr);
                        if (overlayMode)
                        {
                            AppLogger.LogInfo(_projectRoot, string.Format(
                                "REDRAW series code={0} points={1} firstX={2} lastX={3}",
                                code,
                                count,
                                count > 0 ? xArr[0].ToString(CultureInfo.InvariantCulture) : "na",
                                count > 0 ? xArr[count - 1].ToString(CultureInfo.InvariantCulture) : "na"));
                        }
                    }
                    _chart.Series.Add(series);
                    builtSeries++;
                }

                // Update range track bar bounds from displayed X-space
                double dataMin = double.NaN;
                double dataMax = double.NaN;
                if (overlayMode)
                {
                    dataMin = OverlayBaseLocalDate.ToOADate();
                    dataMax = OverlayBaseLocalDate.AddMilliseconds(Math.Max(1000L, maxOverlayDurationMs)).ToOADate();
                }
                else if (oaDates != null && oaDates.Length > 0)
                {
                    dataMin = oaDates[0];
                    dataMax = oaDates[oaDates.Length - 1];
                }

                if (!double.IsNaN(dataMin) && !double.IsNaN(dataMax) && dataMin < dataMax)
                {
                    bool wasFullRange = Math.Abs(_rangeTrackBar.LowerValue - _rangeTrackBar.Minimum) < 1e-10
                                     && Math.Abs(_rangeTrackBar.UpperValue - _rangeTrackBar.Maximum) < 1e-10;
                    RunWithoutRangeSync(delegate
                    {
                        _rangeTrackBar.Minimum = dataMin;
                        _rangeTrackBar.Maximum = dataMax;
                        if (wasFullRange || double.IsNaN(_rangeStartOa))
                        {
                            _rangeTrackBar.LowerValue = dataMin;
                            _rangeTrackBar.UpperValue = dataMax;
                        }
                    });
                }

                // Apply range to chart axis if set
                if (_chart.ChartAreas.Count > 0)
                {
                    if (!double.IsNaN(_rangeStartOa) && !double.IsNaN(_rangeEndOa))
                    {
                        _chart.ChartAreas[0].AxisX.Minimum = _rangeStartOa;
                        _chart.ChartAreas[0].AxisX.Maximum = _rangeEndOa;
                    }
                    else
                    {
                        _chart.ChartAreas[0].AxisX.Minimum = double.NaN;
                        _chart.ChartAreas[0].AxisX.Maximum = double.NaN;
                    }
                }
                _chart.ResetAutoValues();
                if (_chart.ChartAreas.Count > 0)
                {
                    _chart.ChartAreas[0].RecalculateAxesScale();
                }
                if (overlayMode)
                {
                    AppLogger.LogInfo(_projectRoot, string.Format(
                        "REDRAW done overlay=1 builtSeries={0} axisX=[{1};{2}] track=[{3};{4}] maxOverlayMs={5}",
                        builtSeries,
                        _chart.ChartAreas.Count > 0 ? _chart.ChartAreas[0].AxisX.Minimum.ToString(CultureInfo.InvariantCulture) : "na",
                        _chart.ChartAreas.Count > 0 ? _chart.ChartAreas[0].AxisX.Maximum.ToString(CultureInfo.InvariantCulture) : "na",
                        _rangeTrackBar.LowerValue.ToString(CultureInfo.InvariantCulture),
                        _rangeTrackBar.UpperValue.ToString(CultureInfo.InvariantCulture),
                        maxOverlayDurationMs));
                }
            }
            finally
            {
                _chart.ResumeLayout();
                _chart.EndInit();
                _chart.Invalidate();
                _chart.Update();
            }
        }

        private void ApplyAxisXMode(bool overlayMode)
        {
            if (_chart.ChartAreas.Count == 0) return;
            ChartArea area = _chart.ChartAreas[0];
            area.AxisX.LabelStyle.Format = overlayMode ? "HH:mm:ss" : "HH:mm\ndd.MM";
            area.AxisX.Title = overlayMode ? Loc.Get("OverlayXAxisTitle") : string.Empty;
        }

        private bool IsOverlayCompareModeAvailable(TestData data)
        {
            return data != null
                   && data.SourceColumns != null
                   && data.SourceColumns.Count > 1
                   && data.CodeSources != null
                   && data.SourceStartMs != null;
        }

        private bool IsOverlayCompareModeActive()
        {
            return _compareOverlayCheck.Checked && IsOverlayCompareModeAvailable(AppState.Data);
        }

        private static long ResolveOverlayMaxDurationMs(TestData data, List<string> selectedCodes)
        {
            long maxDuration = 0L;
            if (data == null || selectedCodes == null) return maxDuration;
            for (int i = 0; i < selectedCodes.Count; i++)
            {
                string code = selectedCodes[i];
                string source = null;
                if (data.CodeSources != null)
                {
                    data.CodeSources.TryGetValue(code, out source);
                }
                if (string.IsNullOrWhiteSpace(source)) continue;

                long startMs;
                long endMs;
                if (data.SourceStartMs == null || !data.SourceStartMs.TryGetValue(source, out startMs)) continue;
                if (data.SourceEndMs == null || !data.SourceEndMs.TryGetValue(source, out endMs)) continue;
                long duration = Math.Max(0L, endMs - startMs);
                if (duration > maxDuration) maxDuration = duration;
            }

            if (maxDuration == 0L && data.TimestampsMs != null && data.TimestampsMs.Length > 1)
            {
                maxDuration = Math.Max(0L, data.TimestampsMs[data.TimestampsMs.Length - 1] - data.TimestampsMs[0]);
            }
            return maxDuration;
        }

        private string BuildRangeLabelText(double startOa, double endOa)
        {
            if (double.IsNaN(startOa) || double.IsNaN(endOa))
            {
                return Loc.Get("RangeAll");
            }
            if (IsOverlayCompareModeActive())
            {
                return string.Format(Loc.Get("RangeSelectedOverlay"), FormatOverlayElapsed(startOa), FormatOverlayElapsed(endOa));
            }
            DateTime dtStart = DateTime.FromOADate(startOa);
            DateTime dtEnd = DateTime.FromOADate(endOa);
            return string.Format(Loc.Get("RangeSelected"), dtStart, dtEnd);
        }

        private static string FormatOverlayElapsed(double oaValue)
        {
            DateTime dt = DateTime.FromOADate(oaValue);
            TimeSpan ts = dt - OverlayBaseLocalDate;
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            int hh = (int)ts.TotalHours;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", hh, ts.Minutes, ts.Seconds);
        }

        private void RunWithoutRangeSync(Action action)
        {
            if (action == null) return;
            bool prev = _syncingRange;
            _syncingRange = true;
            try
            {
                action();
            }
            finally
            {
                _syncingRange = prev;
            }
        }

        private void RefreshCheckedCodesFromSourceWindows()
        {
            if (_sourceWindows == null || _sourceWindows.Count == 0) return;
            var fresh = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _sourceWindows)
            {
                SourceWindowState state = kv.Value;
                if (state == null || state.List == null || state.List.IsDisposed) continue;
                CheckedListBox list = state.List;
                for (int i = 0; i < list.Items.Count; i++)
                {
                    ChannelItem item = list.Items[i] as ChannelItem;
                    if (item == null) continue;
                    if (list.GetItemChecked(i))
                    {
                        fresh.Add(item.Code);
                    }
                }
            }
            if (fresh.Count > 0)
            {
                _checkedCodes.Clear();
                foreach (string code in fresh) _checkedCodes.Add(code);
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
            // Scale down target when many channels are selected to keep total point count reasonable
            int channelCount = _checkedCodes.Count;
            if (channelCount > 10)
            {
                // Cap total points across all series at ~50k for responsiveness
                int maxTotalPoints = 50000;
                int perChannel = Math.Max(200, maxTotalPoints / channelCount);
                target = Math.Min(target, perChannel);
            }
            return Math.Max(1, totalPoints / target);
        }

        private static bool ShouldForceStepOneForMultiSource(TestData data, List<string> selectedCodes)
        {
            if (data == null || selectedCodes == null || selectedCodes.Count == 0)
            {
                return false;
            }
            if (data.SourceColumns == null || data.SourceColumns.Count <= 1)
            {
                return false;
            }
            if (data.CodeSources == null)
            {
                return true;
            }

            var selectedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < selectedCodes.Count; i++)
            {
                string source;
                if (data.CodeSources.TryGetValue(selectedCodes[i], out source) && !string.IsNullOrWhiteSpace(source))
                {
                    selectedSources.Add(source);
                    if (selectedSources.Count > 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private async void ExportTemplateButtonOnClick(object sender, EventArgs e)
        {
            TestData data = AppState.Data; if (data == null) return;
            string templatePath = Path.Combine(_projectRoot, "template.xlsx");
            if (!File.Exists(templatePath)) { NotifyError(Loc.Get("TemplateNotFound")); return; }
            List<string> selectedCodes = GetSelectedCodes();
            string refrig = _refrigerantBox.SelectedItem == null ? "R290" : _refrigerantBox.SelectedItem.ToString();
            bool includeExtra = _includeExtraCheck.Checked;
            ViewerSettingsModel settings = _viewerSettings;

            // Read selected range from stored values
            long? rangeStartMs = null;
            long? rangeEndMs = null;
            if (!IsOverlayCompareModeActive() && !double.IsNaN(_rangeStartOa) && !double.IsNaN(_rangeEndOa))
            {
                DateTime dtStart = DateTime.FromOADate(_rangeStartOa);
                DateTime dtEnd = DateTime.FromOADate(_rangeEndOa);
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                rangeStartMs = (long)(dtStart.ToUniversalTime() - epoch).TotalMilliseconds;
                rangeEndMs = (long)(dtEnd.ToUniversalTime() - epoch).TotalMilliseconds;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel files (*.xlsx)|*.xlsx"; dialog.FileName = "template_filled_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string savePath = dialog.FileName;
                try
                {
                    SetBusy(true, Loc.Get("ExportingTemplate"));
                    string loadedFolder = AppState.Folder;
                    byte[] payload = await Task.Run(() => TemplateExporter.Export(templatePath, loadedFolder, data, selectedCodes, includeExtra, refrig, settings, rangeStartMs, rangeEndMs));
                    File.WriteAllBytes(savePath, payload);
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
                    try { Process.Start(new ProcessStartInfo(savePath) { UseShellExecute = true }); }
                    catch { }
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

        private void SavePresetFromSource(SourceWindowState state)
        {
            if (state == null) return;
            if (state.PresetNameBox != null) _presetNameBox.Text = state.PresetNameBox.Text;
            SavePresetButtonOnClick(this, EventArgs.Empty);
            if (state.PresetNameBox != null) state.PresetNameBox.Text = _presetNameBox.Text;
            BindPresetControlsForSource(state);
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

        private void LoadPresetFromSource(SourceWindowState state)
        {
            if (state == null || state.PresetsBox == null) return;
            _presetsBox.SelectedIndex = state.PresetsBox.SelectedIndex;
            LoadPresetButtonOnClick(this, EventArgs.Empty);
            BindPresetControlsForSource(state);
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

        private void DeletePresetFromSource(SourceWindowState state)
        {
            if (state == null || state.PresetsBox == null) return;
            _presetsBox.SelectedIndex = state.PresetsBox.SelectedIndex;
            DeletePresetButtonOnClick(this, EventArgs.Empty);
            BindPresetControlsForSource(state);
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

        private void SaveOrderFromSource(SourceWindowState state)
        {
            if (state == null) return;
            if (state.OrderNameBox != null) _orderNameBox.Text = state.OrderNameBox.Text;
            SaveOrderButtonOnClick(this, EventArgs.Empty);
            if (state.OrderNameBox != null) state.OrderNameBox.Text = _orderNameBox.Text;
            BindOrderControlsForSource(state);
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

        private void LoadOrderFromSource(SourceWindowState state)
        {
            if (state == null || state.OrdersBox == null) return;
            _ordersBox.SelectedIndex = state.OrdersBox.SelectedIndex;
            LoadOrderButtonOnClick(this, EventArgs.Empty);
            BindOrderControlsForSource(state);
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

        private void DeleteOrderFromSource(SourceWindowState state)
        {
            if (state == null || state.OrdersBox == null) return;
            _ordersBox.SelectedIndex = state.OrdersBox.SelectedIndex;
            DeleteOrderButtonOnClick(this, EventArgs.Empty);
            BindOrderControlsForSource(state);
        }

        private void ApplyChannelChecks(IList<string> checkedCodes)
        {
            _checkedCodes.Clear();
            if (checkedCodes != null) for (int i = 0; i < checkedCodes.Count; i++) _checkedCodes.Add(checkedCodes[i]);
            RebuildChannelList();
            SyncSourceWindowsChecksFromSelectedCodes();
            UpdateSelectionInfo();
            RedrawChart();
        }

        private List<string> GetSelectedCodes()
        {
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
            // Main list is hidden in multi-window UI mode; selection state is driven by source windows.
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

        private void SourceWindowOptionsChanged(SourceWindowState origin)
        {
            if (origin == null) return;
            if (_syncingSourceChannelSelection) return;
            _syncingSourceChannelSelection = true;
            try
            {
                string filter = origin.FilterBox == null ? string.Empty : origin.FilterBox.Text;
                string sortKey = GetSelectedSortKey(origin.SortModeBox);
                bool selectedOnly = origin.SelectedOnlyCheck != null && origin.SelectedOnlyCheck.Checked;

                _channelFilterBox.Text = filter;
                _selectedOnlyCheck.Checked = selectedOnly;
                SelectSortModeByKey(_sortModeBox, sortKey);

                foreach (var kv in _sourceWindows)
                {
                    SourceWindowState state = kv.Value;
                    if (state == null || state == origin) continue;
                    if (state.FilterBox != null && state.FilterBox.Text != filter) state.FilterBox.Text = filter;
                    if (state.SelectedOnlyCheck != null && state.SelectedOnlyCheck.Checked != selectedOnly) state.SelectedOnlyCheck.Checked = selectedOnly;
                    SelectSortModeByKey(state.SortModeBox, sortKey);
                }

                foreach (var kv in _sourceWindows)
                {
                    RebuildSourceWindowList(kv.Value);
                }
            }
            finally
            {
                _syncingSourceChannelSelection = false;
            }
        }

        private void SelectAllInSource(SourceWindowState state)
        {
            if (state == null || state.Items == null) return;
            for (int i = 0; i < state.Items.Count; i++)
            {
                _checkedCodes.Add(state.Items[i].Code);
            }
            SyncSourceWindowsChecksFromSelectedCodes();
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void ClearAllInSource(SourceWindowState state)
        {
            if (state == null || state.Items == null) return;
            for (int i = 0; i < state.Items.Count; i++)
            {
                _checkedCodes.Remove(state.Items[i].Code);
            }
            SyncSourceWindowsChecksFromSelectedCodes();
            UpdateSelectionInfo();
            RedrawChart();
        }

        private void RebuildSourceWindowList(SourceWindowState state)
        {
            if (state == null || state.List == null || state.List.IsDisposed) return;
            IEnumerable<ChannelItem> items = state.Items ?? Enumerable.Empty<ChannelItem>();
            string filter = state.FilterBox == null ? string.Empty : (state.FilterBox.Text ?? string.Empty).Trim();
            if (filter.Length > 0)
            {
                string f = filter.ToLowerInvariant();
                items = items.Where(c => (c.Code ?? string.Empty).ToLowerInvariant().Contains(f) || (c.Label ?? string.Empty).ToLowerInvariant().Contains(f));
            }
            if (state.SelectedOnlyCheck != null && state.SelectedOnlyCheck.Checked)
            {
                items = items.Where(c => _checkedCodes.Contains(c.Code));
            }

            string mode = GetSelectedSortKey(state.SortModeBox);
            if (mode == "Code") items = items.OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase);
            else if (mode == "Natural code") items = items.OrderBy(c => c.Code, new NaturalStringComparer());
            else if (mode == "Label") items = items.OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase);
            else if (mode == "Unit") items = items.OrderBy(c => c.Unit, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase);
            else if (mode == "Priority A/C") items = items.OrderBy(c => PrefixPriority(c.Code)).ThenBy(c => c.Code, new NaturalStringComparer());
            else if (mode == "Selected first") items = items.OrderByDescending(c => _checkedCodes.Contains(c.Code)).ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase);

            state.List.Items.Clear();
            foreach (ChannelItem c in items)
            {
                state.List.Items.Add(c, _checkedCodes.Contains(c.Code));
            }
        }

        private static void PopulateSortModeBox(ComboBox box)
        {
            if (box == null) return;
            box.Items.Clear();
            box.Items.Add(new SortModeItem("User", "SortUser"));
            box.Items.Add(new SortModeItem("Code", "SortCode"));
            box.Items.Add(new SortModeItem("Natural code", "SortNaturalCode"));
            box.Items.Add(new SortModeItem("Label", "SortLabel"));
            box.Items.Add(new SortModeItem("Unit", "SortUnit"));
            box.Items.Add(new SortModeItem("Priority A/C", "SortPriorityAC"));
            box.Items.Add(new SortModeItem("Selected first", "SortSelectedFirst"));
            if (box.Items.Count > 0) box.SelectedIndex = 0;
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
            foreach (var kv in _sourceWindows)
            {
                BindPresetControlsForSource(kv.Value);
            }
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
            foreach (var kv in _sourceWindows)
            {
                BindOrderControlsForSource(kv.Value);
            }
        }

        private void BindPresetControlsForSource(SourceWindowState state)
        {
            if (state == null || state.PresetsBox == null) return;
            state.PresetsBox.Items.Clear();
            for (int i = 0; i < _presetsBox.Items.Count; i++)
            {
                state.PresetsBox.Items.Add(_presetsBox.Items[i]);
            }
            if (_presetsBox.SelectedIndex >= 0 && _presetsBox.SelectedIndex < state.PresetsBox.Items.Count)
            {
                state.PresetsBox.SelectedIndex = _presetsBox.SelectedIndex;
            }
            if (state.LoadPresetButton != null) state.LoadPresetButton.Enabled = state.PresetsBox.Items.Count > 0;
            if (state.DeletePresetButton != null) state.DeletePresetButton.Enabled = state.PresetsBox.Items.Count > 0;
        }

        private void BindOrderControlsForSource(SourceWindowState state)
        {
            if (state == null || state.OrdersBox == null) return;
            state.OrdersBox.Items.Clear();
            for (int i = 0; i < _ordersBox.Items.Count; i++)
            {
                state.OrdersBox.Items.Add(_ordersBox.Items[i]);
            }
            if (_ordersBox.SelectedIndex >= 0 && _ordersBox.SelectedIndex < state.OrdersBox.Items.Count)
            {
                state.OrdersBox.SelectedIndex = _ordersBox.SelectedIndex;
            }
            if (state.LoadOrderButton != null) state.LoadOrderButton.Enabled = state.OrdersBox.Items.Count > 0;
            if (state.DeleteOrderButton != null) state.DeleteOrderButton.Enabled = state.OrdersBox.Items.Count > 0;
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
            SelectSortModeByKey(_sortModeBox, key);
        }

        private static void SelectSortModeByKey(ComboBox box, string key)
        {
            if (box == null || string.IsNullOrWhiteSpace(key)) return;
            for (int i = 0; i < box.Items.Count; i++)
            {
                var item = box.Items[i] as SortModeItem;
                if (item != null && string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedIndex = i;
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

        private void SetStatus(string text)
        {
            string value = text ?? string.Empty;
            _statusLabel.Text = value;
            foreach (var kv in _sourceWindows)
            {
                if (kv.Value != null && kv.Value.StatusLabel != null)
                {
                    kv.Value.StatusLabel.Text = value;
                }
            }
        }

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

            _browseButton.Enabled = !busy;
            _addDataButton.Enabled = !busy;
            _refreshButton.Enabled = !busy;
            _closeAllButton.Enabled = !busy;
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
            UpdateRecentDropDownWidth();
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
            UpdateRecentDropDownWidth();
            if (_recentFoldersBox.Items.Count > 0) _recentFoldersBox.SelectedIndex = 0;
            JsonHelper.SaveToFile(_recentFoldersFilePath, new RecentFoldersPayload { folders = folders });
        }

        private void UpdateRecentDropDownWidth()
        {
            try
            {
                int max = _recentFoldersBox.Width;
                using (Graphics g = _recentFoldersBox.CreateGraphics())
                {
                    for (int i = 0; i < _recentFoldersBox.Items.Count; i++)
                    {
                        string s = _recentFoldersBox.Items[i] == null ? string.Empty : _recentFoldersBox.Items[i].ToString();
                        int w = (int)Math.Ceiling(g.MeasureString(s, _recentFoldersBox.Font).Width) + 30;
                        if (w > max) max = w;
                    }
                }
                Rectangle wa = Screen.FromControl(this).WorkingArea;
                int maxAllowed = Math.Max(_recentFoldersBox.Width, wa.Width - 120);
                _recentFoldersBox.DropDownWidth = Math.Min(max, maxAllowed);
            }
            catch { }
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
                state.compare_overlay = _compareOverlayCheck.Checked;
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
                _compareOverlayCheck.Checked = state.compare_overlay.GetValueOrDefault(false);
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
            public bool? compare_overlay { get; set; }
            public string sort_mode { get; set; }
            public bool? selected_only { get; set; }
            public string channel_filter { get; set; }
            public bool? include_extra { get; set; }
            public string refrigerant { get; set; }
            public int? splitter_distance { get; set; }
            public List<string> checked_channels { get; set; }
        }

        private sealed class SourceWindowState
        {
            public string SourceRoot { get; set; }
            public Form Form { get; set; }
            public TextBox FilterBox { get; set; }
            public ComboBox SortModeBox { get; set; }
            public CheckBox SelectedOnlyCheck { get; set; }
            public Button SelectAllButton { get; set; }
            public Button ClearButton { get; set; }
            public TextBox PresetNameBox { get; set; }
            public Button SavePresetButton { get; set; }
            public ComboBox PresetsBox { get; set; }
            public Button LoadPresetButton { get; set; }
            public Button DeletePresetButton { get; set; }
            public TextBox OrderNameBox { get; set; }
            public Button SaveOrderButton { get; set; }
            public ComboBox OrdersBox { get; set; }
            public Button LoadOrderButton { get; set; }
            public Button DeleteOrderButton { get; set; }
            public Label StatusLabel { get; set; }
            public CheckedListBox List { get; set; }
            public List<ChannelItem> Items { get; set; }
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

        private static string GetSelectedSortKey(ComboBox box)
        {
            var item = box == null ? null : box.SelectedItem as SortModeItem;
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
