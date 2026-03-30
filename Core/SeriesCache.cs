using System;
using System.Collections.Generic;
using JSQViewer.Application.Charting;

namespace JSQViewer.Core
{
    public sealed class SeriesSlice
    {
        public long[] Timestamps { get; set; }
        public Dictionary<string, double?[]> Series { get; set; }
    }

    public static class SeriesCache
    {
        private static SeriesSliceService _seriesSliceService;

        public static void Configure(SeriesSliceService seriesSliceService)
        {
            _seriesSliceService = seriesSliceService ?? throw new ArgumentNullException(nameof(seriesSliceService));
        }

        public static SeriesSlice GetOrBuild(
            int dataVersion,
            TestData data,
            IEnumerable<string> channels,
            long startMs,
            long endMs,
            int step)
        {
            return Service.GetOrBuild(dataVersion, data, channels, startMs, endMs, step);
        }

        public static void Clear()
        {
            Service.Clear();
        }

        private static SeriesSliceService Service
        {
            get
            {
                if (_seriesSliceService == null)
                {
                    throw new InvalidOperationException("SeriesCache compatibility facade is not configured.");
                }

                return _seriesSliceService;
            }
        }
    }
}
