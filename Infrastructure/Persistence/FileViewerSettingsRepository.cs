using System;
using System.IO;
using JSQViewer.Application.Abstractions;
using JSQViewer.Settings;

namespace JSQViewer.Infrastructure.Persistence
{
    public sealed class FileViewerSettingsRepository : IViewerSettingsRepository
    {
        private readonly string _filePath;

        public FileViewerSettingsRepository(IAppPaths appPaths)
        {
            if (appPaths == null) throw new ArgumentNullException(nameof(appPaths));

            _filePath = Path.Combine(appPaths.ProjectRoot, "viewer_settings.json");
        }

        public ViewerSettingsModel Load()
        {
            return ViewerSettingsStore.Load(_filePath);
        }

        public bool Save(ViewerSettingsModel settings)
        {
            return ViewerSettingsStore.Save(_filePath, settings);
        }
    }
}
