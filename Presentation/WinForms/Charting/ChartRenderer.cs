using System;
using System.Windows.Forms.DataVisualization.Charting;
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
                    var series = new Series(model.Code);
                    series.ChartType = SeriesChartType.FastLine;
                    series.XValueType = viewModel.OverlayMode ? ChartValueType.Double : ChartValueType.DateTime;
                    series.BorderWidth = model.BorderWidth;
                    series.IsVisibleInLegend = model.IsVisibleInLegend;
                    series.LegendText = model.LegendText;
                    series.Points.DataBindXY(model.XValues ?? new double[0], model.YValues ?? new double[0]);
                    chart.Series.Add(series);
                }

                chart.ResetAutoValues();
                if (chart.ChartAreas.Count > 0)
                {
                    ChartArea area = chart.ChartAreas[0];
                    area.RecalculateAxesScale();
                    ApplyXAxis(area, viewModel);
                    ApplyAxis(area.AxisY, viewModel.YAxis);
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
                    : double.NaN;

            area.AxisX.Minimum = minimum;
            area.AxisX.Maximum = maximum;
            area.AxisX.Interval = ResolveInterval(axis);
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
