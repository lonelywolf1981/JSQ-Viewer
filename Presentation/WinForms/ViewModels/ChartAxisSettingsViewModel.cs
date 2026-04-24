using JSQViewer.Application.Charting;

namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class ChartAxisSettingsViewModel
    {
        public bool IsManualEnabled { get; set; }

        public double? Minimum { get; set; }

        public double? Maximum { get; set; }

        public double? Interval { get; set; }

        public ChartAxisSettings ToChartAxisSettings()
        {
            if (!IsManualEnabled)
            {
                return ChartAxisSettings.Automatic();
            }

            return ChartAxisSettings.ForManual(Minimum, Maximum, Interval);
        }
    }
}
