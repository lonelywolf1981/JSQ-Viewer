namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class ChartAxisSettingsViewModel
    {
        public bool IsManualEnabled { get; set; }

        public double? Minimum { get; set; }

        public double? Maximum { get; set; }

        public double? Step { get; set; }
    }
}
