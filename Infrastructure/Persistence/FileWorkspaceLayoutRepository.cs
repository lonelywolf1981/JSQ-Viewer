using System;
using System.IO;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Channels;
using JSQViewer.Settings;

namespace JSQViewer.Infrastructure.Persistence
{
    public sealed class FileWorkspaceLayoutRepository : IWorkspaceLayoutRepository
    {
        private readonly string _layoutsDirectory;

        public FileWorkspaceLayoutRepository(IAppPaths appPaths)
        {
            if (appPaths == null) throw new ArgumentNullException(nameof(appPaths));

            _layoutsDirectory = Path.Combine(appPaths.ProjectRoot, "workspace_layouts");
        }

        public WorkspaceLayoutState Load(string workspaceKey)
        {
            WorkspaceLayoutState state = JsonHelper.LoadFromFile(GetFilePath(workspaceKey), new WorkspaceLayoutState()) ?? new WorkspaceLayoutState();
            state.EnsureInitialized();
            return state;
        }

        public bool Save(string workspaceKey, WorkspaceLayoutState state)
        {
            WorkspaceLayoutState payload = state ?? new WorkspaceLayoutState();
            payload.EnsureInitialized();
            return JsonHelper.SaveToFile(GetFilePath(workspaceKey), payload);
        }

        private string GetFilePath(string workspaceKey)
        {
            string key = string.IsNullOrWhiteSpace(workspaceKey) ? "default" : workspaceKey.Trim();
            return Path.Combine(_layoutsDirectory, key + ".json");
        }
    }
}
