using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Workspace.UseCases;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class MergeLoadedSourcesUseCaseTests
    {
        [TestMethod]
        public void Execute_PreservesDisplayNamesAndInputSourceOrder()
        {
            TestData first = CreateData("jsqdb://recording/first", "A", 10L);
            first.SourceDisplayNames[first.Root] = "First recording";
            first.SourceOrder = new[] { first.Root };
            TestData second = CreateData("C:\\second", "B", 20L);
            second.SourceDisplayNames[second.Root] = "Second file";
            second.SourceOrder = new[] { second.Root };

            TestData result = new MergeLoadedSourcesUseCase().Execute(
                new[] { first, second },
                false);

            Assert.AreEqual("First recording", result.SourceDisplayNames[first.Root]);
            Assert.AreEqual("Second file", result.SourceDisplayNames[second.Root]);
            CollectionAssert.AreEqual(new[] { first.Root, second.Root }, result.SourceOrder);
        }

        [TestMethod]
        public void Execute_SameRootUsesFirstNonBlankDisplayName()
        {
            TestData first = CreateData("jsqdb://recording/one", "A", 10L);
            first.SourceDisplayNames[first.Root] = "   ";
            TestData duplicate = CreateData("JSQDB://RECORDING/ONE", "B", 20L);
            duplicate.SourceDisplayNames[duplicate.Root] = "  Recording one  ";

            TestData result = new MergeLoadedSourcesUseCase().Execute(
                new[] { first, duplicate },
                false);

            Assert.AreSame(first, result);
            Assert.AreEqual(1, result.RowCount);
            Assert.AreEqual("Recording one", result.SourceDisplayNames[first.Root]);
            CollectionAssert.AreEqual(new[] { first.Root }, result.SourceOrder);
        }

        [TestMethod]
        public void Execute_AlreadyMergedInputUsesSourceOrderBeforeMissingSourceColumns()
        {
            TestData merged = CreateData("C:\\combined", "A", 10L);
            merged.SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["C:\\sourceA"] = new[] { "A" },
                ["C:\\sourceC"] = new[] { "C" }
            };
            merged.SourceOrder = new[] { "C:\\sourceB", "C:\\SOURCEA", "c:\\sourceb" };
            TestData last = CreateData("C:\\sourceD", "D", 20L);

            TestData result = new MergeLoadedSourcesUseCase().Execute(
                new[] { merged, last },
                false);

            CollectionAssert.AreEqual(
                new[] { "C:\\sourceB", "C:\\SOURCEA", "C:\\sourceC", "C:\\sourceD" },
                result.SourceOrder);
        }

        [TestMethod]
        public void Execute_DistinctDatabaseRootsWithSameTitleAreBothRetained()
        {
            TestData first = CreateData("jsqdb://recording/one", "A", 10L);
            first.SourceDisplayNames[first.Root] = "Repeated title";
            TestData second = CreateData("jsqdb://recording/two", "B", 20L);
            second.SourceDisplayNames[second.Root] = "Repeated title";

            TestData result = new MergeLoadedSourcesUseCase().Execute(
                new[] { first, second },
                false);

            Assert.AreEqual(2, result.RowCount);
            Assert.AreEqual(2, result.SourceColumns.Count);
            Assert.AreEqual(2, result.SourceDisplayNames.Count);
            CollectionAssert.AreEqual(new[] { first.Root, second.Root }, result.SourceOrder);
        }

        [TestMethod]
        public void Execute_NullLegacyIdentityFieldsProducesNonNullCaseInsensitiveCollections()
        {
            TestData first = CreateData("C:\\first", "A", 10L);
            first.SourceDisplayNames = null;
            first.SourceOrder = null;
            TestData second = CreateData("C:\\second", "B", 20L);
            second.SourceDisplayNames = null;
            second.SourceOrder = null;

            TestData result = new MergeLoadedSourcesUseCase().Execute(
                new[] { first, second },
                false);

            Assert.IsNotNull(result.SourceDisplayNames);
            Assert.AreEqual(StringComparer.OrdinalIgnoreCase, result.SourceDisplayNames.Comparer);
            Assert.IsNotNull(result.SourceOrder);
            CollectionAssert.AreEqual(new[] { first.Root, second.Root }, result.SourceOrder);
            Assert.AreEqual(2, result.SourceOrder.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        private static TestData CreateData(string root, string column, long timestamp)
        {
            var data = new TestData
            {
                Root = root,
                RowCount = 1,
                TimestampsMs = new[] { timestamp },
                ColumnNames = new[] { column },
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = new[] { column }
                },
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = timestamp
                },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = timestamp
                }
            };
            data.Columns[column] = new double?[] { 1d };
            data.Channels[column] = new ChannelInfo { Code = column, Name = column };
            data.CodeSources[column] = root;
            return data;
        }
    }
}
