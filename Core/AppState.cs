using System;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Session;

namespace JSQViewer.Core
{
    public static class AppState
    {
        private static IViewerSession _viewerSession;
        private static TimestampRangeService _timestampRangeService;
        private static DataSummaryService _dataSummaryService;

        public static void Configure(IViewerSession viewerSession, TimestampRangeService timestampRangeService, DataSummaryService dataSummaryService)
        {
            _viewerSession = viewerSession ?? throw new ArgumentNullException(nameof(viewerSession));
            _timestampRangeService = timestampRangeService ?? throw new ArgumentNullException(nameof(timestampRangeService));
            _dataSummaryService = dataSummaryService ?? throw new ArgumentNullException(nameof(dataSummaryService));
        }

        public static int DataVersion
        {
            get { return ViewerSession.DataVersion; }
        }

        public static bool IsLoaded
        {
            get { return ViewerSession.IsLoaded; }
        }

        public static string Folder
        {
            get { return ViewerSession.Folder; }
        }

        public static TestData Data
        {
            get { return ViewerSession.Data; }
        }

        public static void SetData(string folder, TestData data)
        {
            ViewerSession.SetData(folder, data);
        }

        public static DataSummary BuildSummary(TestData data)
        {
            return DataSummaryService.BuildSummary(data);
        }

        public static int NearestIndex(long[] timestamps, long targetMs)
        {
            return TimestampRangeService.NearestIndex(timestamps, targetMs);
        }

        public static Tuple<int, int> SliceByTime(long[] timestamps, long startMs, long endMs)
        {
            return TimestampRangeService.SliceByTime(timestamps, startMs, endMs);
        }

        public static DateTime UnixMsToLocalDateTime(long unixMs)
        {
            return TimestampRangeService.UnixMsToLocalDateTime(unixMs);
        }

        private static IViewerSession ViewerSession
        {
            get
            {
                if (_viewerSession == null)
                {
                    throw new InvalidOperationException("AppState compatibility facade is not configured.");
                }

                return _viewerSession;
            }
        }

        private static TimestampRangeService TimestampRangeService
        {
            get
            {
                if (_timestampRangeService == null)
                {
                    throw new InvalidOperationException("AppState compatibility facade is not configured.");
                }

                return _timestampRangeService;
            }
        }

        private static DataSummaryService DataSummaryService
        {
            get
            {
                if (_dataSummaryService == null)
                {
                    throw new InvalidOperationException("AppState compatibility facade is not configured.");
                }

                return _dataSummaryService;
            }
        }
    }
}
