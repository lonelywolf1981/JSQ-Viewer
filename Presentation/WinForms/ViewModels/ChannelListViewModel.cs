using System.Collections.Generic;

namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class ChannelListViewModel
    {
        public ChannelListViewModel(string filterText, string sortMode, bool selectedOnly, IReadOnlyList<ChannelListItemViewModel> items)
        {
            FilterText = filterText ?? string.Empty;
            SortMode = string.IsNullOrWhiteSpace(sortMode) ? "User" : sortMode;
            SelectedOnly = selectedOnly;
            Items = items ?? new ChannelListItemViewModel[0];
        }

        public string FilterText { get; private set; }

        public string SortMode { get; private set; }

        public bool SelectedOnly { get; private set; }

        public IReadOnlyList<ChannelListItemViewModel> Items { get; private set; }
    }
}
