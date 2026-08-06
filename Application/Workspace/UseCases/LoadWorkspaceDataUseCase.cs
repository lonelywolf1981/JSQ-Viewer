using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Database;
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
        private readonly ITestDataSourceReader _exportedProtocolDataSourceReader;
        private readonly IRecordingDataReader _recordingDataReader;

        public LoadWorkspaceDataUseCase(
            WorkspaceFolderSpecParser folderSpecParser,
            ITestRootLocator testRootLocator,
            ITestMetadataReader testMetadataReader,
            ICanaliDefinitionReader canaliDefinitionReader,
            ITestDataSourceReader testDataSourceReader,
            MergeLoadedSourcesUseCase mergeLoadedSourcesUseCase,
            ITestDataSourceReader exportedProtocolDataSourceReader = null,
            IRecordingDataReader recordingDataReader = null)
        {
            _folderSpecParser = folderSpecParser ?? throw new ArgumentNullException(nameof(folderSpecParser));
            _testRootLocator = testRootLocator ?? throw new ArgumentNullException(nameof(testRootLocator));
            _testMetadataReader = testMetadataReader ?? throw new ArgumentNullException(nameof(testMetadataReader));
            _canaliDefinitionReader = canaliDefinitionReader ?? throw new ArgumentNullException(nameof(canaliDefinitionReader));
            _testDataSourceReader = testDataSourceReader ?? throw new ArgumentNullException(nameof(testDataSourceReader));
            _mergeLoadedSourcesUseCase = mergeLoadedSourcesUseCase ?? throw new ArgumentNullException(nameof(mergeLoadedSourcesUseCase));
            _exportedProtocolDataSourceReader = exportedProtocolDataSourceReader;
            _recordingDataReader = recordingDataReader;
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

            List<string> resolvedRoots = folders
                .Select(ResolveSourceRoot)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var loadedSources = new List<TestData>(resolvedRoots.Count);
            for (int i = 0; i < resolvedRoots.Count; i++)
            {
                string root = resolvedRoots[i];
                string recordingId;
                if (RecordingSourceRef.TryParse(root, out recordingId))
                {
                    if (_recordingDataReader == null)
                    {
                        throw new InvalidOperationException("Recording data reader is not configured.");
                    }

                    loadedSources.Add(_recordingDataReader.ReadRecording(recordingId));
                    continue;
                }

                if (IsExportedProtocolPath(root))
                {
                    if (_exportedProtocolDataSourceReader == null)
                    {
                        throw new InvalidOperationException("Exported protocol reader is not configured.");
                    }

                    loadedSources.Add(_exportedProtocolDataSourceReader.Read(
                        root,
                        new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase),
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
                    continue;
                }

                Dictionary<string, string> metadata = _testMetadataReader.Read(root);
                Dictionary<string, ChannelInfo> channels = _canaliDefinitionReader.Read(root);
                loadedSources.Add(_testDataSourceReader.Read(root, channels, metadata));
            }

            TestData merged = loadedSources.Count == 1
                ? loadedSources[0]
                : _mergeLoadedSourcesUseCase.Execute(loadedSources, request.SplitOverlappingCodes);

            return new WorkspaceLoadResult(_folderSpecParser.Join(resolvedRoots), resolvedRoots, merged);
        }

        private string ResolveSourceRoot(string source)
        {
            if (RecordingSourceRef.IsRecordingSource(source))
            {
                return source.Trim().Trim('"');
            }

            if (IsExportedProtocolPath(source))
            {
                return System.IO.Path.GetFullPath(source);
            }

            return _testRootLocator.FindRoot(source);
        }

        private static bool IsExportedProtocolPath(string source)
        {
            return !string.IsNullOrWhiteSpace(source)
                && string.Equals(System.IO.Path.GetExtension(source), ".xlsx", StringComparison.OrdinalIgnoreCase);
        }
    }
}
