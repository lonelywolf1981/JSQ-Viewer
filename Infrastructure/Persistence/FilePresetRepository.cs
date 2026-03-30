using System;
using System.Collections.Generic;
using JSQViewer.Application.Abstractions;
using JSQViewer.Settings;

namespace JSQViewer.Infrastructure.Persistence
{
    public sealed class FilePresetRepository : IPresetRepository
    {
        private readonly string _baseDirectory;

        public FilePresetRepository(IAppPaths appPaths)
        {
            if (appPaths == null) throw new ArgumentNullException(nameof(appPaths));

            _baseDirectory = appPaths.ProjectRoot;
        }

        public List<ViewerPreset> List()
        {
            return PresetStore.List(_baseDirectory);
        }

        public ViewerPreset Load(string keyOrName)
        {
            return PresetStore.Load(_baseDirectory, keyOrName);
        }

        public bool Exists(string keyOrName)
        {
            return PresetStore.Exists(_baseDirectory, keyOrName);
        }

        public ViewerPreset Save(ViewerPreset preset)
        {
            return PresetStore.Save(_baseDirectory, preset);
        }

        public bool Delete(string keyOrName)
        {
            return PresetStore.Delete(_baseDirectory, keyOrName);
        }
    }
}
