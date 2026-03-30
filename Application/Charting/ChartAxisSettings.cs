using System;

namespace JSQViewer.Application.Charting
{
    public sealed class ChartAxisSettings
    {
        private ChartAxisSettings()
        {
        }

        public bool IsManualEnabled { get; private set; }

        public double? Minimum { get; private set; }

        public double? Maximum { get; private set; }

        public double? Step { get; private set; }

        public static ChartAxisSettings Automatic()
        {
            return new ChartAxisSettings();
        }

        public static ChartAxisSettings ForManual(double minimum, double maximum, double step)
        {
            if (double.IsNaN(minimum) || double.IsInfinity(minimum)
                || double.IsNaN(maximum) || double.IsInfinity(maximum)
                || double.IsNaN(step) || double.IsInfinity(step)
                || minimum >= maximum
                || step <= 0d)
            {
                return Automatic();
            }

            return new ChartAxisSettings
            {
                IsManualEnabled = true,
                Minimum = minimum,
                Maximum = maximum,
                Step = step
            };
        }
    }
}
