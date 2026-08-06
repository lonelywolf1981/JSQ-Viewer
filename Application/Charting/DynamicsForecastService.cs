using System;
using System.Collections.Generic;
using System.Globalization;

namespace JSQViewer.Application.Charting
{
    public sealed class DynamicsForecastService
    {
        // The predicted FULL duration is governed mostly by the load thermal mass, not
        // by how fast the FUNC test cooled. Applying the raw FUNC speed ratio over-
        // stretches the timeline (a 2x slower new FUNC tripled the predicted span on
        // real data). Damping pulls that ratio toward 1: 0 ignores FUNC speed entirely
        // (keep the old FULL timeline), 1 is the original multiplicative warp.
        // Damping pulls the FUNC speed ratio toward 1 (0 = keep old FULL timeline,
        // 1 = full multiplicative warp). 0.45 is the compromise across the validated
        // real pairs: near-perfect on NUY45RA (MAE ~0.5 C) and good in the early region
        // on OMEGA NPT14, without the 3x timeline overshoot of the original full warp.
        private const double DefaultFuncWarpDamping = 0.45;

        private const int MinimumObservedPoints = 2;
        private const double ReferenceFuncStartToleranceCelsius = 3.0;
        private const double ReferenceFuncDurationRatioTolerance = 2.0;
        private const double FirstCoolingMinimumDropThreshold = 5.0;
        private const double FirstCoolingMinimumReboundThreshold = 0.5;
        private const double FirstCoolingMinimumLowerTolerance = 0.3;
        private const double FirstCoolingMinimumReboundLookAheadHours = 0.5;
        private const double FirstCoolingMinimumStabilityLookAheadHours = 2.0;

        private readonly double _funcWarpDamping;

        public DynamicsForecastService()
            : this(DefaultFuncWarpDamping)
        {
        }

        public DynamicsForecastService(double funcWarpDamping)
        {
            _funcWarpDamping = Math.Max(0d, Math.Min(1d, funcWarpDamping));
        }

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

            int pointCount = targetMinIndex + 1;
            if (pointCount < MinimumObservedPoints)
            {
                return null;
            }

            double targetOriginX = roles.Target.XValues[0];
            double startOffset = roles.NewFunc.YValues[0] - roles.Target.YValues[0];
            double newFuncSpan = SeriesDuration(roles.NewFunc);

            // Damped FUNC speed ratio applied as a single GLOBAL factor to the old FULL
            // timeline. Uniform (not per-point) scaling keeps every defrost cycle and the
            // gaps between them in the same proportions as the old FULL - so the defrost
            // pattern looks like the reference - while the overall pace still follows the
            // damped FUNC warp. Uniform scaling also makes the time axis strictly
            // increasing, so no vertical "spread" correction is needed.
            double oldFuncToMin = roles.OldFunc.XValues[oldFuncMinIndex] - roles.OldFunc.XValues[0];
            double newFuncToMin = roles.NewFunc.XValues[newFuncMinIndex] - roles.NewFunc.XValues[0];
            double globalFactor = (oldFuncToMin > 1e-9 && newFuncToMin > 1e-9)
                ? Math.Pow(newFuncToMin / oldFuncToMin, _funcWarpDamping)
                : 1d;

            var xValues = new double[pointCount];
            var yValues = new double[pointCount];
            double maxProgress = 0d;
            double runningMinTemperature = double.PositiveInfinity;

            for (int i = 0; i <= targetMinIndex; i++)
            {
                double progress = CoolingProgressAt(roles.Target, targetMinIndex, roles.Target.YValues[i]);
                if (progress > maxProgress)
                {
                    maxProgress = progress;
                }

                if (roles.Target.YValues[i] < runningMinTemperature)
                {
                    runningMinTemperature = roles.Target.YValues[i];
                }

                double elapsed = (roles.Target.XValues[i] - targetOriginX) * globalFactor;
                xValues[i] = targetOriginX + elapsed;

                // Decay the start lift to zero as cooling completes, so the forecast
                // finishes at the old FULL minimum instead of a constant offset above it.
                double decayedOffset = startOffset * (1d - maxProgress);

                // While the new FUNC still has measured data, follow the monotonic
                // cooling trend (suppress early FULL defrost rebounds); afterwards
                // reproduce the FULL template with its defrosts.
                double baseTemperature = elapsed <= newFuncSpan + 1e-9
                    ? runningMinTemperature
                    : roles.Target.YValues[i];
                yValues[i] = baseTemperature + decayedOffset;
            }

            return new ChartPipelineSeries
            {
                Code = roles.NewFunc.Code + "::forecast",
                LegendText = string.Format(CultureInfo.InvariantCulture, "Прогноз: {0}", roles.Target.LegendText ?? roles.Target.Code),
                SourceRoot = roles.NewFunc.SourceRoot,
                XValues = xValues,
                YValues = yValues,
                BorderWidth = 2,
                IsVisibleInLegend = true,
                IsForecast = true,
                ForecastWarnings = BuildReferenceQualityWarnings(roles)
            };
        }

        // Non-blocking diagnostics: the forecast assumes the old FUNC is a good analogy
        // for the new FUNC. When the reference starts at a very different temperature or
        // ran for a very different duration the analogy is weak, so flag it without
        // aborting the calculation.
        private static IReadOnlyList<DynamicsForecastWarning> BuildReferenceQualityWarnings(ForecastRoles roles)
        {
            var warnings = new List<DynamicsForecastWarning>();

            double startDelta = roles.NewFunc.YValues[0] - roles.OldFunc.YValues[0];
            if (Math.Abs(startDelta) > ReferenceFuncStartToleranceCelsius)
            {
                warnings.Add(new DynamicsForecastWarning(
                    DynamicsForecastWarningCode.ReferenceFuncStartTemperatureMismatch, startDelta));
            }

            double oldDuration = SeriesDuration(roles.OldFunc);
            double newDuration = SeriesDuration(roles.NewFunc);
            if (oldDuration > 1e-9 && newDuration > 1e-9)
            {
                double ratio = newDuration / oldDuration;
                double magnitude = Math.Max(ratio, 1d / ratio);
                if (magnitude > ReferenceFuncDurationRatioTolerance)
                {
                    warnings.Add(new DynamicsForecastWarning(
                        DynamicsForecastWarningCode.ReferenceFuncDurationMismatch, ratio));
                }
            }

            return warnings;
        }

        private static double SeriesDuration(ChartPipelineSeries series)
        {
            int count = CountPoints(series);
            if (count < 1)
            {
                return 0d;
            }

            return series.XValues[count - 1] - series.XValues[0];
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
