using JSQViewer.Application.Channels;

namespace JSQViewer.Application.Abstractions
{
    public interface IWorkspaceLayoutRepository
    {
        WorkspaceLayoutState Load(string workspaceKey);

        bool Save(string workspaceKey, WorkspaceLayoutState state);
    }
}
