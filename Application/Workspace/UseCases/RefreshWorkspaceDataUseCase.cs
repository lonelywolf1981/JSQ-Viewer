using System;

namespace JSQViewer.Application.Workspace.UseCases
{
    public sealed class RefreshWorkspaceDataUseCase
    {
        private readonly LoadWorkspaceDataUseCase _loadWorkspaceDataUseCase;

        public RefreshWorkspaceDataUseCase(LoadWorkspaceDataUseCase loadWorkspaceDataUseCase)
        {
            _loadWorkspaceDataUseCase = loadWorkspaceDataUseCase ?? throw new ArgumentNullException(nameof(loadWorkspaceDataUseCase));
        }

        public WorkspaceLoadResult Execute(WorkspaceLoadRequest request)
        {
            return _loadWorkspaceDataUseCase.Execute(request);
        }
    }
}
