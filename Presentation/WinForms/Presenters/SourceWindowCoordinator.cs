using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JSQViewer.Application.Channels;
using JSQViewer.Presentation.WinForms.ViewModels;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    public sealed class SourceWindowCoordinator
    {
        private readonly Dictionary<string, SourceWindowState> _states = new Dictionary<string, SourceWindowState>(StringComparer.OrdinalIgnoreCase);
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

        public bool BindRoots(IReadOnlyList<string> roots, string defaultSortMode, bool preserveExistingLayout, WorkspaceLayoutState layoutState)
        {
            string[] incoming = roots == null ? new string[0] : roots.ToArray();
            bool canRefreshInPlace = preserveExistingLayout && HaveSameRoots(incoming);
            var previousStates = new Dictionary<string, SourceWindowState>(_states, StringComparer.OrdinalIgnoreCase);

            _states.Clear();

            _roots.Clear();
            _roots.AddRange(incoming);

            for (int i = 0; i < _roots.Count; i++)
            {
                string root = _roots[i];
                SourceWindowState state = null;
                bool hasPrevious = preserveExistingLayout && previousStates.TryGetValue(root, out state);
                if (!hasPrevious)
                {
                    state = new SourceWindowState();
                }

                WorkspaceSourceLayoutState sourceLayout = layoutState == null ? null : layoutState.GetSource(root);
                if (string.IsNullOrWhiteSpace(state.SortMode))
                {
                    state.SortMode = NormalizeSortMode(defaultSortMode);
                }

                if (!hasPrevious)
                {
                    state.SelectedOrderKey = sourceLayout == null ? string.Empty : (sourceLayout.SelectedOrderKey ?? string.Empty);
                }

                _states[root] = state;
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

        public void SetSelectedOrderKey(string sourceRoot, string selectedOrderKey)
        {
            SourceWindowState state;
            if (_states.TryGetValue(sourceRoot ?? string.Empty, out state))
            {
                state.SelectedOrderKey = selectedOrderKey ?? string.Empty;
            }
        }

        public string GetSelectedOrderKey(string sourceRoot)
        {
            SourceWindowState state;
            if (_states.TryGetValue(sourceRoot ?? string.Empty, out state))
            {
                return state.SelectedOrderKey ?? string.Empty;
            }

            return string.Empty;
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
                BuildTitle(root),
                _sharedFilterText,
                sortMode,
                GetSelectedOrderKey(root),
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

        private static string BuildTitle(string sourceRoot)
        {
            string trimmed = (sourceRoot ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string sourceName = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(sourceName) ? (sourceRoot ?? string.Empty) : sourceName;
        }

        private static string NormalizeSortMode(string sortMode)
        {
            return string.IsNullOrWhiteSpace(sortMode) ? "User" : sortMode.Trim();
        }

        private static ChannelListItemViewModel MapItem(ChannelListProjectionItem item)
        {
            return new ChannelListItemViewModel(item.Code, item.Label, item.Unit, item.IsSelected);
        }

        private sealed class SourceWindowState
        {
            public string SortMode { get; set; }

            public string SelectedOrderKey { get; set; }
        }
    }
}
