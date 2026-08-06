using System.Collections.Generic;

namespace JSQViewer.Application.Workspace
{
    public enum WorkspaceSourceAdditionStatus
    {
        Success,
        NoNewSources,
        LimitExceeded
    }

    public sealed class WorkspaceSourceAdditionResult
    {
        public WorkspaceSourceAdditionResult(
            WorkspaceSourceAdditionStatus status,
            IReadOnlyList<string> sources,
            string folderSpec)
        {
            Status = status;
            Sources = sources ?? new string[0];
            FolderSpec = folderSpec ?? string.Empty;
        }

        public WorkspaceSourceAdditionStatus Status { get; private set; }

        public IReadOnlyList<string> Sources { get; private set; }

        public string FolderSpec { get; private set; }
    }
}
