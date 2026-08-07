using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Workspace;
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
        public void Execute_SameRootWithoutSourceColumnsRetainsLaterIdentity()
        {
            TestData first = CreateData("jsqdb://recording/one", "A", 10L);
            first.SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            first.SourceDisplayNames[first.Root] = "   ";
            TestData duplicate = CreateData("JSQDB://RECORDING/ONE", "B", 20L);
            duplicate.SourceDisplayNames[duplicate.Root] = "  Recording one  ";
            duplicate.SourceOrder = new[] { first.Root, duplicate.Root };

            TestData result = new MergeLoadedSourcesUseCase().Execute(
                new[] { first, duplicate },
                false);

            Assert.AreSame(first, result);
            Assert.AreEqual(1, result.RowCount);
            Assert.AreEqual(first.Root, result.Root);
            Assert.AreEqual(0, result.SourceColumns.Count);
            Assert.IsNotNull(result.SourceDisplayNames);
            Assert.AreEqual(StringComparer.OrdinalIgnoreCase, result.SourceDisplayNames.Comparer);
            Assert.AreEqual("Recording one", result.SourceDisplayNames[first.Root]);
            CollectionAssert.AreEqual(new[] { first.Root }, result.SourceOrder);
        }

        [TestMethod]
        public void Execute_SameRootWithNullSourceColumnsRetainsLaterIdentity()
        {
            TestData first = CreateData("jsqdb://recording/one", "A", 10L);
            first.SourceColumns = null;
            TestData duplicate = CreateData("JSQDB://RECORDING/ONE", "B", 20L);
            duplicate.SourceDisplayNames[duplicate.Root] = "Recording one";
            duplicate.SourceOrder = new[] { first.Root };

            TestData result = new MergeLoadedSourcesUseCase().Execute(
                new[] { first, duplicate },
                false);

            Assert.AreSame(first, result);
            Assert.IsNull(result.SourceColumns);
            Assert.AreEqual("Recording one", result.SourceDisplayNames[first.Root]);
            CollectionAssert.AreEqual(new[] { first.Root }, result.SourceOrder);
        }

        [TestMethod]
        public void Execute_AlreadyMergedInputPreservesNestedSourceIdentityThroughRemove()
        {
            const string sourceA = "C:\\sourceA";
            const string sourceB = "C:\\sourceB";
            const string sourceC = "C:\\sourceC";
            const string sourceD = "C:\\sourceD";
            TestData merged = CreateData("C:\\combined", "Shared", 10L);
            merged.ColumnNames = new[] { "Shared", "B", "C" };
            merged.Columns["B"] = new double?[] { 2d };
            merged.Columns["C"] = new double?[] { 3d };
            merged.Channels["B"] = new ChannelInfo { Code = "B", Name = "B" };
            merged.Channels["C"] = new ChannelInfo { Code = "C", Name = "C" };
            merged.CodeSources["Shared"] = sourceA;
            merged.CodeSources["B"] = sourceB;
            merged.CodeSources["C"] = sourceC;
            merged.SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [sourceA] = new[] { "Shared" },
                [sourceB] = new[] { "B" },
                [sourceC] = new[] { "C" }
            };
            merged.SourceOrder = new[] { sourceB, "C:\\SOURCEA", "c:\\sourceb" };
            merged.SourceDisplayNames[sourceA] = "Source A";
            merged.SourceDisplayNames[sourceB] = "Same title";
            merged.SourceDisplayNames[sourceC] = "Same title";
            merged.SourceDisplayNames[merged.Root] = "Combined outer root";
            merged.SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                [sourceA] = 10L,
                [sourceB] = 11L,
                [sourceC] = 12L
            };
            merged.SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                [sourceA] = 20L,
                [sourceB] = 21L,
                [sourceC] = 22L
            };
            TestData last = CreateData(sourceD, "Shared", 30L);
            last.SourceDisplayNames[sourceD] = "Source D";

            TestData result = new MergeLoadedSourcesUseCase().Execute(
                new[] { merged, last },
                true);

            CollectionAssert.AreEqual(
                new[] { sourceB, "C:\\SOURCEA", sourceC, sourceD },
                result.SourceOrder);
            CollectionAssert.AreEquivalent(
                new[] { sourceA, sourceB, sourceC, sourceD },
                result.SourceColumns.Keys.ToArray());
            Assert.IsFalse(result.SourceColumns.ContainsKey(merged.Root));
            CollectionAssert.AreEqual(new[] { "combined::Shared" }, result.SourceColumns[sourceA]);
            Assert.AreEqual(sourceA, result.CodeSources["combined::Shared"]);
            Assert.AreEqual(sourceB, result.CodeSources["B"]);
            Assert.AreEqual(sourceC, result.CodeSources["C"]);
            Assert.AreEqual(sourceD, result.CodeSources["sourceD::Shared"]);
            Assert.AreEqual("Source A", result.SourceDisplayNames[sourceA]);
            Assert.AreEqual("Same title", result.SourceDisplayNames[sourceB]);
            Assert.AreEqual("Same title", result.SourceDisplayNames[sourceC]);
            Assert.AreEqual("Source D", result.SourceDisplayNames[sourceD]);
            Assert.IsFalse(result.SourceDisplayNames.ContainsKey(merged.Root));
            Assert.AreEqual(11L, result.SourceStartMs[sourceB]);
            Assert.AreEqual(22L, result.SourceEndMs[sourceC]);
            Assert.AreEqual(string.Join(" ; ", result.SourceOrder), result.Root);

            TestData afterRemove = new RemoveLoadedSourceUseCase().Execute(result, sourceA);

            CollectionAssert.AreEquivalent(
                new[] { sourceB, sourceC, sourceD },
                afterRemove.SourceColumns.Keys.ToArray());
            CollectionAssert.AreEqual(new[] { sourceB, sourceC, sourceD }, afterRemove.SourceOrder);
            Assert.IsFalse(afterRemove.SourceDisplayNames.ContainsKey(sourceA));
            Assert.IsTrue(afterRemove.SourceDisplayNames.ContainsKey(sourceB));
            Assert.IsTrue(afterRemove.SourceDisplayNames.ContainsKey(sourceC));
            Assert.IsTrue(afterRemove.SourceDisplayNames.ContainsKey(sourceD));
            Assert.IsFalse(afterRemove.ColumnNames.Contains("combined::Shared"));
            CollectionAssert.AreEquivalent(
                new[] { "B", "C", "sourceD::Shared" },
                afterRemove.ColumnNames);
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
        public void Execute_DropsSourceSpecificMetadataKeys()
        {
            TestData first = CreateData("jsqdb://recording/first", "A", 10L);
            first.Meta[WorkspaceMetadataKeys.EquipmentModel] = "DINAMIC";
            first.Meta[WorkspaceMetadataKeys.ExperimentType] = "FUNC";
            first.Meta[WorkspaceMetadataKeys.ClimateMode] = "32/65";
            TestData second = CreateData("C:\\second", "B", 20L);

            TestData result = new MergeLoadedSourcesUseCase().Execute(
                new[] { first, second },
                false);

            Assert.IsFalse(result.Meta.ContainsKey(WorkspaceMetadataKeys.EquipmentModel));
            Assert.IsFalse(result.Meta.ContainsKey(WorkspaceMetadataKeys.ExperimentType));
            Assert.IsFalse(result.Meta.ContainsKey(WorkspaceMetadataKeys.ClimateMode));
        }

        [TestMethod]
        public void Execute_ThenRemoveFirstSource_TitleHasNoMetadataTailFromRemovedSource()
        {
            TestData first = CreateData("jsqdb://recording/first", "A", 10L);
            first.SourceDisplayNames[first.Root] = "Post A 2026-08-06 10-00-00";
            first.Meta[WorkspaceMetadataKeys.EquipmentModel] = "DINAMIC";
            first.Meta[WorkspaceMetadataKeys.ExperimentType] = "FUNC";
            first.Meta[WorkspaceMetadataKeys.ClimateMode] = "32/65";
            TestData second = CreateData("jsqdb://recording/second", "B", 20L);
            second.SourceDisplayNames[second.Root] = "Post B 2026-08-06 14-33-12";

            TestData merged = new MergeLoadedSourcesUseCase().Execute(
                new[] { first, second },
                false);
            TestData afterRemove = new RemoveLoadedSourceUseCase().Execute(merged, first.Root);

            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver())
                .Build(afterRemove, "запасной");

            Assert.AreEqual("Post B 2026-08-06 14-33-12", title);
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
