namespace JSQViewer.Application.Channels
{
    public sealed class ChannelListProjectionItem
    {
        public ChannelListProjectionItem(string code, string label, string unit, bool isSelected)
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
    }
}
