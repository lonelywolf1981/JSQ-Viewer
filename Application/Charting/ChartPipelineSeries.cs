using System.Collections.Generic;

namespace JSQViewer.Application.Charting
{
    public sealed class ChartPipelineSeries
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

        public IReadOnlyList<DynamicsForecastWarning> ForecastWarnings { get; set; }
    }
}
