using System;
using System.Collections.Generic;
using JSQViewer.Application.Workspace;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    public sealed class DatabaseRecordingSelectionPresenter
    {
        private readonly WorkspaceLoadOrchestrationService _service;

        public DatabaseRecordingSelectionPresenter(WorkspaceLoadOrchestrationService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            _service = service;
        }

        public void ApplySelection(
            string currentSpec,
            IEnumerable<string> selectedSources,
            Action<string> loadFolder,
            Action<string> notifyByLocalizationKey)
        {
            if (loadFolder == null) throw new ArgumentNullException(nameof(loadFolder));
            if (notifyByLocalizationKey == null) throw new ArgumentNullException(nameof(notifyByLocalizationKey));

            WorkspaceSourceAdditionResult result = _service.AddSources(currentSpec, selectedSources);
            if (result.Status == WorkspaceSourceAdditionStatus.NoNewSources)
            {
                notifyByLocalizationKey("SourceAlreadyAdded");
                return;
            }

            if (result.Status == WorkspaceSourceAdditionStatus.LimitExceeded)
            {
                notifyByLocalizationKey("TooManyFolders");
                return;
            }

            loadFolder(result.FolderSpec);
        }
    }
}
