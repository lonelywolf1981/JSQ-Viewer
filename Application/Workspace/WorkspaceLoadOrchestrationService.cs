using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JSQViewer.Application.Abstractions;

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
