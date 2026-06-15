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
            var envelopeX = new double[pointCount];
            var targetX = new double[pointCount];
            var rawY = new double[pointCount];
            var coolingEnvelopeY = new double[pointCount];
            double targetOriginX = roles.Target.XValues[0];
            double verticalOffset = roles.NewFunc.YValues[0] - roles.Target.YValues[0];
            double maxProgress = 0d;
            double runningMinTemperature = double.PositiveInfinity;

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

                envelopeX[i] = targetOriginX + predictedElapsed;
                targetX[i] = roles.Target.XValues[i];
                if (roles.Target.YValues[i] < runningMinTemperature)
                {
                    runningMinTemperature = roles.Target.YValues[i];
                }

                rawY[i] = roles.Target.YValues[i] + verticalOffset;
                coolingEnvelopeY[i] = runningMinTemperature + verticalOffset;
            }

            if (pointCount < MinimumObservedPoints)
            {
                return null;
            }

            double[] xValues = SpreadFrozenProgressSegments(envelopeX, targetX);

            // While the new FUNC still has measured data the forecast follows the
            // monotonic cooling trend, so an early FULL defrost rebound does not show
            // up as a temperature hump in a region where the new test was still
            // cooling. The full FULL template (with defrosts) resumes only after the
            // new FUNC data ends.
            double[] yValues = ApplyNewFuncRegion(rawY, coolingEnvelopeY, xValues, targetOriginX, roles.NewFunc);

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

        // The cooling-progress mapping freezes predicted time whenever the target
        // temperature rises (defrost rebound), which collapses every rebound point
        // onto a single X and produces the torn, vertical-step look. Here those
        // frozen runs are spread across the predicted time gap proportionally to the
        // target's own elapsed time, so rebound peaks stay visible but the forecast
        // advances smoothly instead of jumping.
        private static double[] ApplyNewFuncRegion(
            double[] rawY,
            double[] coolingEnvelopeY,
            double[] xValues,
            double targetOriginX,
            ChartPipelineSeries newFunc)
        {
            int newFuncCount = CountPoints(newFunc);
            double newFuncSpan = newFuncCount > 0
                ? newFunc.XValues[newFuncCount - 1] - newFunc.XValues[0]
                : 0d;

            var result = new double[rawY.Length];
            for (int i = 0; i < rawY.Length; i++)
            {
                double elapsed = xValues[i] - targetOriginX;
                result[i] = elapsed <= newFuncSpan + 1e-9
                    ? coolingEnvelopeY[i]
                    : rawY[i];
            }

            return result;
        }

        private static double[] SpreadFrozenProgressSegments(double[] envelopeX, double[] targetX)
        {
            var result = (double[])envelopeX.Clone();
            int anchor = 0;
            for (int i = 1; i < envelopeX.Length; i++)
            {
                if (envelopeX[i] <= envelopeX[anchor] + 1e-9)
                {
                    continue;
                }

                double timeSpan = targetX[i] - targetX[anchor];
                double xSpan = envelopeX[i] - envelopeX[anchor];
                for (int m = anchor + 1; m < i; m++)
                {
                    double fraction = timeSpan > 1e-9
                        ? (targetX[m] - targetX[anchor]) / timeSpan
                        : 0d;
                    fraction = Math.Max(0d, Math.Min(1d, fraction));
                    result[m] = envelopeX[anchor] + (xSpan * fraction);
                }

                anchor = i;
            }

            return result;
        }

        private double MapNewFuncElapsedToPredictedFullElapsed(
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

            double funcSpeedRatio = newFuncElapsed / oldFuncElapsed;
            double dampedRatio = Math.Pow(funcSpeedRatio, _funcWarpDamping);
            return oldFullElapsed * dampedRatio;
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
