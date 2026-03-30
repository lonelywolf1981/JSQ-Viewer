using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Workspace;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Application.Workspace.UseCases;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class WorkspaceFolderSpecParserTests
    {
        [TestMethod]
        public void Parse_DeduplicatesAndTrimsSeparators()
        {
            var parser = new WorkspaceFolderSpecParser();

            IReadOnlyList<string> folders = parser.Parse("  C:\\A  ;\r\n\"C:\\B\"\nC:\\a  ");

            CollectionAssert.AreEqual(new[] { "C:\\A", "C:\\B" }, folders.ToArray());
        }

        [TestMethod]
        public void Join_UsesUiSeparator()
        {
            var parser = new WorkspaceFolderSpecParser();

            string spec = parser.Join(new[] { "C:\\A", "C:\\B" });

            Assert.AreEqual("C:\\A ; C:\\B", spec);
        }
    }

    [TestClass]
    public class LoadWorkspaceDataUseCaseTests
    {
        [TestMethod]
        public void Execute_LoadsSingleFolderWithoutMerge()
        {
            var rootLocator = new FakeRootLocator("C:\\src", "C:\\root");
            var metadataReader = new FakeMetadataReader();
            var canaliReader = new FakeCanaliReader();
            var dataSourceReader = new FakeDataSourceReader(new TestData { Root = "C:\\root", RowCount = 3, ColumnNames = new[] { "A-01" } });
            var mergeUseCase = new MergeLoadedSourcesUseCase();
            var parser = new WorkspaceFolderSpecParser();
            var useCase = new LoadWorkspaceDataUseCase(parser, rootLocator, metadataReader, canaliReader, dataSourceReader, mergeUseCase);

            WorkspaceLoadResult result = useCase.Execute(new WorkspaceLoadRequest("C:\\src"));

            Assert.AreEqual("C:\\src", result.NormalizedFolderSpec);
            Assert.AreEqual(3, result.Data.RowCount);
            Assert.AreEqual("C:\\root", result.Data.Root);
            Assert.AreEqual(1, dataSourceReader.ReadRoots.Count);
            Assert.AreEqual("C:\\root", dataSourceReader.ReadRoots[0]);
        }

        [TestMethod]
        public void Execute_MergesMultipleFoldersWithOverlapSplit()
        {
            var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["C:\\srcA"] = "C:\\rootA",
                ["C:\\srcB"] = "C:\\rootB"
            };
            var dataByRoot = new Dictionary<string, TestData>(StringComparer.OrdinalIgnoreCase)
            {
                ["C:\\rootA"] = CreateData("C:\\rootA", new[] { "A-01" }, 10L),
                ["C:\\rootB"] = CreateData("C:\\rootB", new[] { "A-01" }, 20L)
            };
            var useCase = new LoadWorkspaceDataUseCase(
                new WorkspaceFolderSpecParser(),
                new FakeRootLocator(roots),
                new FakeMetadataReader(),
                new FakeCanaliReader(),
                new FakeDataSourceReader(dataByRoot),
                new MergeLoadedSourcesUseCase());

            WorkspaceLoadResult result = useCase.Execute(new WorkspaceLoadRequest("C:\\srcA ; C:\\srcB"));

            CollectionAssert.AreEquivalent(new[] { "rootA::A-01", "rootB::A-01" }, result.Data.ColumnNames);
            Assert.AreEqual(2, result.Data.RowCount);
        }

        private static TestData CreateData(string root, string[] columns, long timestamp)
        {
            var data = new TestData
            {
                Root = root,
                RowCount = 1,
                TimestampsMs = new[] { timestamp },
                ColumnNames = columns,
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = columns
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

            foreach (string column in columns)
            {
                data.Columns[column] = new double?[] { 1d };
                data.Channels[column] = new ChannelInfo { Code = column, Name = column, Unit = "u" };
                data.CodeSources[column] = root;
            }

            return data;
        }

        private sealed class FakeRootLocator : ITestRootLocator
        {
            private readonly Dictionary<string, string> _roots;

            public FakeRootLocator(string source, string root)
            {
                _roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [source] = root
                };
            }

            public FakeRootLocator(Dictionary<string, string> roots)
            {
                _roots = roots;
            }

            public string FindRoot(string folder)
            {
                return _roots[folder];
            }
        }

        private sealed class FakeMetadataReader : ITestMetadataReader
        {
            public Dictionary<string, string> Read(string root)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["root"] = root
                };
            }
        }

        private sealed class FakeCanaliReader : ICanaliDefinitionReader
        {
            public Dictionary<string, ChannelInfo> Read(string root)
            {
                return new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private sealed class FakeDataSourceReader : ITestDataSourceReader
        {
            private readonly Dictionary<string, TestData> _dataByRoot;

            public FakeDataSourceReader(TestData data)
                : this(new Dictionary<string, TestData>(StringComparer.OrdinalIgnoreCase)
                {
                    [data.Root] = data
                })
            {
            }

            public FakeDataSourceReader(Dictionary<string, TestData> dataByRoot)
            {
                _dataByRoot = dataByRoot;
                ReadRoots = new List<string>();
            }

            public List<string> ReadRoots { get; private set; }

            public TestData Read(string root, Dictionary<string, ChannelInfo> channels, Dictionary<string, string> metadata)
            {
                ReadRoots.Add(root);
                return _dataByRoot[root];
            }
        }
    }
}
