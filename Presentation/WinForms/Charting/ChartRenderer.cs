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
                    area.AxisX.LabelStyle.Format = viewModel.XAxisLabelFormat ?? string.Empty;
                    area.AxisX.Title = viewModel.XAxisTitle ?? string.Empty;
                    ApplyAxisSettings(area.AxisX, viewModel.XAxisSettings);
                    ApplyAxisSettings(area.AxisY, viewModel.YAxisSettings);
                    if (!IsManualEnabled(viewModel.XAxisSettings) && viewModel.Range != null && viewModel.Range.IsActive)
                    {
                        area.AxisX.Minimum = viewModel.Range.Start;
                        area.AxisX.Maximum = viewModel.Range.End;
                    }
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

        private static void ApplyAxisSettings(Axis axis, ChartAxisSettingsViewModel settings)
        {
            if (axis == null)
            {
                return;
            }

            if (settings == null || !IsManualEnabled(settings))
            {
                axis.Minimum = double.NaN;
                axis.Maximum = double.NaN;
                axis.Interval = 0d;
                return;
            }

            axis.Minimum = settings.Minimum.Value;
            axis.Maximum = settings.Maximum.Value;
            axis.Interval = settings.Step.Value;
        }

        private static bool IsManualEnabled(ChartAxisSettingsViewModel settings)
        {
            return settings != null
                && settings.IsManualEnabled
                && settings.Minimum.HasValue
                && settings.Maximum.HasValue
                && settings.Step.HasValue;
        }
    }
}
