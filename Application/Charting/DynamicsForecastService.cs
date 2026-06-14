using System;
using System.Collections.Generic;
using System.Globalization;

namespace JSQViewer.Application.Charting
{
    public sealed class DynamicsForecastService
    {
        private const int MinimumObservedPoints = 2;

        public ChartPipelineSeries BuildForecast(IReadOnlyList<ChartPipelineSeries> sourceSeries)
        {
            return BuildForecast(sourceSeries, null);
        }

        public ChartPipelineSeries BuildForecast(IReadOnlyList<ChartPipelineSeries> sourceSeries, DynamicsForecastRoleSelection roleSelection)
        {
            ForecastRoles roles = ResolveRoles(sourceSeries);
            if (roleSelection != null && roleSelection.HasAllRoles)
            {
                roles = ResolveExplicitRoles(sourceSeries, roleSelection);
            }

            if (roles == null)
            {
                return null;
            }

            int newFuncCount = CountPoints(roles.NewFunc);
            int oldFuncCount = CountPoints(roles.OldFunc);
            int targetCount = CountPoints(roles.Target);
            if (newFuncCount < MinimumObservedPoints || oldFuncCount < MinimumObservedPoints || targetCount < MinimumObservedPoints)
            {
                return null;
            }

            var xValues = new List<double>(targetCount);
            var yValues = new List<double>(targetCount);
            double targetOriginX = roles.Target.XValues[0];
            double newFuncStartY = roles.NewFunc.YValues[0];
            double verticalOffset = newFuncStartY - roles.Target.YValues[0];
            bool cooling = IsCoolingPair(roles.OldFunc, roles.NewFunc);
            double loadedProgressRange = ResolveLoadedProgressRange(roles.Target, cooling);
            double maxLoadedProgress = 0d;

            for (int i = 0; i < targetCount; i++)
            {
                double x = roles.Target.XValues[i];
                double loadedProgress = DirectionalDeltaFromStart(roles.Target, roles.Target.YValues[i], cooling);
                if (loadedProgress > maxLoadedProgress)
                {
                    maxLoadedProgress = loadedProgress;
                }

                double offsetFactor = loadedProgressRange > 1e-9
                    ? 1d - Math.Min(1d, Math.Max(0d, maxLoadedProgress / loadedProgressRange))
                    : 0d;
                xValues.Add(targetOriginX + MapOldElapsedToNewElapsed(roles.OldFunc, roles.NewFunc, x - roles.Target.XValues[0], cooling));
                yValues.Add(roles.Target.YValues[i] + (verticalOffset * offsetFactor));
            }

            if (xValues.Count < MinimumObservedPoints)
            {
                return null;
            }

            return new ChartPipelineSeries
            {
                Code = roles.NewFunc.Code + "::forecast",
                LegendText = string.Format(CultureInfo.InvariantCulture, "Прогноз: {0}", roles.Target.LegendText ?? roles.Target.Code),
                SourceRoot = roles.NewFunc.SourceRoot,
                XValues = xValues.ToArray(),
                YValues = yValues.ToArray(),
                BorderWidth = 2,
                IsVisibleInLegend = true,
                IsForecast = true
            };
        }

        private static bool IsCoolingPair(ChartPipelineSeries oldFunc, ChartPipelineSeries newFunc)
        {
            int oldCount = CountPoints(oldFunc);
            int newCount = CountPoints(newFunc);
            if (oldCount < MinimumObservedPoints || newCount < MinimumObservedPoints)
            {
                return true;
            }

            double oldDelta = oldFunc.YValues[oldCount - 1] - oldFunc.YValues[0];
            double newDelta = newFunc.YValues[newCount - 1] - newFunc.YValues[0];
            if (Math.Abs(oldDelta) <= 1e-9 || Math.Abs(newDelta) <= 1e-9)
            {
                return true;
            }

            return oldDelta + newDelta < 0d;
        }

        private static double MapOldElapsedToNewElapsed(ChartPipelineSeries oldFunc, ChartPipelineSeries newFunc, double oldElapsed, bool cooling)
        {
            int oldCount = CountPoints(oldFunc);
            int newCount = CountPoints(newFunc);
            if (oldCount < MinimumObservedPoints || newCount < MinimumObservedPoints)
            {
                return oldElapsed;
            }

            double oldStartX = oldFunc.XValues[0];
            double oldEndElapsed = oldFunc.XValues[oldCount - 1] - oldStartX;
            double clampedOldElapsed = Math.Max(0d, Math.Min(oldElapsed, oldEndElapsed));
            double oldX = oldStartX + clampedOldElapsed;
            double oldDelta = SampleDirectionalDelta(oldFunc, oldX, cooling);
            double newStartX = newFunc.XValues[0];
            double newEndElapsed = newFunc.XValues[newCount - 1] - newStartX;
            double? mappedX = InterpolateXForDirectionalDelta(newFunc, oldDelta, cooling);
            if (mappedX.HasValue)
            {
                double mappedElapsed = mappedX.Value - newStartX;
                if (oldElapsed <= oldEndElapsed)
                {
                    return mappedElapsed;
                }

                return mappedElapsed + ((oldElapsed - oldEndElapsed) * EstimateTailScale(oldFunc, newFunc, cooling));
            }

            double maxNewDelta = MaxDirectionalDelta(newFunc, cooling);
            double? oldAtMaxNewDeltaX = InterpolateXForDirectionalDelta(oldFunc, maxNewDelta, cooling);
            if (!oldAtMaxNewDeltaX.HasValue)
            {
                return oldElapsed;
            }

            double oldAtMaxNewDeltaElapsed = oldAtMaxNewDeltaX.Value - oldStartX;
            double tailScale = EstimateScaleNearDelta(oldFunc, newFunc, maxNewDelta, cooling);
            return newEndElapsed + ((oldElapsed - oldAtMaxNewDeltaElapsed) * tailScale);
        }

        private static double EstimateTailScale(ChartPipelineSeries oldFunc, ChartPipelineSeries newFunc, bool cooling)
        {
            double maxOldDelta = MaxDirectionalDelta(oldFunc, cooling);
            return EstimateScaleNearDelta(oldFunc, newFunc, maxOldDelta, cooling);
        }

        private static double EstimateScaleNearDelta(ChartPipelineSeries oldFunc, ChartPipelineSeries newFunc, double endDelta, bool cooling)
        {
            if (endDelta <= 1e-9)
            {
                return 1d;
            }

            double startDelta = endDelta * 0.5d;
            double? oldStartX = InterpolateXForDirectionalDelta(oldFunc, startDelta, cooling);
            double? oldEndX = InterpolateXForDirectionalDelta(oldFunc, endDelta, cooling);
            double? newStartX = InterpolateXForDirectionalDelta(newFunc, startDelta, cooling);
            double? newEndX = InterpolateXForDirectionalDelta(newFunc, endDelta, cooling);
            if (!oldStartX.HasValue || !oldEndX.HasValue || !newStartX.HasValue || !newEndX.HasValue)
            {
                return 1d;
            }

            double oldSpan = oldEndX.Value - oldStartX.Value;
            double newSpan = newEndX.Value - newStartX.Value;
            if (oldSpan <= 1e-9 || newSpan <= 1e-9)
            {
                return 1d;
            }

            double scale = newSpan / oldSpan;
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d)
            {
                return 1d;
            }

            return Math.Max(0.1d, Math.Min(20d, scale));
        }

        private static double SampleDirectionalDelta(ChartPipelineSeries series, double x, bool cooling)
        {
            double y = SampleWithHold(series, x);
            return DirectionalDeltaFromStart(series, y, cooling);
        }

        private static double MaxDirectionalDelta(ChartPipelineSeries series, bool cooling)
        {
            int count = CountPoints(series);
            if (count == 0)
            {
                return 0d;
            }

            double maxDelta = 0d;
            for (int i = 0; i < count; i++)
            {
                double delta = cooling ? series.YValues[0] - series.YValues[i] : series.YValues[i] - series.YValues[0];
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                }
            }

            return maxDelta;
        }

        private static double ResolveLoadedProgressRange(ChartPipelineSeries series, bool cooling)
        {
            return MaxDirectionalDelta(series, cooling);
        }

        private static double DirectionalDeltaFromStart(ChartPipelineSeries series, double y, bool cooling)
        {
            return cooling ? series.YValues[0] - y : y - series.YValues[0];
        }

        private static double SampleWithHold(ChartPipelineSeries series, double x)
        {
            int count = CountPoints(series);
            if (count == 0)
            {
                return 0d;
            }

            if (x <= series.XValues[0])
            {
                return series.YValues[0];
            }

            if (x >= series.XValues[count - 1])
            {
                return series.YValues[count - 1];
            }

            for (int i = 1; i < count; i++)
            {
                double x0 = series.XValues[i - 1];
                double x1 = series.XValues[i];
                if (x < x0 || x > x1 || x1 <= x0)
                {
                    continue;
                }

                double y0 = series.YValues[i - 1];
                double y1 = series.YValues[i];
                double t = (x - x0) / (x1 - x0);
                return y0 + ((y1 - y0) * t);
            }

            return series.YValues[count - 1];
        }

        private static ForecastRoles ResolveExplicitRoles(IReadOnlyList<ChartPipelineSeries> sourceSeries, DynamicsForecastRoleSelection selection)
        {
            if (sourceSeries == null || selection == null || !selection.HasAllRoles)
            {
                return null;
            }

            return new ForecastRoles
            {
                OldFunc = FindByCode(sourceSeries, selection.OldFuncCode),
                Target = FindByCode(sourceSeries, selection.TargetCode),
                NewFunc = FindByCode(sourceSeries, selection.NewFuncCode)
            };
        }

        private static ChartPipelineSeries FindByCode(IReadOnlyList<ChartPipelineSeries> sourceSeries, string code)
        {
            if (sourceSeries == null || string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            for (int i = 0; i < sourceSeries.Count; i++)
            {
                ChartPipelineSeries series = sourceSeries[i];
                if (series != null && string.Equals(series.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    return series;
                }
            }

            return null;
        }

        private static ForecastRoles ResolveRoles(IReadOnlyList<ChartPipelineSeries> sourceSeries)
        {
            if (sourceSeries == null || sourceSeries.Count < 3)
            {
                return null;
            }

            var funcSeries = new List<ChartPipelineSeries>();
            ChartPipelineSeries target = null;
            for (int i = 0; i < sourceSeries.Count; i++)
            {
                ChartPipelineSeries series = sourceSeries[i];
                if (!IsUsable(series) || series.IsForecast)
                {
                    continue;
                }

                string roleText = BuildRoleText(series);
                if (ContainsToken(roleText, "FULL") || ContainsToken(roleText, "HALF"))
                {
                    if (target == null || MaxX(series) > MaxX(target))
                    {
                        target = series;
                    }
                }
                else if (ContainsToken(roleText, "FUNC"))
                {
                    funcSeries.Add(series);
                }
            }

            if (target == null || funcSeries.Count < 2)
            {
                return null;
            }

            funcSeries.Sort(delegate (ChartPipelineSeries left, ChartPipelineSeries right)
            {
                int durationCompare = MaxX(left).CompareTo(MaxX(right));
                if (durationCompare != 0)
                {
                    return durationCompare;
                }

                return CountPoints(left).CompareTo(CountPoints(right));
            });

            return new ForecastRoles
            {
                NewFunc = funcSeries[0],
                OldFunc = funcSeries[funcSeries.Count - 1],
                Target = target
            };
        }

        private static bool IsUsable(ChartPipelineSeries series)
        {
            return series != null
                && series.XValues != null
                && series.YValues != null
                && Math.Min(series.XValues.Length, series.YValues.Length) > 0;
        }

        private static int CountPoints(ChartPipelineSeries series)
        {
            if (series == null || series.XValues == null || series.YValues == null)
            {
                return 0;
            }

            return Math.Min(series.XValues.Length, series.YValues.Length);
        }

        private static double MaxX(ChartPipelineSeries series)
        {
            int count = CountPoints(series);
            if (count == 0)
            {
                return double.NaN;
            }

            return series.XValues[count - 1];
        }

        private static string BuildRoleText(ChartPipelineSeries series)
        {
            return string.Join(" ",
                series.Code ?? string.Empty,
                series.LegendText ?? string.Empty,
                series.SourceRoot ?? string.Empty).ToUpperInvariant();
        }

        private static bool ContainsToken(string text, string token)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static double? InterpolateXForDirectionalDelta(ChartPipelineSeries series, double targetDelta, bool cooling)
        {
            int count = CountPoints(series);
            if (count == 0)
            {
                return null;
            }

            for (int i = 0; i < count; i++)
            {
                double delta = cooling ? series.YValues[0] - series.YValues[i] : series.YValues[i] - series.YValues[0];
                if (Math.Abs(delta - targetDelta) <= 1e-9)
                {
                    return series.XValues[i];
                }
            }

            for (int i = 1; i < count; i++)
            {
                double d0 = cooling ? series.YValues[0] - series.YValues[i - 1] : series.YValues[i - 1] - series.YValues[0];
                double d1 = cooling ? series.YValues[0] - series.YValues[i] : series.YValues[i] - series.YValues[0];
                bool contains = targetDelta >= Math.Min(d0, d1) && targetDelta <= Math.Max(d0, d1);
                if (!contains || Math.Abs(d1 - d0) <= 1e-9)
                {
                    continue;
                }

                double x0 = series.XValues[i - 1];
                double x1 = series.XValues[i];
                double t = (targetDelta - d0) / (d1 - d0);
                return x0 + ((x1 - x0) * t);
            }

            return null;
        }

        private sealed class ForecastRoles
        {
            public ChartPipelineSeries OldFunc { get; set; }

            public ChartPipelineSeries Target { get; set; }

            public ChartPipelineSeries NewFunc { get; set; }
        }
    }
}
