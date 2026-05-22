using JSQViewer.Application.Abstractions;
using JSQViewer.Infrastructure.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class SourceSelectionLocalizationTests
    {
        [TestMethod]
        public void RussianSourceSelectionTextUsesFolderAndFileLabels()
        {
            var service = new DictionaryLocalizationService();
            service.CurrentLanguage = AppLanguage.Ru;

            Assert.AreEqual("Папка", service.Get("SelectSourceFolder"));
            Assert.AreEqual("Файл", service.Get("SelectSourceFile"));
            Assert.IsFalse(service.Get("SelectSourcePrompt").Contains("Да"));
            Assert.IsFalse(service.Get("SelectSourcePrompt").Contains("Нет"));
        }
    }
}
