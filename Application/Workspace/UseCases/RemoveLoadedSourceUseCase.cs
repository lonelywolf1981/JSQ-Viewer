using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Core;

namespace JSQViewer.Application.Workspace.UseCases
{
    public sealed class RemoveLoadedSourceUseCase
    {
        public TestData Execute(TestData data, string sourceRoot)
        {
            if (data == null || string.IsNullOrWhiteSpace(sourceRoot))
            {
                return data;
            }

            if (data.SourceColumns == null || !data.SourceColumns.ContainsKey(sourceRoot))
            {
                return data;
            }

            var remainingSourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string[]> pair in data.SourceColumns)
            {
                if (!string.Equals(pair.Key, sourceRoot, StringComparison.OrdinalIgnoreCase))
                {
                    remainingSourceColumns[pair.Key] = pair.Value ?? new string[0];
                }
            }

            if (remainingSourceColumns.Count == 0)
            {
                return null;
            }

            string[] remainingColumns = remainingSourceColumns
                .SelectMany(pair => pair.Value ?? new string[0])
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var remainingColumnSet = new HashSet<string>(remainingColumns, StringComparer.OrdinalIgnoreCase);
            List<int> retainedRows = BuildRetainedRows(data, remainingColumns);

            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string column in remainingColumns)
            {
                double?[] source;
                if (data.Columns == null || !data.Columns.TryGetValue(column, out source) || source == null)
                {
                    columns[column] = new double?[retainedRows.Count];
                    continue;
                }

                var values = new double?[retainedRows.Count];
                for (int i = 0; i < retainedRows.Count; i++)
                {
                    int sourceIndex = retainedRows[i];
                    if (sourceIndex >= 0 && sourceIndex < source.Length)
                    {
                        values[i] = source[sourceIndex];
                    }
                }

                columns[column] = values;
            }

            long[] timestamps = data.TimestampsMs ?? new long[0];
            return new TestData
            {
                Root = string.Join(" ; ", remainingSourceColumns.Keys),
                Meta = CloneDictionary(data.Meta),
                Channels = CloneChannels(data.Channels, remainingColumnSet),
                CodeSources = FilterByColumns(data.CodeSources, remainingColumnSet),
                SourceStartMs = FilterBySources(data.SourceStartMs, remainingSourceColumns.Keys),
                SourceEndMs = FilterBySources(data.SourceEndMs, remainingSourceColumns.Keys),
                TimestampsMs = retainedRows.Select(index => timestamps[index]).ToArray(),
                Columns = columns,
                ColumnNames = remainingColumns,
                SourceColumns = remainingSourceColumns,
                RowCount = retainedRows.Count
            };
        }

        private static List<int> BuildRetainedRows(TestData data, string[] remainingColumns)
        {
            long[] timestamps = data.TimestampsMs ?? new long[0];
            int rowCount = Math.Min(data.RowCount, timestamps.Length);

            var retainedRows = new List<int>();
            for (int row = 0; row < rowCount; row++)
            {
                for (int columnIndex = 0; columnIndex < remainingColumns.Length; columnIndex++)
                {
                    double?[] values;
                    if (data.Columns != null
                        && data.Columns.TryGetValue(remainingColumns[columnIndex], out values)
                        && values != null
                        && row < values.Length
                        && values[row].HasValue)
                    {
                        retainedRows.Add(row);
                        break;
                    }
                }
            }

            return retainedRows;
        }

        private static Dictionary<string, string> CloneDictionary(Dictionary<string, string> source)
        {
            return source == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, ChannelInfo> CloneChannels(
            Dictionary<string, ChannelInfo> source,
            HashSet<string> remainingColumns)
        {
            var result = new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, ChannelInfo> pair in source)
            {
                if (!remainingColumns.Contains(pair.Key))
                {
                    continue;
                }

                ChannelInfo channel = pair.Value;
                result[pair.Key] = channel == null
                    ? null
                    : new ChannelInfo
                    {
                        Code = channel.Code,
                        Name = channel.Name,
                        Unit = channel.Unit
                    };
            }

            return result;
        }

        private static Dictionary<string, string> FilterByColumns(
            Dictionary<string, string> source,
            HashSet<string> remainingColumns)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, string> pair in source)
            {
                if (remainingColumns.Contains(pair.Key))
                {
                    result[pair.Key] = pair.Value;
                }
            }

            return result;
        }

        private static Dictionary<string, long> FilterBySources(
            Dictionary<string, long> source,
            IEnumerable<string> remainingSources)
        {
            var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (string remainingSource in remainingSources)
            {
                long value;
                if (source.TryGetValue(remainingSource, out value))
                {
                    result[remainingSource] = value;
                }
            }

            return result;
        }
    }
}
