using System;
using System.Collections.Generic;
using JSQViewer.Application.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingDisplayNameBuilderTests
    {
        private static Dictionary<string, string> CreateMetadata()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Название", "Post B 2026-08-06 14-33-12" },
                { "Модель оборудования", "LIDER" },
                { "Компрессор", "NPT14RA (220-240V/50Hz)" },
                { "Тип испытания", "FUNC" },
                { "Климатический режим", "32/65" }
            };
        }

        [TestMethod]
        public void Build_WithEveryField_JoinsThemInReadingOrder()
        {
            string name = RecordingDisplayNameBuilder.Build(CreateMetadata(), "8aa4fe95");

            Assert.AreEqual("Post B 2026-08-06 14-33-12 · LIDER · NPT14RA · FUNC · 32/65", name);
        }

        [TestMethod]
        public void Build_StripsVoltageSuffixFromCompressor()
        {
            Dictionary<string, string> metadata = CreateMetadata();
            metadata["Компрессор"] = "KA90HHA (220-240V/50Гц)";

            string name = RecordingDisplayNameBuilder.Build(metadata, "8aa4fe95");

            StringAssert.Contains(name, "KA90HHA");
            Assert.IsFalse(name.Contains("220-240V"), "Напряжение не должно попадать в имя.");
        }

        [TestMethod]
        public void Build_KeepsCompressorWithoutSuffixIntact()
        {
            Dictionary<string, string> metadata = CreateMetadata();
            metadata["Компрессор"] = "NUY45";

            string name = RecordingDisplayNameBuilder.Build(metadata, "8aa4fe95");

            Assert.AreEqual("Post B 2026-08-06 14-33-12 · LIDER · NUY45 · FUNC · 32/65", name);
        }

        [TestMethod]
        public void Build_SkipsMissingAndBlankFields()
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Название", "Post C 2026-08-04 08-53-22" },
                { "Модель оборудования", "   " },
                { "Тип испытания", "POWER" }
            };

            string name = RecordingDisplayNameBuilder.Build(metadata, "e165ba52");

            Assert.AreEqual("Post C 2026-08-04 08-53-22 · POWER", name);
        }

        [TestMethod]
        public void Build_WithoutTitle_UsesFallbackAndStillAppendsFields()
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Модель оборудования", "REX" },
                { "Тип испытания", "POWER" }
            };

            string name = RecordingDisplayNameBuilder.Build(metadata, "e165ba52");

            Assert.AreEqual("e165ba52 · REX · POWER", name);
        }

        [TestMethod]
        public void Build_WhileRecordingIsRunning_PrefixesActiveMarker()
        {
            Dictionary<string, string> metadata = CreateMetadata();
            metadata["Статус"] = "recording";

            string name = RecordingDisplayNameBuilder.Build(metadata, "8aa4fe95");

            Assert.AreEqual("● Post B 2026-08-06 14-33-12 · LIDER · NPT14RA · FUNC · 32/65", name);
        }

        [TestMethod]
        public void Build_WhenRecordingStopped_HasNoMarker()
        {
            Dictionary<string, string> metadata = CreateMetadata();
            metadata["Статус"] = "stopped";

            string name = RecordingDisplayNameBuilder.Build(metadata, "8aa4fe95");

            Assert.IsFalse(name.StartsWith("●", StringComparison.Ordinal), "Завершённый прогон не помечается.");
        }

        [TestMethod]
        public void Build_WithoutStatus_HasNoMarker()
        {
            string name = RecordingDisplayNameBuilder.Build(CreateMetadata(), "8aa4fe95");

            Assert.IsFalse(name.StartsWith("●", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Build_WithoutMetadata_ReturnsFallback()
        {
            Assert.AreEqual("e165ba52", RecordingDisplayNameBuilder.Build(null, "e165ba52"));
        }

        [TestMethod]
        public void Build_TrimsSurroundingWhitespace()
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Название", "  Post A  " },
                { "Модель оборудования", " OMEGA " }
            };

            string name = RecordingDisplayNameBuilder.Build(metadata, "abc");

            Assert.AreEqual("Post A · OMEGA", name);
        }
    }
}
