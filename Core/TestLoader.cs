using System.Collections.Generic;
using JSQViewer.Application.Workspace;
using JSQViewer.Application.Workspace.UseCases;
using JSQViewer.Infrastructure.Composition;

namespace JSQViewer.Core
{
    public static class TestLoader
    {
        public static TestData LoadTest(string folder)
        {
            return BuildLoadWorkspaceUseCase().Execute(new WorkspaceLoadRequest(folder, false)).Data;
        }

        public static List<TestData> LoadTests(IList<string> folders)
        {
            if (folders == null || folders.Count == 0)
            {
                throw new System.ArgumentException("No folders provided for loading.", nameof(folders));
            }

            var list = new List<TestData>(folders.Count);
            for (int i = 0; i < folders.Count; i++)
            {
                list.Add(LoadTest(folders[i]));
            }

            return list;
        }

        public static List<string> FindOverlappingCodes(IList<TestData> list)
        {
            return new AnalyzeOverlapConflictsUseCase().Execute(list);
        }

        public static TestData MergeLoadedTests(IList<TestData> list, bool splitOverlappingCodes)
        {
            return new MergeLoadedSourcesUseCase().Execute(list, splitOverlappingCodes);
        }

        public static TestData LoadAndMergeTests(IList<string> folders)
        {
            List<TestData> list = LoadTests(folders);
            return MergeLoadedTests(list, false);
        }

        public static string FindTestRoot(string folder)
        {
            return WorkspaceLoadingComposition.CreateTestRootLocator().FindRoot(folder);
        }

        private static LoadWorkspaceDataUseCase BuildLoadWorkspaceUseCase()
        {
            return WorkspaceLoadingComposition.CreateLoadWorkspaceDataUseCase(WorkspaceLoadingComposition.CreateFolderSpecParser());
        }
    }
}
