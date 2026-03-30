using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Core;

namespace JSQViewer.Application.Charting
{
    public sealed class SeriesSliceService
    {
        private readonly ISeriesSliceCache _cache;
        private readonly TimestampRangeService _timestampRangeService;

        public SeriesSliceService(ISeriesSliceCache cache, TimestampRangeService timestampRangeService)
        {
            _cache = cache;
            _timestampRangeService = timestampRangeService;
        }

        public SeriesSlice GetOrBuild(
            int dataVersion,
            TestData data,
            IEnumerable<string> channels,
            long startMs,
            long endMs,
            int step)
        {
            if (data == null)
            {
                return EmptySlice();
            }

            string[] channelArray = channels == null
                ? new string[0]
                : channels.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            if (channelArray.Length == 0)
            {
                return EmptySlice();
            }

            if (step < 1)
            {
                step = 1;
            }

            string cacheKey = BuildKey(dataVersion, channelArray, startMs, endMs, step);
            SeriesSlice cached;
            if (_cache != null && _cache.TryGet(cacheKey, out cached))
            {
                return cached;
            }

            SeriesSlice built = BuildSlice(data, channelArray, startMs, endMs, step);
            if (_cache != null)
            {
                _cache.Set(cacheKey, built);
            }

            return built;
        }

        public void Clear()
        {
            if (_cache != null)
            {
                _cache.Clear();
            }
        }

        private SeriesSlice BuildSlice(TestData data, string[] channels, long startMs, long endMs, int step)
        {
            Tuple<int, int> range = _timestampRangeService.SliceByTime(data.TimestampsMs, startMs, endMs);
            int i0 = range.Item1;
            int i1 = range.Item2;
            if (i1 <= i0)
            {
                return EmptySlice();
            }

            int len = ((i1 - i0) + step - 1) / step;
            var timestamps = new long[len];
            int timestampIndex = 0;
            for (int i = i0; i < i1; i += step)
            {
                timestamps[timestampIndex++] = data.TimestampsMs[i];
            }

            var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < channels.Length; c++)
            {
                string code = channels[c];
                double?[] source;
                if (!data.Columns.TryGetValue(code, out source))
                {
                    series[code] = new double?[len];
                    continue;
                }

                var target = new double?[len];
                int seriesIndex = 0;
                for (int i = i0; i < i1; i += step)
                {
                    target[seriesIndex++] = source[i];
                }

                series[code] = target;
            }

            return new SeriesSlice { Timestamps = timestamps, Series = series };
        }

        private static SeriesSlice EmptySlice()
        {
            return new SeriesSlice
            {
                Timestamps = new long[0],
                Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            };
        }

        private static string BuildKey(int dataVersion, string[] channels, long startMs, long endMs, int step)
        {
            string joined = string.Join(",", channels.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray());
            return string.Concat(
                dataVersion.ToString(), "|",
                joined, "|",
                startMs.ToString(), "|",
                endMs.ToString(), "|",
                step.ToString());
        }
    }
}
