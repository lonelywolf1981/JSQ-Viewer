using System;
using System.Collections.Generic;
using JSQViewer.Core;

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

        public double? Interval { get; private set; }

        public static ChartAxisSettings Automatic()
        {
            return new ChartAxisSettings();
        }

        public static ChartAxisSettings ForManual(double? minimum = null, double? maximum = null, double? interval = null)
        {
            return new ChartAxisSettings
            {
                IsManualEnabled = true,
                Minimum = minimum,
                Maximum = maximum,
                Interval = interval
            };
        }

        public ChartAxisSettings Disable()
        {
            return Automatic();
        }
    }

    public sealed class ChartPipelineRequest
    {
        private ChartPipelineRequest()
        {
            SelectedCodes = new string[0];
            XAxis = ChartAxisSettings.Automatic();
            YAxis = ChartAxisSettings.Automatic();
        }

        public TestData Data { get; private set; }

        public IReadOnlyList<string> SelectedCodes { get; private set; }

        public bool OverlayMode { get; private set; }

        public bool AutoStepEnabled { get; private set; }

        public int ManualStep { get; private set; }

        public int TargetPoints { get; private set; }

        public int SelectedChannelCount { get; private set; }

        public int DataVersion { get; private set; }

        public double SelectedRangeStart { get; private set; }

        public double SelectedRangeEnd { get; private set; }

        public ChartAxisSettings XAxis { get; private set; }

        public ChartAxisSettings YAxis { get; private set; }

        public static ChartPipelineRequest ForChart(
            TestData data,
            IEnumerable<string> selectedCodes,
            bool overlayMode,
            int dataVersion,
            bool autoStepEnabled,
            int manualStep,
            int targetPoints,
            int selectedChannelCount,
            double selectedRangeStart = double.NaN,
            double selectedRangeEnd = double.NaN,
            ChartAxisSettings xAxisSettings = null,
            ChartAxisSettings yAxisSettings = null)
        {
            IReadOnlyList<string> codes;
            if (selectedCodes == null)
            {
                codes = new string[0];
            }
            else
            {
                codes = new List<string>(selectedCodes);
            }

            return new ChartPipelineRequest
            {
                Data = data,
                SelectedCodes = codes,
                OverlayMode = overlayMode,
                DataVersion = dataVersion,
                AutoStepEnabled = autoStepEnabled,
                ManualStep = manualStep,
                TargetPoints = targetPoints,
                SelectedChannelCount = selectedChannelCount,
                SelectedRangeStart = selectedRangeStart,
                SelectedRangeEnd = selectedRangeEnd,
                XAxis = xAxisSettings ?? ChartAxisSettings.Automatic(),
                YAxis = yAxisSettings ?? ChartAxisSettings.Automatic()
            };
        }
    }
}
