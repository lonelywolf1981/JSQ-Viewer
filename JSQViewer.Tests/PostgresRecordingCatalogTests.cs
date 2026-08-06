using JSQViewer.Infrastructure.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class PostgresRecordingCatalogTests
    {
        [TestMethod]
        public void EscapeLikePattern_EscapesBackslashPercentAndUnderscore()
        {
            Assert.AreEqual("50\\%", PostgresRecordingCatalog.EscapeLikePattern("50%"));
            Assert.AreEqual("A\\_B", PostgresRecordingCatalog.EscapeLikePattern("A_B"));
            Assert.AreEqual("C:\\\\x", PostgresRecordingCatalog.EscapeLikePattern("C:\\x"));
            Assert.AreEqual("a\\_b\\%c\\\\d", PostgresRecordingCatalog.EscapeLikePattern("a_b%c\\d"));
            Assert.AreEqual("LIDER", PostgresRecordingCatalog.EscapeLikePattern("LIDER"));
        }
    }
}
