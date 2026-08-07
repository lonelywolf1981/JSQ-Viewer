using System.Collections.Generic;

namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class SourceChannelWindowViewModel
    {
        public SourceChannelWindowViewModel(
            string sourceRoot,
            string title,
            string filterText,
            string sortMode,
            string selectedOrderKey,
            bool selectedOnly,
            IReadOnlyList<ChannelListItemViewModel> items)
        {
            SourceRoot = sourceRoot ?? string.Empty;
            Title = title ?? string.Empty;
            FilterText = filterText ?? string.Empty;
            SortMode = string.IsNullOrWhiteSpace(sortMode) ? "User" : sortMode;
            SelectedOrderKey = selectedOrderKey ?? string.Empty;
            SelectedOnly = selectedOnly;
            Items = items ?? new ChannelListItemViewModel[0];
        }

        public string SourceRoot { get; private set; }

        public string Title { get; private set; }

        public string FilterText { get; private set; }

        public string SortMode { get; private set; }

        public string SelectedOrderKey { get; private set; }

        public bool SelectedOnly { get; private set; }

        public IReadOnlyList<ChannelListItemViewModel> Items { get; private set; }
    }
}
