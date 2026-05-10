using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using JSQViewer.Settings;

namespace JSQViewer.Tests
{
    [TestClass]
    public class PresetStoreTests
    {
        [TestMethod]
        public void Save_WithFullPreset_DoesNotMutateCallerObject()
        {
            string dir = Path.Combine(Path.GetTempPath(), "jsq_presettest_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            var original = new ViewerPreset
            {
                name = "My Preset",
                key  = "original-key-set-by-caller",
                channels = new List<string> { "CH01", "CH02" }
            };
            string keyBefore = original.key;
            string savedAtBefore = original.saved_at;

            PresetStore.Save(dir, original);

            Assert.AreEqual(keyBefore, original.key,
                "Save не должен изменять поле key у объекта вызывающего кода");
            Assert.AreEqual(savedAtBefore, original.saved_at,
                "Save не должен изменять поле saved_at у объекта вызывающего кода");

            Directory.Delete(dir, true);
        }
    }
}
