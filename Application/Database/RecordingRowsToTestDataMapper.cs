using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Core;

namespace JSQViewer.Application.Database
{
    public sealed class RecordingAggregateRow
    {
        public string ChannelId { get; set; }

        public long TimestampMs { get; set; }

        public double Value { get; set; }
    }

    public sealed class RecordingRowsToTestDataMapper
    {
        public TestData Map(
            string source,
            string postId,
            IList<RecordingAggregateRow> rows,
            IDictionary<string, ChannelInfo> channels,
            IDictionary<string, string> metadata)
        {
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source is required.", nameof(source));

            IList<RecordingAggregateRow> safeRows = rows ?? new List<RecordingAggregateRow>();

            long[] timestamps = safeRows
                .Select(row => row.TimestampMs)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();

            var indexByTimestamp = new Dictionary<long, int>(timestamps.Length);
            for (int i = 0; i < timestamps.Length; i++)
            {
                indexByTimestamp[timestamps[i]] = i;
            }

            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            var columnOrder = new List<string>();
            for (int i = 0; i < safeRows.Count; i++)
            {
                RecordingAggregateRow row = safeRows[i];
                string code = ChannelCodeNormalizer.StripPostPrefix(row.ChannelId, postId);
                double?[] column;
                if (!columns.TryGetValue(code, out column))
                {
                    column = new double?[timestamps.Length];
                    columns[code] = column;
                    columnOrder.Add(code);
                }

                column[indexByTimestamp[row.TimestampMs]] = row.Value;
            }

            string[] columnNames = columnOrder.ToArray();
            var normalizedChannels = new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            if (channels != null)
            {
                foreach (KeyValuePair<string, ChannelInfo> pair in channels)
                {
                    normalizedChannels[pair.Key] = pair.Value;
                }
            }

            var normalizedMetadata = metadata == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
            string displayName = GetDisplayName(source, normalizedMetadata);

            return new TestData
            {
                Root = source,
                Meta = normalizedMetadata,
                Channels = normalizedChannels,
                CodeSources = columnNames.ToDictionary(code => code, code => source, StringComparer.OrdinalIgnoreCase),
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, timestamps.Length > 0 ? timestamps[0] : 0L }
                },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, timestamps.Length > 0 ? timestamps[timestamps.Length - 1] : 0L }
                },
                SourceDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, displayName }
                },
                SourceOrder = new[] { source },
                TimestampsMs = timestamps,
                Columns = columns,
                ColumnNames = columnNames,
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, columnNames.ToArray() }
                },
                RowCount = timestamps.Length
            };
        }

        public long GetLastTimestampMs(TestData data)
        {
            if (data == null || data.TimestampsMs == null || data.TimestampsMs.Length == 0)
            {
                return -1L;
            }

            return data.TimestampsMs[data.TimestampsMs.Length - 1];
        }

        public TestData Append(TestData existing, string postId, IList<RecordingAggregateRow> rows)
        {
            return Append(existing, postId, rows, null);
        }

        /// <summary>
        /// Appends newly measured windows. <paramref name="freshMetadata"/> is the recording card as it
        /// reads right now: passing it lets the display name follow the recording's status, so the
        /// "still recording" marker disappears as soon as the run stops — even on a tick that brought
        /// no new windows.
        /// </summary>
        public TestData Append(
            TestData existing,
            string postId,
            IList<RecordingAggregateRow> rows,
            IDictionary<string, string> freshMetadata)
        {
            if (existing == null) throw new ArgumentNullException(nameof(existing));

            long lastTimestamp = GetLastTimestampMs(existing);
            List<RecordingAggregateRow> freshRows = (rows ?? new List<RecordingAggregateRow>())
                .Where(row => row.TimestampMs > lastTimestamp)
                .ToList();

            if (freshRows.Count == 0)
            {
                return RefreshIdentity(existing, freshMetadata);
            }

            long[] newTimestamps = freshRows
                .Select(row => row.TimestampMs)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();

            int oldLength = existing.TimestampsMs.Length;
            int newLength = oldLength + newTimestamps.Length;

            var mergedTimestamps = new long[newLength];
            Array.Copy(existing.TimestampsMs, mergedTimestamps, oldLength);
            Array.Copy(newTimestamps, 0, mergedTimestamps, oldLength, newTimestamps.Length);

            var indexByTimestamp = new Dictionary<long, int>(newTimestamps.Length);
            for (int i = 0; i < newTimestamps.Length; i++)
            {
                indexByTimestamp[newTimestamps[i]] = oldLength + i;
            }

            var mergedColumns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            var columnOrder = new List<string>(existing.ColumnNames);
            foreach (KeyValuePair<string, double?[]> pair in existing.Columns)
            {
                var extended = new double?[newLength];
                Array.Copy(pair.Value, extended, Math.Min(pair.Value.Length, oldLength));
                mergedColumns[pair.Key] = extended;
            }

            for (int i = 0; i < freshRows.Count; i++)
            {
                RecordingAggregateRow row = freshRows[i];
                string code = ChannelCodeNormalizer.StripPostPrefix(row.ChannelId, postId);
                double?[] column;
                if (!mergedColumns.TryGetValue(code, out column))
                {
                    column = new double?[newLength];
                    mergedColumns[code] = column;
                    columnOrder.Add(code);
                }

                column[indexByTimestamp[row.TimestampMs]] = row.Value;
            }

            string[] mergedColumnNames = columnOrder.ToArray();
            string source = existing.Root;
            var sourceDisplayNames = existing.SourceDisplayNames == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(existing.SourceDisplayNames, StringComparer.OrdinalIgnoreCase);
            string[] sourceOrder = existing.SourceOrder == null
                ? new string[0]
                : existing.SourceOrder.ToArray();
            Dictionary<string, string> metadata = NormalizeMetadata(existing, freshMetadata);
            if (freshMetadata != null)
            {
                sourceDisplayNames[source] = GetDisplayName(source, metadata);
            }

            return new TestData
            {
                Root = source,
                Meta = metadata,
                Channels = existing.Channels,
                CodeSources = mergedColumnNames.ToDictionary(code => code, code => source, StringComparer.OrdinalIgnoreCase),
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, mergedTimestamps[0] }
                },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, mergedTimestamps[newLength - 1] }
                },
                SourceDisplayNames = sourceDisplayNames,
                SourceOrder = sourceOrder,
                TimestampsMs = mergedTimestamps,
                Columns = mergedColumns,
                ColumnNames = mergedColumnNames,
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, mergedColumnNames.ToArray() }
                },
                RowCount = newLength
            };
        }

        /// <summary>
        /// Returns the workspace with a display name rebuilt from the recording card, or the very same
        /// instance when nothing about its identity changed — callers rebind on reference change, so an
        /// unchanged tick must not create a new object.
        /// </summary>
        private static TestData RefreshIdentity(TestData existing, IDictionary<string, string> freshMetadata)
        {
            if (freshMetadata == null || string.IsNullOrWhiteSpace(existing.Root))
            {
                return existing;
            }

            Dictionary<string, string> metadata = NormalizeMetadata(existing, freshMetadata);
            string displayName = GetDisplayName(existing.Root, metadata);
            string currentName;
            if (existing.SourceDisplayNames != null
                && existing.SourceDisplayNames.TryGetValue(existing.Root, out currentName)
                && string.Equals(currentName, displayName, StringComparison.Ordinal))
            {
                return existing;
            }

            var sourceDisplayNames = existing.SourceDisplayNames == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(existing.SourceDisplayNames, StringComparer.OrdinalIgnoreCase);
            sourceDisplayNames[existing.Root] = displayName;

            return new TestData
            {
                Root = existing.Root,
                Meta = metadata,
                Channels = existing.Channels,
                CodeSources = existing.CodeSources,
                SourceStartMs = existing.SourceStartMs,
                SourceEndMs = existing.SourceEndMs,
                SourceDisplayNames = sourceDisplayNames,
                SourceOrder = existing.SourceOrder,
                TimestampsMs = existing.TimestampsMs,
                Columns = existing.Columns,
                ColumnNames = existing.ColumnNames,
                SourceColumns = existing.SourceColumns,
                RowCount = existing.RowCount
            };
        }

        private static Dictionary<string, string> NormalizeMetadata(
            TestData existing,
            IDictionary<string, string> freshMetadata)
        {
            if (freshMetadata == null)
            {
                return existing.Meta;
            }

            return new Dictionary<string, string>(freshMetadata, StringComparer.OrdinalIgnoreCase);
        }

        private static string GetDisplayName(string source, IDictionary<string, string> metadata)
        {
            string recordingId;
            string fallback = RecordingSourceRef.TryParse(source, out recordingId) ? recordingId : source;
            return RecordingDisplayNameBuilder.Build(metadata, fallback);
        }
    }
}
