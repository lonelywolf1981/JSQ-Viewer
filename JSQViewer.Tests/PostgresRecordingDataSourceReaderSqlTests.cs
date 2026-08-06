using JSQViewer.Infrastructure.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class PostgresRecordingDataSourceReaderSqlTests
    {
        [TestMethod]
        public void ChannelsSql_IncludesCommonChannels()
        {
            StringAssert.Contains(
                PostgresRecordingDataSourceReader.ChannelsSql,
                "(post_id = @post OR is_common)");
        }

        [TestMethod]
        public void ChannelsSql_KeepsHiddenChannelsExcluded()
        {
            StringAssert.Contains(PostgresRecordingDataSourceReader.ChannelsSql, "NOT is_hidden");
        }

        [TestMethod]
        public void RowsSql_IncludesCommonChannels()
        {
            StringAssert.Contains(
                PostgresRecordingDataSourceReader.RowsSql,
                "(cc.post_id = r.post_id OR cc.is_common)");
        }

        [TestMethod]
        public void RowsSql_KeepsExclusionFilter()
        {
            StringAssert.Contains(
                PostgresRecordingDataSourceReader.RowsSql,
                "recording_aggregate_exclusions");
        }

        [TestMethod]
        public void RecordingSql_SelectsClimateModeAndFirstWindowAverages()
        {
            StringAssert.Contains(PostgresRecordingDataSourceReader.RecordingSql, "r.climate_mode");
            StringAssert.Contains(PostgresRecordingDataSourceReader.RecordingSql, "a.channel_id = 'T-sie'");
            StringAssert.Contains(PostgresRecordingDataSourceReader.RecordingSql, "a.channel_id = 'UR-sie'");
            StringAssert.Contains(
                PostgresRecordingDataSourceReader.RecordingSql,
                "ORDER BY a.window_start LIMIT 5");
        }
    }
}
