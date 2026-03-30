namespace JSQViewer.Application.Workspace
{
    public sealed class WorkspaceLoadRequest
    {
        public WorkspaceLoadRequest(string folderSpec, bool splitOverlappingCodes = true)
        {
            FolderSpec = folderSpec ?? string.Empty;
            SplitOverlappingCodes = splitOverlappingCodes;
        }

        public string FolderSpec { get; private set; }

        public bool SplitOverlappingCodes { get; private set; }
    }
}
