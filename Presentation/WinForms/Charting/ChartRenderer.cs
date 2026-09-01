using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using JSQViewer.Application.Charting;
using JSQViewer.Presentation.WinForms.ViewModels;

namespace JSQViewer.Presentation.WinForms.Charting
{
    public sealed class ChartRenderer
    {
        public void Render(Chart chart, ChartViewModel viewModel)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));

            chart.Series.Clear();
            chart.Tag = viewModel;
            if (chart.Legends.Count > 0)
            {
                chart.Legends[0].Enabled = viewModel.ShowLegend;
            }

            if (chart.ChartAreas.Count > 0)
            {
                ChartArea area = chart.ChartAreas[0];
                area.AxisX.LabelStyle.Format = viewModel.XAxisLabelFormat ?? string.Empty;
                area.AxisX.Title = viewModel.XAxisTitle ?? string.Empty;
            }

            chart.BeginInit();
            chart.SuspendLayout();
            chart.AntiAliasing = viewModel.Series.Count > 10 ? AntiAliasingStyles.None : AntiAliasingStyles.All;
            try
            {
                for (int i = 0; i < viewModel.Series.Count; i++)
                {
                    ChartSeriesViewModel model = viewModel.Series[i];
                    chart.Series.Add(CreateSeries(viewModel, model));
                }

                chart.ResetAutoValues();
                if (chart.ChartAreas.Count > 0)
                {
                    ChartArea area = chart.ChartAreas[0];
                    area.RecalculateAxesScale();
                    ApplyXAxis(area, viewModel);
                    ApplyOverlayXAxisLabels(area, viewModel != null && viewModel.OverlayMode);
                    ApplyAxis(area.AxisY, viewModel.YAxis);
                    ApplyLevelLines(area, viewModel);
                    EnsureLevelsFitAxis(area, viewModel);
                }
            }
            finally
            {
                chart.ResumeLayout();
                chart.EndInit();
                chart.Invalidate();
                chart.Update();
            }
        }

        private static Series CreateSeries(ChartViewModel viewModel, ChartSeriesViewModel model)
        {
            var series = new Series(model.Code);
            series.ChartType = SeriesChartType.FastLine;
            series.XValueType = viewModel.OverlayMode ? ChartValueType.Double : ChartValueType.DateTime;
            series.BorderWidth = model.BorderWidth;
            series.BorderDashStyle = model.IsForecast ? ChartDashStyle.Dash : ChartDashStyle.Solid;
            series.IsVisibleInLegend = model.IsVisibleInLegend;
            series.LegendText = model.LegendText;
            series.Points.DataBindXY(model.XValues ?? new double[0], model.YValues ?? new double[0]);
            return series;
        }

        private static void ApplyLevelLines(ChartArea area, ChartViewModel viewModel)
        {
            // Полосы накапливались бы при каждой перерисовке, поэтому чистим всегда,
            // даже когда уровней нет.
            area.AxisY.StripLines.Clear();

            IReadOnlyList<ChartLevelLineViewModel> levels = viewModel.LevelLines;
            if (levels == null)
            {
                return;
            }

            // Порядок задаёт место подписи, поэтому он должен быть устойчивым:
            // сначала по роли, затем по источнику.
            var ordered = new List<ChartLevelLineViewModel>(levels);
            ordered.Sort(CompareLevelLines);

            var placed = new Dictionary<ChartSeriesRole, int>();

            for (int i = 0; i < ordered.Count; i++)
            {
                ChartLevelLineViewModel level = ordered[i];

                int ordinal;
                placed.TryGetValue(level.Role, out ordinal);
                placed[level.Role] = ordinal + 1;

                var strip = new StripLine();
                strip.IntervalOffset = level.Value;
                strip.Interval = 0d;
                strip.StripWidth = 0d;
                strip.BorderColor = SourceColorPalette.ForSourceIndex(level.SourceIndex);
                strip.BorderWidth = 2;
                strip.BorderDashStyle = ResolveLevelDashStyle(level.Role);
                strip.Text = level.Label ?? string.Empty;
                ApplyLabelPlacement(strip, ordinal);
                area.AxisY.StripLines.Add(strip);
            }
        }

        /// <summary>
        /// Раздвигает ось Y так, чтобы уровни попали в видимую область.
        /// MS Chart считает автоматический диапазон только по сериям, а полосы в
        /// него не входят: уровень ниже самой холодной показанной кривой иначе
        /// обрезается краем области и выглядит как «линия не работает».
        /// Двигается только та граница, которой не хватает, — вторая остаётся
        /// автоматической, чтобы подписи оси сохранили круглые значения.
        /// </summary>
        private static void EnsureLevelsFitAxis(ChartArea area, ChartViewModel viewModel)
        {
            if (viewModel.YAxis != null && viewModel.YAxis.IsManualEnabled)
            {
                return;
            }

            IReadOnlyList<ChartLevelLineViewModel> levels = viewModel.LevelLines;
            if (levels == null || levels.Count == 0)
            {
                return;
            }

            double seriesMinimum;
            double seriesMaximum;
            if (!TryGetSeriesRange(viewModel, out seriesMinimum, out seriesMaximum))
            {
                return;
            }

            double levelMinimum = levels[0].Value;
            double levelMaximum = levels[0].Value;
            for (int i = 1; i < levels.Count; i++)
            {
                if (levels[i].Value < levelMinimum) levelMinimum = levels[i].Value;
                if (levels[i].Value > levelMaximum) levelMaximum = levels[i].Value;
            }

            double low = Math.Min(seriesMinimum, levelMinimum);
            double high = Math.Max(seriesMaximum, levelMaximum);
            double margin = (high - low) * 0.03;
            if (margin <= 0d)
            {
                margin = 1d;
            }

            if (levelMinimum < seriesMinimum)
            {
                area.AxisY.Minimum = levelMinimum - margin;
            }

            if (levelMaximum > seriesMaximum)
            {
                area.AxisY.Maximum = levelMaximum + margin;
            }
        }

        private static bool TryGetSeriesRange(ChartViewModel viewModel, out double minimum, out double maximum)
        {
            minimum = 0d;
            maximum = 0d;
            bool found = false;

            IReadOnlyList<ChartSeriesViewModel> series = viewModel.Series;
            if (series == null)
            {
                return false;
            }

            for (int i = 0; i < series.Count; i++)
            {
                double[] values = series[i].YValues;
                if (values == null)
                {
                    continue;
                }

                for (int v = 0; v < values.Length; v++)
                {
                    double value = values[v];
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        continue;
                    }

                    if (!found || value < minimum) minimum = value;
                    if (!found || value > maximum) maximum = value;
                    found = true;
                }
            }

            return found;
        }

        private static int CompareLevelLines(ChartLevelLineViewModel first, ChartLevelLineViewModel second)
        {
            int byRole = ((int)first.Role).CompareTo((int)second.Role);
            return byRole != 0 ? byRole : first.SourceIndex.CompareTo(second.SourceIndex);
        }

        /// <summary>
        /// Разводит подписи уровней одной роли, чтобы при сравнении прогонов они
        /// не ложились одна на другую: у близких по значению линий подписи иначе
        /// полностью перекрываются. Правило одно на все роли — первая подпись
        /// слева, вторая справа; вертикаль задействуется только начиная с третьего
        /// источника, потому что под линиями минимума места нет.
        /// </summary>
        private static void ApplyLabelPlacement(StripLine strip, int ordinal)
        {
            strip.TextAlignment = (ordinal % 2) == 0 ? StringAlignment.Near : StringAlignment.Far;
            strip.TextLineAlignment = ((ordinal / 2) % 2) == 0 ? StringAlignment.Far : StringAlignment.Near;
        }

        private static ChartDashStyle ResolveLevelDashStyle(ChartSeriesRole role)
        {
            if (role == ChartSeriesRole.T8Minimum)
            {
                return ChartDashStyle.Dot;
            }

            if (role == ChartSeriesRole.T8Maximum)
            {
                // Не Dash: штриховой стиль занят линией прогноза динамики.
                return ChartDashStyle.DashDot;
            }

            return ChartDashStyle.Solid;
        }

        private static void ApplyXAxis(ChartArea area, ChartViewModel viewModel)
        {
            if (area == null)
            {
                return;
            }

            ChartRangeViewModel range = viewModel == null ? null : viewModel.Range;
            ChartAxisSettingsViewModel axis = viewModel == null ? null : viewModel.XAxis;
            bool manualAxisEnabled = axis != null && axis.IsManualEnabled;
            double minimum = manualAxisEnabled
                ? ResolveMinimum(axis)
                : range != null && range.IsActive
                    ? range.Start
                    : double.NaN;
            double maximum = manualAxisEnabled
                ? ResolveMaximum(axis)
                : range != null && range.IsActive
                    ? range.End
                    : viewModel != null && viewModel.OverlayMode
                        ? viewModel.DataMaximum
                        : double.NaN;

            if (!manualAxisEnabled && (range == null || !range.IsActive) && viewModel != null && viewModel.OverlayMode)
            {
                minimum = 0d;
                if (double.IsNaN(maximum) || double.IsInfinity(maximum) || maximum <= minimum)
                {
                    maximum = minimum + 1d / 3600d;
                }
            }

            area.AxisX.Minimum = minimum;
            area.AxisX.Maximum = maximum;
            area.AxisX.Interval = ResolveInterval(axis);
        }

        public static void ApplyElapsedXAxisLabels(ChartArea area)
        {
            if (area != null)
            {
                area.AxisX.Interval = 0d;
            }

            ApplyOverlayXAxisLabels(area, true);
        }

        private static void ApplyOverlayXAxisLabels(ChartArea area, bool overlayMode)
        {
            if (area == null)
            {
                return;
            }

            area.AxisX.CustomLabels.Clear();
            if (!overlayMode)
            {
                return;
            }

            double minimum = area.AxisX.Minimum;
            double maximum = area.AxisX.Maximum;
            if (double.IsNaN(minimum) || double.IsInfinity(minimum) || double.IsNaN(maximum) || double.IsInfinity(maximum) || maximum <= minimum)
            {
                return;
            }

            double interval = area.AxisX.Interval;
            if (interval <= 0d || double.IsNaN(interval) || double.IsInfinity(interval))
            {
                interval = ResolveElapsedLabelInterval(maximum - minimum);
                area.AxisX.Interval = interval;
            }

            var values = new List<double>();
            double first = Math.Ceiling(minimum / interval) * interval;
            if (Math.Abs(first - minimum) > 1e-9)
            {
                values.Add(minimum);
            }

            for (double value = first; value <= maximum + interval * 0.001d; value += interval)
            {
                values.Add(Math.Max(minimum, Math.Min(maximum, value)));
            }

            if (values.Count == 0 || Math.Abs(values[values.Count - 1] - maximum) > interval * 0.1d)
            {
                values.Add(maximum);
            }

            double halfWidth = interval / 2d;
            for (int i = 0; i < values.Count; i++)
            {
                double value = values[i];
                double from = Math.Max(minimum, value - halfWidth);
                double to = Math.Min(maximum, value + halfWidth);
                if (to <= from)
                {
                    to = from + Math.Max(interval, 1d / 3600d);
                }

                area.AxisX.CustomLabels.Add(new CustomLabel(from, to, FormatElapsedHoursLabel(value), 0, LabelMarkStyle.None));
            }
        }

        private static double ResolveElapsedLabelInterval(double spanHours)
        {
            if (spanHours <= 5d / 60d) return 1d / 120d;
            if (spanHours <= 0.25d) return 1d / 60d;
            if (spanHours <= 0.5d) return 5d / 60d;
            if (spanHours <= 1d) return 1d / 6d;
            if (spanHours <= 3d) return 0.5d;
            if (spanHours <= 24d) return 1d;
            if (spanHours <= 72d) return 2d;
            if (spanHours <= 168d) return 6d;
            return 24d;
        }

        private static string FormatElapsedHoursLabel(double hours)
        {
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours <= 0d)
            {
                return "0";
            }

            int totalSeconds = (int)Math.Round(hours * 3600d, MidpointRounding.AwayFromZero);
            if (totalSeconds < 60)
            {
                return string.Format("{0} сек", totalSeconds);
            }

            int totalMinutes = (int)Math.Round(totalSeconds / 60d, MidpointRounding.AwayFromZero);
            int days = totalMinutes / (24 * 60);
            int remainder = totalMinutes % (24 * 60);
            int wholeHours = remainder / 60;
            int minutes = remainder % 60;

            if (days > 0)
            {
                return minutes == 0
                    ? string.Format("{0} д {1} ч", days, wholeHours)
                    : string.Format("{0} д {1} ч {2} мин", days, wholeHours, minutes);
            }

            if (wholeHours > 0)
            {
                return minutes == 0
                    ? string.Format("{0} ч", wholeHours)
                    : string.Format("{0} ч {1} мин", wholeHours, minutes);
            }

            return string.Format("{0} мин", minutes);
        }

        private static void ApplyAxis(Axis axis, ChartAxisSettingsViewModel settings)
        {
            if (axis == null)
            {
                return;
            }

            axis.Minimum = ResolveMinimum(settings);
            axis.Maximum = ResolveMaximum(settings);
            axis.Interval = ResolveInterval(settings);
        }

        private static double ResolveMinimum(ChartAxisSettingsViewModel settings)
        {
            if (settings == null || !settings.IsManualEnabled || !settings.Minimum.HasValue)
            {
                return double.NaN;
            }

            return settings.Minimum.Value;
        }

        private static double ResolveMaximum(ChartAxisSettingsViewModel settings)
        {
            if (settings == null || !settings.IsManualEnabled || !settings.Maximum.HasValue)
            {
                return double.NaN;
            }

            return settings.Maximum.Value;
        }

        private static double ResolveInterval(ChartAxisSettingsViewModel settings)
        {
            if (settings == null || !settings.IsManualEnabled || !settings.Interval.HasValue)
            {
                return 0d;
            }

            return settings.Interval.Value;
        }
    }
}
