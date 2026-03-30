using System;

namespace JSQViewer.Application.Charting
{
    public sealed class TimestampRangeService
    {
        public int NearestIndex(long[] timestamps, long targetMs)
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

        public Tuple<int, int> SliceByTime(long[] timestamps, long startMs, long endMs)
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

        public DateTime UnixMsToLocalDateTime(long unixMs)
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
