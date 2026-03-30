using System;
using System.Collections.Generic;
using JSQViewer.Core;

namespace JSQViewer.Application.Charting
{
    public sealed class ChartPipelineRequest
    {
        private ChartPipelineRequest()
        {
            SelectedCodes = new string[0];
        }

        public TestData Data { get; private set; }

        public IReadOnlyList<string> SelectedCodes { get; private set; }

        public bool OverlayMode { get; private set; }

        public bool AutoStepEnabled { get; private set; }

        public int ManualStep { get; private set; }

        public int TargetPoints { get; private set; }

        public int SelectedChannelCount { get; private set; }

        public double RangeStartOa { get; private set; }

        public double RangeEndOa { get; private set; }

        public static ChartPipelineRequest ForChart(
            TestData data,
            IEnumerable<string> selectedCodes,
            bool overlayMode,
            bool autoStepEnabled,
            int manualStep,
            int targetPoints,
            int selectedChannelCount,
            double rangeStartOa = double.NaN,
            double rangeEndOa = double.NaN)
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
                AutoStepEnabled = autoStepEnabled,
                ManualStep = manualStep,
                TargetPoints = targetPoints,
                SelectedChannelCount = selectedChannelCount,
                RangeStartOa = rangeStartOa,
                RangeEndOa = rangeEndOa
            };
        }
    }
}
