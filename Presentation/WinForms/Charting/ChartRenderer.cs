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
                area.AxisX.Minimum = viewModel.Range != null && viewModel.Range.IsActive ? viewModel.Range.Start : double.NaN;
                area.AxisX.Maximum = viewModel.Range != null && viewModel.Range.IsActive ? viewModel.Range.End : double.NaN;
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
                    chart.ChartAreas[0].RecalculateAxesScale();
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
    }
}
