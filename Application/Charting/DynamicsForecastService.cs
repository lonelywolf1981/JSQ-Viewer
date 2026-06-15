using System;
using System.Collections.Generic;
using System.Globalization;

namespace JSQViewer.Application.Charting
{
    public sealed class DynamicsForecastService
    {
        private const int MinimumObservedPoints = 2;
        private const double FirstCoolingMinimumDropThreshold = 5.0;
        private const double FirstCoolingMinimumReboundThreshold = 0.5;
        private const double FirstCoolingMinimumLowerTolerance = 0.3;
        private const double FirstCoolingMinimumReboundLookAheadHours = 0.5;
        private const double FirstCoolingMinimumStabilityLookAheadHours = 2.0;

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

            int oldFuncMinIndex = FindFirstCoolingMinimumIndex(roles.OldFunc);
            int targetMinIndex = FindFirstCoolingMinimumIndex(roles.Target);
            int newFuncMinIndex = FindFirstCoolingMinimumIndex(roles.NewFunc);
            if (oldFuncMinIndex <= 0 || targetMinIndex <= 0 || newFuncMinIndex <= 0)
            {
                return null;
            }

            var xValues = new List<double>(targetMinIndex + 1);
            var yValues = new List<double>(targetMinIndex + 1);
            double targetOriginX = roles.Target.XValues[0];
            double verticalOffset = roles.NewFunc.YValues[0] - roles.Target.YValues[0];
            double maxProgress = 0d;

            for (int i = 0; i <= targetMinIndex; i++)
            {
                double progress = CoolingProgressAt(roles.Target, targetMinIndex, roles.Target.YValues[i]);
                if (progress > maxProgress)
                {
                    maxProgress = progress;
                }

                double predictedElapsed = MapNewFuncElapsedToPredictedFullElapsed(
                    roles.OldFunc,
                    oldFuncMinIndex,
                    roles.Target,
                    targetMinIndex,
                    roles.NewFunc,
                    newFuncMinIndex,
                    maxProgress);

                xValues.Add(targetOriginX + predictedElapsed);
                yValues.Add(roles.Target.YValues[i] + verticalOffset);
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

        private static double MapNewFuncElapsedToPredictedFullElapsed(
            ChartPipelineSeries oldFunc,
            int oldFuncMinIndex,
            ChartPipelineSeries oldFull,
            int oldFullMinIndex,
            ChartPipelineSeries newFunc,
            int newFuncMinIndex,
            double progress)
        {
            if (progress <= 1e-9)
            {
                return 0d;
            }

            double oldFuncElapsed = ElapsedAtProgress(oldFunc, oldFuncMinIndex, progress);
            double oldFullElapsed = ElapsedAtProgress(oldFull, oldFullMinIndex, progress);
            double newFuncElapsed = ElapsedAtProgress(newFunc, newFuncMinIndex, progress);
            if (oldFuncElapsed <= 1e-9)
            {
                return oldFullElapsed;
            }

            return newFuncElapsed * (oldFullElapsed / oldFuncElapsed);
        }

        private static double ElapsedAtProgress(ChartPipelineSeries series, int minIndex, double targetProgress)
        {
            double startX = series.XValues[0];
            if (targetProgress <= 1e-9)
            {
                return 0d;
            }

            for (int i = 1; i <= minIndex; i++)
            {
                double p0 = CoolingProgressAt(series, minIndex, series.YValues[i - 1]);
                double p1 = CoolingProgressAt(series, minIndex, series.YValues[i]);
                bool contains = targetProgress >= Math.Min(p0, p1) && targetProgress <= Math.Max(p0, p1);
                if (!contains || Math.Abs(p1 - p0) <= 1e-9)
                {
                    continue;
                }

                double t = (targetProgress - p0) / (p1 - p0);
                double x = series.XValues[i - 1] + ((series.XValues[i] - series.XValues[i - 1]) * t);
                return Math.Max(0d, x - startX);
            }

            return Math.Max(0d, series.XValues[minIndex] - startX);
        }

        private static double CoolingProgressAt(ChartPipelineSeries series, int minIndex, double value)
        {
            double range = series.YValues[0] - series.YValues[minIndex];
            if (Math.Abs(range) <= 1e-9)
            {
                return 0d;
            }

            return Math.Max(0d, Math.Min(1d, (series.YValues[0] - value) / range));
        }

        private static int FindFirstCoolingMinimumIndex(ChartPipelineSeries series)
        {
            int count = CountPoints(series);
            if (count < MinimumObservedPoints)
            {
                return -1;
            }

            int fallbackMinIndex = 0;
            double fallbackMin = series.YValues[0];
            for (int i = 1; i < count; i++)
            {
                if (series.YValues[i] < fallbackMin)
                {
                    fallbackMin = series.YValues[i];
                    fallbackMinIndex = i;
                }
            }

            double firstValue = series.YValues[0];
            for (int i = 0; i < count; i++)
            {
                double value = series.YValues[i];
                if (firstValue - value < FirstCoolingMinimumDropThreshold)
                {
                    continue;
                }

                double reboundEnd = series.XValues[i] + FirstCoolingMinimumReboundLookAheadHours;
                double stabilityEnd = series.XValues[i] + FirstCoolingMinimumStabilityLookAheadHours;
                double futureMax = value;
                double futureMin = value;
                double reboundWindowMin = value;
                int reboundWindowMinIndex = i;
                bool hasFuture = false;

                for (int j = i + 1; j < count && series.XValues[j] <= stabilityEnd; j++)
                {
                    double future = series.YValues[j];
                    if (series.XValues[j] <= reboundEnd && future > futureMax)
                    {
                        futureMax = future;
                    }

                    if (series.XValues[j] <= reboundEnd && future < reboundWindowMin)
                    {
                        reboundWindowMin = future;
                        reboundWindowMinIndex = j;
                    }

                    if (future < futureMin)
                    {
                        futureMin = future;
                    }

                    hasFuture = true;
                }

                if (!hasFuture)
                {
                    continue;
                }

                bool hasRebound = futureMax - value >= FirstCoolingMinimumReboundThreshold;
                bool doesNotContinueCooling = futureMin >= value - FirstCoolingMinimumLowerTolerance;
                if (hasRebound && doesNotContinueCooling)
                {
                    return reboundWindowMinIndex;
                }
            }

            return fallbackMinIndex;
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

        private sealed class ForecastRoles
        {
            public ChartPipelineSeries OldFunc { get; set; }

            public ChartPipelineSeries Target { get; set; }

            public ChartPipelineSeries NewFunc { get; set; }
        }
    }
}
