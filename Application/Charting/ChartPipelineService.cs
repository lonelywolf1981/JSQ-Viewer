using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JSQViewer.Core;

namespace JSQViewer.Application.Charting
{
    public sealed class ChartPipelineService
    {
        private readonly SeriesSliceService _seriesSliceService;

        public ChartPipelineService(SeriesSliceService seriesSliceService)
        {
            _seriesSliceService = seriesSliceService ?? throw new ArgumentNullException(nameof(seriesSliceService));
        }

        public ChartPipelineResult Execute(ChartPipelineRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            TestData data = request.Data;
            if (data == null || data.RowCount == 0 || data.TimestampsMs == null || data.TimestampsMs.Length == 0)
            {
                return new ChartPipelineResult
                {
                    HasData = false,
                    OverlayMode = request.OverlayMode,
                    ShowLegend = false,
                    Step = 1,
                    Series = new ChartPipelineSeries[0]
                };
            }

            List<string> selectedCodes = NormalizeCodes(request.SelectedCodes);
            if (selectedCodes.Count == 0)
            {
                return new ChartPipelineResult
                {
                    HasData = true,
                    OverlayMode = request.OverlayMode,
                    ShowLegend = false,
                    Step = 1,
                    Series = new ChartPipelineSeries[0]
                };
            }

            int step = ResolveStep(
                data.TimestampsMs.Length,
                request.AutoStepEnabled,
                request.ManualStep,
                request.TargetPoints,
                request.SelectedChannelCount);

            if (step > 1 && ShouldForceStepOneForMultiSource(data, selectedCodes))
            {
                step = 1;
            }

            List<string> codesToRender = selectedCodes;
            SeriesSlice slice = _seriesSliceService.GetOrBuild(
                request.DataVersion,
                data,
                codesToRender,
                data.TimestampsMs[0],
                data.TimestampsMs[data.TimestampsMs.Length - 1],
                step);
            long[] timestamps = slice.Timestamps;
            bool overlayMode = request.OverlayMode;
            bool showLegend = codesToRender.Count <= 20;
            ChartAxisSettings effectiveXAxis = NormalizeAxis(request.XAxis);
            ChartAxisSettings effectiveYAxis = NormalizeAxis(request.YAxis);

            long maxOverlayDurationMs = 0L;
            var series = new List<ChartPipelineSeries>(codesToRender.Count);
            for (int codeIndex = 0; codeIndex < codesToRender.Count; codeIndex++)
            {
                string code = codesToRender[codeIndex];
                double?[] values;
                if (!slice.Series.TryGetValue(code, out values))
                {
                    continue;
                }

                int count = CountNonNull(values);
                if (count <= 0)
                {
                    series.Add(new ChartPipelineSeries
                    {
                        Code = code,
                        LegendText = BuildSeriesLegendText(data, code),
                        XValues = new double[0],
                        YValues = new double[0],
                        BorderWidth = codesToRender.Count > 20 ? 1 : 2,
                        IsVisibleInLegend = showLegend
                    });
                    continue;
                }

                int n = Math.Min(timestamps.Length, values.Length);
                int firstValueIndex = FirstValueIndex(values, n);
                long seriesBaseMs = firstValueIndex >= 0 ? timestamps[firstValueIndex] : timestamps[0];
                double[] xArr = new double[count];
                double[] yArr = new double[count];
                int writeIndex = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!values[i].HasValue)
                    {
                        continue;
                    }

                    long relativeMs = Math.Max(0L, timestamps[i] - seriesBaseMs);
                    xArr[writeIndex] = overlayMode ? relativeMs / 3600000.0 : timestamps[i];
                    yArr[writeIndex] = values[i].Value;
                    if (overlayMode && relativeMs > maxOverlayDurationMs)
                    {
                        maxOverlayDurationMs = relativeMs;
                    }

                    writeIndex++;
                }

                series.Add(new ChartPipelineSeries
                {
                    Code = code,
                    LegendText = BuildSeriesLegendText(data, code),
                    XValues = xArr,
                    YValues = yArr,
                    BorderWidth = codesToRender.Count > 20 ? 1 : 2,
                    IsVisibleInLegend = showLegend
                });
            }

            double dataMin = double.NaN;
            double dataMax = double.NaN;
            if (overlayMode)
            {
                long maxDurationMs = Math.Max(ResolveOverlayMaxDurationMs(data, selectedCodes), maxOverlayDurationMs);
                dataMin = 0.0;
                dataMax = Math.Max(1.0 / 3600.0, maxDurationMs / 3600000.0);
            }
            else if (timestamps.Length > 0)
            {
                dataMin = timestamps[0];
                dataMax = timestamps[timestamps.Length - 1];
            }

            return new ChartPipelineResult
            {
                HasData = true,
                OverlayMode = overlayMode,
                ShowLegend = showLegend,
                Step = step,
                DataMinimum = dataMin,
                DataMaximum = dataMax,
                SelectedRangeStart = request.SelectedRangeStart,
                SelectedRangeEnd = request.SelectedRangeEnd,
                MaxOverlayDurationMs = maxOverlayDurationMs,
                XAxis = effectiveXAxis,
                YAxis = effectiveYAxis,
                Series = series
            };
        }

        private static ChartAxisSettings NormalizeAxis(ChartAxisSettings settings)
        {
            if (settings == null || !settings.IsManualEnabled)
            {
                return ChartAxisSettings.Automatic();
            }

            double? minimum = NormalizeFinite(settings.Minimum);
            double? maximum = NormalizeFinite(settings.Maximum);
            double? interval = NormalizePositive(settings.Interval);

            if (minimum.HasValue && maximum.HasValue && maximum.Value <= minimum.Value)
            {
                minimum = null;
                maximum = null;
            }

            return ChartAxisSettings.ForManual(minimum, maximum, interval);
        }

        private static double? NormalizeFinite(double? value)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                return null;
            }

            return value.Value;
        }

        private static double? NormalizePositive(double? value)
        {
            double? normalized = NormalizeFinite(value);
            if (!normalized.HasValue || normalized.Value <= 0d)
            {
                return null;
            }

            return normalized.Value;
        }

        private static List<string> NormalizeCodes(IEnumerable<string> selectedCodes)
        {
            if (selectedCodes == null)
            {
                return new List<string>();
            }

            return selectedCodes.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        private static int ResolveStep(int totalPoints, bool autoStepEnabled, int manualStep, int targetPoints, int selectedChannelCount)
        {
            if (!autoStepEnabled)
            {
                return Math.Max(1, manualStep);
            }

            int target = Math.Max(1, targetPoints);
            if (selectedChannelCount > 10)
            {
                int maxTotalPoints = 50000;
                int perChannel = Math.Max(200, maxTotalPoints / selectedChannelCount);
                target = Math.Min(target, perChannel);
            }

            return Math.Max(1, totalPoints / target);
        }

        private static bool ShouldForceStepOneForMultiSource(TestData data, IReadOnlyList<string> selectedCodes)
        {
            if (data == null || selectedCodes == null || selectedCodes.Count == 0)
            {
                return false;
            }

            if (data.SourceColumns == null || data.SourceColumns.Count <= 1)
            {
                return false;
            }

            if (data.CodeSources == null)
            {
                return true;
            }

            var selectedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < selectedCodes.Count; i++)
            {
                string source;
                if (data.CodeSources.TryGetValue(selectedCodes[i], out source) && !string.IsNullOrWhiteSpace(source))
                {
                    selectedSources.Add(source);
                    if (selectedSources.Count > 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static long ResolveOverlayMaxDurationMs(TestData data, IReadOnlyList<string> selectedCodes)
        {
            long maxDuration = 0L;
            if (data == null || selectedCodes == null)
            {
                return maxDuration;
            }

            for (int i = 0; i < selectedCodes.Count; i++)
            {
                string code = selectedCodes[i];
                string source = null;
                if (data.CodeSources != null)
                {
                    data.CodeSources.TryGetValue(code, out source);
                }

                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                long startMs;
                long endMs;
                if (data.SourceStartMs == null || !data.SourceStartMs.TryGetValue(source, out startMs))
                {
                    continue;
                }

                if (data.SourceEndMs == null || !data.SourceEndMs.TryGetValue(source, out endMs))
                {
                    continue;
                }

                long duration = Math.Max(0L, endMs - startMs);
                if (duration > maxDuration)
                {
                    maxDuration = duration;
                }
            }

            if (maxDuration == 0L && data.TimestampsMs != null && data.TimestampsMs.Length > 1)
            {
                maxDuration = Math.Max(0L, data.TimestampsMs[data.TimestampsMs.Length - 1] - data.TimestampsMs[0]);
            }

            return maxDuration;
        }

        private static string BuildSeriesLegendText(TestData data, string code)
        {
            string displayCode = NormalizeChannelCodeForDisplay(code);
            if (data == null || data.SourceColumns == null || data.SourceColumns.Count <= 1 || data.CodeSources == null)
            {
                return displayCode;
            }

            string source;
            if (!data.CodeSources.TryGetValue(code, out source) || string.IsNullOrWhiteSpace(source))
            {
                return displayCode;
            }

            string trimmed = source.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            string sourceName = System.IO.Path.GetFileName(trimmed);
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                sourceName = source;
            }

            return string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", sourceName, displayCode);
        }

        private static string NormalizeChannelCodeForDisplay(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            string result = code.Trim();
            int sep = result.IndexOf("::", StringComparison.Ordinal);
            if (sep >= 0)
            {
                result = result.Substring(sep + 2);
            }

            int hash = result.IndexOf('#');
            if (hash > 0)
            {
                result = result.Substring(0, hash);
            }

            return result;
        }

        private static int CountNonNull(double?[] values)
        {
            int count = 0;
            if (values == null)
            {
                return 0;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].HasValue)
                {
                    count++;
                }
            }

            return count;
        }

        private static int FirstValueIndex(double?[] values, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (values[i].HasValue)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
