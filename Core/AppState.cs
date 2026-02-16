using System;
using System.Threading;

namespace LeMuReViewer.Core
{
    public static class AppState
    {
        private static readonly object Sync = new object();
        private static int _dataVersion;
        private static bool _loaded;
        private static string _folder = string.Empty;
        private static TestData _data;

        public static int DataVersion
        {
            get { return Volatile.Read(ref _dataVersion); }
        }

        public static bool IsLoaded
        {
            get
            {
                lock (Sync)
                {
                    return _loaded;
                }
            }
        }

        public static string Folder
        {
            get
            {
                lock (Sync)
                {
                    return _folder;
                }
            }
        }

        public static TestData Data
        {
            get
            {
                lock (Sync)
                {
                    return _data;
                }
            }
        }

        public static void SetData(string folder, TestData data)
        {
            lock (Sync)
            {
                _folder = folder ?? string.Empty;
                _data = data;
                _loaded = data != null;
                _dataVersion++;
            }

            SeriesCache.Clear();
        }

        public static DataSummary BuildSummary(TestData data)
        {
            if (data == null || data.TimestampsMs == null || data.TimestampsMs.Length == 0)
            {
                return new DataSummary { Points = 0 };
            }

            long t0 = data.TimestampsMs[0];
            long t1 = data.TimestampsMs[data.TimestampsMs.Length - 1];
            return new DataSummary
            {
                Points = data.TimestampsMs.Length,
                StartMs = t0,
                EndMs = t1,
                Start = UnixMsToLocalDateTime(t0),
                End = UnixMsToLocalDateTime(t1)
            };
        }

        public static int NearestIndex(long[] timestamps, long targetMs)
        {
            if (timestamps == null || timestamps.Length == 0)
            {
                return -1;
            }

            int idx = Array.BinarySearch(timestamps, targetMs);
            if (idx >= 0)
            {
                return idx;
            }

            idx = ~idx;
            if (idx <= 0)
            {
                return 0;
            }

            if (idx >= timestamps.Length)
            {
                return timestamps.Length - 1;
            }

            long left = timestamps[idx - 1];
            long right = timestamps[idx];
            return Math.Abs(targetMs - left) <= Math.Abs(right - targetMs) ? idx - 1 : idx;
        }

        public static Tuple<int, int> SliceByTime(long[] timestamps, long startMs, long endMs)
        {
            if (timestamps == null || timestamps.Length == 0)
            {
                return Tuple.Create(0, 0);
            }

            if (startMs > endMs)
            {
                long tmp = startMs;
                startMs = endMs;
                endMs = tmp;
            }

            int i0 = LowerBound(timestamps, startMs);
            int i1 = UpperBound(timestamps, endMs);
            return Tuple.Create(i0, i1);
        }

        public static DateTime UnixMsToLocalDateTime(long unixMs)
        {
            DateTime epochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epochUtc.AddMilliseconds(unixMs).ToLocalTime();
        }

        private static int LowerBound(long[] arr, long value)
        {
            int l = 0;
            int r = arr.Length;
            while (l < r)
            {
                int m = l + ((r - l) / 2);
                if (arr[m] < value)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }

            return l;
        }

        private static int UpperBound(long[] arr, long value)
        {
            int l = 0;
            int r = arr.Length;
            while (l < r)
            {
                int m = l + ((r - l) / 2);
                if (arr[m] <= value)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }

            return l;
        }
    }
}
