using System.IO;
using JSQViewer.Infrastructure.DataImport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ProvaMetadataReaderTests
    {
        [TestMethod]
        public void Read_WithIniStyleDat_ReturnsKeyValueMetadata()
        {
            string root = Path.Combine(Path.GetTempPath(), "jsq_meta_" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(
                    Path.Combine(root, "Prova001.dat"),
                    "[Recording]\r\nOperator = Administrator\r\nPost = Post B\r\nRefrigerant = R290\r\n");

                var reader = new ProvaMetadataReader();

                var meta = reader.Read(root);

                Assert.AreEqual("Administrator", meta["Operator"]);
                Assert.AreEqual("Post B", meta["Post"]);
                Assert.AreEqual("R290", meta["Refrigerant"]);
                Assert.IsFalse(meta.ContainsKey("[Recording]"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Read_WithNonProvaDatFile_FallsBackToAnyDat()
        {
            string root = Path.Combine(Path.GetTempPath(), "jsq_meta_" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "metadata.dat"), "MODEL/TYPE;KA140\r\n");

                var reader = new ProvaMetadataReader();

                var meta = reader.Read(root);

                Assert.AreEqual("KA140", meta["MODEL/TYPE"]);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
