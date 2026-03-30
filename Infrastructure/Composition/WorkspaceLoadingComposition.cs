using JSQViewer.Application.Workspace;
using JSQViewer.Application.Workspace.UseCases;
using JSQViewer.Infrastructure.DataImport;
using JSQViewer.Application.Workspace.Ports;

namespace JSQViewer.Infrastructure.Composition
{
    public static class WorkspaceLoadingComposition
    {
        public static WorkspaceFolderSpecParser CreateFolderSpecParser()
        {
            return new WorkspaceFolderSpecParser();
        }

        public static LoadWorkspaceDataUseCase CreateLoadWorkspaceDataUseCase(WorkspaceFolderSpecParser folderSpecParser)
        {
            return new LoadWorkspaceDataUseCase(
                folderSpecParser ?? CreateFolderSpecParser(),
                CreateTestRootLocator(),
                new ProvaMetadataReader(),
                new CanaliDefinitionReader(),
                new DbfTestDataSourceReader(),
                new MergeLoadedSourcesUseCase());
        }

        public static ITestRootLocator CreateTestRootLocator()
        {
            return new FileSystemTestRootLocator();
        }

        public static RefreshWorkspaceDataUseCase CreateRefreshWorkspaceDataUseCase(LoadWorkspaceDataUseCase loadWorkspaceDataUseCase)
        {
            return new RefreshWorkspaceDataUseCase(loadWorkspaceDataUseCase);
        }
    }
}
