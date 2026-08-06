using JSQViewer.Application.Database;
using JSQViewer.Infrastructure.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class DatabaseConnectionSettingsTests
    {
        [TestMethod]
        public void CreateDefault_UsesLaboratoryServer()
        {
            DatabaseConnectionSettings settings = DatabaseConnectionSettings.CreateDefault();

            Assert.AreEqual("192.168.66.100", settings.host);
            Assert.AreEqual(5432, settings.port);
            Assert.AreEqual("jsq_db", settings.database);
            Assert.AreEqual("jsq_user", settings.username);
            Assert.AreEqual(30, settings.refresh_interval_seconds);
            Assert.AreEqual(10, settings.connect_timeout_seconds);
            Assert.AreEqual(120, settings.command_timeout_seconds);
        }

        [TestMethod]
        public void BuildConnectionString_IncludesTimeoutsAndCredentials()
        {
            DatabaseConnectionSettings settings = DatabaseConnectionSettings.CreateDefault();
            var factory = new NpgsqlConnectionFactory();

            string connectionString = factory.BuildConnectionString(settings, "secret");

            StringAssert.Contains(connectionString, "Host=192.168.66.100");
            StringAssert.Contains(connectionString, "Port=5432");
            StringAssert.Contains(connectionString, "Database=jsq_db");
            StringAssert.Contains(connectionString, "Username=jsq_user");
            StringAssert.Contains(connectionString, "Password=secret");
            StringAssert.Contains(connectionString, "Timeout=10");
            StringAssert.Contains(connectionString, "Command Timeout=120");
        }
    }
}
