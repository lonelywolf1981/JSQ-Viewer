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
        private readonly Panel _rowColorPanel;
        private readonly NumericUpDown _rowIntensity;
        private readonly TextBox _dischargeThreshold;
        private readonly Panel _dischargeColorPanel;
        private readonly TextBox _suctionThreshold;
        private readonly Panel _suctionColorPanel;
        private readonly DataGridView _scalesGrid;
        private readonly Button _okButton;
        private readonly Button _cancelButton;

        private string _rowColor;
        private string _dischargeColor;
        private string _suctionColor;

        public ViewerSettingsModel Result { get; private set; }

        public SettingsDialog(ViewerSettingsModel source)
        {
            Result = Clone(source ?? ViewerSettingsModel.CreateDefault());

            Text = Loc.Get("StylesTitle");
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

            // Row mark group
            var rowMarkGroup = new GroupBox();
            rowMarkGroup.Text = Loc.Get("RowMark");
            rowMarkGroup.Dock = DockStyle.Fill;
            root.Controls.Add(rowMarkGroup, 0, 0);

            var rowMarkPanel = new FlowLayoutPanel();
            rowMarkPanel.Dock = DockStyle.Fill;
            rowMarkPanel.AutoSize = true;
            rowMarkPanel.WrapContents = false;
            rowMarkGroup.Controls.Add(rowMarkPanel);

            rowMarkPanel.Controls.Add(NewLabel(Loc.Get("TThreshold")));
            _rowThreshold = new NumericUpDown();
            _rowThreshold.DecimalPlaces = 2;
            _rowThreshold.Minimum = -100000;
            _rowThreshold.Maximum = 100000;
            _rowThreshold.Value = ToDecimal(Result.row_mark.threshold_T, 150m);
            rowMarkPanel.Controls.Add(_rowThreshold);

            rowMarkPanel.Controls.Add(NewLabel(Loc.Get("Color")));
            _rowColor = Result.row_mark.color ?? "#EAD706";
            _rowColorPanel = CreateColorButton(_rowColor, c => _rowColor = c);
            rowMarkPanel.Controls.Add(_rowColorPanel);

            rowMarkPanel.Controls.Add(NewLabel(Loc.Get("Intensity")));
            _rowIntensity = new NumericUpDown();
            _rowIntensity.Minimum = 0;
            _rowIntensity.Maximum = 100;
            _rowIntensity.Value = Clamp(Result.row_mark.intensity, 0, 100);
            rowMarkPanel.Controls.Add(_rowIntensity);

            // Discharge / Suction group
            var marksGroup = new GroupBox();
            marksGroup.Text = Loc.Get("DischargeSuction");
            marksGroup.Dock = DockStyle.Fill;
            root.Controls.Add(marksGroup, 0, 1);

            var marksPanel = new FlowLayoutPanel();
            marksPanel.Dock = DockStyle.Fill;
            marksPanel.AutoSize = true;
            marksPanel.WrapContents = false;
            marksGroup.Controls.Add(marksPanel);

            marksPanel.Controls.Add(NewLabel(Loc.Get("DischargeThreshold")));
            _dischargeThreshold = NewTextBox(FormatNullable(Result.discharge_mark.threshold), 70);
            marksPanel.Controls.Add(_dischargeThreshold);
            marksPanel.Controls.Add(NewLabel(Loc.Get("Color")));
            _dischargeColor = Result.discharge_mark.color ?? "#FFC000";
            _dischargeColorPanel = CreateColorButton(_dischargeColor, c => _dischargeColor = c);
            marksPanel.Controls.Add(_dischargeColorPanel);

            marksPanel.Controls.Add(NewLabel(Loc.Get("SuctionThreshold")));
            _suctionThreshold = NewTextBox(FormatNullable(Result.suction_mark.threshold), 70);
            marksPanel.Controls.Add(_suctionThreshold);
            marksPanel.Controls.Add(NewLabel(Loc.Get("Color")));
            _suctionColor = Result.suction_mark.color ?? "#00B0F0";
            _suctionColorPanel = CreateColorButton(_suctionColor, c => _suctionColor = c);
            marksPanel.Controls.Add(_suctionColorPanel);

            // Scales group
            var scalesGroup = new GroupBox();
            scalesGroup.Text = Loc.Get("ScalesWXY");
            scalesGroup.Dock = DockStyle.Fill;
            root.Controls.Add(scalesGroup, 0, 2);

            _scalesGrid = new DataGridView();
            _scalesGrid.Dock = DockStyle.Fill;
            _scalesGrid.AllowUserToAddRows = false;
            _scalesGrid.AllowUserToDeleteRows = false;
            _scalesGrid.RowHeadersVisible = false;
            _scalesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            _scalesGrid.Columns.Add("key", Loc.Get("Scale"));
            _scalesGrid.Columns.Add("min", Loc.Get("Min"));
            _scalesGrid.Columns.Add("opt", Loc.Get("Opt"));
            _scalesGrid.Columns.Add("max", Loc.Get("Max"));

            var cminCol = new DataGridViewButtonColumn();
            cminCol.Name = "cmin"; cminCol.HeaderText = Loc.Get("ColorMin"); cminCol.FlatStyle = FlatStyle.Flat; cminCol.UseColumnTextForButtonValue = false;
            _scalesGrid.Columns.Add(cminCol);
            var coptCol = new DataGridViewButtonColumn();
            coptCol.Name = "copt"; coptCol.HeaderText = Loc.Get("ColorOpt"); coptCol.FlatStyle = FlatStyle.Flat; coptCol.UseColumnTextForButtonValue = false;
            _scalesGrid.Columns.Add(coptCol);
            var cmaxCol = new DataGridViewButtonColumn();
            cmaxCol.Name = "cmax"; cmaxCol.HeaderText = Loc.Get("ColorMax"); cmaxCol.FlatStyle = FlatStyle.Flat; cmaxCol.UseColumnTextForButtonValue = false;
            _scalesGrid.Columns.Add(cmaxCol);

            _scalesGrid.Columns[0].ReadOnly = true;
            _scalesGrid.Columns[1].ReadOnly = false;
            _scalesGrid.Columns[2].ReadOnly = false;
            _scalesGrid.Columns[3].ReadOnly = false;

            scalesGroup.Controls.Add(_scalesGrid);

            AddScaleRow("W");
            AddScaleRow("X");
            AddScaleRow("Y");

            _scalesGrid.CellPainting += ScalesGridOnCellPainting;
            _scalesGrid.CellClick += ScalesGridOnCellClick;

            // Buttons
            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            root.Controls.Add(buttons, 0, 3);

            _okButton = new Button();
            _okButton.Text = Loc.Get("Save");
            _okButton.Click += OkButtonOnClick;
            buttons.Controls.Add(_okButton);

            _cancelButton = new Button();
            _cancelButton.Text = Loc.Get("Cancel");
            _cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(_cancelButton);
        }

        private static Panel CreateColorButton(string hexColor, Action<string> onColorChanged)
        {
            var panel = new Panel();
            panel.Width = 60;
            panel.Height = 24;
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.BackColor = HexToColor(hexColor);
            panel.Cursor = Cursors.Hand;
            panel.Click += delegate
            {
                using (var dlg = new ColorDialog())
                {
                    dlg.Color = panel.BackColor;
                    dlg.FullOpen = true;
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        panel.BackColor = dlg.Color;
                        onColorChanged(ColorToHex(dlg.Color));
                    }
                }
            };
            return panel;
        }

        private static Color HexToColor(string hex)
        {
            string s = (hex ?? string.Empty).Trim().TrimStart('#');
            if (s.Length == 6)
            {
                try
                {
                    int r = int.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    int g = int.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    int b = int.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return Color.FromArgb(r, g, b);
                }
                catch { }
            }
            return Color.White;
        }

        private static string ColorToHex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
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
            int rowIdx = _scalesGrid.Rows.Add(
                key,
                Fmt(s.min),
                Fmt(s.opt),
                Fmt(s.max),
                "",
                "",
                "");
            _scalesGrid.Rows[rowIdx].Tag = new string[]
            {
                Safe(s.colors.min, "#1CBCF2"),
                Safe(s.colors.opt, "#00FF00"),
                Safe(s.colors.max, "#F3919B")
            };
        }

        private void ScalesGridOnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 4 || e.ColumnIndex > 6)
                return;

            var colors = _scalesGrid.Rows[e.RowIndex].Tag as string[];
            if (colors == null) return;

            int ci = e.ColumnIndex - 4;
            Color c = HexToColor(colors[ci]);

            e.PaintBackground(e.ClipBounds, true);
            using (var brush = new SolidBrush(c))
            {
                var rect = e.CellBounds;
                rect.Inflate(-4, -3);
                e.Graphics.FillRectangle(brush, rect);
                e.Graphics.DrawRectangle(Pens.Gray, rect);
            }
            e.Handled = true;
        }

        private void ScalesGridOnCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 4 || e.ColumnIndex > 6)
                return;

            var colors = _scalesGrid.Rows[e.RowIndex].Tag as string[];
            if (colors == null) return;

            int ci = e.ColumnIndex - 4;
            using (var dlg = new ColorDialog())
            {
                dlg.Color = HexToColor(colors[ci]);
                dlg.FullOpen = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    colors[ci] = ColorToHex(dlg.Color);
                    _scalesGrid.InvalidateCell(e.ColumnIndex, e.RowIndex);
                }
            }
        }

        private void OkButtonOnClick(object sender, EventArgs e)
        {
            ViewerSettingsModel updated = ViewerSettingsModel.CreateDefault();
            updated.row_mark.threshold_T = (double)_rowThreshold.Value;
            updated.row_mark.color = NormalizeHex(_rowColor, "#EAD706");
            updated.row_mark.intensity = (int)_rowIntensity.Value;

            double? dThr;
            if (!TryParseNullableDouble(_dischargeThreshold.Text, out dThr))
            {
                MessageBox.Show(this, Loc.Get("InvalidDischargeThreshold"), Loc.Get("Settings"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            updated.discharge_mark.threshold = dThr;
            updated.discharge_mark.color = NormalizeHex(_dischargeColor, "#FFC000");

            double? sThr;
            if (!TryParseNullableDouble(_suctionThreshold.Text, out sThr))
            {
                MessageBox.Show(this, Loc.Get("InvalidSuctionThreshold"), Loc.Get("Settings"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            updated.suction_mark.threshold = sThr;
            updated.suction_mark.color = NormalizeHex(_suctionColor, "#00B0F0");

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
                    MessageBox.Show(this, Loc.Get("InvalidScaleValues"), Loc.Get("Settings"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (min >= opt) min = opt - 1;
                if (opt >= max) max = opt + 1;

                var colors = row.Tag as string[];
                var ss = ScaleSettings.CreateDefault();
                ss.min = min;
                ss.opt = opt;
                ss.max = max;
                ss.colors.min = NormalizeHex(colors != null ? colors[0] : "#1CBCF2", "#1CBCF2");
                ss.colors.opt = NormalizeHex(colors != null ? colors[1] : "#00FF00", "#00FF00");
                ss.colors.max = NormalizeHex(colors != null ? colors[2] : "#F3919B", "#F3919B");
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
