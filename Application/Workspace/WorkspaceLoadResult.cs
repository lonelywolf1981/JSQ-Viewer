using System.Collections.Generic;
using JSQViewer.Core;

namespace JSQViewer.Application.Workspace
{
    public sealed class WorkspaceLoadResult
    {
        public WorkspaceLoadResult(string normalizedFolderSpec, IReadOnlyList<string> folders, TestData data)
        {
            NormalizedFolderSpec = normalizedFolderSpec ?? string.Empty;
            Folders = folders ?? new string[0];
            Data = data ?? new TestData();
        }

        public string NormalizedFolderSpec { get; private set; }

        public IReadOnlyList<string> Folders { get; private set; }

        public TestData Data { get; private set; }
    }
}
