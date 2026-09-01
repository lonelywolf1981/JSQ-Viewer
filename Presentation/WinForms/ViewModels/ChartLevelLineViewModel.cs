using JSQViewer.Application.Charting;

namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class ChartLevelLineViewModel
    {
        public int SourceIndex { get; set; }

        public ChartSeriesRole Role { get; set; }

        public double Value { get; set; }

        public string Label { get; set; }
    }
}
