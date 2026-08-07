using System;
using System.Collections.Generic;
using JSQViewer.Application.Workspace;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class WorkspaceTitleBuilderTests
    {
        private static TestData CreateSingleRecording(Dictionary<string, string> meta)
        {
            return new TestData
            {
                Root = "jsqdb://recording/abc123",
                SourceOrder = new[] { "jsqdb://recording/abc123" },
                SourceDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "jsqdb://recording/abc123", "Post B 2026-08-06 14-33-12" }
                },
                Meta = meta
            };
        }

        [TestMethod]
        public void Build_WithSingleRecording_AppendsModelExperimentAndClimateMode()
        {
            TestData data = CreateSingleRecording(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Модель оборудования", "DINAMIC" },
                { "Тип испытания", "FUNC" },
                { "Климатический режим", "32/65" }
            });

            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(data, "запасной");

            Assert.AreEqual("Post B 2026-08-06 14-33-12 · DINAMIC · FUNC · 32/65", title);
        }

        [TestMethod]
        public void Build_WithSingleRecording_SkipsMissingParts()
        {
            TestData data = CreateSingleRecording(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Модель оборудования", "LIDER" },
                { "Тип испытания", "   " }
            });

            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(data, "запасной");

            Assert.AreEqual("Post B 2026-08-06 14-33-12 · LIDER", title);
        }

        [TestMethod]
        public void Build_WithFolderSource_KeepsPlainName()
        {
            var data = new TestData
            {
                Root = @"C:\tests\FORCE KA50",
                SourceOrder = new[] { @"C:\tests\FORCE KA50" },
                Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(data, "запасной");

            Assert.AreEqual("FORCE KA50", title);
        }

        [TestMethod]
        public void Build_WithSeveralSources_JoinsNamesWithoutMetadata()
        {
            var data = new TestData
            {
                Root = "jsqdb://recording/abc123",
                SourceOrder = new[] { "jsqdb://recording/abc123", "jsqdb://recording/def456" },
                SourceDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "jsqdb://recording/abc123", "Прогон 1" },
                    { "jsqdb://recording/def456", "Прогон 2" }
                },
                Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Модель оборудования", "DINAMIC" }
                }
            };

            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(data, "запасной");

            Assert.AreEqual("Прогон 1; Прогон 2", title);
        }

        [TestMethod]
        public void Build_WithoutSources_ReturnsFallback()
        {
            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(null, "запасной");

            Assert.AreEqual("запасной", title);
        }
    }
}
