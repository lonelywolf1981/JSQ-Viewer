using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JSQViewer.Application.Abstractions;
using JSQViewer.Settings;

namespace JSQViewer.Infrastructure.Persistence
{
    public sealed class FileRecentFoldersRepository : IRecentFoldersRepository
    {
        private readonly string _filePath;

        public FileRecentFoldersRepository(IAppPaths appPaths)
        {
            if (appPaths == null) throw new ArgumentNullException(nameof(appPaths));

            _filePath = Path.Combine(appPaths.ProjectRoot, "recent_folders.json");
        }

        public List<string> Load()
        {
            RecentFoldersPayload payload = JsonHelper.LoadFromFile(_filePath, new RecentFoldersPayload());
            return payload == null || payload.folders == null
                ? new List<string>()
                : payload.folders;
        }

        public bool Save(IList<string> folders)
        {
            var payload = new RecentFoldersPayload
            {
                folders = folders == null ? new List<string>() : folders.ToList()
            };

            return JsonHelper.SaveToFile(_filePath, payload);
        }

        private sealed class RecentFoldersPayload
        {
            public List<string> folders { get; set; }
        }
    }
}
