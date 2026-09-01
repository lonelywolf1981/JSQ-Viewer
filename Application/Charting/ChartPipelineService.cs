using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JSQViewer.Application.Workspace;
using JSQViewer.Core;

namespace JSQViewer.Application.Charting
{
    public sealed class ChartPipelineService
    {
        private readonly SeriesSliceService _seriesSliceService;
        private readonly DynamicsForecastService _dynamicsForecastService;
        private readonly SourceDisplayNameResolver _sourceDisplayNameResolver;
        private readonly T8PlusSeriesBuilder _t8PlusSeriesBuilder = new T8PlusSeriesBuilder();
        private readonly Dictionary<string, T8PlusSeries> _t8PlusCache =
            new Dictionary<string, T8PlusSeries>(StringComparer.OrdinalIgnoreCase);
        private int _t8PlusCacheDataVersion = int.MinValue;

        public ChartPipelineService(SeriesSliceService seriesSliceService)
            : this(seriesSliceService, new DynamicsForecastService(), new SourceDisplayNameResolver())
        {
        }

        public ChartPipelineService(SeriesSliceService seriesSliceService, DynamicsForecastService dynamicsForecastService)
            : this(seriesSliceService, dynamicsForecastService, new SourceDisplayNameResolver())
        {
        }

        public ChartPipelineService(
            SeriesSliceService seriesSliceService,
            DynamicsForecastService dynamicsForecastService,
            SourceDisplayNameResolver sourceDisplayNameResolver)
        {
            _seriesSliceService = seriesSliceService ?? throw new ArgumentNullException(nameof(seriesSliceService));
            _dynamicsForecastService = dynamicsForecastService ?? throw new ArgumentNullException(nameof(dynamicsForecastService));
            _sourceDisplayNameResolver = sourceDisplayNameResolver
                ?? throw new ArgumentNullException(nameof(sourceDisplayNameResolver));
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
                        SourceRoot = ResolveSeriesSourceRoot(data, code),
                        XValues = new double[0],
                        YValues = new double[0],
                        BorderWidth = 1,
                        IsVisibleInLegend = showLegend
                    });
                    continue;
                }

                int n = Math.Min(timestamps.Length, values.Length);
                long seriesBaseMs = overlayMode ? ResolveSeriesBaseMs(data, code, timestamps[0]) : timestamps[0];
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
                    SourceRoot = ResolveSeriesSourceRoot(data, code),
                    XValues = xArr,
                    YValues = yArr,
                    BorderWidth = 1,
                    IsVisibleInLegend = showLegend
                });
            }

            if (overlayMode && request.IncludeDynamicsForecast)
            {
                ChartPipelineSeries forecast = _dynamicsForecastService.BuildForecast(series, request.DynamicsForecastRoleSelection);
                if (forecast != null)
                {
                    series.Add(forecast);
                    showLegend = true;
                }
            }

            IReadOnlyList<ChartLevelLine> levelLines = BuildT8PlusLevels(request, data, overlayMode);

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
                Series = series,
                LevelLines = levelLines
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

        private static long ResolveSeriesBaseMs(TestData data, string code, long fallbackMs)
        {
            string source = null;
            if (data != null && data.CodeSources != null)
            {
                data.CodeSources.TryGetValue(code, out source);
            }

            long startMs;
            if (!string.IsNullOrWhiteSpace(source)
                && data != null
                && data.SourceStartMs != null
                && data.SourceStartMs.TryGetValue(source, out startMs))
            {
                return startMs;
            }

            return fallbackMs;
        }

        private string BuildSeriesLegendText(TestData data, string code)
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

            string sourceName = _sourceDisplayNameResolver.Resolve(data, source);

            return string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", sourceName, displayCode);
        }

        private static string ResolveSeriesSourceRoot(TestData data, string code)
        {
            string source;
            if (data != null
                && data.CodeSources != null
                && data.CodeSources.TryGetValue(code, out source)
                && !string.IsNullOrWhiteSpace(source))
            {
                return source;
            }

            return data == null ? string.Empty : data.Root ?? string.Empty;
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

        private IReadOnlyList<ChartLevelLine> BuildT8PlusLevels(
            ChartPipelineRequest request,
            TestData data,
            bool overlayMode)
        {
            var levels = new List<ChartLevelLine>();
            IReadOnlyList<T8PlusSeriesRequest> requests = request.T8PlusSeries;
            if (requests == null || requests.Count == 0
                || data.TimestampsMs == null || data.TimestampsMs.Length == 0)
            {
                return levels;
            }

            EnsureT8PlusCacheVersion(request.DataVersion);

            bool multipleSources = data.SourceColumns != null && data.SourceColumns.Count > 1;

            for (int i = 0; i < requests.Count; i++)
            {
                T8PlusSeriesRequest item = requests[i];
                if (item == null || !item.HasAny || string.IsNullOrWhiteSpace(item.SourceRoot))
                {
                    continue;
                }

                T8PlusSeries built;
                if (!_t8PlusCache.TryGetValue(item.SourceRoot, out built))
                {
                    built = _t8PlusSeriesBuilder.Build(data, item.SourceRoot);
                    _t8PlusCache[item.SourceRoot] = built;
                }

                if (!built.HasChannels)
                {
                    continue;
                }

                long edgeMs = ResolveVisibleEdgeMs(request, data, item.SourceRoot, overlayMode);
                int edgeIndex = VisibleRangeEdgeResolver.ResolveIndex(data.TimestampsMs, edgeMs);
                if (edgeIndex < 0)
                {
                    continue;
                }

                long startMs = ResolveVisibleStartMs(request, data, item.SourceRoot, overlayMode);
                int valueIndex = FindLastValidIndex(built.Average, edgeIndex, data.TimestampsMs, startMs);
                if (valueIndex < 0)
                {
                    continue;
                }

                int sourceIndex = ResolveSourceIndex(data, item.SourceRoot);
                string sourceName = multipleSources
                    ? _sourceDisplayNameResolver.Resolve(data, item.SourceRoot)
                    : null;

                if (item.ShowMinimum)
                {
                    AddLevel(levels, built.Minimum, valueIndex, item.SourceRoot, sourceIndex, sourceName, ChartSeriesRole.T8Minimum);
                }

                if (item.ShowAverage)
                {
                    AddLevel(levels, built.Average, valueIndex, item.SourceRoot, sourceIndex, sourceName, ChartSeriesRole.T8Average);
                }

                if (item.ShowMaximum)
                {
                    AddLevel(levels, built.Maximum, valueIndex, item.SourceRoot, sourceIndex, sourceName, ChartSeriesRole.T8Maximum);
                }
            }

            return levels;
        }

        private static void AddLevel(
            List<ChartLevelLine> levels,
            double?[] values,
            int index,
            string sourceRoot,
            int sourceIndex,
            string sourceName,
            ChartSeriesRole role)
        {
            if (values == null || index >= values.Length || !values[index].HasValue)
            {
                return;
            }

            double value = values[index].Value;
            levels.Add(new ChartLevelLine(
                sourceRoot, sourceIndex, role, value, BuildLevelLabel(sourceName, role, value)));
        }

        private static string BuildLevelLabel(string sourceName, ChartSeriesRole role, double value)
        {
            string roleText;
            if (role == ChartSeriesRole.T8Minimum)
            {
                roleText = "T8+ мин";
            }
            else if (role == ChartSeriesRole.T8Maximum)
            {
                roleText = "T8+ макс";
            }
            else
            {
                roleText = "T8+ сред";
            }

            string valueText = value.ToString("0.0", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(sourceName)
                ? string.Format(CultureInfo.InvariantCulture, "{0} {1}", roleText, valueText)
                : string.Format(CultureInfo.InvariantCulture, "[{0}] {1} {2}", sourceName, roleText, valueText);
        }

        /// <summary>
        /// Ищет ближайший к правому краю отсчёт с валидным значением, не выходя
        /// за левую границу видимого участка. Без этого отступа линия пропадала бы
        /// на любом пропуске в данных ровно на крае.
        /// </summary>
        private static int FindLastValidIndex(double?[] values, int edgeIndex, long[] timestampsMs, long startMs)
        {
            if (values == null)
            {
                return -1;
            }

            for (int i = Math.Min(edgeIndex, values.Length - 1); i >= 0; i--)
            {
                if (timestampsMs[i] < startMs)
                {
                    return -1;
                }

                if (values[i].HasValue)
                {
                    return i;
                }
            }

            return -1;
        }

        private static long ResolveVisibleEdgeMs(
            ChartPipelineRequest request,
            TestData data,
            string sourceRoot,
            bool overlayMode)
        {
            long lastMs = data.TimestampsMs[data.TimestampsMs.Length - 1];

            double edge = request.SelectedRangeEnd;
            if (double.IsNaN(edge) && request.XAxis != null && request.XAxis.IsManualEnabled && request.XAxis.Maximum.HasValue)
            {
                edge = request.XAxis.Maximum.Value;
            }

            if (double.IsNaN(edge))
            {
                return lastMs;
            }

            if (!overlayMode)
            {
                return (long)edge;
            }

            // В наложении ось задана в часах от начала своего прогона.
            long baseMs = ResolveSourceBaseMs(data, sourceRoot, data.TimestampsMs[0]);
            return baseMs + (long)(edge * 3600000.0);
        }

        private static long ResolveVisibleStartMs(
            ChartPipelineRequest request,
            TestData data,
            string sourceRoot,
            bool overlayMode)
        {
            double start = request.SelectedRangeStart;
            if (double.IsNaN(start) && request.XAxis != null && request.XAxis.IsManualEnabled && request.XAxis.Minimum.HasValue)
            {
                start = request.XAxis.Minimum.Value;
            }

            if (double.IsNaN(start))
            {
                return long.MinValue;
            }

            if (!overlayMode)
            {
                return (long)start;
            }

            long baseMs = ResolveSourceBaseMs(data, sourceRoot, data.TimestampsMs[0]);
            return baseMs + (long)(start * 3600000.0);
        }

        private void EnsureT8PlusCacheVersion(int dataVersion)
        {
            if (_t8PlusCacheDataVersion == dataVersion)
            {
                return;
            }

            _t8PlusCache.Clear();
            _t8PlusCacheDataVersion = dataVersion;
        }

        private static long ResolveSourceBaseMs(TestData data, string sourceRoot, long fallbackMs)
        {
            long startMs;
            if (data != null
                && data.SourceStartMs != null
                && !string.IsNullOrWhiteSpace(sourceRoot)
                && data.SourceStartMs.TryGetValue(sourceRoot, out startMs))
            {
                return startMs;
            }

            return fallbackMs;
        }

        private static int ResolveSourceIndex(TestData data, string sourceRoot)
        {
            if (data == null || data.SourceOrder == null)
            {
                return 0;
            }

            for (int i = 0; i < data.SourceOrder.Length; i++)
            {
                if (string.Equals(data.SourceOrder[i], sourceRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
