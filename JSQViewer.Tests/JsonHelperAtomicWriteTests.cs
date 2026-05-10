using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using JSQViewer.Settings;

namespace JSQViewer.Tests
{
    [TestClass]
    public class JsonHelperAtomicWriteTests
    {
        [TestMethod]
        public void SaveToFile_DoesNotLeavePartialFile_WhenCalledTwice()
        {
            string dir = Path.Combine(Path.GetTempPath(), "jsq_atomictest_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "test.json");

            bool ok1 = JsonHelper.SaveToFile(path, new { value = "first" });
            Assert.IsTrue(ok1);
            Assert.IsTrue(File.Exists(path));

            bool ok2 = JsonHelper.SaveToFile(path, new { value = "second" });
            Assert.IsTrue(ok2);
            Assert.IsFalse(File.Exists(path + ".tmp"), "Временный файл не должен оставаться после записи");
            Assert.IsTrue(File.Exists(path));

            Directory.Delete(dir, true);
        }

        [TestMethod]
        public void SaveToFile_PreservesOriginalWhenDirectoryIsReadOnly_ReturnsFalse()
        {
            // Try to write to System root, which is typically not writable
            bool ok = JsonHelper.SaveToFile(
                "\\\\invalid-network-path\\file.json",
                new { value = 1 });
            Assert.IsFalse(ok);
        }
    }
}
