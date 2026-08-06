using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Channels;
using JSQViewer.Presentation.WinForms.ViewModels;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    public sealed class SourceWindowCoordinator
    {
        private readonly Dictionary<string, SourceWindowState> _states = new Dictionary<string, SourceWindowState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _roots = new List<string>();
        private string _sharedFilterText = string.Empty;
        private bool _sharedSelectedOnly;

        public string SharedFilterText
        {
            get { return _sharedFilterText; }
        }

        public bool SharedSelectedOnly
        {
            get { return _sharedSelectedOnly; }
        }

        public void Initialize(string filterText, bool selectedOnly)
        {
            _sharedFilterText = filterText ?? string.Empty;
            _sharedSelectedOnly = selectedOnly;
        }

        public bool BindRoots(
            IReadOnlyList<string> roots,
            IReadOnlyDictionary<string, string> titles,
            string defaultSortMode,
            bool preserveExistingLayout)
        {
            string[] incoming = roots == null ? new string[0] : roots.ToArray();
            bool canRefreshInPlace = preserveExistingLayout && HaveSameRoots(incoming);
            string normalizedDefaultSortMode = NormalizeSortMode(defaultSortMode);

            if (!canRefreshInPlace)
            {
                _states.Clear();
            }

            _roots.Clear();
            _roots.AddRange(incoming);
            _titles.Clear();

            for (int i = 0; i < _roots.Count; i++)
            {
                string root = _roots[i];
                string title;
                if (titles == null
                    || !titles.TryGetValue(root, out title)
                    || string.IsNullOrWhiteSpace(title))
                {
                    title = root;
                }

                _titles[root] = title;

                SourceWindowState state;
                if (!_states.TryGetValue(root, out state))
                {
                    state = new SourceWindowState();
                    _states[root] = state;
                }

                if (!canRefreshInPlace)
                {
                    state.SortMode = "User";
                }
                else if (string.IsNullOrWhiteSpace(state.SortMode))
                {
                    state.SortMode = normalizedDefaultSortMode;
                }
            }

            var staleKeys = _states.Keys.Where(key => !_roots.Contains(key, StringComparer.OrdinalIgnoreCase)).ToList();
            for (int i = 0; i < staleKeys.Count; i++)
            {
                _states.Remove(staleKeys[i]);
            }

            return canRefreshInPlace;
        }

        public void UpdateFromMain(string filterText, bool selectedOnly)
        {
            _sharedFilterText = filterText ?? string.Empty;
            _sharedSelectedOnly = selectedOnly;
        }

        public void UpdateFromSource(string sourceRoot, string filterText, string sortMode, bool selectedOnly)
        {
            _sharedFilterText = filterText ?? string.Empty;
            _sharedSelectedOnly = selectedOnly;

            SourceWindowState state;
            if (_states.TryGetValue(sourceRoot ?? string.Empty, out state))
            {
                state.SortMode = NormalizeSortMode(sortMode);
            }
        }

        public void SetAllSortModes(string sortMode)
        {
            string normalized = NormalizeSortMode(sortMode);
            foreach (string root in _roots)
            {
                SourceWindowState state;
                if (_states.TryGetValue(root, out state))
                {
                    state.SortMode = normalized;
                }
            }
        }

        public string GetSortMode(string sourceRoot)
        {
            SourceWindowState state;
            if (_states.TryGetValue(sourceRoot ?? string.Empty, out state))
            {
                return NormalizeSortMode(state.SortMode);
            }

            return "User";
        }

        public IReadOnlyList<string> GetRoots()
        {
            return _roots.ToArray();
        }

        public SourceChannelWindowViewModel BuildWindow(ChannelWorkspaceModel workspace, string sourceRoot)
        {
            string root = sourceRoot ?? string.Empty;
            string sortMode = GetSortMode(root);
            IReadOnlyList<ChannelListProjectionItem> items = workspace.BuildSourceList(root, _sharedFilterText, sortMode, _sharedSelectedOnly);
            return new SourceChannelWindowViewModel(
                root,
                GetTitle(root),
                _sharedFilterText,
                sortMode,
                _sharedSelectedOnly,
                items.Select(MapItem).ToArray());
        }

        public IReadOnlyList<SourceChannelWindowViewModel> BuildWindows(ChannelWorkspaceModel workspace)
        {
            return _roots.Select(root => BuildWindow(workspace, root)).ToArray();
        }

        private bool HaveSameRoots(IReadOnlyList<string> incoming)
        {
            if (_roots.Count != incoming.Count)
            {
                return false;
            }

            var current = new HashSet<string>(_roots, StringComparer.OrdinalIgnoreCase);
            return current.SetEquals(incoming);
        }

        private string GetTitle(string sourceRoot)
        {
            string title;
            if (_titles.TryGetValue(sourceRoot ?? string.Empty, out title)
                && !string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return sourceRoot ?? string.Empty;
        }

        private static string NormalizeSortMode(string sortMode)
        {
            if (string.IsNullOrWhiteSpace(sortMode)) return "User";

            string trimmed = sortMode.Trim();
            if (string.Equals(trimmed, "Priority A/C", StringComparison.OrdinalIgnoreCase))
                return "User";

            return trimmed;
        }

        private static ChannelListItemViewModel MapItem(ChannelListProjectionItem item)
        {
            return new ChannelListItemViewModel(item.Code, item.Label, item.Unit, item.IsSelected);
        }

        private sealed class SourceWindowState
        {
            public string SortMode { get; set; }
        }
    }
}
