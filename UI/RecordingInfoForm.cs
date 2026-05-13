// UI/RecordingInfoForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using JSQViewer.Application.Recording;

namespace JSQViewer.UI
{
    public sealed class RecordingInfoForm : Form
    {
        private readonly Func<T8PlusTemperatureThresholds, RecordingInfoResult> _recalculate;
        private readonly Action<T8PlusTemperatureThresholds> _thresholdsChanged;
        private readonly TableLayoutPanel _table;
        private RecordingInfoResult _result;
        private T8PlusTemperatureThresholds _thresholds = T8PlusTemperatureThresholds.Default;
        private bool _rendering;
        private TextBox _averageThresholdValueBox;
        private TextBox _averageThresholdElapsedBox;
        private TextBox _averageThresholdTimeBox;
        private Label _averageThresholdLabel;
        private TextBox _minimumThresholdValueBox;
        private TextBox _minimumThresholdElapsedBox;
        private TextBox _minimumThresholdTimeBox;
        private Label _minimumThresholdLabel;
        private TextBox _maximumThresholdValueBox;
        private TextBox _maximumThresholdElapsedBox;
        private TextBox _maximumThresholdTimeBox;
        private Label _maximumThresholdLabel;
        private TextBox _t8PlusDropRateBox;

        public RecordingInfoForm(RecordingInfoResult result, Icon appIcon = null)
            : this(result, appIcon, null)
        {
        }

        public RecordingInfoForm(
            RecordingInfoResult result,
            Icon appIcon,
            Func<T8PlusTemperatureThresholds, RecordingInfoResult> recalculate)
            : this(result, appIcon, T8PlusTemperatureThresholds.Default, recalculate, null)
        {
        }

        public RecordingInfoForm(
            RecordingInfoResult result,
            Icon appIcon,
            T8PlusTemperatureThresholds initialThresholds,
            Func<T8PlusTemperatureThresholds, RecordingInfoResult> recalculate,
            Action<T8PlusTemperatureThresholds> thresholdsChanged)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (appIcon != null) Icon = appIcon;

            _result = result;
            _thresholds = initialThresholds ?? T8PlusTemperatureThresholds.Default;
            _recalculate = recalculate;
            _thresholdsChanged = thresholdsChanged;

            Text = TruncatePath(result.SourceRoot ?? string.Empty);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            StartPosition = FormStartPosition.Manual;
            Font = new Font("Microsoft Sans Serif", 9f);
            Padding = new Padding(10);

            _table = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(_table);

            RebuildContent();
        }

        private void RebuildContent()
        {
            _rendering = true;
            try
            {
                _table.SuspendLayout();
                _table.Controls.Clear();
                _table.RowStyles.Clear();
                _table.RowCount = 0;

                int row = 0;

                AddHeader(_table, "ТЕМПЕРАТУРА T1", ref row, topPad: 0);

                if (_result.T1Min.HasValue)
                {
                    string startStr = _result.SourceStartTime.HasValue
                        ? _result.SourceStartTime.Value.ToString("dd.MM.yy HH:mm:ss")
                        : "—";
                    AddRow(_table, "Старт записи", startStr, ref row);
                    if (_result.T1InitialTemperature.HasValue)
                    {
                        AddRow(_table, "Начальная температура",
                            _result.T1InitialTemperature.Value.ToString("F1") + " °C",
                            ref row);
                    }

                    if (_result.T1FirstCoolingMin.HasValue)
                    {
                        AddRow(_table, "Первый минимум",
                            _result.T1FirstCoolingMin.Value.ToString("F1") + " °C", ref row);
                        AddRow(_table, "Время до первого минимума",
                            _result.T1FirstCoolingMinElapsedMs.HasValue
                                ? FormatElapsed(_result.T1FirstCoolingMinElapsedMs.Value)
                                : "—",
                            ref row);
                        AddRow(_table, "Дата/время первого минимума",
                            _result.T1FirstCoolingMinTime.HasValue
                                ? _result.T1FirstCoolingMinTime.Value.ToString("dd.MM.yy HH:mm:ss")
                                : "—",
                            ref row);
                    }

                    AddRow(_table, "Минимум",
                        _result.T1Min.Value.ToString("F1") + " °C", ref row);
                    string elapsedStr = _result.T1MinElapsedMs.HasValue
                        ? FormatElapsed(_result.T1MinElapsedMs.Value)
                        : "—";
                    string absStr = _result.T1MinTime.HasValue
                        ? _result.T1MinTime.Value.ToString("dd.MM.yy HH:mm:ss")
                        : "—";
                    AddRow(_table, "Время до минимума", elapsedStr, ref row);
                    AddRow(_table, "Дата/время минимума", absStr, ref row);

                    string rate = _result.T1DropRatePerMinute.HasValue
                        ? _result.T1DropRatePerMinute.Value.ToString("F2") + " °C/мин"
                        : "—";
                    AddRow(_table, "Скорость падения", rate, ref row);
                    if (_result.T1EnergyToTargetKWh.HasValue)
                    {
                        AddRow(_table, "Энергопотребление",
                            _result.T1EnergyToTargetKWh.Value.ToString("F3") + " кВт⋅ч",
                            ref row);
                    }
                }
                else
                {
                    AddRow(_table, "T1 не найден", "—", ref row);
                }

                if (_result.T8PlusStats != null && _result.T8PlusStats.HasChannels)
                {
                    AddHeader(_table, "ТЕМПЕРАТУРЫ T8+", ref row, topPad: 8);
                    AddThresholdEditorRows(_table, ref row);
                    AddThresholdRows(
                        _table,
                        "Средняя T8+",
                        "Средняя <= " + FormatThreshold(_thresholds.AverageThreshold) + " °C",
                        _result.T8PlusStats.AverageReached,
                        _result.T8PlusStats.AverageValue,
                        _result.T8PlusStats.AverageElapsedMs,
                        _result.T8PlusStats.AverageTime,
                        "AverageT8Plus",
                        ref row);
                    AddThresholdRows(
                        _table,
                        "Минимум T8+",
                        "Минимум <= " + FormatThreshold(_thresholds.MinimumThreshold) + " °C",
                        _result.T8PlusStats.MinimumReached,
                        _result.T8PlusStats.MinimumValue,
                        _result.T8PlusStats.MinimumElapsedMs,
                        _result.T8PlusStats.MinimumTime,
                        "MinimumT8Plus",
                        ref row);
                    AddThresholdRows(
                        _table,
                        "Максимум T8+",
                        "Максимум <= " + FormatThreshold(_thresholds.MaximumThreshold) + " °C",
                        _result.T8PlusStats.MaximumReached,
                        _result.T8PlusStats.MaximumValue,
                        _result.T8PlusStats.MaximumElapsedMs,
                        _result.T8PlusStats.MaximumTime,
                        "MaximumT8Plus",
                        ref row);
                    if (ShouldShowT8PlusDropRate(_result))
                    {
                        _t8PlusDropRateBox = AddRow(_table,
                            "Скорость падения T8+",
                            _result.T8PlusStats.AverageDropRatePerMinute.Value.ToString("F2") + " °C/мин",
                            ref row);
                    }
                }

                if (_result.Meta != null && _result.Meta.Count > 0)
                {
                    AddHeader(_table, "МЕТАДАННЫЕ", ref row, topPad: 8);
                    foreach (KeyValuePair<string, string> kv in _result.Meta)
                    {
                        AddRow(_table, kv.Key, kv.Value ?? string.Empty, ref row);
                    }
                }
            }
            finally
            {
                _table.ResumeLayout(true);
                _rendering = false;
            }
        }

        private void AddThresholdEditorRows(TableLayoutPanel table, ref int row)
        {
            if (_recalculate == null)
            {
                return;
            }

            AddThresholdEditorRow(table, "Порог средней T8+", _thresholds.AverageThreshold,
                "AverageT8PlusThresholdUpDown", ref row);
            AddThresholdEditorRow(table, "Порог минимума T8+", _thresholds.MinimumThreshold,
                "MinimumT8PlusThresholdUpDown", ref row);
            AddThresholdEditorRow(table, "Порог максимума T8+", _thresholds.MaximumThreshold,
                "MaximumT8PlusThresholdUpDown", ref row);
        }

        private void AddThresholdEditorRow(
            TableLayoutPanel table,
            string key,
            double value,
            string name,
            ref int row)
        {
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var keyLbl = new Label
            {
                Text = key,
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(0, 1, 20, 1),
                Margin = new Padding(0)
            };
            var numeric = new NumericUpDown
            {
                Name = name,
                DecimalPlaces = 1,
                Minimum = -1000,
                Maximum = 1000,
                Increment = 0.1m,
                Value = ClampDecimal((decimal)value, -1000m, 1000m),
                Width = 70,
                Margin = new Padding(0, 0, 0, 1)
            };
            numeric.ValueChanged += ThresholdNumericOnValueChanged;
            table.Controls.Add(keyLbl, 0, row);
            table.Controls.Add(numeric, 1, row);
            row++;
        }

        private void ThresholdNumericOnValueChanged(object sender, EventArgs e)
        {
            if (_rendering || _recalculate == null)
            {
                return;
            }

            NumericUpDown average = FindNumeric("AverageT8PlusThresholdUpDown");
            NumericUpDown minimum = FindNumeric("MinimumT8PlusThresholdUpDown");
            NumericUpDown maximum = FindNumeric("MaximumT8PlusThresholdUpDown");
            if (average == null || minimum == null || maximum == null)
            {
                return;
            }

            _thresholds = new T8PlusTemperatureThresholds(
                (double)average.Value,
                (double)minimum.Value,
                (double)maximum.Value);
            if (_thresholdsChanged != null)
            {
                _thresholdsChanged(_thresholds);
            }
            RecordingInfoResult recalculated = _recalculate(_thresholds);
            if (recalculated != null)
            {
                _result = recalculated;
                UpdateT8PlusRows();
            }
        }

        private void UpdateT8PlusRows()
        {
            T8PlusTemperatureStats stats = _result == null ? null : _result.T8PlusStats;
            if (stats == null || !stats.HasChannels)
            {
                return;
            }

            if (_averageThresholdLabel != null)
            {
                _averageThresholdLabel.Text = "Средняя <= " + FormatThreshold(_thresholds.AverageThreshold) + " °C";
            }
            if (_minimumThresholdLabel != null)
            {
                _minimumThresholdLabel.Text = "Минимум <= " + FormatThreshold(_thresholds.MinimumThreshold) + " °C";
            }
            if (_maximumThresholdLabel != null)
            {
                _maximumThresholdLabel.Text = "Максимум <= " + FormatThreshold(_thresholds.MaximumThreshold) + " °C";
            }

            UpdateThresholdBoxes(
                _averageThresholdValueBox,
                _averageThresholdElapsedBox,
                _averageThresholdTimeBox,
                stats.AverageReached,
                stats.AverageValue,
                stats.AverageElapsedMs,
                stats.AverageTime);
            UpdateThresholdBoxes(
                _minimumThresholdValueBox,
                _minimumThresholdElapsedBox,
                _minimumThresholdTimeBox,
                stats.MinimumReached,
                stats.MinimumValue,
                stats.MinimumElapsedMs,
                stats.MinimumTime);
            UpdateThresholdBoxes(
                _maximumThresholdValueBox,
                _maximumThresholdElapsedBox,
                _maximumThresholdTimeBox,
                stats.MaximumReached,
                stats.MaximumValue,
                stats.MaximumElapsedMs,
                stats.MaximumTime);

            if (_t8PlusDropRateBox != null && stats.AverageDropRatePerMinute.HasValue)
            {
                _t8PlusDropRateBox.Text = stats.AverageDropRatePerMinute.Value.ToString("F2") + " °C/мин";
                ResizeValueBox(_t8PlusDropRateBox, _t8PlusDropRateBox.Text);
            }
        }

        private void UpdateThresholdBoxes(
            TextBox valueBox,
            TextBox elapsedBox,
            TextBox timeBox,
            bool reached,
            double? value,
            long? elapsedMs,
            DateTime? time)
        {
            Color color = reached ? Color.Green : Color.Red;
            SetValueBox(valueBox, value.HasValue ? value.Value.ToString("F1") + " °C" : "—", color);
            SetValueBox(elapsedBox, elapsedMs.HasValue ? FormatElapsed(elapsedMs.Value) : "—", color);
            SetValueBox(timeBox, time.HasValue ? time.Value.ToString("dd.MM.yy HH:mm:ss") : "—", color);
        }

        private static void SetValueBox(TextBox box, string text, Color color)
        {
            if (box == null)
            {
                return;
            }

            box.Text = text;
            box.ForeColor = color;
            ResizeValueBox(box, text);
        }

        private NumericUpDown FindNumeric(string name)
        {
            foreach (Control control in _table.Controls)
            {
                NumericUpDown numeric = control as NumericUpDown;
                if (numeric != null && string.Equals(numeric.Name, name, StringComparison.Ordinal))
                {
                    return numeric;
                }
            }

            return null;
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static string FormatThreshold(double value)
        {
            return value.ToString("0.#");
        }

        private static void AddHeader(TableLayoutPanel table, string text,
            ref int row, int topPad)
        {
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold),
                ForeColor = SystemColors.GrayText,
                AutoSize = true,
                Padding = new Padding(0, topPad, 0, 2),
                Margin = new Padding(0)
            };
            table.Controls.Add(lbl, 0, row);
            table.SetColumnSpan(lbl, 2);
            row++;
        }

        private void AddThresholdRows(
            TableLayoutPanel table,
            string title,
            string valueKey,
            bool reached,
            double? value,
            long? elapsedMs,
            DateTime? time,
            string controlNamePrefix,
            ref int row)
        {
            Color color = reached ? Color.Green : Color.Red;
            AddRow(table, valueKey, value.HasValue ? value.Value.ToString("F1") + " °C" : "—", ref row, color, controlNamePrefix + "ValueBox");
            AddRow(table, title + " время", elapsedMs.HasValue ? FormatElapsed(elapsedMs.Value) : "—", ref row, color, controlNamePrefix + "ElapsedBox");
            AddRow(table, title + " дата/время", time.HasValue ? time.Value.ToString("dd.MM.yy HH:mm:ss") : "—", ref row, color, controlNamePrefix + "TimeBox");
        }

        private TextBox AddRow(TableLayoutPanel table, string key, string value,
            ref int row)
        {
            return AddRow(table, key, value, ref row, null);
        }

        private static bool ShouldShowT8PlusDropRate(RecordingInfoResult result)
        {
            if (result == null || result.T8PlusStats == null ||
                !result.T8PlusStats.AverageDropRatePerMinute.HasValue)
            {
                return false;
            }

            if (!result.T1DropRatePerMinute.HasValue)
            {
                return true;
            }

            double t1Rounded = Math.Round(result.T1DropRatePerMinute.Value, 2);
            double t8Rounded = Math.Round(result.T8PlusStats.AverageDropRatePerMinute.Value, 2);
            return Math.Abs(t1Rounded - t8Rounded) > 0.000001;
        }

        private TextBox AddRow(TableLayoutPanel table, string key, string value,
            ref int row, Color? valueColor)
        {
            return AddRow(table, key, value, ref row, valueColor, null);
        }

        private TextBox AddRow(TableLayoutPanel table, string key, string value,
            ref int row, Color? valueColor, string valueControlName)
        {
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var keyLbl = new Label
            {
                Text = key,
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(0, 1, 20, 1),
                Margin = new Padding(0)
            };
            var valBox = new TextBox
            {
                Name = valueControlName ?? string.Empty,
                Text = value,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                ForeColor = valueColor ?? SystemColors.ControlText,
                TabStop = false,
                Width = Math.Max(60, TextRenderer.MeasureText(value ?? string.Empty, table.Font).Width + 6),
                Padding = new Padding(0, 1, 0, 1),
                Margin = new Padding(0)
            };
            table.Controls.Add(keyLbl, 0, row);
            table.Controls.Add(valBox, 1, row);
            RegisterT8PlusLabel(keyLbl, valueControlName);
            RegisterT8PlusValueBox(valBox);
            row++;
            return valBox;
        }

        private void RegisterT8PlusLabel(Label label, string valueControlName)
        {
            if (label == null)
            {
                return;
            }

            switch (valueControlName)
            {
                case "AverageT8PlusValueBox":
                    _averageThresholdLabel = label;
                    break;
                case "MinimumT8PlusValueBox":
                    _minimumThresholdLabel = label;
                    break;
                case "MaximumT8PlusValueBox":
                    _maximumThresholdLabel = label;
                    break;
            }
        }

        private void RegisterT8PlusValueBox(TextBox box)
        {
            if (box == null)
            {
                return;
            }

            switch (box.Name)
            {
                case "AverageT8PlusValueBox":
                    _averageThresholdValueBox = box;
                    break;
                case "AverageT8PlusElapsedBox":
                    _averageThresholdElapsedBox = box;
                    break;
                case "AverageT8PlusTimeBox":
                    _averageThresholdTimeBox = box;
                    break;
                case "MinimumT8PlusValueBox":
                    _minimumThresholdValueBox = box;
                    break;
                case "MinimumT8PlusElapsedBox":
                    _minimumThresholdElapsedBox = box;
                    break;
                case "MinimumT8PlusTimeBox":
                    _minimumThresholdTimeBox = box;
                    break;
                case "MaximumT8PlusValueBox":
                    _maximumThresholdValueBox = box;
                    break;
                case "MaximumT8PlusElapsedBox":
                    _maximumThresholdElapsedBox = box;
                    break;
                case "MaximumT8PlusTimeBox":
                    _maximumThresholdTimeBox = box;
                    break;
            }
        }

        private static void ResizeValueBox(TextBox box, string value)
        {
            if (box == null)
            {
                return;
            }

            Control parent = box.Parent;
            Font font = parent == null ? box.Font : parent.Font;
            box.Width = Math.Max(60, TextRenderer.MeasureText(value ?? string.Empty, font).Width + 6);
        }

        private static string FormatElapsed(long elapsedMs)
        {
            long totalSec = elapsedMs / 1000;
            long hours = totalSec / 3600;
            long minutes = (totalSec % 3600) / 60;
            long seconds = totalSec % 60;
            return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
        }

        private static string TruncatePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            char sep = System.IO.Path.DirectorySeparatorChar;
            char alt = System.IO.Path.AltDirectorySeparatorChar;
            string[] parts = path.Split(sep, alt);
            if (parts.Length <= 2) return path;
            return "..." + sep + string.Join(sep.ToString(),
                parts[parts.Length - 2], parts[parts.Length - 1]);
        }
    }
}
