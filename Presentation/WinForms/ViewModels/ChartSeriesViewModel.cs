namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class ChartSeriesViewModel
    {
        public string Code { get; set; }

        public string LegendText { get; set; }

        public double[] XValues { get; set; }

        public double[] YValues { get; set; }

        public int BorderWidth { get; set; }

        public bool IsVisibleInLegend { get; set; }
    }
}
