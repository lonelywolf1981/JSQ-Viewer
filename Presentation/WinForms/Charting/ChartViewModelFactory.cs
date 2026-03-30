using System;
using System.Collections.Generic;
using JSQViewer.Application.Charting;
using JSQViewer.Presentation.WinForms.ViewModels;

namespace JSQViewer.Presentation.WinForms.Charting
{
    public sealed class ChartViewModelFactory
    {
        private readonly TimestampRangeService _timestampRangeService;

        public ChartViewModelFactory(TimestampRangeService timestampRangeService)
        {
            _timestampRangeService = timestampRangeService ?? throw new ArgumentNullException(nameof(timestampRangeService));
        }

        public ChartViewModel Create(ChartPipelineResult result, string overlayAxisTitle)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var series = new List<ChartSeriesViewModel>(result.Series.Count);
            for (int i = 0; i < result.Series.Count; i++)
            {
                ChartPipelineSeries item = result.Series[i];
                series.Add(new ChartSeriesViewModel
                {
                    Code = item.Code,
                    LegendText = item.LegendText,
                    XValues = ConvertXValues(item.XValues, result.OverlayMode),
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
                DataMinimum = ConvertBound(result.DataMinimum, result.OverlayMode),
                DataMaximum = ConvertBound(result.DataMaximum, result.OverlayMode),
                MaxOverlayDurationMs = result.MaxOverlayDurationMs,
                XAxisLabelFormat = result.OverlayMode ? "0.##" : "HH:mm\ndd.MM",
                XAxisTitle = result.OverlayMode ? (overlayAxisTitle ?? string.Empty) : string.Empty,
                XAxisSettings = CreateAxisSettings(result.XAxisSettings, result.OverlayMode),
                YAxisSettings = CreateAxisSettings(result.YAxisSettings, true),
                Range = new ChartRangeViewModel
                {
                    IsActive = !double.IsNaN(result.SelectedRangeStart) && !double.IsNaN(result.SelectedRangeEnd),
                    Start = ConvertBound(result.SelectedRangeStart, result.OverlayMode),
                    End = ConvertBound(result.SelectedRangeEnd, result.OverlayMode)
                },
                Series = series
            };
        }

        private double[] ConvertXValues(double[] source, bool overlayMode)
        {
            if (source == null || source.Length == 0)
            {
                return new double[0];
            }

            var target = new double[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                target[i] = ConvertBound(source[i], overlayMode);
            }

            return target;
        }

        private double ConvertBound(double value, bool overlayMode)
        {
            if (overlayMode || double.IsNaN(value) || double.IsInfinity(value))
            {
                return value;
            }

            return _timestampRangeService.UnixMsToLocalDateTime((long)value).ToOADate();
        }

        private ChartAxisSettingsViewModel CreateAxisSettings(ChartAxisSettings settings, bool overlayMode)
        {
            settings = settings ?? ChartAxisSettings.Automatic();
            return new ChartAxisSettingsViewModel
            {
                IsManualEnabled = settings.IsManualEnabled,
                Minimum = settings.Minimum.HasValue ? (double?)ConvertBound(settings.Minimum.Value, overlayMode) : null,
                Maximum = settings.Maximum.HasValue ? (double?)ConvertBound(settings.Maximum.Value, overlayMode) : null,
                Step = settings.Step.HasValue ? (double?)ConvertStep(settings.Step.Value, overlayMode) : null
            };
        }

        private double ConvertStep(double value, bool overlayMode)
        {
            if (overlayMode || double.IsNaN(value) || double.IsInfinity(value))
            {
                return value;
            }

            return TimeSpan.FromMilliseconds(value).TotalDays;
        }
    }
}
