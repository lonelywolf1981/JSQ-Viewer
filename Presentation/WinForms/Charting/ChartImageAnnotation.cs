using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using JSQViewer.Presentation.WinForms.ViewModels;

namespace JSQViewer.Presentation.WinForms.Charting
{
    public static class ChartImageAnnotation
    {
        private static readonly Size FullHdSize = new Size(1920, 1080);

        public static IReadOnlyList<string> CollectSourcePaths(ChartViewModel viewModel)
        {
            if (viewModel == null || viewModel.Series == null)
            {
                return new string[0];
            }

            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ChartSeriesViewModel series in viewModel.Series)
            {
                if (series == null
                    || series.XValues == null
                    || series.YValues == null
                    || series.XValues.Length == 0
                    || series.YValues.Length == 0
                    || string.IsNullOrWhiteSpace(series.SourceRoot))
                {
                    continue;
                }

                string path = series.SourceRoot.Trim();
                if (seen.Add(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        public static void SaveChartImage(Chart chart, string fileName, ChartImageFormat format, ChartViewModel viewModel)
        {
            if (chart == null) throw new ArgumentNullException("chart");
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.", "fileName");

            if (format != ChartImageFormat.Png)
            {
                chart.SaveImage(fileName, format);
                return;
            }

            Size originalSize = chart.Size;
            Size exportSize = ResolvePngExportSize(originalSize);
            using (var ms = new MemoryStream())
            {
                SavePngAtSize(chart, ms, exportSize);
                ms.Position = 0;
                using (var bitmap = new Bitmap(ms))
                {
                    DrawSourcePaths(bitmap, CollectSourcePaths(viewModel));
                    bitmap.Save(fileName, ImageFormat.Png);
                }
            }
        }

        public static Size ResolvePngExportSize(Size currentSize)
        {
            int width = Math.Max(currentSize.Width, FullHdSize.Width);
            int height = Math.Max(currentSize.Height, FullHdSize.Height);
            return new Size(width, height);
        }

        private static void SavePngAtSize(Chart chart, Stream target, Size exportSize)
        {
            Size originalSize = chart.Size;
            bool resize = exportSize.Width > 0
                && exportSize.Height > 0
                && (originalSize.Width != exportSize.Width || originalSize.Height != exportSize.Height);

            Control parent = chart.Parent;
            try
            {
                if (resize)
                {
                    if (parent != null)
                    {
                        parent.SuspendLayout();
                    }

                    chart.SuspendLayout();
                    chart.Size = exportSize;
                    chart.PerformLayout();
                }

                chart.SaveImage(target, ChartImageFormat.Png);
            }
            finally
            {
                if (resize)
                {
                    chart.Size = originalSize;
                    chart.ResumeLayout(true);
                    if (parent != null)
                    {
                        parent.ResumeLayout(true);
                    }
                }
            }
        }

        public static void DrawSourcePaths(Bitmap bitmap, IEnumerable<string> sourcePaths)
        {
            if (bitmap == null || sourcePaths == null)
            {
                return;
            }

            string[] lines = sourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .ToArray();
            if (lines.Length == 0)
            {
                return;
            }

            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var font = new Font("Microsoft Sans Serif", 8f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Color.FromArgb(220, Color.Black)))
            using (var background = new SolidBrush(Color.FromArgb(190, Color.White)))
            using (var format = new StringFormat())
            {
                format.Alignment = StringAlignment.Far;
                format.LineAlignment = StringAlignment.Near;
                string text = string.Join(Environment.NewLine, lines);
                SizeF size = graphics.MeasureString(text, font, Math.Max(1, bitmap.Width - 12), format);
                var rect = new RectangleF(
                    Math.Max(4f, bitmap.Width - size.Width - 8f),
                    4f,
                    Math.Min(size.Width + 4f, bitmap.Width - 8f),
                    Math.Min(size.Height + 4f, bitmap.Height - 8f));
                graphics.FillRectangle(background, rect);
                graphics.DrawString(text, font, brush, rect, format);
            }
        }
    }
}
