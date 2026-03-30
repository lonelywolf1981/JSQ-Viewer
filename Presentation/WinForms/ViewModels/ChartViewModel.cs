using System.Collections.Generic;

namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class ChartViewModel
    {
        public ChartViewModel()
        {
            Series = new ChartSeriesViewModel[0];
            Range = new ChartRangeViewModel();
        }

        public bool HasData { get; set; }

        public bool OverlayMode { get; set; }

        public bool ShowLegend { get; set; }

        public int Step { get; set; }

        public double DataMinimum { get; set; }

        public double DataMaximum { get; set; }

        public long MaxOverlayDurationMs { get; set; }

        public string XAxisLabelFormat { get; set; }

        public string XAxisTitle { get; set; }

        public ChartRangeViewModel Range { get; set; }

        public IReadOnlyList<ChartSeriesViewModel> Series { get; set; }
    }
}
