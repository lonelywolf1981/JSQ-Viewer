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
        public RecordingInfoForm(RecordingInfoResult result, System.Drawing.Icon appIcon = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (appIcon != null) Icon = appIcon;

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

            var table = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            int row = 0;

            // --- Секция T1 ---
            AddHeader(table, "ТЕМПЕРАТУРА T1", ref row, topPad: 0);

            if (result.T1Min.HasValue)
            {
                AddRow(table, "Минимум",
                    result.T1Min.Value.ToString("F1") + " °C", ref row);
                // Показываем оба представления: относительное (как на графике) и абсолютное
                string elapsedStr = result.T1MinElapsedMs.HasValue
                    ? FormatElapsed(result.T1MinElapsedMs.Value)
                    : "—";
                string startStr = result.SourceStartTime.HasValue
                    ? result.SourceStartTime.Value.ToString("dd.MM.yy HH:mm:ss")
                    : "—";
                string absStr = result.T1MinTime.HasValue
                    ? result.T1MinTime.Value.ToString("dd.MM.yy HH:mm:ss")
                    : "—";
                AddRow(table, "Старт записи", startStr, ref row);
                AddRow(table, "Время до минимума", elapsedStr, ref row);
                AddRow(table, "Дата/время минимума", absStr, ref row);
                string rate = result.T1DropRatePerMinute.HasValue
                    ? result.T1DropRatePerMinute.Value.ToString("F2") + " °C/мин"
                    : "—";
                AddRow(table, "Скорость падения", rate, ref row);
            }
            else
            {
                AddRow(table, "T1 не найден", "—", ref row);
            }

            // --- Секция метаданных ---
            if (result.Meta != null && result.Meta.Count > 0)
            {
                AddHeader(table, "МЕТАДАННЫЕ", ref row, topPad: 8);
                foreach (KeyValuePair<string, string> kv in result.Meta)
                    AddRow(table, kv.Key, kv.Value ?? string.Empty, ref row);
            }

            Controls.Add(table);
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

        private static void AddRow(TableLayoutPanel table, string key, string value,
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
            var valBox = new TextBox
            {
                Text = value,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                ForeColor = SystemColors.ControlText,
                TabStop = false,
                Width = Math.Max(60, TextRenderer.MeasureText(value ?? string.Empty, table.Font).Width + 6),
                Padding = new Padding(0, 1, 0, 1),
                Margin = new Padding(0)
            };
            table.Controls.Add(keyLbl, 0, row);
            table.Controls.Add(valBox, 1, row);
            row++;
        }

        // Форматирует прошедшее время в виде ЧЧ:мм:сс (часы могут быть > 23)
        private static string FormatElapsed(long elapsedMs)
        {
            long totalSec = elapsedMs / 1000;
            long hours   = totalSec / 3600;
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
