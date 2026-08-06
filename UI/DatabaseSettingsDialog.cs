using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Infrastructure.Database;

namespace JSQViewer.UI
{
    public sealed class DatabaseSettingsDialog : Form
    {
        private readonly IDatabaseSettingsRepository _settingsRepository;
        private readonly NpgsqlConnectionFactory _connectionFactory;
        private readonly TextBox _hostBox;
        private readonly NumericUpDown _portBox;
        private readonly TextBox _databaseBox;
        private readonly TextBox _userBox;
        private readonly TextBox _passwordBox;
        private readonly NumericUpDown _refreshIntervalBox;
        private readonly Label _statusLabel;
        private readonly Button _testButton;
        private readonly Button _okButton;
        private DatabaseConnectionSettings _loadedSettings;

        public DatabaseSettingsDialog(
            IDatabaseSettingsRepository settingsRepository,
            NpgsqlConnectionFactory connectionFactory)
        {
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

            Text = Loc.Get("DatabaseSettingsTitle");
            Width = 470;
            Height = 355;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                Padding = new Padding(12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            _hostBox = AddTextEditor(root, 0, Loc.Get("DatabaseHost"));
            _portBox = AddNumericEditor(root, 1, Loc.Get("DatabasePort"), 1, 65535);
            _databaseBox = AddTextEditor(root, 2, Loc.Get("DatabaseName"));
            _userBox = AddTextEditor(root, 3, Loc.Get("DatabaseUser"));
            _passwordBox = AddTextEditor(root, 4, Loc.Get("DatabasePassword"));
            _passwordBox.UseSystemPasswordChar = true;
            _refreshIntervalBox = AddNumericEditor(root, 5, Loc.Get("DatabaseRefreshInterval"), 5, 3600);

            _testButton = new Button
            {
                Text = Loc.Get("DatabaseTestConnection"),
                AutoSize = true,
                Margin = new Padding(3, 8, 3, 3)
            };
            _testButton.Click += async delegate { await TestConnectionAsync(); };
            root.Controls.Add(_testButton, 0, 6);

            _statusLabel = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 12, 3, 3)
            };
            root.Controls.Add(_statusLabel, 1, 6);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, 12, 0, 0)
            };
            root.SetColumnSpan(buttons, 2);
            root.Controls.Add(buttons, 0, 8);

            var cancelButton = new Button
            {
                Text = Loc.Get("Cancel"),
                AutoSize = true,
                DialogResult = DialogResult.Cancel
            };
            buttons.Controls.Add(cancelButton);

            _okButton = new Button
            {
                Text = Loc.Get("Ok"),
                AutoSize = true
            };
            _okButton.Click += OkButtonOnClick;
            buttons.Controls.Add(_okButton);

            AcceptButton = _okButton;
            CancelButton = cancelButton;

            LoadSettings();
        }

        private void LoadSettings()
        {
            _loadedSettings = _settingsRepository.Load() ?? DatabaseConnectionSettings.CreateDefault();
            _hostBox.Text = _loadedSettings.host ?? string.Empty;
            _portBox.Value = Clamp(_loadedSettings.port, _portBox.Minimum, _portBox.Maximum);
            _databaseBox.Text = _loadedSettings.database ?? string.Empty;
            _userBox.Text = _loadedSettings.username ?? string.Empty;
            _passwordBox.Text = _settingsRepository.LoadPassword() ?? string.Empty;
            _refreshIntervalBox.Value = Clamp(
                _loadedSettings.refresh_interval_seconds,
                _refreshIntervalBox.Minimum,
                _refreshIntervalBox.Maximum);
        }

        private async Task TestConnectionAsync()
        {
            DatabaseConnectionSettings settings = ReadFromEditors();
            string password = _passwordBox.Text;
            SetTestBusy(true);
            try
            {
                string error = await Task.Run(() => _connectionFactory.TestConnection(settings, password));
                if (IsDisposed || Disposing)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(error))
                {
                    ShowStatus(Loc.Get("DatabaseConnectionOk"), Color.Green);
                    return;
                }

                string key = IsAuthenticationError(error) ? "DatabaseAuthFailed" : "DatabaseConnectionFailed";
                ShowStatus(Loc.Get(key) + Environment.NewLine + error, Color.Firebrick);
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                {
                    ShowStatus(Loc.Get("DatabaseConnectionFailed") + Environment.NewLine + ex.Message, Color.Firebrick);
                }
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    SetTestBusy(false);
                }
            }
        }

        private void OkButtonOnClick(object sender, EventArgs e)
        {
            if (!_settingsRepository.SavePassword(ReadFromEditors(), _passwordBox.Text))
            {
                ShowStatus(Loc.Get("DatabaseConnectionFailed"), Color.Firebrick);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private DatabaseConnectionSettings ReadFromEditors()
        {
            DatabaseConnectionSettings baseline = _loadedSettings ?? DatabaseConnectionSettings.CreateDefault();
            return new DatabaseConnectionSettings
            {
                host = _hostBox.Text.Trim(),
                port = decimal.ToInt32(_portBox.Value),
                database = _databaseBox.Text.Trim(),
                username = _userBox.Text.Trim(),
                password_protected = baseline.password_protected ?? string.Empty,
                refresh_interval_seconds = decimal.ToInt32(_refreshIntervalBox.Value),
                connect_timeout_seconds = baseline.connect_timeout_seconds,
                command_timeout_seconds = baseline.command_timeout_seconds
            };
        }

        private void ShowStatus(string text, Color color)
        {
            _statusLabel.Text = text ?? string.Empty;
            _statusLabel.ForeColor = color;
        }

        private void SetTestBusy(bool busy)
        {
            _hostBox.Enabled = !busy;
            _portBox.Enabled = !busy;
            _databaseBox.Enabled = !busy;
            _userBox.Enabled = !busy;
            _passwordBox.Enabled = !busy;
            _refreshIntervalBox.Enabled = !busy;
            _testButton.Enabled = !busy;
            _okButton.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private static bool IsAuthenticationError(string error)
        {
            return error.IndexOf("28P01", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static TextBox AddTextEditor(TableLayoutPanel root, int row, string caption)
        {
            AddLabel(root, row, caption);
            var editor = new TextBox { Dock = DockStyle.Fill };
            root.Controls.Add(editor, 1, row);
            return editor;
        }

        private static NumericUpDown AddNumericEditor(
            TableLayoutPanel root,
            int row,
            string caption,
            decimal minimum,
            decimal maximum)
        {
            AddLabel(root, row, caption);
            var editor = new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                Width = 120,
                Anchor = AnchorStyles.Left
            };
            root.Controls.Add(editor, 1, row);
            return editor;
        }

        private static void AddLabel(TableLayoutPanel root, int row, string caption)
        {
            root.Controls.Add(new Label
            {
                Text = caption,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 12, 6)
            }, 0, row);
        }

        private static decimal Clamp(int value, decimal minimum, decimal maximum)
        {
            return Math.Min(maximum, Math.Max(minimum, value));
        }
    }
}
