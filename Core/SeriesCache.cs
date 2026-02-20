using System;
using System.Collections.Generic;
using System.Linq;

namespace JSQViewer.Core
{
    public sealed class SeriesSlice
    {
        public long[] Timestamps { get; set; }
        public Dictionary<string, double?[]> Series { get; set; }
    }

    public static class SeriesCache
    {
        private const int MaxEntries = 8;
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, LinkedListNode<CacheItem>> ByKey = new Dictionary<string, LinkedListNode<CacheItem>>();
        private static readonly LinkedList<CacheItem> Lru = new LinkedList<CacheItem>();

        public static SeriesSlice GetOrBuild(
            int dataVersion,
            TestData data,
            IEnumerable<string> channels,
            long startMs,
            long endMs,
            int step)
        {
            if (data == null)
            {
                return new SeriesSlice { Timestamps = new long[0], Series = new Dictionary<string, double?[]>() };
            }

            string[] channelArray = channels == null ? new string[0] : channels.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            if (channelArray.Length == 0)
            {
                return new SeriesSlice { Timestamps = new long[0], Series = new Dictionary<string, double?[]>() };
            }

            if (step < 1)
            {
                step = 1;
            }

            string cacheKey = BuildKey(dataVersion, channelArray, startMs, endMs, step);
            lock (Sync)
            {
                LinkedListNode<CacheItem> existingNode;
                if (ByKey.TryGetValue(cacheKey, out existingNode))
                {
                    Lru.Remove(existingNode);
                    Lru.AddFirst(existingNode);
                    return existingNode.Value.Value;
                }
            }

            SeriesSlice built = BuildSlice(data, channelArray, startMs, endMs, step);
            lock (Sync)
            {
                LinkedListNode<CacheItem> existingNode;
                if (ByKey.TryGetValue(cacheKey, out existingNode))
                {
                    Lru.Remove(existingNode);
                    Lru.AddFirst(existingNode);
                    return existingNode.Value.Value;
                }

                var node = new LinkedListNode<CacheItem>(new CacheItem { Key = cacheKey, Value = built });
                Lru.AddFirst(node);
                ByKey[cacheKey] = node;

                while (Lru.Count > MaxEntries)
                {
                    LinkedListNode<CacheItem> last = Lru.Last;
                    if (last == null)
                    {
                        break;
                    }

                    Lru.RemoveLast();
                    ByKey.Remove(last.Value.Key);
                }
            }

            return built;
        }

        public static void Clear()
        {
            lock (Sync)
            {
                ByKey.Clear();
                Lru.Clear();
            }
        }

        private static SeriesSlice BuildSlice(TestData data, string[] channels, long startMs, long endMs, int step)
        {
            Tuple<int, int> range = AppState.SliceByTime(data.TimestampsMs, startMs, endMs);
            int i0 = range.Item1;
            int i1 = range.Item2;
            if (i1 <= i0)
            {
                return new SeriesSlice { Timestamps = new long[0], Series = new Dictionary<string, double?[]>() };
            }

            int len = ((i1 - i0) + step - 1) / step;
            var t = new long[len];
            int ti = 0;
            for (int i = i0; i < i1; i += step)
            {
                t[ti++] = data.TimestampsMs[i];
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
                int si = 0;
                for (int i = i0; i < i1; i += step)
                {
                    target[si++] = source[i];
                }

                series[code] = target;
            }

            return new SeriesSlice { Timestamps = t, Series = series };
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

        private sealed class CacheItem
        {
            public string Key { get; set; }
            public SeriesSlice Value { get; set; }
        }
    }
}
