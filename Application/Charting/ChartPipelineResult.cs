using System.Collections.Generic;

namespace JSQViewer.Application.Charting
{
    public sealed class ChartPipelineResult
    {
        public ChartPipelineResult()
        {
            Series = new ChartPipelineSeries[0];
            XAxis = ChartAxisSettings.Automatic();
            YAxis = ChartAxisSettings.Automatic();
        }

        public bool HasData { get; set; }

        public bool OverlayMode { get; set; }

        public bool ShowLegend { get; set; }

        public int Step { get; set; }

        public double DataMinimum { get; set; }

        public double DataMaximum { get; set; }

        public double SelectedRangeStart { get; set; }

        public double SelectedRangeEnd { get; set; }

        public long MaxOverlayDurationMs { get; set; }

        public IReadOnlyList<ChartPipelineSeries> Series { get; set; }

        public ChartAxisSettings XAxis { get; set; }

        public ChartAxisSettings YAxis { get; set; }
    }
}
