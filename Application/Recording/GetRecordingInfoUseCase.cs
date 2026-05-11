using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Charting;
using JSQViewer.Core;

namespace JSQViewer.Application.Recording
{
    public sealed class GetRecordingInfoUseCase
    {
        private readonly TimestampRangeService _timestampRangeService;

        public GetRecordingInfoUseCase(TimestampRangeService timestampRangeService)
        {
            if (timestampRangeService == null)
                throw new ArgumentNullException(nameof(timestampRangeService));
            _timestampRangeService = timestampRangeService;
        }

        public RecordingInfoResult Execute(TestData data, string sourceRoot)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (sourceRoot == null) throw new ArgumentNullException(nameof(sourceRoot));

            var result = new RecordingInfoResult
            {
                SourceRoot = sourceRoot,
                Meta = data.Meta != null
                    ? data.Meta.ToList()
                    : new List<KeyValuePair<string, string>>()
            };

            string t1Column = FindT1Column(data, sourceRoot);
            if (t1Column == null)
                return result;

            double?[] values;
            if (!data.Columns.TryGetValue(t1Column, out values) || values == null)
                return result;

            long startMs, endMs;
            if (!data.SourceStartMs.TryGetValue(sourceRoot, out startMs))
                startMs = data.TimestampsMs.Length > 0 ? data.TimestampsMs[0] : 0;
            if (!data.SourceEndMs.TryGetValue(sourceRoot, out endMs))
                endMs = data.TimestampsMs.Length > 0 ? data.TimestampsMs[data.TimestampsMs.Length - 1] : 0;

            var slice = _timestampRangeService.SliceByTime(data.TimestampsMs, startMs, endMs);
            int i0 = slice.Item1;
            int i1 = slice.Item2;
            if (i1 <= i0)
                return result;

            int minIdx = -1;
            double minVal = double.MaxValue;
            double? firstVal = null;
            for (int i = i0; i < i1; i++)
            {
                if (!values[i].HasValue) continue;
                if (firstVal == null) firstVal = values[i];
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

            double durationMin = (endMs - startMs) / 60_000.0;
            if (durationMin > 0 && firstVal.HasValue)
                result.T1DropRatePerMinute = (minVal - firstVal.Value) / durationMin;

            return result;
        }

        private static string FindT1Column(TestData data, string sourceRoot)
        {
            string[] cols;
            if (!data.SourceColumns.TryGetValue(sourceRoot, out cols) || cols == null)
                return null;

            foreach (string col in cols)
            {
                if (col == null) continue;
                if (string.Equals(col, "T1", StringComparison.OrdinalIgnoreCase))
                    return col;
                // Формат X-T1: однобуквенный префикс
                if (col.Length >= 4 && col[1] == '-' &&
                    string.Equals(col.Substring(2), "T1", StringComparison.OrdinalIgnoreCase))
                    return col;
            }
            return null;
        }
    }
}
