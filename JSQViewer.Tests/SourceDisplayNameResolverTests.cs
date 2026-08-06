using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Workspace;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class SourceDisplayNameResolverTests
    {
        [TestMethod]
        public void Resolve_DatabaseSourceDisplayNameIsTrimmed()
        {
            const string source = "jsqdb://recording/a";
            TestData data = CreateData(source);
            data.SourceDisplayNames[source] = "  Прогон A  ";

            string result = new SourceDisplayNameResolver().Resolve(data, source);

            Assert.AreEqual("Прогон A", result);
        }

        [TestMethod]
        public void Resolve_BlankDatabaseDisplayNameFallsBackToRecordingId()
        {
            const string source = "jsqdb://recording/recording-42";
            TestData data = CreateData(source);
            data.SourceDisplayNames[source] = "   ";

            string result = new SourceDisplayNameResolver().Resolve(data, source);

            Assert.AreEqual("recording-42", result);
        }

        [TestMethod]
        public void Resolve_LegacySingleDatabaseSourceUsesMetadataTitle()
        {
            const string source = "jsqdb://recording/a";
            TestData data = CreateData(source);
            data.SourceDisplayNames = null;
            data.Meta["Название"] = "  Старый прогон  ";

            string result = new SourceDisplayNameResolver().Resolve(data, source);

            Assert.AreEqual("Старый прогон", result);
        }

        [TestMethod]
        public void Resolve_FileSourceIgnoresMetadataTitleAndUsesFileName()
        {
            const string source = @"C:\data\run.dbf";
            TestData data = CreateData(source);
            data.Meta["Название"] = "Название из метаданных";

            string result = new SourceDisplayNameResolver().Resolve(data, source);

            Assert.AreEqual("run.dbf", result);
        }

        [TestMethod]
        public void Resolve_FolderSourceTrimsTrailingSeparators()
        {
            const string source = @"C:\data\series\";
            TestData data = CreateData(source);

            string result = new SourceDisplayNameResolver().Resolve(data, source);

            Assert.AreEqual("series", result);
        }

        [TestMethod]
        public void GetOrderedRoots_SourceOrderComesFirstAndMissingColumnsAreAppended()
        {
            TestData data = CreateData(@"C:\unused");
            data.SourceOrder = new[] { @"C:\second", @"C:\FIRST", @"c:\second" };
            data.SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { @"C:\first", new string[0] },
                { @"C:\third", new string[0] }
            };

            IReadOnlyList<string> result = new SourceDisplayNameResolver().GetOrderedRoots(data);

            CollectionAssert.AreEqual(
                new[] { @"C:\second", @"C:\FIRST", @"C:\third" },
                result.ToArray());
        }

        [TestMethod]
        public void GetOrderedRoots_UsesRootWhenOrderAndColumnsHaveNoRoots()
        {
            TestData data = CreateData(@"C:\fallback\run.dbf");
            data.SourceOrder = new string[0];
            data.SourceColumns.Clear();

            IReadOnlyList<string> result = new SourceDisplayNameResolver().GetOrderedRoots(data);

            CollectionAssert.AreEqual(new[] { @"C:\fallback\run.dbf" }, result.ToArray());
        }

        [TestMethod]
        public void GetOrderedRoots_NullDataIsEmpty()
        {
            IReadOnlyList<string> result = new SourceDisplayNameResolver().GetOrderedRoots(null);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ResolveAll_SameDatabaseTitlesKeepBothRootsAndAddIds()
        {
            TestData data = CreateData(
                new[] { "jsqdb://recording/a", "jsqdb://recording/b" },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "jsqdb://recording/a", "KA50" },
                    { "jsqdb://recording/b", "ka50" }
                });

            IReadOnlyDictionary<string, string> result = new SourceDisplayNameResolver().ResolveAll(data);

            Assert.AreEqual("KA50 [a]", result["jsqdb://recording/a"]);
            Assert.AreEqual("ka50 [b]", result["jsqdb://recording/b"]);
            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void ResolveAll_DatabaseAndFileCollisionDisambiguatesOnlyDatabase()
        {
            const string databaseSource = "jsqdb://recording/a";
            const string fileSource = @"C:\data\KA50";
            TestData data = CreateData(
                new[] { databaseSource, fileSource },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { databaseSource, "KA50" }
                });

            IReadOnlyDictionary<string, string> result = new SourceDisplayNameResolver().ResolveAll(data);

            Assert.AreEqual("KA50 [a]", result[databaseSource]);
            Assert.AreEqual("KA50", result[fileSource]);
            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void ResolveAll_SameFileBaseNamesRetainsBothRoots()
        {
            const string first = @"C:\first\run.dbf";
            const string second = @"D:\second\run.dbf";
            TestData data = CreateData(new[] { first, second }, null);

            IReadOnlyDictionary<string, string> result = new SourceDisplayNameResolver().ResolveAll(data);

            Assert.AreEqual("run.dbf", result[first]);
            Assert.AreEqual("run.dbf", result[second]);
            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void Resolve_IsCollisionAwareForWorkspaceRoot()
        {
            const string first = "jsqdb://recording/a";
            const string second = "jsqdb://recording/b";
            TestData data = CreateData(
                new[] { first, second },
                new Dictionary<string, string>
                {
                    { first.ToUpperInvariant(), "Same" },
                    { second, "same" }
                });

            string result = new SourceDisplayNameResolver().Resolve(data, first);

            Assert.AreEqual("Same [a]", result);
        }

        [TestMethod]
        public void Resolve_RootOutsideWorkspaceUsesSafeSingleRootFallback()
        {
            TestData data = CreateData("jsqdb://recording/current");
            data.Meta["Название"] = "Current recording";

            string result = new SourceDisplayNameResolver().Resolve(
                data,
                "jsqdb://recording/outside");

            Assert.AreEqual("outside", result);
        }

        [TestMethod]
        public void Resolve_EmptyRootIsEmpty()
        {
            Assert.AreEqual(string.Empty, new SourceDisplayNameResolver().Resolve(new TestData(), "  "));
        }

        [TestMethod]
        public void Resolve_NullCollectionsAndLegacyCaseSensitiveDisplayNamesAreSafe()
        {
            const string source = "jsqdb://recording/a";
            TestData data = CreateData(source);
            data.SourceOrder = null;
            data.SourceDisplayNames = new Dictionary<string, string>
            {
                { "JSQDB://RECORDING/A", "  Legacy title  " }
            };

            string result = new SourceDisplayNameResolver().Resolve(data, source);

            Assert.AreEqual("Legacy title", result);
        }

        [TestMethod]
        public void Build_SingleSourceUsesResolvedName()
        {
            const string source = "jsqdb://recording/a";
            TestData data = CreateData(source);
            data.SourceDisplayNames[source] = "Прогон A";

            string result = CreateTitleBuilder().Build(data, "fallback");

            Assert.AreEqual("Прогон A", result);
        }

        [TestMethod]
        public void Build_MultipleSourcesUsesSourceOrderAndSeparator()
        {
            const string first = "jsqdb://recording/a";
            const string second = @"C:\data\run.dbf";
            TestData data = CreateData(new[] { first, second }, null);
            data.SourceDisplayNames[first] = "Прогон A";

            string result = CreateTitleBuilder().Build(data, "fallback");

            Assert.AreEqual("Прогон A; run.dbf", result);
        }

        [TestMethod]
        public void Build_DoesNotDeduplicateEqualDisplayValues()
        {
            TestData data = CreateData(
                new[] { @"C:\one\run.dbf", @"D:\two\run.dbf" },
                null);

            string result = CreateTitleBuilder().Build(data, "fallback");

            Assert.AreEqual("run.dbf; run.dbf", result);
        }

        [TestMethod]
        public void Build_NoUsableSourceReturnsFallbackOrEmpty()
        {
            WorkspaceTitleBuilder builder = CreateTitleBuilder();

            Assert.AreEqual("session folder", builder.Build(null, "session folder"));
            Assert.AreEqual(string.Empty, builder.Build(null, null));
        }

        [TestMethod]
        public void BuildCaption_DataAlreadyLoadedBeforeWindowCreation_UsesDatabaseTitle()
        {
            const string source = "jsqdb://recording/a";
            TestData data = CreateData(source);
            data.SourceDisplayNames[source] = "Прогон A";

            string result = CreateTitleBuilder().BuildCaption(data, "fallback", "График — {0}");

            Assert.AreEqual("График — Прогон A", result);
        }

        [TestMethod]
        public void BuildCaption_BlankFormatBehavesAsValuePlaceholder()
        {
            const string source = @"C:\data\run.dbf";
            TestData data = CreateData(source);

            Assert.AreEqual("run.dbf", CreateTitleBuilder().BuildCaption(data, "fallback", "   "));
        }

        private static WorkspaceTitleBuilder CreateTitleBuilder()
        {
            return new WorkspaceTitleBuilder(new SourceDisplayNameResolver());
        }

        private static TestData CreateData(string source)
        {
            return CreateData(new[] { source }, null);
        }

        private static TestData CreateData(
            string[] sources,
            Dictionary<string, string> displayNames)
        {
            TestData data = new TestData
            {
                Root = sources == null || sources.Length == 0 ? string.Empty : sources[0],
                SourceOrder = sources ?? new string[0],
                SourceDisplayNames = displayNames
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            };

            foreach (string source in sources ?? new string[0])
            {
                data.SourceColumns[source] = new string[0];
            }

            return data;
        }
    }
}
