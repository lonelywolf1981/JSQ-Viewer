using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using JSQViewer.Application.Database;
using JSQViewer.Application.Workspace.Ports;

namespace JSQViewer.UI
{
    public sealed class OpenFromDatabaseForm : Form
    {
        private readonly IRecordingCatalog _recordingCatalog;
        private readonly int _maxSelectionCount;
        private readonly ComboBox _postBox;
        private readonly DateTimePicker _fromPicker;
        private readonly DateTimePicker _toPicker;
        private readonly ComboBox _experimentBox;
        private readonly TextBox _titleBox;
        private readonly DataGridView _recordingsGrid;
        private readonly FlowLayoutPanel _filtersPanel;
        private readonly Button _okButton;
        private readonly Font _activeRowFont;

        public IList<string> SelectedSources { get; private set; }

        public OpenFromDatabaseForm(IRecordingCatalog recordingCatalog, int maxSelectionCount)
        {
            _recordingCatalog = recordingCatalog ?? throw new ArgumentNullException(nameof(recordingCatalog));
            _maxSelectionCount = Math.Max(1, maxSelectionCount);
            SelectedSources = new List<string>();

            Text = Loc.Get("OpenFromDatabaseTitle");
            Width = 1120;
            Height = 680;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            _activeRowFont = new Font(Font, FontStyle.Bold);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            _filtersPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(_filtersPanel, 0, 0);

            _postBox = AddFilterCombo(_filtersPanel, Loc.Get("RecordingFilterPost"), 130);
            _fromPicker = AddDateFilter(_filtersPanel, Loc.Get("RecordingFilterFrom"));
            _toPicker = AddDateFilter(_filtersPanel, Loc.Get("RecordingFilterTo"));
            _experimentBox = AddFilterCombo(_filtersPanel, Loc.Get("RecordingFilterExperiment"), 160);
            _titleBox = AddFilterTextBox(_filtersPanel, Loc.Get("RecordingFilterTitle"), 180);

            var applyButton = new Button
            {
                Text = Loc.Get("RecordingFilterApply"),
                AutoSize = true,
                Margin = new Padding(8, 18, 3, 3)
            };
            applyButton.Click += async delegate { await RefreshRecordingsAsync(); };
            _filtersPanel.Controls.Add(applyButton);

            _recordingsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                MultiSelect = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false
            };
            AddColumns();
            _recordingsGrid.CellDoubleClick += RecordingsGridOnCellDoubleClick;
            root.Controls.Add(_recordingsGrid, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, 8, 0, 0)
            };
            root.Controls.Add(buttons, 0, 2);

            var cancelButton = new Button
            {
                Text = Loc.Get("Cancel"),
                AutoSize = true,
                DialogResult = DialogResult.Cancel
            };
            buttons.Controls.Add(cancelButton);

            _okButton = new Button { Text = Loc.Get("Ok"), AutoSize = true };
            _okButton.Click += delegate { AcceptSelection(); };
            buttons.Controls.Add(_okButton);

            AcceptButton = _okButton;
            CancelButton = cancelButton;
            Shown += async delegate { await LoadCatalogAsync(); };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _activeRowFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private async Task LoadCatalogAsync()
        {
            SetCatalogBusy(true);
            try
            {
                CatalogLoadResult result = await Task.Run(() => new CatalogLoadResult
                {
                    Posts = _recordingCatalog.ListPosts(),
                    ExperimentTypes = _recordingCatalog.ListExperimentTypes(),
                    Recordings = _recordingCatalog.List(new RecordingCatalogFilter())
                });
                if (IsDisposed || Disposing)
                {
                    return;
                }

                PopulateFilter(_postBox, result.Posts);
                PopulateFilter(_experimentBox, result.ExperimentTypes);
                BindRecordings(result.Recordings ?? new List<RecordingSummaryItem>());
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                {
                    ShowCatalogError(ex);
                }
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    SetCatalogBusy(false);
                }
            }
        }

        private async Task RefreshRecordingsAsync()
        {
            RecordingCatalogFilter filter = ReadFilter();
            SetCatalogBusy(true);
            try
            {
                IList<RecordingSummaryItem> recordings = await Task.Run(() => _recordingCatalog.List(filter));
                if (IsDisposed || Disposing)
                {
                    return;
                }

                BindRecordings(recordings ?? new List<RecordingSummaryItem>());
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                {
                    ShowCatalogError(ex);
                }
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    SetCatalogBusy(false);
                }
            }
        }

        private RecordingCatalogFilter ReadFilter()
        {
            return new RecordingCatalogFilter
            {
                PostId = SelectedFilterValue(_postBox),
                From = _fromPicker.Checked ? _fromPicker.Value.Date : (DateTime?)null,
                To = _toPicker.Checked ? _toPicker.Value.Date.AddDays(1) : (DateTime?)null,
                ExperimentType = SelectedFilterValue(_experimentBox),
                TitleContains = _titleBox.Text.Trim()
            };
        }

        private void BindRecordings(IList<RecordingSummaryItem> recordings)
        {
            _recordingsGrid.Rows.Clear();
            foreach (RecordingSummaryItem item in recordings)
            {
                if (item == null)
                {
                    continue;
                }

                int index = _recordingsGrid.Rows.Add(
                    item.StartedAt.HasValue ? item.StartedAt.Value.ToString("g") : string.Empty,
                    item.PostId ?? string.Empty,
                    item.Title ?? string.Empty,
                    item.EquipmentModel ?? string.Empty,
                    item.ExperimentType ?? string.Empty,
                    item.DurationHours.ToString("0.##"),
                    item.IsActive ? Loc.Get("RecordingStatusActive") : Loc.Get("RecordingStatusStopped"));
                DataGridViewRow row = _recordingsGrid.Rows[index];
                row.Tag = item;
                if (item.IsActive)
                {
                    row.DefaultCellStyle.Font = _activeRowFont;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        cell.ToolTipText = Loc.Get("RecordingLiveUpdating");
                    }
                }
            }
        }

        private void RecordingsGridOnCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                _recordingsGrid.ClearSelection();
                _recordingsGrid.Rows[e.RowIndex].Selected = true;
                AcceptSelection();
            }
        }

        private void AcceptSelection()
        {
            List<RecordingSummaryItem> selected = _recordingsGrid.SelectedRows
                .Cast<DataGridViewRow>()
                .OrderBy(row => row.Index)
                .Select(row => row.Tag as RecordingSummaryItem)
                .Where(item => item != null)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(this, Loc.Get("RecordingSelectAtLeastOne"), Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (selected.Count > _maxSelectionCount)
            {
                MessageBox.Show(this,
                    string.Format(Loc.Get("RecordingTooManySelected"), _maxSelectionCount),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedSources = selected.Select(item => item.ToSourceString()).ToList();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ShowCatalogError(Exception ex)
        {
            MessageBox.Show(this,
                Loc.Get("RecordingConnectionLost") + Environment.NewLine + ex.Message,
                Loc.Get("OpenFromDatabaseTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void SetCatalogBusy(bool busy)
        {
            _filtersPanel.Enabled = !busy;
            _recordingsGrid.Enabled = !busy;
            _okButton.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void AddColumns()
        {
            _recordingsGrid.Columns.Add(CreateColumn(Loc.Get("RecordingColumnStarted"), 135));
            _recordingsGrid.Columns.Add(CreateColumn(Loc.Get("RecordingColumnPost"), 90));
            _recordingsGrid.Columns.Add(CreateFillColumn(Loc.Get("RecordingColumnTitle"), 180));
            _recordingsGrid.Columns.Add(CreateColumn(Loc.Get("RecordingColumnModel"), 130));
            _recordingsGrid.Columns.Add(CreateColumn(Loc.Get("RecordingColumnExperiment"), 140));
            _recordingsGrid.Columns.Add(CreateColumn(Loc.Get("RecordingColumnDuration"), 105));
            _recordingsGrid.Columns.Add(CreateColumn(Loc.Get("RecordingColumnStatus"), 105));
        }

        private static DataGridViewTextBoxColumn CreateColumn(string header, int width)
        {
            return new DataGridViewTextBoxColumn { HeaderText = header, Width = width, SortMode = DataGridViewColumnSortMode.Automatic };
        }

        private static DataGridViewTextBoxColumn CreateFillColumn(string header, int minimumWidth)
        {
            return new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                MinimumWidth = minimumWidth,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
        }

        private static ComboBox AddFilterCombo(FlowLayoutPanel parent, string caption, int width)
        {
            var panel = CreateFilterPanel(parent, caption, width);
            var box = new ComboBox
            {
                Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Top = 18
            };
            panel.Controls.Add(box);
            return box;
        }

        private static DateTimePicker AddDateFilter(FlowLayoutPanel parent, string caption)
        {
            var panel = CreateFilterPanel(parent, caption, 120);
            var picker = new DateTimePicker
            {
                Width = 120,
                Format = DateTimePickerFormat.Short,
                ShowCheckBox = true,
                Checked = false,
                Top = 18
            };
            panel.Controls.Add(picker);
            return picker;
        }

        private static TextBox AddFilterTextBox(FlowLayoutPanel parent, string caption, int width)
        {
            var panel = CreateFilterPanel(parent, caption, width);
            var box = new TextBox { Width = width, Top = 18 };
            panel.Controls.Add(box);
            return box;
        }

        private static Panel CreateFilterPanel(FlowLayoutPanel parent, string caption, int width)
        {
            var panel = new Panel { Width = width, Height = 48, Margin = new Padding(3) };
            panel.Controls.Add(new Label { Text = caption, AutoSize = true, Left = 0, Top = 0 });
            parent.Controls.Add(panel);
            return panel;
        }

        private static void PopulateFilter(ComboBox box, IList<string> values)
        {
            box.BeginUpdate();
            try
            {
                box.Items.Clear();
                box.Items.Add(Loc.Get("RecordingFilterAny"));
                if (values != null)
                {
                    foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
                    {
                        box.Items.Add(value);
                    }
                }
                box.SelectedIndex = 0;
            }
            finally
            {
                box.EndUpdate();
            }
        }

        private static string SelectedFilterValue(ComboBox box)
        {
            return box.SelectedIndex > 0 ? Convert.ToString(box.SelectedItem) : null;
        }

        private sealed class CatalogLoadResult
        {
            public IList<string> Posts { get; set; }

            public IList<string> ExperimentTypes { get; set; }

            public IList<RecordingSummaryItem> Recordings { get; set; }
        }
    }
}
