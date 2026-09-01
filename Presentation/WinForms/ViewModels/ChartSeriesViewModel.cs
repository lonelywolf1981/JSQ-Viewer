using JSQViewer.Application.Charting;

namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class ChartSeriesViewModel
    {
        public string Code { get; set; }

        public string LegendText { get; set; }

        public string SourceRoot { get; set; }

        public double[] XValues { get; set; }

        public double[] YValues { get; set; }

        public int BorderWidth { get; set; }

        public bool IsVisibleInLegend { get; set; }

        public bool IsForecast { get; set; }

        public ChartSeriesRole Role { get; set; }

        public int SourceIndex { get; set; }
    }
}
