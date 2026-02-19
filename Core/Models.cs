using System;
using System.Collections.Generic;

namespace LeMuReViewer.Core
{
    public sealed class ChannelInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }

        public string Label
        {
            get
            {
                string unitPart = string.IsNullOrEmpty(Unit) ? string.Empty : " (" + Unit + ")";
                string namePart = string.IsNullOrEmpty(Name) ? string.Empty : " - " + Name;
                return (Code ?? string.Empty) + namePart + unitPart;
            }
        }
    }

    public sealed class TestData
    {
        public string Root { get; set; }
        public Dictionary<string, string> Meta { get; set; }
        public Dictionary<string, ChannelInfo> Channels { get; set; }
        public Dictionary<string, string> CodeSources { get; set; }
        public Dictionary<string, long> SourceStartMs { get; set; }
        public Dictionary<string, long> SourceEndMs { get; set; }
        public long[] TimestampsMs { get; set; }
        public Dictionary<string, double?[]> Columns { get; set; }
        public string[] ColumnNames { get; set; }
        public Dictionary<string, string[]> SourceColumns { get; set; }
        public int RowCount { get; set; }

        public TestData()
        {
            Root = string.Empty;
            Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Channels = new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            CodeSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            TimestampsMs = new long[0];
            Columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            ColumnNames = new string[0];
            SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class DataSummary
    {
        public int Points { get; set; }
        public long StartMs { get; set; }
        public long EndMs { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}
