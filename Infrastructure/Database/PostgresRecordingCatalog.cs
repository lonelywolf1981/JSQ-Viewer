using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Application.Workspace.Ports;
using Npgsql;

namespace JSQViewer.Infrastructure.Database
{
    public sealed class PostgresRecordingCatalog : IRecordingCatalog
    {
        private readonly NpgsqlConnectionFactory _connectionFactory;
        private readonly IDatabaseSettingsRepository _settingsRepository;
        private readonly RecordingCatalogQueryBuilder _queryBuilder;
        private readonly ClimateModeResolver _climateModeResolver;

        public PostgresRecordingCatalog(
            NpgsqlConnectionFactory connectionFactory,
            IDatabaseSettingsRepository settingsRepository)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _queryBuilder = new RecordingCatalogQueryBuilder();
            _climateModeResolver = new ClimateModeResolver();
        }

        public IList<RecordingSummaryItem> List(RecordingCatalogFilter filter)
        {
            RecordingCatalogFilter safeFilter = filter ?? new RecordingCatalogFilter();
            var parameterNames = new List<string>();
            string sql = _queryBuilder.Build(safeFilter, parameterNames);
            var result = new List<RecordingSummaryItem>();

            using (NpgsqlConnection connection = OpenConnection())
            using (var command = new NpgsqlCommand(sql, connection))
            {
                for (int i = 0; i < parameterNames.Count; i++)
                {
                    string name = parameterNames[i];
                    if (name == "post_id") command.Parameters.AddWithValue(name, safeFilter.PostId.Trim());
                    else if (name == "from") command.Parameters.AddWithValue(name, safeFilter.From.Value);
                    else if (name == "to") command.Parameters.AddWithValue(name, safeFilter.To.Value);
                    else if (name == "experiment_type") command.Parameters.AddWithValue(name, safeFilter.ExperimentType.Trim());
                    else if (name == "title") command.Parameters.AddWithValue(name, "%" + EscapeLikePattern(safeFilter.TitleContains.Trim()) + "%");
                    else if (name == "limit") command.Parameters.AddWithValue(name, safeFilter.Limit <= 0 ? 500 : safeFilter.Limit);
                }

                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new RecordingSummaryItem
                        {
                            Id = ReadString(reader, 0),
                            PostId = ReadString(reader, 1),
                            Title = ReadString(reader, 2),
                            Status = ReadString(reader, 3),
                            StartedAt = ReadLocalDateTime(reader, 4),
                            StoppedAt = ReadLocalDateTime(reader, 5),
                            EquipmentModel = ReadString(reader, 6),
                            ExperimentType = ReadString(reader, 7),
                            ClimateMode = _climateModeResolver.Resolve(
                                ReadString(reader, 8),
                                ReadNullableDouble(reader, 9),
                                ReadNullableDouble(reader, 10))
                        });
                    }
                }
            }

            return result;
        }

        public IList<string> ListPosts()
        {
            return ReadSingleColumn("SELECT id FROM posts WHERE is_active ORDER BY id");
        }

        public IList<string> ListExperimentTypes()
        {
            return ReadSingleColumn("SELECT name FROM experiment_types WHERE is_active ORDER BY name");
        }

        public string GetStatus(string recordingId)
        {
            using (NpgsqlConnection connection = OpenConnection())
            using (var command = new NpgsqlCommand("SELECT status FROM recordings WHERE id = @id", connection))
            {
                command.Parameters.AddWithValue("id", recordingId ?? string.Empty);
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
        }

        private IList<string> ReadSingleColumn(string sql)
        {
            var result = new List<string>();
            using (NpgsqlConnection connection = OpenConnection())
            using (var command = new NpgsqlCommand(sql, connection))
            using (NpgsqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(ReadString(reader, 0));
                }
            }

            return result;
        }

        private NpgsqlConnection OpenConnection()
        {
            DatabaseConnectionSettings settings = _settingsRepository.Load();
            NpgsqlConnection connection = _connectionFactory.Create(settings, _settingsRepository.LoadPassword());
            connection.Open();
            return connection;
        }

        internal static string EscapeLikePattern(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
        }

        private static string ReadString(IDataRecord record, int index)
        {
            return record.IsDBNull(index) ? string.Empty : Convert.ToString(record.GetValue(index));
        }

        private static DateTime? ReadLocalDateTime(IDataRecord record, int index)
        {
            if (record.IsDBNull(index))
            {
                return null;
            }

            var value = (DateTime)record.GetValue(index);
            return value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        }

        private static double? ReadNullableDouble(IDataRecord record, int index)
        {
            return record.IsDBNull(index)
                ? (double?)null
                : Convert.ToDouble(record.GetValue(index), CultureInfo.InvariantCulture);
        }
    }
}
