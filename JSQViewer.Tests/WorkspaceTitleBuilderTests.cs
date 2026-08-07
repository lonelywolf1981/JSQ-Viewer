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
        public void Build_WithSingleRecording_ShowsComposedDisplayNameVerbatim()
        {
            // RecordingDisplayNameBuilder composes model, compressor, experiment type and climate
            // mode into the display name when the recording is read, so every window that shows a
            // source name shows the same text. The title builder must not append them a second time.
            var data = new TestData
            {
                Root = "jsqdb://recording/abc123",
                SourceOrder = new[] { "jsqdb://recording/abc123" },
                SourceDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "jsqdb://recording/abc123", "Post B 2026-08-06 14-33-12 · LIDER · NPT14RA · FUNC · 32/65" }
                },
                Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Модель оборудования", "LIDER" },
                    { "Тип испытания", "FUNC" },
                    { "Климатический режим", "32/65" }
                }
            };

            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(data, "запасной");

            Assert.AreEqual("Post B 2026-08-06 14-33-12 · LIDER · NPT14RA · FUNC · 32/65", title);
        }

        [TestMethod]
        public void Build_WithSingleRecording_DoesNotAppendMetadataToTheName()
        {
            TestData data = CreateSingleRecording(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Модель оборудования", "LIDER" },
                { "Тип испытания", "FUNC" }
            });

            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(data, "запасной");

            Assert.AreEqual("Post B 2026-08-06 14-33-12", title);
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
