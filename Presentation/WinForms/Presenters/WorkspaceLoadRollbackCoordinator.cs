using System;
using JSQViewer.Core;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    public sealed class WorkspaceLoadRollbackCoordinator
    {
        public void RestoreAfterFailure(
            bool loadSucceeded,
            bool isCurrentGeneration,
            TestData previousData,
            string previousFolder,
            TestData currentData,
            string currentFolder,
            Action restoreSourceText,
            Action restoreLiveRefresh)
        {
            if (loadSucceeded || !isCurrentGeneration)
            {
                return;
            }

            if (!ReferenceEquals(previousData, currentData)
                || !string.Equals(previousFolder, currentFolder, StringComparison.Ordinal))
            {
                return;
            }

            if (restoreSourceText != null)
            {
                restoreSourceText();
            }

            if (restoreLiveRefresh != null)
            {
                restoreLiveRefresh();
            }
        }
    }
}
