using System;
using System.Collections.Generic;
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
                if (!_fileSystem.DirectoryExists(folders[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public WorkspaceLoadRequest CreateLoadRequest(string normalizedSpec)
        {
            return new WorkspaceLoadRequest(normalizedSpec, true);
        }

        public string BuildWorkspaceKey(IEnumerable<string> folders)
        {
            return _parser.BuildWorkspaceKey(folders);
        }
    }
}
