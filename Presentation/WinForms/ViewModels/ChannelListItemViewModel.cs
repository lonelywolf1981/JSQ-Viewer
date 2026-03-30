namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class ChannelListItemViewModel
    {
        public ChannelListItemViewModel(string code, string label, string unit, bool isSelected)
        {
            Code = code ?? string.Empty;
            Label = label ?? string.Empty;
            Unit = unit ?? string.Empty;
            IsSelected = isSelected;
        }

        public string Code { get; private set; }

        public string Label { get; private set; }

        public string Unit { get; private set; }

        public bool IsSelected { get; private set; }

        public override string ToString()
        {
            return Label;
        }
    }
}
