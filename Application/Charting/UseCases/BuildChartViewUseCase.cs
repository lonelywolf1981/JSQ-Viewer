using System;
using System.Collections.Generic;
using JSQViewer.Presentation.WinForms.ViewModels;

namespace JSQViewer.Application.Charting.UseCases
{
    public sealed class BuildChartViewUseCase
    {
        private readonly ChartPipelineService _chartPipelineService;

        public BuildChartViewUseCase(ChartPipelineService chartPipelineService)
        {
            _chartPipelineService = chartPipelineService ?? throw new ArgumentNullException(nameof(chartPipelineService));
        }

        public ChartViewModel Execute(ChartPipelineRequest request, string overlayAxisTitle)
        {
            ChartPipelineResult result = _chartPipelineService.Execute(request);
            var series = new List<ChartSeriesViewModel>(result.Series.Count);
            for (int i = 0; i < result.Series.Count; i++)
            {
                ChartPipelineSeries item = result.Series[i];
                series.Add(new ChartSeriesViewModel
                {
                    Code = item.Code,
                    LegendText = item.LegendText,
                    XValues = item.XValues ?? new double[0],
                    YValues = item.YValues ?? new double[0],
                    BorderWidth = item.BorderWidth,
                    IsVisibleInLegend = item.IsVisibleInLegend
                });
            }

            return new ChartViewModel
            {
                HasData = result.HasData,
                OverlayMode = result.OverlayMode,
                ShowLegend = result.ShowLegend,
                Step = result.Step,
                DataMinimum = result.DataMinimum,
                DataMaximum = result.DataMaximum,
                MaxOverlayDurationMs = result.MaxOverlayDurationMs,
                XAxisLabelFormat = result.OverlayMode ? "0.##" : "HH:mm\ndd.MM",
                XAxisTitle = result.OverlayMode ? (overlayAxisTitle ?? string.Empty) : string.Empty,
                AxisMinimum = result.AxisMinimum,
                AxisMaximum = result.AxisMaximum,
                Range = new ChartRangeViewModel
                {
                    IsActive = !double.IsNaN(result.RangeStartOa) && !double.IsNaN(result.RangeEndOa),
                    Start = result.RangeStartOa,
                    End = result.RangeEndOa
                },
                Series = series
            };
        }
    }
}
