using JSQViewer.Core;

namespace JSQViewer.Application.Charting
{
    public sealed class DataSummaryService
    {
        private readonly TimestampRangeService _timestampRangeService;

        public DataSummaryService(TimestampRangeService timestampRangeService)
        {
            _timestampRangeService = timestampRangeService;
        }

        public DataSummary BuildSummary(TestData data)
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
                Start = _timestampRangeService.UnixMsToLocalDateTime(t0),
                End = _timestampRangeService.UnixMsToLocalDateTime(t1)
            };
        }
    }
}
