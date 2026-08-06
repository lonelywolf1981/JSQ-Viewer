using System;
using JSQViewer.Application.Database;
using Npgsql;

namespace JSQViewer.Infrastructure.Database
{
    public sealed class NpgsqlConnectionFactory
    {
        public string BuildConnectionString(DatabaseConnectionSettings settings, string password)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = settings.host ?? string.Empty,
                Port = settings.port,
                Database = settings.database ?? string.Empty,
                Username = settings.username ?? string.Empty,
                Password = password ?? string.Empty,
                Timeout = settings.connect_timeout_seconds,
                CommandTimeout = settings.command_timeout_seconds,
                ApplicationName = "JSQViewer"
            };

            return builder.ToString();
        }

        public NpgsqlConnection Create(DatabaseConnectionSettings settings, string password)
        {
            return new NpgsqlConnection(BuildConnectionString(settings, password));
        }

        public string TestConnection(DatabaseConnectionSettings settings, string password)
        {
            try
            {
                using (NpgsqlConnection connection = Create(settings, password))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT 1", connection))
                    {
                        command.ExecuteScalar();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
