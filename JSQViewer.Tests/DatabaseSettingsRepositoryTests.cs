using System;
using System.IO;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Application.Exporting;
using JSQViewer.Infrastructure.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class DatabaseSettingsRepositoryTests
    {
        private sealed class ReversingProtector : ISecretProtector
        {
            public string Protect(string plainText)
            {
                if (string.IsNullOrEmpty(plainText)) return string.Empty;
                char[] chars = plainText.ToCharArray();
                Array.Reverse(chars);
                return new string(chars);
            }

            public string Unprotect(string protectedText)
            {
                return Protect(protectedText);
            }
        }

        private sealed class FailingProtector : ISecretProtector
        {
            public string Protect(string plainText)
            {
                return "broken";
            }

            public string Unprotect(string protectedText)
            {
                throw new InvalidOperationException("Ciphertext from another user.");
            }
        }

        private sealed class TempPaths : IAppPaths, IDisposable
        {
            public TempPaths()
            {
                ApplicationBaseDirectory = Path.Combine(Path.GetTempPath(), "jsq_db_settings_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(ApplicationBaseDirectory);
            }

            public string ApplicationBaseDirectory { get; }

            public string ProjectRoot
            {
                get { return ApplicationBaseDirectory; }
            }

            public string LogDirectory
            {
                get { return Path.Combine(ApplicationBaseDirectory, "log"); }
            }

            public string GetProtocolTemplatePath(ProtocolTemplateMode mode)
            {
                return Path.Combine(ApplicationBaseDirectory, "template.xlsx");
            }

            public void Dispose()
            {
                try { Directory.Delete(ApplicationBaseDirectory, true); } catch { }
            }
        }

        [TestMethod]
        public void Load_WithoutFile_ReturnsDefaults()
        {
            using (var paths = new TempPaths())
            {
                var repository = new FileDatabaseSettingsRepository(paths, new ReversingProtector());

                DatabaseConnectionSettings settings = repository.Load();

                Assert.AreEqual("192.168.66.100", settings.host);
                Assert.AreEqual("jsq_db", settings.database);
                Assert.AreEqual(string.Empty, repository.LoadPassword());
            }
        }

        [TestMethod]
        public void SavePassword_RoundTripsThroughProtector()
        {
            using (var paths = new TempPaths())
            {
                var repository = new FileDatabaseSettingsRepository(paths, new ReversingProtector());
                DatabaseConnectionSettings settings = DatabaseConnectionSettings.CreateDefault();
                settings.host = "10.0.0.5";

                Assert.IsTrue(repository.SavePassword(settings, "s3cr3t-test-value"));

                var reopened = new FileDatabaseSettingsRepository(paths, new ReversingProtector());
                Assert.AreEqual("10.0.0.5", reopened.Load().host);
                Assert.AreEqual("s3cr3t-test-value", reopened.LoadPassword());
            }
        }

        [TestMethod]
        public void SavePassword_DoesNotWritePlainTextToDisk()
        {
            using (var paths = new TempPaths())
            {
                var repository = new FileDatabaseSettingsRepository(paths, new ReversingProtector());

                repository.SavePassword(DatabaseConnectionSettings.CreateDefault(), "s3cr3t-test-value");

                string json = File.ReadAllText(Path.Combine(paths.ProjectRoot, "database_settings.json"));
                Assert.IsFalse(json.Contains("s3cr3t-test-value"), "Пароль не должен попадать на диск в открытом виде.");
            }
        }

        [TestMethod]
        public void LoadPassword_WhenDecryptionFails_ReturnsEmptyInsteadOfThrowing()
        {
            using (var paths = new TempPaths())
            {
                new FileDatabaseSettingsRepository(paths, new ReversingProtector())
                    .SavePassword(DatabaseConnectionSettings.CreateDefault(), "s3cr3t-test-value");

                var repository = new FileDatabaseSettingsRepository(paths, new FailingProtector());

                Assert.AreEqual(string.Empty, repository.LoadPassword());
            }
        }
    }
}
