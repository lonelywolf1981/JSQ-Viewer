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
        private const int ContentWidth = 530;

        private sealed class ScaleEditors
        {
            public TextBox MinBox;
            public TextBox MaxBox;
            public TextBox LowHexBox;
            public TextBox NormHexBox;
            public TextBox HighHexBox;
        }

        private readonly NumericUpDown _rowThreshold;
        private readonly TextBox _dischargeThreshold;
        private readonly TextBox _suctionThreshold;
        private readonly TextBox _rowHexBox;
        private readonly TextBox _dischargeHexBox;
        private readonly TextBox _suctionHexBox;
        private readonly Button _okButton;
        private readonly Dictionary<string, ScaleEditors> _scaleEditors = new Dictionary<string, ScaleEditors>(StringComparer.OrdinalIgnoreCase);

        public ViewerSettingsModel Result { get; private set; }

        public SettingsDialog(ViewerSettingsModel source)
        {
            Result = Clone(source ?? ViewerSettingsModel.CreateDefault());

            Text = Loc.Get("StylesTitle");
            Width = 560;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = SystemColors.Control;

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.Padding = new Padding(8, 8, 8, 6);
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 1f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var content = new FlowLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.FlowDirection = FlowDirection.TopDown;
            content.WrapContents = false;
            content.AutoScroll = false;
            content.Padding = new Padding(0);
            root.Controls.Add(content, 0, 0);

            var title = new Label();
            title.Text = Loc.Get("StylesHeader");
            title.Font = new Font(Font, FontStyle.Bold);
            title.AutoSize = true;
            title.Margin = new Padding(0, 0, 0, 8);
            content.Controls.Add(title);

            _rowHexBox = CreateThresholdRow(content, Loc.Get("PowerThreshold"), ToDecimal(Result.row_mark.threshold_T, 150m), Safe(Result.row_mark.color, "#DBEA06"), out _rowThreshold);
            _dischargeHexBox = CreateThresholdRow(content, Loc.Get("DischargeThresholdShort"), FormatNullable(Result.discharge_mark.threshold), Safe(Result.discharge_mark.color, "#F52952"), out _dischargeThreshold);
            _suctionHexBox = CreateThresholdRow(content, Loc.Get("SuctionThresholdShort"), FormatNullable(Result.suction_mark.threshold), Safe(Result.suction_mark.color, "#F52952"), out _suctionThreshold);

            AddScaleSection(content, "W", Loc.Get("ScaleWTitle"));
            AddScaleSection(content, "X", Loc.Get("ScaleXTitle"));
            AddScaleSection(content, "Y", Loc.Get("ScaleYTitle"));

            var line = new Panel();
            line.Height = 1;
            line.Dock = DockStyle.Fill;
            line.BackColor = Color.Silver;
            root.Controls.Add(line, 0, 1);

            var bottom = new Panel();
            bottom.Height = 42;
            bottom.Dock = DockStyle.Fill;
            root.Controls.Add(bottom, 0, 2);

            _okButton = new Button();
            _okButton.Text = Loc.Get("Apply");
            _okButton.AutoSize = false;
            _okButton.Width = 98;
            _okButton.Height = 32;
            _okButton.Left = 0;
            _okButton.Top = 6;
            _okButton.Click += OkButtonOnClick;
            bottom.Controls.Add(_okButton);

            var loadedLabel = new Label();
            loadedLabel.Text = Loc.Get("SettingsLoaded");
            loadedLabel.AutoSize = true;
            loadedLabel.Left = 114;
            loadedLabel.Top = 14;
            bottom.Controls.Add(loadedLabel);
        }

        private static Panel NewRowPanel(int width, int height)
        {
            var p = new Panel();
            p.Width = width;
            p.Height = height;
            p.Margin = new Padding(0, 0, 0, 6);
            return p;
        }

        private static Label NewPlainLabel(string text, int x, int y)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Left = x;
            l.Top = y;
            l.Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Regular);
            return l;
        }

        private static Label NewMutedLabel(string text, int x, int y)
        {
            var l = NewPlainLabel(text, x, y);
            l.ForeColor = SystemColors.ControlDarkDark;
            return l;
        }

        private TextBox CreateThresholdRow(Control parent, string title, decimal value, string colorHex, out NumericUpDown editor)
        {
            var row = NewRowPanel(ContentWidth, 30);
            row.Controls.Add(NewPlainLabel(title, 0, 7));

            editor = new NumericUpDown();
            editor.DecimalPlaces = 0;
            editor.Minimum = -100000;
            editor.Maximum = 100000;
            editor.Width = 88;
            editor.Left = 178;
            editor.Top = 3;
            editor.Value = value;
            row.Controls.Add(editor);

            row.Controls.Add(NewPlainLabel(Loc.Get("Color"), 282, 7));
            var hexBox = AddColorPicker(row, colorHex, 338);
            hexBox.Width = 152;
            parent.Controls.Add(row);
            return hexBox;
        }

        private TextBox CreateThresholdRow(Control parent, string title, string value, string colorHex, out TextBox editor)
        {
            var row = NewRowPanel(ContentWidth, 30);
            row.Controls.Add(NewPlainLabel(title, 0, 7));

            editor = new TextBox();
            editor.Width = 88;
            editor.Left = 178;
            editor.Top = 3;
            editor.Text = value ?? string.Empty;
            row.Controls.Add(editor);

            row.Controls.Add(NewPlainLabel(Loc.Get("Color"), 282, 7));
            var hexBox = AddColorPicker(row, colorHex, 338);
            hexBox.Width = 152;
            parent.Controls.Add(row);
            return hexBox;
        }

        private static TextBox AddColorPicker(Control row, string initialHex, int left)
        {
            string hex = NormalizeHex(initialHex, "#FFFFFF");
            var panel = new Panel();
            panel.Width = 34;
            panel.Height = 24;
            panel.Left = left;
            panel.Top = 2;
            panel.BackColor = HexToColor(hex);
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Cursor = Cursors.Hand;
            row.Controls.Add(panel);

            var hexBox = new TextBox();
            hexBox.Width = 122;
            hexBox.Left = left + 42;
            hexBox.Top = 2;
            hexBox.Text = hex;
            row.Controls.Add(hexBox);

            panel.Click += delegate
            {
                using (var dlg = new ColorDialog())
                {
                    dlg.Color = panel.BackColor;
                    dlg.FullOpen = true;
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        string selected = ColorToHex(dlg.Color);
                        panel.BackColor = dlg.Color;
                        hexBox.Text = selected;
                    }
                }
            };

            hexBox.TextChanged += delegate
            {
                string normalized = NormalizeHex(hexBox.Text, string.Empty);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    panel.BackColor = HexToColor(normalized);
                }
            };

            return hexBox;
        }

        private static TextBox AddColorPickerAt(Control row, string initialHex, int panelLeft, int panelTop, int hexLeft, int hexTop)
        {
            string hex = NormalizeHex(initialHex, "#FFFFFF");
            var panel = new Panel();
            panel.Width = 34;
            panel.Height = 24;
            panel.Left = panelLeft;
            panel.Top = panelTop;
            panel.BackColor = HexToColor(hex);
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Cursor = Cursors.Hand;
            row.Controls.Add(panel);

            var hexBox = new TextBox();
            hexBox.Width = 122;
            hexBox.Left = hexLeft;
            hexBox.Top = hexTop;
            hexBox.Text = hex;
            row.Controls.Add(hexBox);

            panel.Click += delegate
            {
                using (var dlg = new ColorDialog())
                {
                    dlg.Color = panel.BackColor;
                    dlg.FullOpen = true;
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        string selected = ColorToHex(dlg.Color);
                        panel.BackColor = dlg.Color;
                        hexBox.Text = selected;
                    }
                }
            };

            hexBox.TextChanged += delegate
            {
                string normalized = NormalizeHex(hexBox.Text, string.Empty);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    panel.BackColor = HexToColor(normalized);
                }
            };

            return hexBox;
        }

        private void AddScaleSection(Control parent, string key, string title)
        {
            ScaleSettings s = Result.scales.ContainsKey(key) ? Result.scales[key] : ScaleSettings.CreateDefault();
            ScaleColors colors = s.colors ?? ScaleSettings.CreateDefault().colors;

            var section = new Panel();
            section.Width = ContentWidth;
            section.Height = 122;
            section.Margin = new Padding(0, 0, 0, 4);
            parent.Controls.Add(section);

            section.Controls.Add(NewPlainLabel(title, 0, 2));
            section.Controls.Add(NewMutedLabel(Loc.Get("NormFrom"), 156, 2));

            var minBox = new TextBox();
            minBox.Width = 70;
            minBox.Left = 230;
            minBox.Top = 0;
            minBox.Text = Fmt(s.min);
            section.Controls.Add(minBox);

            section.Controls.Add(NewMutedLabel(Loc.Get("NormTo"), 312, 2));

            var maxBox = new TextBox();
            maxBox.Width = 70;
            maxBox.Left = 346;
            maxBox.Top = 0;
            maxBox.Text = Fmt(s.opt);
            section.Controls.Add(maxBox);

            section.Controls.Add(NewMutedLabel(Loc.Get("Low"), 0, 38));
            section.Controls.Add(NewMutedLabel(Loc.Get("Norm"), 186, 38));
            section.Controls.Add(NewMutedLabel(Loc.Get("High"), 370, 38));

            var lowHex = AddColorPickerAt(section, Safe(colors.min, "#1CBCF2"), 120, 56, 0, 84);
            var normHex = AddColorPickerAt(section, Safe(colors.opt, "#00FF00"), 304, 56, 184, 84);
            var highHex = AddColorPickerAt(section, Safe(colors.max, "#F3919B"), 488, 56, 368, 84);
            lowHex.Width = 150;
            normHex.Width = 150;
            highHex.Width = 150;

            _scaleEditors[key] = new ScaleEditors
            {
                MinBox = minBox,
                MaxBox = maxBox,
                LowHexBox = lowHex,
                NormHexBox = normHex,
                HighHexBox = highHex
            };
        }

        private void OkButtonOnClick(object sender, EventArgs e)
        {
            ViewerSettingsModel updated = ViewerSettingsModel.CreateDefault();
            updated.row_mark.threshold_T = (double)_rowThreshold.Value;
            updated.row_mark.color = NormalizeHex(_rowHexBox.Text, "#EAD706");
            updated.row_mark.intensity = 100;

            double? dThr;
            if (!TryParseNullableDouble(_dischargeThreshold.Text, out dThr))
            {
                MessageBox.Show(this, Loc.Get("InvalidDischargeThreshold"), Loc.Get("Settings"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            updated.discharge_mark.threshold = dThr;
            updated.discharge_mark.color = NormalizeHex(_dischargeHexBox.Text, "#F52952");

            double? sThr;
            if (!TryParseNullableDouble(_suctionThreshold.Text, out sThr))
            {
                MessageBox.Show(this, Loc.Get("InvalidSuctionThreshold"), Loc.Get("Settings"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            updated.suction_mark.threshold = sThr;
            updated.suction_mark.color = NormalizeHex(_suctionHexBox.Text, "#F52952");

            updated.scales = new Dictionary<string, ScaleSettings>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _scaleEditors)
            {
                string key = kv.Key;
                ScaleEditors ed = kv.Value;
                double min;
                double max;
                if (!TryParseDouble(ed.MinBox.Text, out min) || !TryParseDouble(ed.MaxBox.Text, out max))
                {
                    MessageBox.Show(this, Loc.Get("InvalidScaleValues"), Loc.Get("Settings"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (min >= max) max = min + 1;

                var ss = ScaleSettings.CreateDefault();
                ss.min = min;
                ss.opt = max;
                ss.max = max;
                ss.colors.min = NormalizeHex(ed.LowHexBox.Text, "#1CBCF2");
                ss.colors.opt = NormalizeHex(ed.NormHexBox.Text, "#00FF00");
                ss.colors.max = NormalizeHex(ed.HighHexBox.Text, "#F3919B");
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
