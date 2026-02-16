using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using LeMuReViewer.Settings;

namespace LeMuReViewer.UI
{
    public sealed class SettingsDialog : Form
    {
        private readonly NumericUpDown _rowThreshold;
        private readonly TextBox _rowColor;
        private readonly NumericUpDown _rowIntensity;
        private readonly TextBox _dischargeThreshold;
        private readonly TextBox _dischargeColor;
        private readonly TextBox _suctionThreshold;
        private readonly TextBox _suctionColor;
        private readonly DataGridView _scalesGrid;
        private readonly Button _okButton;
        private readonly Button _cancelButton;

        public ViewerSettingsModel Result { get; private set; }

        public SettingsDialog(ViewerSettingsModel source)
        {
            Result = Clone(source ?? ViewerSettingsModel.CreateDefault());

            Text = "Export Style Settings";
            Width = 900;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var rowMarkGroup = new GroupBox();
            rowMarkGroup.Text = "Row Mark";
            rowMarkGroup.Dock = DockStyle.Fill;
            root.Controls.Add(rowMarkGroup, 0, 0);

            var rowMarkPanel = new FlowLayoutPanel();
            rowMarkPanel.Dock = DockStyle.Fill;
            rowMarkPanel.AutoSize = true;
            rowMarkPanel.WrapContents = false;
            rowMarkGroup.Controls.Add(rowMarkPanel);

            rowMarkPanel.Controls.Add(NewLabel("T threshold:"));
            _rowThreshold = new NumericUpDown();
            _rowThreshold.DecimalPlaces = 2;
            _rowThreshold.Minimum = -100000;
            _rowThreshold.Maximum = 100000;
            _rowThreshold.Value = ToDecimal(Result.row_mark.threshold_T, 150m);
            rowMarkPanel.Controls.Add(_rowThreshold);

            rowMarkPanel.Controls.Add(NewLabel("Color:"));
            _rowColor = NewTextBox(Result.row_mark.color, 90);
            rowMarkPanel.Controls.Add(_rowColor);

            rowMarkPanel.Controls.Add(NewLabel("Intensity:"));
            _rowIntensity = new NumericUpDown();
            _rowIntensity.Minimum = 0;
            _rowIntensity.Maximum = 100;
            _rowIntensity.Value = Clamp(Result.row_mark.intensity, 0, 100);
            rowMarkPanel.Controls.Add(_rowIntensity);

            var marksGroup = new GroupBox();
            marksGroup.Text = "Discharge / Suction";
            marksGroup.Dock = DockStyle.Fill;
            root.Controls.Add(marksGroup, 0, 1);

            var marksPanel = new FlowLayoutPanel();
            marksPanel.Dock = DockStyle.Fill;
            marksPanel.AutoSize = true;
            marksPanel.WrapContents = false;
            marksGroup.Controls.Add(marksPanel);

            marksPanel.Controls.Add(NewLabel("Discharge threshold:"));
            _dischargeThreshold = NewTextBox(FormatNullable(Result.discharge_mark.threshold), 70);
            marksPanel.Controls.Add(_dischargeThreshold);
            marksPanel.Controls.Add(NewLabel("Color:"));
            _dischargeColor = NewTextBox(Result.discharge_mark.color, 90);
            marksPanel.Controls.Add(_dischargeColor);

            marksPanel.Controls.Add(NewLabel("Suction threshold:"));
            _suctionThreshold = NewTextBox(FormatNullable(Result.suction_mark.threshold), 70);
            marksPanel.Controls.Add(_suctionThreshold);
            marksPanel.Controls.Add(NewLabel("Color:"));
            _suctionColor = NewTextBox(Result.suction_mark.color, 90);
            marksPanel.Controls.Add(_suctionColor);

            var scalesGroup = new GroupBox();
            scalesGroup.Text = "Scales W / X / Y";
            scalesGroup.Dock = DockStyle.Fill;
            root.Controls.Add(scalesGroup, 0, 2);

            _scalesGrid = new DataGridView();
            _scalesGrid.Dock = DockStyle.Fill;
            _scalesGrid.AllowUserToAddRows = false;
            _scalesGrid.AllowUserToDeleteRows = false;
            _scalesGrid.RowHeadersVisible = false;
            _scalesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _scalesGrid.Columns.Add("key", "Scale");
            _scalesGrid.Columns.Add("min", "Min");
            _scalesGrid.Columns.Add("opt", "Opt");
            _scalesGrid.Columns.Add("max", "Max");
            _scalesGrid.Columns.Add("cmin", "Color Min");
            _scalesGrid.Columns.Add("copt", "Color Opt");
            _scalesGrid.Columns.Add("cmax", "Color Max");
            _scalesGrid.Columns[0].ReadOnly = true;
            scalesGroup.Controls.Add(_scalesGrid);

            AddScaleRow("W");
            AddScaleRow("X");
            AddScaleRow("Y");

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            root.Controls.Add(buttons, 0, 3);

            _okButton = new Button();
            _okButton.Text = "Save";
            _okButton.Click += OkButtonOnClick;
            buttons.Controls.Add(_okButton);

            _cancelButton = new Button();
            _cancelButton.Text = "Cancel";
            _cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(_cancelButton);
        }

        private static Label NewLabel(string text)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Padding = new Padding(6, 7, 2, 0);
            return l;
        }

        private static TextBox NewTextBox(string text, int width)
        {
            var tb = new TextBox();
            tb.Width = width;
            tb.Text = text ?? string.Empty;
            return tb;
        }

        private void AddScaleRow(string key)
        {
            ScaleSettings s = Result.scales.ContainsKey(key) ? Result.scales[key] : ScaleSettings.CreateDefault();
            _scalesGrid.Rows.Add(
                key,
                Fmt(s.min),
                Fmt(s.opt),
                Fmt(s.max),
                Safe(s.colors.min, "#1CBCF2"),
                Safe(s.colors.opt, "#00FF00"),
                Safe(s.colors.max, "#F3919B"));
        }

        private void OkButtonOnClick(object sender, EventArgs e)
        {
            ViewerSettingsModel updated = ViewerSettingsModel.CreateDefault();
            updated.row_mark.threshold_T = (double)_rowThreshold.Value;
            updated.row_mark.color = NormalizeHex(_rowColor.Text, "#EAD706");
            updated.row_mark.intensity = (int)_rowIntensity.Value;

            double? dThr;
            if (!TryParseNullableDouble(_dischargeThreshold.Text, out dThr))
            {
                MessageBox.Show(this, "Invalid discharge threshold.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            updated.discharge_mark.threshold = dThr;
            updated.discharge_mark.color = NormalizeHex(_dischargeColor.Text, "#FFC000");

            double? sThr;
            if (!TryParseNullableDouble(_suctionThreshold.Text, out sThr))
            {
                MessageBox.Show(this, "Invalid suction threshold.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            updated.suction_mark.threshold = sThr;
            updated.suction_mark.color = NormalizeHex(_suctionColor.Text, "#00B0F0");

            updated.scales = new Dictionary<string, ScaleSettings>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _scalesGrid.Rows.Count; i++)
            {
                DataGridViewRow row = _scalesGrid.Rows[i];
                string key = Convert.ToString(row.Cells[0].Value, CultureInfo.InvariantCulture);
                double min, opt, max;
                if (!TryParseDouble(Convert.ToString(row.Cells[1].Value, CultureInfo.InvariantCulture), out min)
                    || !TryParseDouble(Convert.ToString(row.Cells[2].Value, CultureInfo.InvariantCulture), out opt)
                    || !TryParseDouble(Convert.ToString(row.Cells[3].Value, CultureInfo.InvariantCulture), out max))
                {
                    MessageBox.Show(this, "Invalid numeric value in scales.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (min >= opt) min = opt - 1;
                if (opt >= max) max = opt + 1;

                var ss = ScaleSettings.CreateDefault();
                ss.min = min;
                ss.opt = opt;
                ss.max = max;
                ss.colors.min = NormalizeHex(Convert.ToString(row.Cells[4].Value, CultureInfo.InvariantCulture), "#1CBCF2");
                ss.colors.opt = NormalizeHex(Convert.ToString(row.Cells[5].Value, CultureInfo.InvariantCulture), "#00FF00");
                ss.colors.max = NormalizeHex(Convert.ToString(row.Cells[6].Value, CultureInfo.InvariantCulture), "#F3919B");
                updated.scales[key] = ss;
            }

            Result = updated;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string NormalizeHex(string value, string fallback)
        {
            string s = (value ?? string.Empty).Trim();
            if (!s.StartsWith("#")) s = "#" + s;
            if (s.Length != 7) return fallback;
            for (int i = 1; i < s.Length; i++)
            {
                char ch = s[i];
                bool ok = (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');
                if (!ok) return fallback;
            }
            return s.ToUpperInvariant();
        }

        private static bool TryParseDouble(string s, out double value)
        {
            return double.TryParse((s ?? string.Empty).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseNullableDouble(string s, out double? value)
        {
            string t = (s ?? string.Empty).Trim();
            if (t.Length == 0)
            {
                value = null;
                return true;
            }
            double v;
            bool ok = TryParseDouble(t, out v);
            value = ok ? (double?)v : null;
            return ok;
        }

        private static string FormatNullable(double? v)
        {
            return v.HasValue ? v.Value.ToString("G", CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string Fmt(double v)
        {
            return v.ToString("G", CultureInfo.InvariantCulture);
        }

        private static decimal ToDecimal(double value, decimal fallback)
        {
            try { return (decimal)value; } catch { return fallback; }
        }

        private static decimal Clamp(int value, int min, int max)
        {
            if (value < min) value = min;
            if (value > max) value = max;
            return value;
        }

        private static ViewerSettingsModel Clone(ViewerSettingsModel src)
        {
            ViewerSettingsModel def = ViewerSettingsModel.CreateDefault();
            ViewerSettingsModel c = ViewerSettingsModel.CreateDefault();
            RowMarkSettings srcRow = src.row_mark ?? def.row_mark;
            c.row_mark.threshold_T = srcRow.threshold_T;
            c.row_mark.color = srcRow.color;
            c.row_mark.intensity = srcRow.intensity;
            ThresholdColorSettings srcDis = src.discharge_mark ?? def.discharge_mark;
            c.discharge_mark.threshold = srcDis.threshold;
            c.discharge_mark.color = srcDis.color;
            ThresholdColorSettings srcSuc = src.suction_mark ?? def.suction_mark;
            c.suction_mark.threshold = srcSuc.threshold;
            c.suction_mark.color = srcSuc.color;
            c.scales = new Dictionary<string, ScaleSettings>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ScaleSettings> srcScales = src.scales ?? def.scales;
            foreach (string key in srcScales.Keys)
            {
                ScaleSettings s = srcScales[key] ?? ScaleSettings.CreateDefault();
                var n = ScaleSettings.CreateDefault();
                n.min = s.min;
                n.opt = s.opt;
                n.max = s.max;
                ScaleColors sc = s.colors ?? ScaleSettings.CreateDefault().colors;
                n.colors.min = sc.min;
                n.colors.opt = sc.opt;
                n.colors.max = sc.max;
                c.scales[key] = n;
            }
            return c;
        }
    }
}
