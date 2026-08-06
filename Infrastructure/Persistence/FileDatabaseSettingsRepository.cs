using System;
using System.IO;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Settings;

namespace JSQViewer.Infrastructure.Persistence
{
    public sealed class FileDatabaseSettingsRepository : IDatabaseSettingsRepository
    {
        private readonly string _filePath;
        private readonly ISecretProtector _secretProtector;

        public FileDatabaseSettingsRepository(IAppPaths appPaths, ISecretProtector secretProtector)
        {
            if (appPaths == null) throw new ArgumentNullException(nameof(appPaths));

            _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
            _filePath = Path.Combine(appPaths.ProjectRoot, "database_settings.json");
        }

        public DatabaseConnectionSettings Load()
        {
            DatabaseConnectionSettings settings = JsonHelper.LoadFromFile(_filePath, DatabaseConnectionSettings.CreateDefault());
            return Sanitize(settings);
        }

        public bool Save(DatabaseConnectionSettings settings)
        {
            return JsonHelper.SaveToFile(_filePath, Sanitize(settings));
        }

        public string LoadPassword()
        {
            string protectedPassword = Load().password_protected;
            if (string.IsNullOrEmpty(protectedPassword))
            {
                return string.Empty;
            }

            try
            {
                return _secretProtector.Unprotect(protectedPassword) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public bool SavePassword(DatabaseConnectionSettings settings, string password)
        {
            DatabaseConnectionSettings sanitized = Sanitize(settings);
            sanitized.password_protected = string.IsNullOrEmpty(password)
                ? string.Empty
                : _secretProtector.Protect(password);
            return JsonHelper.SaveToFile(_filePath, sanitized);
        }

        private static DatabaseConnectionSettings Sanitize(DatabaseConnectionSettings settings)
        {
            DatabaseConnectionSettings defaults = DatabaseConnectionSettings.CreateDefault();
            if (settings == null)
            {
                return defaults;
            }

            if (string.IsNullOrWhiteSpace(settings.host)) settings.host = defaults.host;
            if (string.IsNullOrWhiteSpace(settings.database)) settings.database = defaults.database;
            if (string.IsNullOrWhiteSpace(settings.username)) settings.username = defaults.username;
            if (settings.password_protected == null) settings.password_protected = string.Empty;
            if (settings.port <= 0 || settings.port > 65535) settings.port = defaults.port;
            if (settings.refresh_interval_seconds < 5) settings.refresh_interval_seconds = defaults.refresh_interval_seconds;
            if (settings.connect_timeout_seconds <= 0) settings.connect_timeout_seconds = defaults.connect_timeout_seconds;
            if (settings.command_timeout_seconds <= 0) settings.command_timeout_seconds = defaults.command_timeout_seconds;
            return settings;
        }
    }
}
