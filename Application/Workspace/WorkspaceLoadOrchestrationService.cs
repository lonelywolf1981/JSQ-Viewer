using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;

namespace JSQViewer.Application.Workspace
{
    public sealed class WorkspaceLoadOrchestrationService
    {
        private readonly WorkspaceFolderSpecParser _parser;
        private readonly IFileSystem _fileSystem;

        public WorkspaceLoadOrchestrationService(WorkspaceFolderSpecParser parser, IFileSystem fileSystem)
        {
            if (parser == null) throw new ArgumentNullException(nameof(parser));
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));

            _parser = parser;
            _fileSystem = fileSystem;
        }

        public IReadOnlyList<string> ParseSpec(string spec)
        {
            return _parser.Parse(spec);
        }

        public string JoinSpec(IEnumerable<string> folders)
        {
            return _parser.Join(folders);
        }

        public bool IsValidSpec(string spec)
        {
            IReadOnlyList<string> folders = _parser.Parse(spec);
            if (folders.Count == 0 || folders.Count > WorkspaceFolderSpecParser.MaxFolderCount)
            {
                return false;
            }

            for (int i = 0; i < folders.Count; i++)
            {
                if (RecordingSourceRef.IsRecordingSource(folders[i]))
                {
                    continue;
                }

                if (!_fileSystem.DirectoryExists(folders[i])
                    && !(IsExportedProtocolPath(folders[i]) && _fileSystem.FileExists(folders[i])))
                {
                    return false;
                }
            }

            return true;
        }

        public string ResolveSelectedFolderSource(string selectedFolder)
        {
            if (RecordingSourceRef.IsRecordingSource(selectedFolder))
            {
                return selectedFolder.Trim().Trim('"');
            }

            if (string.IsNullOrWhiteSpace(selectedFolder) || !_fileSystem.DirectoryExists(selectedFolder))
            {
                return selectedFolder;
            }

            if (_fileSystem.GetFiles(selectedFolder, "*.dat", SearchOption.TopDirectoryOnly).Length > 0)
            {
                return selectedFolder;
            }

            string latestProtocol = _fileSystem.GetFiles(selectedFolder, "*.xlsx", SearchOption.TopDirectoryOnly)
                .OrderByDescending(_fileSystem.GetLastWriteTime)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(latestProtocol) ? selectedFolder : latestProtocol;
        }

        public WorkspaceLoadRequest CreateLoadRequest(string normalizedSpec)
        {
            return new WorkspaceLoadRequest(normalizedSpec, true);
        }

        public WorkspaceSourceAdditionResult AddSources(string currentSpec, IEnumerable<string> selectedSources)
        {
            IReadOnlyList<string> current = _parser.Parse(currentSpec);
            var combined = new List<string>(current);
            var known = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);

            int addedCount = 0;
            if (selectedSources != null)
            {
                foreach (string selected in selectedSources)
                {
                    if (string.IsNullOrWhiteSpace(selected))
                    {
                        continue;
                    }

                    string source = selected.Trim().Trim('"');
                    if (source.Length == 0 || !known.Add(source))
                    {
                        continue;
                    }

                    combined.Add(source);
                    addedCount++;
                }
            }

            if (combined.Count > WorkspaceFolderSpecParser.MaxFolderCount)
            {
                return new WorkspaceSourceAdditionResult(
                    WorkspaceSourceAdditionStatus.LimitExceeded,
                    current,
                    _parser.Join(current));
            }

            if (addedCount == 0)
            {
                return new WorkspaceSourceAdditionResult(
                    WorkspaceSourceAdditionStatus.NoNewSources,
                    current,
                    _parser.Join(current));
            }

            return new WorkspaceSourceAdditionResult(
                WorkspaceSourceAdditionStatus.Success,
                combined,
                _parser.Join(combined));
        }

        public bool TryGetSingleRecordingId(string spec, out string recordingId)
        {
            recordingId = null;

            IReadOnlyList<string> sources = _parser.Parse(spec);
            if (sources.Count != 1)
            {
                return false;
            }

            string parsedId;
            if (!RecordingSourceRef.TryParse(sources[0], out parsedId)
                || !string.Equals(sources[0], RecordingSourceRef.Build(parsedId), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            recordingId = parsedId;
            return true;
        }

        public string BuildWorkspaceKey(IEnumerable<string> folders)
        {
            return _parser.BuildWorkspaceKey(folders);
        }

        private static bool IsExportedProtocolPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && string.Equals(System.IO.Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase);
        }
    }
}
