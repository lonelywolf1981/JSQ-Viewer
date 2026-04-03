using System;
using System.Collections.Generic;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;

namespace JSQViewer.Application.Workspace.UseCases
{
    public sealed class LoadWorkspaceDataUseCase
    {
        private readonly WorkspaceFolderSpecParser _folderSpecParser;
        private readonly ITestRootLocator _testRootLocator;
        private readonly ITestMetadataReader _testMetadataReader;
        private readonly ICanaliDefinitionReader _canaliDefinitionReader;
        private readonly ITestDataSourceReader _testDataSourceReader;
        private readonly MergeLoadedSourcesUseCase _mergeLoadedSourcesUseCase;

        public LoadWorkspaceDataUseCase(
            WorkspaceFolderSpecParser folderSpecParser,
            ITestRootLocator testRootLocator,
            ITestMetadataReader testMetadataReader,
            ICanaliDefinitionReader canaliDefinitionReader,
            ITestDataSourceReader testDataSourceReader,
            MergeLoadedSourcesUseCase mergeLoadedSourcesUseCase)
        {
            _folderSpecParser = folderSpecParser ?? throw new ArgumentNullException(nameof(folderSpecParser));
            _testRootLocator = testRootLocator ?? throw new ArgumentNullException(nameof(testRootLocator));
            _testMetadataReader = testMetadataReader ?? throw new ArgumentNullException(nameof(testMetadataReader));
            _canaliDefinitionReader = canaliDefinitionReader ?? throw new ArgumentNullException(nameof(canaliDefinitionReader));
            _testDataSourceReader = testDataSourceReader ?? throw new ArgumentNullException(nameof(testDataSourceReader));
            _mergeLoadedSourcesUseCase = mergeLoadedSourcesUseCase ?? throw new ArgumentNullException(nameof(mergeLoadedSourcesUseCase));
        }

        public WorkspaceLoadResult Execute(WorkspaceLoadRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            IReadOnlyList<string> folders = _folderSpecParser.Parse(request.FolderSpec);
            if (folders.Count == 0)
            {
                throw new ArgumentException("No folders provided for loading.", nameof(request));
            }

            if (folders.Count > WorkspaceFolderSpecParser.MaxFolderCount)
            {
                throw new ArgumentException(
                    "No more than " + WorkspaceFolderSpecParser.MaxFolderCount + " folders can be loaded at once.",
                    nameof(request));
            }

            var loadedSources = new List<TestData>(folders.Count);
            for (int i = 0; i < folders.Count; i++)
            {
                string root = _testRootLocator.FindRoot(folders[i]);
                Dictionary<string, string> metadata = _testMetadataReader.Read(root);
                Dictionary<string, ChannelInfo> channels = _canaliDefinitionReader.Read(root);
                loadedSources.Add(_testDataSourceReader.Read(root, channels, metadata));
            }

            TestData merged = loadedSources.Count == 1
                ? loadedSources[0]
                : _mergeLoadedSourcesUseCase.Execute(loadedSources, request.SplitOverlappingCodes);

            return new WorkspaceLoadResult(_folderSpecParser.Join(folders), folders, merged);
        }
    }
}
