using System;
using System.Collections.Generic;
using JSQViewer.Export;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class TemplateExporterPreparationTests
    {
        [TestMethod]
        public void ResolveFixedChannelMap_PrefersSelectedAAndCMatches()
        {
            var selectedChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "C-Pc",
                "A-Pe"
            };

            string[] columnNames =
            {
                "Pc",
                "A-Pc",
                "C-Pc",
                "Z-Pc",
                "Pe",
                "A-Pe",
                "T-sie"
            };

            Dictionary<string, string> fixedChannels = TemplateExporter.ResolveFixedChannelMap(columnNames, selectedChannels);

            Assert.AreEqual("C-Pc", fixedChannels["Pc"]);
            Assert.AreEqual("A-Pe", fixedChannels["Pe"]);
            Assert.AreEqual(string.Empty, fixedChannels["T-sie"]);
        }

        [TestMethod]
        public void ResolveFixedChannelMap_WithNullSelection_StillPrefersAAndCMatches()
        {
            string[] columnNames =
            {
                "Pc",
                "A-Pc",
                "C-Pc",
                "Pe",
                "A-Pe",
                "T-sie"
            };

            Dictionary<string, string> fixedChannels = TemplateExporter.ResolveFixedChannelMap(columnNames, null);

            Assert.AreEqual("A-Pc", fixedChannels["Pc"]);
            Assert.AreEqual("A-Pe", fixedChannels["Pe"]);
        }

        [TestMethod]
        public void ResolveFixedChannelMap_WithEmptySelection_StillPrefersAAndCMatches()
        {
            string[] columnNames =
            {
                "Pc",
                "A-Pc",
                "C-Pc",
                "Pe",
                "C-Pe",
                "T-sie"
            };

            Dictionary<string, string> fixedChannels = TemplateExporter.ResolveFixedChannelMap(columnNames, new string[0]);

            Assert.AreEqual("A-Pc", fixedChannels["Pc"]);
            Assert.AreEqual("C-Pe", fixedChannels["Pe"]);
        }

        [TestMethod]
        public void ResolveExtraChannelCodes_ReturnsSelectedNonFixedChannelsInColumnOrder()
        {
            var selectedChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "X-02",
                "A-Pc",
                "X-01",
                "C-Pc"
            };

            string[] columnNames =
            {
                "X-02",
                "A-Pc",
                "X-01",
                "C-Pc"
            };

            var fixedChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "A-Pc",
                "C-Pc"
            };

            List<string> extraChannels = TemplateExporter.ResolveExtraChannelCodes(columnNames, selectedChannels, fixedChannels);

            CollectionAssert.AreEqual(new[] { "X-02", "X-01" }, extraChannels);
        }

        [TestMethod]
        public void ResolveExtraChannelCodes_WithNullSelection_ReturnsAllNonFixedColumns()
        {
            string[] columnNames =
            {
                "Pc",
                "A-Pc",
                "C-Pc",
                "Pe",
                "X-01",
                "T-sie"
            };

            var fixedChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "A-Pc",
                "C-Pc"
            };

            List<string> extraChannels = TemplateExporter.ResolveExtraChannelCodes(columnNames, null, fixedChannels);

            CollectionAssert.AreEqual(new[] { "Pc", "Pe", "X-01", "T-sie" }, extraChannels);
        }

        [TestMethod]
        public void ResolveExtraChannelCodes_WithEmptySelection_ReturnsAllNonFixedColumns()
        {
            string[] columnNames =
            {
                "Pc",
                "A-Pc",
                "C-Pc",
                "Pe",
                "X-01",
                "T-sie"
            };

            var fixedChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "A-Pc",
                "C-Pc"
            };

            List<string> extraChannels = TemplateExporter.ResolveExtraChannelCodes(columnNames, new string[0], fixedChannels);

            CollectionAssert.AreEqual(new[] { "Pc", "Pe", "X-01", "T-sie" }, extraChannels);
        }

        [TestMethod]
        public void BuildTimeGridAndIndices_SwapsBoundsAndMarksDistantGapsAsMissing()
        {
            TemplateExporter.TimeGridPreparationResult result = TemplateExporter.BuildTimeGridAndIndices(
                new long[] { 0L, 100000L },
                rangeStartMs: 100000L,
                rangeEndMs: 0L);

            CollectionAssert.AreEqual(
                new[] { 0L, 20000L, 40000L, 60000L, 80000L, 100000L },
                result.GridMs);
            CollectionAssert.AreEqual(new[] { 0, 0, -1, -1, 1, 1 }, result.Indices);
        }

        [TestMethod]
        public void BuildTimeGridAndIndices_UsesDetectedTimestampStep()
        {
            TemplateExporter.TimeGridPreparationResult result = TemplateExporter.BuildTimeGridAndIndices(
                new long[] { 0L, 10000L, 20000L, 30000L });

            CollectionAssert.AreEqual(
                new[] { 0L, 10000L, 20000L, 30000L },
                result.GridMs);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, result.Indices);
        }
    }
}
