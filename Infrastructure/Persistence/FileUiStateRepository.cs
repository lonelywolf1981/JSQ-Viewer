using System;
using System.IO;
using JSQViewer.Application.Abstractions;
using JSQViewer.Settings;

namespace JSQViewer.Infrastructure.Persistence
{
    public sealed class FileUiStateRepository : IUiStateRepository
    {
        private readonly string _filePath;

        public FileUiStateRepository(IAppPaths appPaths)
        {
            if (appPaths == null) throw new ArgumentNullException(nameof(appPaths));

            _filePath = Path.Combine(appPaths.ProjectRoot, "ui_state.json");
        }

        public UiStateModel Load()
        {
            return JsonHelper.LoadFromFile(_filePath, new UiStateModel());
        }

        public bool Save(UiStateModel state)
        {
            return JsonHelper.SaveToFile(_filePath, state ?? new UiStateModel());
        }
    }
}
