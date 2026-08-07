using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Channels;
using JSQViewer.Core;
using JSQViewer.Presentation.WinForms.ViewModels;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    public sealed class ChannelWorkspacePresenter
    {
        private readonly ChannelWorkspaceModel _workspace = new ChannelWorkspaceModel();
        private readonly SourceWindowCoordinator _sourceWindowCoordinator = new SourceWindowCoordinator();
        private string _mainSortMode = "User";
        private string _mainSelectedOrderKey = string.Empty;

        public int TotalChannelCount
        {
            get { return _workspace.TotalCount; }
        }

        public int SelectedChannelCount
        {
            get { return _workspace.SelectedCount; }
        }

        public string FilterText
        {
            get { return _sourceWindowCoordinator.SharedFilterText; }
        }

        public bool SelectedOnly
        {
            get { return _sourceWindowCoordinator.SharedSelectedOnly; }
        }

        public string MainSortMode
        {
            get { return _mainSortMode; }
        }

        public void Initialize(string filterText, string sortMode, bool selectedOnly)
        {
            _mainSortMode = NormalizeSortMode(sortMode);
            _sourceWindowCoordinator.Initialize(filterText, selectedOnly);
        }

        public SourceWindowRefreshPlan BindData(TestData data, IEnumerable<string> savedOrder, IEnumerable<string> preferredCheckedCodes, bool preserveSourceWindowsLayout)
        {
            return BindData(data, savedOrder, preferredCheckedCodes, preserveSourceWindowsLayout, null);
        }

        public SourceWindowRefreshPlan BindData(TestData data, IEnumerable<string> savedOrder, IEnumerable<string> preferredCheckedCodes, bool preserveSourceWindowsLayout, WorkspaceLayoutState layoutState)
        {
            WorkspaceLayoutState effectiveLayout = BuildEffectiveLayoutState(layoutState, preserveSourceWindowsLayout);
            _mainSelectedOrderKey = effectiveLayout.MainSelectedOrderKey ?? string.Empty;
            _workspace.Load(data, savedOrder, preferredCheckedCodes, effectiveLayout);
            bool canRefreshInPlace = _sourceWindowCoordinator.BindRoots(_workspace.SourceRoots, _mainSortMode, preserveSourceWindowsLayout, effectiveLayout);
            return new SourceWindowRefreshPlan(canRefreshInPlace, GetSourceWindows());
        }

        public void UpdateMainViewOptions(string filterText, string sortMode, bool selectedOnly)
        {
            _mainSortMode = NormalizeSortMode(sortMode);
            _sourceWindowCoordinator.UpdateFromMain(filterText, selectedOnly);
        }

        public void UpdateSourceWindowOptions(string sourceRoot, string filterText, string sortMode, bool selectedOnly)
        {
            _sourceWindowCoordinator.UpdateFromSource(sourceRoot, filterText, sortMode, selectedOnly);
        }

        public void SetAllSortModes(string sortMode)
        {
            string normalized = NormalizeSortMode(sortMode);
            _mainSortMode = normalized;
            _sourceWindowCoordinator.SetAllSortModes(normalized);
        }

        public void ApplyCheckedCodes(IEnumerable<string> checkedCodes)
        {
            _workspace.ReplaceSelectedCodes(checkedCodes);
        }

        public void SetChannelSelected(string code, bool isSelected)
        {
            _workspace.SetChannelSelected(code, isSelected);
        }

        public void SelectAllChannels()
        {
            _workspace.SelectAll();
        }

        public void ClearAllChannels()
        {
            _workspace.ClearAll();
        }

        public void SelectAllSourceChannels(string sourceRoot)
        {
            _workspace.SelectAllInSource(sourceRoot);
        }

        public void ClearSourceChannels(string sourceRoot)
        {
            _workspace.ClearAllInSource(sourceRoot);
        }

        public bool MoveMainChannel(int fromIndex, int toIndex)
        {
            if (_sourceWindowCoordinator.SharedSelectedOnly
                || !string.IsNullOrWhiteSpace(_sourceWindowCoordinator.SharedFilterText)
                || _mainSortMode != "User")
            {
                return false;
            }

            return _workspace.MoveMainItem(fromIndex, toIndex);
        }

        public bool MoveSourceChannel(string sourceRoot, int fromIndex, int toIndex)
        {
            return _workspace.MoveSourceItem(sourceRoot, fromIndex, toIndex);
        }

        public void ApplyOrder(IEnumerable<string> order)
        {
            _workspace.ApplyOrder(order);
        }

        public void ApplySourceOrder(string sourceRoot, IEnumerable<string> order)
        {
            _workspace.ApplySourceOrder(sourceRoot, order);
        }

        public void SetMainSelectedOrderKey(string selectedOrderKey)
        {
            _mainSelectedOrderKey = selectedOrderKey ?? string.Empty;
        }

        public string GetMainSelectedOrderKey()
        {
            return _mainSelectedOrderKey ?? string.Empty;
        }

        public void SetSourceSelectedOrderKey(string sourceRoot, string selectedOrderKey)
        {
            _sourceWindowCoordinator.SetSelectedOrderKey(sourceRoot, selectedOrderKey);
        }

        public string GetSourceSelectedOrderKey(string sourceRoot)
        {
            return _sourceWindowCoordinator.GetSelectedOrderKey(sourceRoot);
        }

        public IReadOnlyList<string> GetSelectedCodes()
        {
            return _workspace.GetSelectedCodes();
        }

        public IReadOnlyList<string> GetCurrentOrder()
        {
            return _workspace.GetCurrentOrder();
        }

        public IReadOnlyList<string> GetCurrentOrderForSource(string sourceRoot)
        {
            return _workspace.GetCurrentOrderForSource(sourceRoot);
        }

        public ChannelListViewModel GetMainChannelList()
        {
            IReadOnlyList<ChannelListProjectionItem> items = _workspace.BuildMainList(
                _sourceWindowCoordinator.SharedFilterText,
                _mainSortMode,
                _sourceWindowCoordinator.SharedSelectedOnly);
            return new ChannelListViewModel(
                _sourceWindowCoordinator.SharedFilterText,
                _mainSortMode,
                _sourceWindowCoordinator.SharedSelectedOnly,
                items.Select(MapItem).ToArray());
        }

        public SourceChannelWindowViewModel GetSourceWindow(string sourceRoot)
        {
            return _sourceWindowCoordinator.BuildWindow(_workspace, sourceRoot);
        }

        public IReadOnlyList<SourceChannelWindowViewModel> GetSourceWindows()
        {
            return _sourceWindowCoordinator.BuildWindows(_workspace);
        }

        public WorkspaceLayoutState GetWorkspaceLayoutState()
        {
            var state = new WorkspaceLayoutState
            {
                MainSelectedOrderKey = _mainSelectedOrderKey ?? string.Empty,
                MainOrder = _workspace.GetCurrentOrder().ToList()
            };

            foreach (string root in _workspace.SourceRoots)
            {
                state.Sources[root] = new WorkspaceSourceLayoutState
                {
                    SelectedOrderKey = _sourceWindowCoordinator.GetSelectedOrderKey(root),
                    Order = _workspace.GetSourceOrder(root).ToList()
                };
            }

            return state;
        }

        private static string NormalizeSortMode(string sortMode)
        {
            return string.IsNullOrWhiteSpace(sortMode) ? "User" : sortMode.Trim();
        }

        private WorkspaceLayoutState BuildEffectiveLayoutState(WorkspaceLayoutState layoutState, bool preserveSourceWindowsLayout)
        {
            WorkspaceLayoutState effective = layoutState == null ? new WorkspaceLayoutState() : layoutState.Clone();
            if (!preserveSourceWindowsLayout)
            {
                effective.EnsureInitialized();
                return effective;
            }

            WorkspaceLayoutState current = GetWorkspaceLayoutState();
            if (!string.IsNullOrWhiteSpace(current.MainSelectedOrderKey))
            {
                effective.MainSelectedOrderKey = current.MainSelectedOrderKey;
            }

            if (current.MainOrder.Count > 0)
            {
                effective.MainOrder = current.MainOrder.ToList();
            }

            foreach (KeyValuePair<string, WorkspaceSourceLayoutState> entry in current.Sources)
            {
                effective.Sources[entry.Key] = entry.Value == null ? new WorkspaceSourceLayoutState() : entry.Value.Clone();
            }

            effective.EnsureInitialized();
            return effective;
        }

        private static ChannelListItemViewModel MapItem(ChannelListProjectionItem item)
        {
            return new ChannelListItemViewModel(item.Code, item.Label, item.Unit, item.IsSelected);
        }
    }
}
