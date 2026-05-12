using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;

namespace JSQViewer.Application.Recording
{
    public sealed class GetRecordingInfoUseCase
    {
        private const double FirstCoolingMinimumDropThreshold = 5.0;
        private const double FirstCoolingMinimumReboundThreshold = 0.5;
        private const double FirstCoolingMinimumLowerTolerance = 0.3;
        private const long FirstCoolingMinimumReboundLookAheadMs = 30L * 60_000L;
        private const long FirstCoolingMinimumStabilityLookAheadMs = 120L * 60_000L;

        private readonly TimestampRangeService _timestampRangeService;
        private readonly ITestMetadataReader _metadataReader;

        public GetRecordingInfoUseCase(
            TimestampRangeService timestampRangeService,
            ITestMetadataReader metadataReader = null)
        {
            if (timestampRangeService == null)
                throw new ArgumentNullException(nameof(timestampRangeService));
            _timestampRangeService = timestampRangeService;
            _metadataReader = metadataReader;
        }

        public RecordingInfoResult Execute(TestData data, string sourceRoot)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (sourceRoot == null) throw new ArgumentNullException(nameof(sourceRoot));

            // Читаем метаданные напрямую из .dat-файла источника, если reader доступен.
            // data.Meta — слитый словарь всех источников (первый выигрывает), поэтому
            // при N>=2 записях он содержит не те данные для источника 2, 3, ...
            IReadOnlyList<KeyValuePair<string, string>> meta;
            if (_metadataReader != null)
            {
                try
                {
                    meta = FilterDisplayMetadata(_metadataReader.Read(sourceRoot));
                }
                catch
                {
                    meta = data.Meta != null
                        ? FilterDisplayMetadata(data.Meta)
                        : new List<KeyValuePair<string, string>>();
                }
            }
            else
            {
                meta = data.Meta != null
                    ? FilterDisplayMetadata(data.Meta)
                    : new List<KeyValuePair<string, string>>();
            }

            var result = new RecordingInfoResult
            {
                SourceRoot = sourceRoot,
                Meta = meta
            };

            long startMs, endMs;
            if (!data.SourceStartMs.TryGetValue(sourceRoot, out startMs))
                startMs = data.TimestampsMs.Length > 0 ? data.TimestampsMs[0] : 0;
            if (!data.SourceEndMs.TryGetValue(sourceRoot, out endMs))
                endMs = data.TimestampsMs.Length > 0 ? data.TimestampsMs[data.TimestampsMs.Length - 1] : 0;

            result.SourceStartTime = _timestampRangeService.UnixMsToLocalDateTime(startMs);

            var slice = _timestampRangeService.SliceByTime(data.TimestampsMs, startMs, endMs);
            int i0 = slice.Item1;
            int i1 = slice.Item2;
            if (i1 <= i0)
                return result;

            result.T8PlusStats = CalculateT8PlusStats(data, sourceRoot, i0, i1, startMs);

            string t1Column = FindT1Column(data, sourceRoot);
            if (t1Column == null)
                return result;

            double?[] values;
            if (!data.Columns.TryGetValue(t1Column, out values) || values == null)
                return result;

            int minIdx = -1;
            double minVal = double.MaxValue;
            double? firstVal = null;
            for (int i = i0; i < i1; i++)
            {
                if (!values[i].HasValue) continue;
                if (firstVal == null)
                    firstVal = values[i];
                if (values[i].Value < minVal)
                {
                    minVal = values[i].Value;
                    minIdx = i;
                }
            }

            if (minIdx < 0) return result;

            result.T1Min = minVal;
            result.T1MinTime = _timestampRangeService.UnixMsToLocalDateTime(
                data.TimestampsMs[minIdx]);
            result.T1MinElapsedMs = data.TimestampsMs[minIdx] - startMs;

            int firstCoolingMinIdx = FindFirstCoolingMinimumIndex(data.TimestampsMs, values, i0, i1, firstVal);
            if (firstCoolingMinIdx >= 0)
            {
                result.T1FirstCoolingMin = values[firstCoolingMinIdx].Value;
                result.T1FirstCoolingMinTime = _timestampRangeService.UnixMsToLocalDateTime(
                    data.TimestampsMs[firstCoolingMinIdx]);
                result.T1FirstCoolingMinElapsedMs = data.TimestampsMs[firstCoolingMinIdx] - startMs;
            }

            int dropRateIdx = firstCoolingMinIdx >= 0 ? firstCoolingMinIdx : minIdx;
            double dropRateMinVal = values[dropRateIdx].Value;
            double durationMin = (data.TimestampsMs[dropRateIdx] - startMs) / 60_000.0;
            if (durationMin > 0 && firstVal.HasValue)
                result.T1DropRatePerMinute = (dropRateMinVal - firstVal.Value) / durationMin;

            return result;
        }

        private static int FindFirstCoolingMinimumIndex(
            long[] timestamps,
            double?[] values,
            int i0,
            int i1,
            double? firstValue)
        {
            if (!firstValue.HasValue)
            {
                return -1;
            }

            for (int i = i0; i < i1; i++)
            {
                if (!values[i].HasValue)
                {
                    continue;
                }

                double value = values[i].Value;
                if (firstValue.Value - value < FirstCoolingMinimumDropThreshold)
                {
                    continue;
                }

                long reboundEnd = timestamps[i] + FirstCoolingMinimumReboundLookAheadMs;
                long stabilityEnd = timestamps[i] + FirstCoolingMinimumStabilityLookAheadMs;
                double futureMax = value;
                double futureMin = value;
                bool hasFuture = false;

                for (int j = i + 1; j < i1 && timestamps[j] <= stabilityEnd; j++)
                {
                    if (!values[j].HasValue)
                    {
                        continue;
                    }

                    double future = values[j].Value;
                    if (timestamps[j] <= reboundEnd && future > futureMax)
                    {
                        futureMax = future;
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
                    return i;
                }
            }

            return -1;
        }

        private T8PlusTemperatureStats CalculateT8PlusStats(
            TestData data,
            string sourceRoot,
            int i0,
            int i1,
            long startMs)
        {
            List<string> columns = FindTColumns(data, sourceRoot, 8);
            if (columns.Count == 0)
            {
                return null;
            }

            var stats = new T8PlusTemperatureStats { HasChannels = true };
            double bestAverage = double.MaxValue;
            long bestAverageTimestampMs = 0L;
            bool hasAverage = false;
            double? firstAverage = null;
            long firstAverageTimestampMs = 0L;
            double bestMinimum = double.MaxValue;
            long bestMinimumTimestampMs = 0L;
            bool hasMinimum = false;
            double bestMaximum = double.MaxValue;
            long bestMaximumTimestampMs = 0L;
            bool hasMaximum = false;

            for (int i = i0; i < i1; i++)
            {
                double sum = 0d;
                double min = double.MaxValue;
                double max = double.MinValue;
                int count = 0;

                foreach (string column in columns)
                {
                    double?[] values;
                    if (!data.Columns.TryGetValue(column, out values) || values == null ||
                        i >= values.Length || !values[i].HasValue)
                    {
                        continue;
                    }

                    double value = values[i].Value;
                    if (!IsValidT8PlusAggregateTemperature(value))
                    {
                        continue;
                    }

                    sum += value;
                    if (value < min) min = value;
                    if (value > max) max = value;
                    count++;
                }

                if (count == 0)
                {
                    continue;
                }

                long timestampMs = data.TimestampsMs[i];
                double average = sum / count;
                if (!firstAverage.HasValue)
                {
                    firstAverage = average;
                    firstAverageTimestampMs = timestampMs;
                }

                if (average < bestAverage)
                {
                    bestAverage = average;
                    bestAverageTimestampMs = timestampMs;
                    hasAverage = true;
                }

                if (min < bestMinimum)
                {
                    bestMinimum = min;
                    bestMinimumTimestampMs = timestampMs;
                    hasMinimum = true;
                }

                if (max < bestMaximum)
                {
                    bestMaximum = max;
                    bestMaximumTimestampMs = timestampMs;
                    hasMaximum = true;
                }

                if (!stats.AverageReached && average <= 5.0)
                {
                    stats.AverageReached = true;
                    stats.AverageValue = average;
                    stats.AverageElapsedMs = timestampMs - startMs;
                    stats.AverageTime = _timestampRangeService.UnixMsToLocalDateTime(timestampMs);
                }

                if (!stats.MinimumReached && min <= 1.0)
                {
                    stats.MinimumReached = true;
                    stats.MinimumValue = min;
                    stats.MinimumElapsedMs = timestampMs - startMs;
                    stats.MinimumTime = _timestampRangeService.UnixMsToLocalDateTime(timestampMs);
                }

                if (!stats.MaximumReached && max <= 9.0)
                {
                    stats.MaximumReached = true;
                    stats.MaximumValue = max;
                    stats.MaximumElapsedMs = timestampMs - startMs;
                    stats.MaximumTime = _timestampRangeService.UnixMsToLocalDateTime(timestampMs);
                }
            }

            if (!stats.AverageReached && hasAverage)
            {
                stats.AverageValue = bestAverage;
                stats.AverageElapsedMs = bestAverageTimestampMs - startMs;
                stats.AverageTime = _timestampRangeService.UnixMsToLocalDateTime(bestAverageTimestampMs);
            }

            if (firstAverage.HasValue && hasAverage)
            {
                double durationMin = (bestAverageTimestampMs - firstAverageTimestampMs) / 60_000.0;
                if (durationMin > 0)
                {
                    stats.AverageDropRatePerMinute = (bestAverage - firstAverage.Value) / durationMin;
                }
            }

            if (!stats.MinimumReached && hasMinimum)
            {
                stats.MinimumValue = bestMinimum;
                stats.MinimumElapsedMs = bestMinimumTimestampMs - startMs;
                stats.MinimumTime = _timestampRangeService.UnixMsToLocalDateTime(bestMinimumTimestampMs);
            }

            if (!stats.MaximumReached && hasMaximum)
            {
                stats.MaximumValue = bestMaximum;
                stats.MaximumElapsedMs = bestMaximumTimestampMs - startMs;
                stats.MaximumTime = _timestampRangeService.UnixMsToLocalDateTime(bestMaximumTimestampMs);
            }

            return stats;
        }

        private static bool IsValidT8PlusAggregateTemperature(double value)
        {
            return value > -90.0;
        }

        private static string FindT1Column(TestData data, string sourceRoot)
        {
            string[] cols;
            if (data.SourceColumns.TryGetValue(sourceRoot, out cols) && cols != null)
            {
                string found = FindT1InArray(cols);
                if (found != null) return found;
            }

            // Fallback: search global column list
            if (data.ColumnNames != null)
            {
                string found = FindT1InArray(data.ColumnNames);
                if (found != null) return found;
            }

            return null;
        }

        private static List<string> FindTColumns(TestData data, string sourceRoot, int minimumNumber)
        {
            var result = new List<string>();
            string[] cols;
            if (data.SourceColumns.TryGetValue(sourceRoot, out cols) && cols != null)
            {
                AddTColumns(result, cols, minimumNumber);
                return result;
            }

            if (data.ColumnNames != null && data.SourceColumns.Count <= 1)
            {
                AddTColumns(result, data.ColumnNames, minimumNumber);
            }

            return result;
        }

        private static void AddTColumns(List<string> result, string[] cols, int minimumNumber)
        {
            foreach (string col in cols)
            {
                int number;
                if (TryGetTChannelNumber(col, out number) && number >= minimumNumber)
                {
                    result.Add(col);
                }
            }
        }

        private static IReadOnlyList<KeyValuePair<string, string>> FilterDisplayMetadata(
            IEnumerable<KeyValuePair<string, string>> meta)
        {
            if (meta == null)
            {
                return new List<KeyValuePair<string, string>>();
            }

            var result = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> kv in meta)
            {
                if (!IsRecordingBoundaryMetadataKey(kv.Key))
                {
                    result.Add(kv);
                }
            }

            return result;
        }

        private static bool IsRecordingBoundaryMetadataKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string normalized = key.Trim()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);

            return string.Equals(normalized, "StartedAt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "StoppedAt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "StartAt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "StopAt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "StartTime", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "StopTime", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "EndTime", StringComparison.OrdinalIgnoreCase);
        }

        private static string FindT1InArray(string[] cols)
        {
            foreach (string col in cols)
            {
                int number;
                if (TryGetTChannelNumber(col, out number) && number == 1) return col;
            }
            return null;
        }

        private static bool TryGetTChannelNumber(string col, out int number)
        {
            number = 0;
            if (string.IsNullOrEmpty(col)) return false;

            string name = col.Trim();
            int sep = name.LastIndexOf("::", StringComparison.Ordinal);
            if (sep >= 0)
                name = name.Substring(sep + 2);

            int hash = name.LastIndexOf('#');
            if (hash > 0)
            {
                string hashPart = name.Substring(hash + 1);
                bool allDigits = hashPart.Length > 0;
                foreach (char c in hashPart)
                {
                    if (!char.IsDigit(c)) { allDigits = false; break; }
                }
                if (allDigits)
                    name = name.Substring(0, hash);
            }

            if (name.Length >= 3 && name[1] == '-')
                name = name.Substring(2);

            if (name.Length < 2 || (name[0] != 'T' && name[0] != 't'))
                return false;

            string digits = name.Substring(1);
            if (digits.Length == 0)
                return false;

            foreach (char c in digits)
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return int.TryParse(digits, out number);
        }
    }
}
