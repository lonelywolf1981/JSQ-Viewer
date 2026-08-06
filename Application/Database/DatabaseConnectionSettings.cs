namespace JSQViewer.Application.Database
{
    public sealed class DatabaseConnectionSettings
    {
        public string host { get; set; }
        public int port { get; set; }
        public string database { get; set; }
        public string username { get; set; }
        public string password_protected { get; set; }
        public int refresh_interval_seconds { get; set; }
        public int connect_timeout_seconds { get; set; }
        public int command_timeout_seconds { get; set; }

        public static DatabaseConnectionSettings CreateDefault()
        {
            return new DatabaseConnectionSettings
            {
                host = "192.168.66.100",
                port = 5432,
                database = "jsq_db",
                username = "jsq_user",
                password_protected = string.Empty,
                refresh_interval_seconds = 30,
                connect_timeout_seconds = 10,
                command_timeout_seconds = 120
            };
        }
    }
}
