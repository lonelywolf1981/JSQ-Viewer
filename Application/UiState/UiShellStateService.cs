using System;
using System.Collections.Generic;
using JSQViewer.Application.Abstractions;

namespace JSQViewer.Application.UiState
{
    public sealed class UiShellStateService
    {
        private readonly IRecentFoldersRepository _recentFoldersRepository;
        private readonly IUiStateRepository _uiStateRepository;

        public UiShellStateService(
            IRecentFoldersRepository recentFoldersRepository,
            IUiStateRepository uiStateRepository)
        {
            if (recentFoldersRepository == null) throw new ArgumentNullException(nameof(recentFoldersRepository));
            if (uiStateRepository == null) throw new ArgumentNullException(nameof(uiStateRepository));

            _recentFoldersRepository = recentFoldersRepository;
            _uiStateRepository = uiStateRepository;
        }

        public List<string> LoadRecentFolders()
        {
            List<string> raw = _recentFoldersRepository.Load() ?? new List<string>();
            var result = new List<string>();
            for (int i = 0; i < raw.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(raw[i]))
                    result.Add(raw[i]);
            }
            return result;
        }

        public List<string> AddRecentFolder(IList<string> currentList, string folder)
        {
            string path = (folder ?? string.Empty).Trim();
            if (path.Length == 0)
                return currentList == null ? new List<string>() : new List<string>(currentList);

            var folders = new List<string>();
            folders.Add(path);

            if (currentList != null)
            {
                for (int i = 0; i < currentList.Count; i++)
                {
                    string existing = currentList[i];
                    if (!string.Equals(existing, path, StringComparison.OrdinalIgnoreCase))
                        folders.Add(existing);
                }
            }

            while (folders.Count > 12)
                folders.RemoveAt(folders.Count - 1);

            _recentFoldersRepository.Save(folders);
            return folders;
        }

        public UiStateModel LoadUiState()
        {
            return _uiStateRepository.Load();
        }

        public bool SaveUiState(UiStateModel state)
        {
            return _uiStateRepository.Save(state);
        }
    }
}
