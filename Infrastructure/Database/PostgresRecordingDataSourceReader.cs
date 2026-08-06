using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;
using Npgsql;

namespace JSQViewer.Infrastructure.Database
{
    public sealed class PostgresRecordingDataSourceReader : IRecordingDataReader
    {
        private const string RecordingSql = @"
SELECT r.post_id,
       p.name AS post_name,
       r.title,
       r.status,
       r.started_at,
       r.stopped_at,
       COALESCE(to_jsonb(r) ->> 'operator_name', to_jsonb(r) ->> 'operator') AS operator_name,
       r.equipment_model,
       r.experiment_type,
       to_jsonb(r) ->> 'refrigerant' AS refrigerant,
       COALESCE(to_jsonb(r) ->> 'charge', to_jsonb(r) ->> 'refrigerant_charge', to_jsonb(r) ->> 'charge_grams') AS charge,
       COALESCE(to_jsonb(r) ->> 'compressor', to_jsonb(r) ->> 'compressor_model') AS compressor,
       to_jsonb(r) ->> 'modification' AS modification,
       to_jsonb(r) ->> 'climate_class' AS climate_class,
       to_jsonb(r) ->> 'temperature_class' AS temperature_class,
       to_jsonb(r) ->> 'notes' AS notes
FROM recordings r
JOIN posts p ON p.id = r.post_id
WHERE r.id = @id";

        private const string ChannelsSql = @"
SELECT channel_id, alias, unit
FROM channel_config
WHERE post_id = @post
  AND NOT is_hidden
ORDER BY channel_id";

        private const string RowsSql = @"
SELECT a.channel_id, a.window_start, a.avg_value
FROM recording_aggregates a
WHERE a.recording_id = @id
  AND a.avg_value IS NOT NULL
  AND a.window_start > @since
  AND EXISTS (
        SELECT 1
        FROM recordings r
        JOIN channel_config cc
          ON cc.post_id = r.post_id
         AND cc.channel_id = a.channel_id
        WHERE r.id = @id
          AND NOT cc.is_hidden)
  AND NOT EXISTS (
        SELECT 1
        FROM recording_aggregate_exclusions e
        WHERE e.recording_id = a.recording_id
          AND e.channel_id = a.channel_id
          AND e.window_start = a.window_start
          AND e.restored_at IS NULL)
ORDER BY a.window_start";

        private readonly NpgsqlConnectionFactory _connectionFactory;
        private readonly IDatabaseSettingsRepository _settingsRepository;
        private readonly RecordingRowsToTestDataMapper _mapper;

        public PostgresRecordingDataSourceReader(
            NpgsqlConnectionFactory connectionFactory,
            IDatabaseSettingsRepository settingsRepository)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _mapper = new RecordingRowsToTestDataMapper();
        }

        public TestData ReadRecording(string recordingId)
        {
            string normalizedId = NormalizeRecordingId(recordingId);

            using (NpgsqlConnection connection = OpenConnection())
            {
                RecordingSnapshot recording = ReadRecordingSnapshot(connection, normalizedId);
                if (recording == null)
                {
                    throw new InvalidOperationException("Прогон с идентификатором '" + normalizedId + "' не найден.");
                }

                Dictionary<string, ChannelInfo> channels = ReadChannels(connection, recording.PostId);
                List<RecordingAggregateRow> rows = ReadRows(connection, normalizedId, DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc));

                return _mapper.Map(
                    RecordingSourceRef.Build(normalizedId),
                    recording.PostId,
                    rows,
                    channels,
                    recording.Metadata);
            }
        }

        public TestData AppendNewWindows(TestData existing, string recordingId)
        {
            if (existing == null) throw new ArgumentNullException(nameof(existing));

            string normalizedId = NormalizeRecordingId(recordingId);
            long sinceMs = _mapper.GetLastTimestampMs(existing);
            DateTime since = sinceMs < 0
                ? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
                : DateTimeOffset.FromUnixTimeMilliseconds(sinceMs).UtcDateTime;

            using (NpgsqlConnection connection = OpenConnection())
            {
                RecordingSnapshot recording = ReadRecordingSnapshot(connection, normalizedId);
                if (recording == null)
                {
                    return existing;
                }

                List<RecordingAggregateRow> rows = ReadRows(connection, normalizedId, since);
                return _mapper.Append(existing, recording.PostId, rows);
            }
        }

        private NpgsqlConnection OpenConnection()
        {
            DatabaseConnectionSettings settings = _settingsRepository.Load();
            NpgsqlConnection connection = _connectionFactory.Create(settings, _settingsRepository.LoadPassword());
            try
            {
                connection.Open();
                return connection;
            }
            catch
            {
                try
                {
                    connection.Dispose();
                }
                catch
                {
                    // Preserve the connection failure reported by Open().
                }

                throw;
            }
        }

        private static RecordingSnapshot ReadRecordingSnapshot(NpgsqlConnection connection, string recordingId)
        {
            using (var command = new NpgsqlCommand(RecordingSql, connection))
            {
                command.Parameters.AddWithValue("id", recordingId);
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    AddMetadata(metadata, "Пост", reader, 1);
                    AddMetadata(metadata, "Название", reader, 2);
                    AddMetadata(metadata, "Статус", reader, 3);
                    AddDateTimeMetadata(metadata, "Начало", reader, 4);
                    AddDateTimeMetadata(metadata, "Окончание", reader, 5);
                    AddMetadata(metadata, "Оператор", reader, 6);
                    AddMetadata(metadata, "Модель оборудования", reader, 7);
                    AddMetadata(metadata, "Тип испытания", reader, 8);
                    AddMetadata(metadata, "Хладагент", reader, 9);
                    AddMetadata(metadata, "Заправка", reader, 10);
                    AddMetadata(metadata, "Компрессор", reader, 11);
                    AddMetadata(metadata, "Модификация", reader, 12);
                    AddMetadata(metadata, "Климатический класс", reader, 13);
                    AddMetadata(metadata, "Температурный класс", reader, 14);
                    AddMetadata(metadata, "Примечания", reader, 15);

                    return new RecordingSnapshot
                    {
                        PostId = ReadString(reader, 0),
                        Metadata = metadata
                    };
                }
            }
        }

        private static Dictionary<string, ChannelInfo> ReadChannels(NpgsqlConnection connection, string postId)
        {
            var channels = new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            using (var command = new NpgsqlCommand(ChannelsSql, connection))
            {
                command.Parameters.AddWithValue("post", postId);
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string code = ChannelCodeNormalizer.StripPostPrefix(ReadString(reader, 0), postId);
                        if (string.IsNullOrWhiteSpace(code))
                        {
                            continue;
                        }

                        channels[code] = new ChannelInfo
                        {
                            Code = code,
                            Name = ReadString(reader, 1),
                            Unit = ReadString(reader, 2)
                        };
                    }
                }
            }

            return channels;
        }

        private static List<RecordingAggregateRow> ReadRows(
            NpgsqlConnection connection,
            string recordingId,
            DateTime since)
        {
            var rows = new List<RecordingAggregateRow>();
            using (var command = new NpgsqlCommand(RowsSql, connection))
            {
                command.Parameters.AddWithValue("id", recordingId);
                command.Parameters.AddWithValue("since", since);
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new RecordingAggregateRow
                        {
                            ChannelId = ReadString(reader, 0),
                            TimestampMs = ToUnixTimeMilliseconds(reader.GetValue(1)),
                            Value = Convert.ToDouble(reader.GetValue(2), CultureInfo.InvariantCulture)
                        });
                    }
                }
            }

            return rows;
        }

        private static string NormalizeRecordingId(string recordingId)
        {
            if (string.IsNullOrWhiteSpace(recordingId))
            {
                throw new ArgumentException("Recording ID is required.", nameof(recordingId));
            }

            return recordingId.Trim();
        }

        private static long ToUnixTimeMilliseconds(object value)
        {
            if (value is DateTimeOffset)
            {
                return ((DateTimeOffset)value).ToUniversalTime().ToUnixTimeMilliseconds();
            }

            DateTime timestamp = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            if (timestamp.Kind == DateTimeKind.Unspecified)
            {
                timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
            }

            return new DateTimeOffset(timestamp.ToUniversalTime()).ToUnixTimeMilliseconds();
        }

        private static void AddMetadata(
            IDictionary<string, string> metadata,
            string key,
            IDataRecord record,
            int index)
        {
            string value = ReadString(record, index);
            if (!string.IsNullOrWhiteSpace(value))
            {
                metadata[key] = value;
            }
        }

        private static void AddDateTimeMetadata(
            IDictionary<string, string> metadata,
            string key,
            IDataRecord record,
            int index)
        {
            if (record.IsDBNull(index))
            {
                return;
            }

            object value = record.GetValue(index);
            DateTime timestamp = ReadLocalDateTime(value);
            metadata[key] = timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static DateTime ReadLocalDateTime(object value)
        {
            if (value is DateTimeOffset)
            {
                return ((DateTimeOffset)value).LocalDateTime;
            }

            DateTime timestamp = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            return timestamp.Kind == DateTimeKind.Utc ? timestamp.ToLocalTime() : timestamp;
        }

        private static string ReadString(IDataRecord record, int index)
        {
            return record.IsDBNull(index)
                ? string.Empty
                : Convert.ToString(record.GetValue(index), CultureInfo.InvariantCulture);
        }

        private sealed class RecordingSnapshot
        {
            public string PostId { get; set; }

            public Dictionary<string, string> Metadata { get; set; }
        }
    }
}
