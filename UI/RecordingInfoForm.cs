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
        public RecordingInfoForm(RecordingInfoResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

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
                AddRow(table, "Время минимума",
                    result.T1MinTime.HasValue
                        ? result.T1MinTime.Value.ToString("dd.MM.yy HH:mm:ss")
                        : "—", ref row);
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
            var valLbl = new Label
            {
                Text = value,
                AutoSize = true,
                Padding = new Padding(0, 1, 0, 1),
                Margin = new Padding(0)
            };
            table.Controls.Add(keyLbl, 0, row);
            table.Controls.Add(valLbl, 1, row);
            row++;
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
