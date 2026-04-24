using System;
using System.Reflection;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Charting.UseCases;
using JSQViewer.Core;
using JSQViewer.Infrastructure.Cache;
using JSQViewer.Presentation.WinForms.Charting;
using JSQViewer.Presentation.WinForms.ViewModels;
using JSQViewer.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms.DataVisualization.Charting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class BuildChartViewUseCaseTests
    {
        [TestMethod]
        public void Execute_BuildsOverlayAxisMetadataAndActiveRange()
        {
            var data = SessionAndChartingTestData.CreateData(new long[] { 0L, 3600000L, 7200000L });
            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01" },
                overlayMode: true,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1,
                selectedRangeStart: 0.5,
                selectedRangeEnd: 1.5);
            var useCase = new BuildChartViewUseCase(new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService())));

            ChartPipelineResult result = useCase.Execute(request);

            Assert.IsTrue(result.HasData);
            Assert.IsTrue(result.OverlayMode);
            Assert.AreEqual(0.5, result.SelectedRangeStart);
            Assert.AreEqual(1.5, result.SelectedRangeEnd);
        }

        [TestMethod]
        public void Execute_UsesDateTimeAxisMetadataWhenOverlayIsDisabled()
        {
            var baseTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Local).ToUniversalTime();
            long startMs = new DateTimeOffset(baseTime).ToUnixTimeMilliseconds();
            var data = SessionAndChartingTestData.CreateData(new long[] { startMs, startMs + 60000L, startMs + 120000L });
            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1);
            var useCase = new BuildChartViewUseCase(new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService())));

            ChartPipelineResult result = useCase.Execute(request);

            Assert.IsFalse(result.OverlayMode);
            Assert.IsTrue(result.DataMaximum > result.DataMinimum);
            Assert.IsTrue(double.IsNaN(result.SelectedRangeStart));
            Assert.AreEqual(1, result.Series.Count);
        }

        [TestMethod]
        public void Execute_BuildsRenderableManualAxisSettings_ForOverlayAndValueAxes()
        {
            var data = SessionAndChartingTestData.CreateData(new long[] { 0L, 3600000L, 7200000L });
            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01" },
                overlayMode: true,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1,
                xAxisSettings: ChartAxisSettings.ForManual(minimum: 0.25, maximum: 1.75, interval: 0.5),
                yAxisSettings: ChartAxisSettings.ForManual(minimum: 10.0, maximum: 30.0, interval: 2.5));
            var useCase = new BuildChartViewUseCase(new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService())));
            var factory = new ChartViewModelFactory(new TimestampRangeService());
            var renderer = new ChartRenderer();
            var attachedChart = CreateChart();
            var detachedChart = CreateChart();

            ChartPipelineResult result = useCase.Execute(request);
            ChartViewModel viewModel = factory.Create(result, "Overlay hours");
            renderer.Render(attachedChart, viewModel);
            renderer.Render(detachedChart, viewModel);

            Assert.IsTrue(viewModel.XAxis.IsManualEnabled);
            Assert.AreEqual(0.25, viewModel.XAxis.Minimum.GetValueOrDefault(), 1e-9);
            Assert.AreEqual(1.75, viewModel.XAxis.Maximum.GetValueOrDefault(), 1e-9);
            Assert.AreEqual(0.5, viewModel.XAxis.Interval.GetValueOrDefault(), 1e-9);
            Assert.IsTrue(viewModel.YAxis.IsManualEnabled);
            Assert.AreEqual(10.0, viewModel.YAxis.Minimum.GetValueOrDefault(), 1e-9);
            Assert.AreEqual(30.0, viewModel.YAxis.Maximum.GetValueOrDefault(), 1e-9);
            Assert.AreEqual(2.5, viewModel.YAxis.Interval.GetValueOrDefault(), 1e-9);
            Assert.AreEqual(attachedChart.ChartAreas[0].AxisX.Minimum, detachedChart.ChartAreas[0].AxisX.Minimum, 1e-9);
            Assert.AreEqual(attachedChart.ChartAreas[0].AxisX.Maximum, detachedChart.ChartAreas[0].AxisX.Maximum, 1e-9);
            Assert.AreEqual(attachedChart.ChartAreas[0].AxisY.Minimum, detachedChart.ChartAreas[0].AxisY.Minimum, 1e-9);
            Assert.AreEqual(attachedChart.ChartAreas[0].AxisY.Maximum, detachedChart.ChartAreas[0].AxisY.Maximum, 1e-9);
        }

        [TestMethod]
        public void Execute_KeepsRendererAutomaticAxes_WhenManualModeIsDisabled()
        {
            var baseTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Local).ToUniversalTime();
            long startMs = new DateTimeOffset(baseTime).ToUnixTimeMilliseconds();
            var data = SessionAndChartingTestData.CreateData(new long[] { startMs, startMs + 60000L, startMs + 120000L });
            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1,
                xAxisSettings: ChartAxisSettings.ForManual(minimum: (double)startMs, maximum: (double)(startMs + 120000L), interval: 60000.0).Disable(),
                yAxisSettings: ChartAxisSettings.ForManual(minimum: 5.0, maximum: 15.0, interval: 1.0).Disable());
            var useCase = new BuildChartViewUseCase(new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService())));
            var factory = new ChartViewModelFactory(new TimestampRangeService());
            var renderer = new ChartRenderer();
            var chart = CreateChart();

            ChartPipelineResult result = useCase.Execute(request);
            ChartViewModel viewModel = factory.Create(result, "ignored");
            renderer.Render(chart, viewModel);

            Assert.IsFalse(viewModel.XAxis.IsManualEnabled);
            Assert.IsFalse(viewModel.YAxis.IsManualEnabled);
            Assert.IsTrue(double.IsNaN(chart.ChartAreas[0].AxisY.Minimum));
            Assert.IsTrue(double.IsNaN(chart.ChartAreas[0].AxisY.Maximum));
            Assert.AreEqual(0.0, chart.ChartAreas[0].AxisY.Interval, 1e-9);
        }

        private static Chart CreateChart()
        {
            var chart = new Chart();
            chart.ChartAreas.Add(new ChartArea("main"));
            chart.Legends.Add(new Legend("legend"));
            return chart;
        }
    }

    [TestClass]
    public class BuildWorkspaceSummaryUseCaseTests
    {
        [TestMethod]
        public void Execute_ReturnsCurrentWorkspaceSummary()
        {
            var timestampRangeService = new TimestampRangeService();
            var useCase = new BuildWorkspaceSummaryUseCase(new DataSummaryService(timestampRangeService));
            var data = SessionAndChartingTestData.CreateData(new long[] { 1000L, 2000L, 3000L });

            DataSummary summary = useCase.Execute(data);

            Assert.AreEqual(3, summary.Points);
            Assert.AreEqual(timestampRangeService.UnixMsToLocalDateTime(1000L), summary.Start);
            Assert.AreEqual(timestampRangeService.UnixMsToLocalDateTime(3000L), summary.End);
        }
    }

    [TestClass]
    public class ChartRendererTests
    {
        [TestMethod]
        public void Create_ConvertsUnixMillisecondsIntoOaDatesForDateTimeCharts()
        {
            var timestampRangeService = new TimestampRangeService();
            var factory = new ChartViewModelFactory(timestampRangeService);
            long startMs = new DateTimeOffset(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            var result = new ChartPipelineResult
            {
                HasData = true,
                OverlayMode = false,
                ShowLegend = true,
                DataMinimum = startMs,
                DataMaximum = startMs + 60000L,
                SelectedRangeStart = startMs,
                SelectedRangeEnd = startMs + 60000L,
                Series = new[]
                {
                    new ChartPipelineSeries
                    {
                        Code = "A-01",
                        LegendText = "A-01",
                        XValues = new[] { (double)startMs, (double)(startMs + 60000L) },
                        YValues = new[] { 1d, 2d },
                        BorderWidth = 2,
                        IsVisibleInLegend = true
                    }
                }
            };

            ChartViewModel viewModel = factory.Create(result, "ignored");

            Assert.AreEqual("HH:mm\ndd.MM", viewModel.XAxisLabelFormat);
            Assert.AreEqual(timestampRangeService.UnixMsToLocalDateTime(startMs).ToOADate(), viewModel.DataMinimum);
            Assert.AreEqual(timestampRangeService.UnixMsToLocalDateTime(startMs).ToOADate(), viewModel.Range.Start);
        }

        [TestMethod]
        public void Render_AppliesOverlayAxisMetadataAndRange()
        {
            var renderer = new ChartRenderer();
            var chart = new Chart();
            chart.ChartAreas.Add(new ChartArea("main"));
            chart.Legends.Add(new Legend("legend"));
            var viewModel = new ChartViewModel
            {
                HasData = true,
                OverlayMode = true,
                ShowLegend = true,
                XAxisLabelFormat = "0.##",
                XAxisTitle = "Overlay hours",
                Range = new ChartRangeViewModel { IsActive = true, Start = 0.5, End = 1.5 },
                Series = new[]
                {
                    new ChartSeriesViewModel
                    {
                        Code = "A-01",
                        LegendText = "A-01",
                        XValues = new[] { 0.0, 1.0 },
                        YValues = new[] { 1d, 2d },
                        BorderWidth = 2,
                        IsVisibleInLegend = true
                    }
                }
            };

            renderer.Render(chart, viewModel);

            Assert.AreEqual("0.##", chart.ChartAreas[0].AxisX.LabelStyle.Format);
            Assert.AreEqual("Overlay hours", chart.ChartAreas[0].AxisX.Title);
            Assert.AreEqual(0.5, chart.ChartAreas[0].AxisX.Minimum);
            Assert.AreEqual(1.5, chart.ChartAreas[0].AxisX.Maximum);
        }

        [TestMethod]
        public void Render_PrefersManualXAxisBoundsOverActiveRange()
        {
            var renderer = new ChartRenderer();
            var chart = new Chart();
            chart.ChartAreas.Add(new ChartArea("main"));
            chart.Legends.Add(new Legend("legend"));
            var viewModel = new ChartViewModel
            {
                HasData = true,
                OverlayMode = true,
                ShowLegend = true,
                XAxis = new ChartAxisSettingsViewModel
                {
                    IsManualEnabled = true,
                    Minimum = 0.25,
                    Maximum = 1.75,
                    Interval = 0.5
                },
                Range = new ChartRangeViewModel { IsActive = true, Start = 0.5, End = 1.5 },
                Series = new[]
                {
                    new ChartSeriesViewModel
                    {
                        Code = "A-01",
                        LegendText = "A-01",
                        XValues = new[] { 0.0, 1.0 },
                        YValues = new[] { 1d, 2d },
                        BorderWidth = 2,
                        IsVisibleInLegend = true
                    }
                }
            };

            renderer.Render(chart, viewModel);

            Assert.AreEqual(0.25, chart.ChartAreas[0].AxisX.Minimum, 1e-9);
            Assert.AreEqual(1.75, chart.ChartAreas[0].AxisX.Maximum, 1e-9);
            Assert.AreEqual(0.5, chart.ChartAreas[0].AxisX.Interval, 1e-9);
        }

        [TestMethod]
        public void NonOverlayManualXAxisInput_IsParsedAsDateTimeTextInsteadOfRawUnixMilliseconds()
        {
            MethodInfo method = typeof(MainForm).GetMethod("BuildManualXAxisSettings", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Expected MainForm.BuildManualXAxisSettings helper.");

            DateTime start = new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Local);
            DateTime end = new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Local);
            var timestampRangeService = new TimestampRangeService();
            var factory = new ChartViewModelFactory(timestampRangeService);
            ChartAxisSettings axis = (ChartAxisSettings)method.Invoke(
                null,
                new object[] { false, true, "24.04.2026 10:00", "24.04.2026 12:00", "30" });
            var result = new ChartPipelineResult
            {
                HasData = true,
                OverlayMode = false,
                ShowLegend = true,
                XAxis = axis,
                Series = new[]
                {
                    new ChartPipelineSeries
                    {
                        Code = "A-01",
                        LegendText = "A-01",
                        XValues = new[] { (double)new DateTimeOffset(start).ToUnixTimeMilliseconds(), (double)new DateTimeOffset(end).ToUnixTimeMilliseconds() },
                        YValues = new[] { 1d, 2d },
                        BorderWidth = 2,
                        IsVisibleInLegend = true
                    }
                }
            };

            ChartViewModel viewModel = factory.Create(result, "ignored");

            Assert.IsTrue(axis.IsManualEnabled);
            Assert.AreEqual(new DateTimeOffset(start).ToUnixTimeMilliseconds(), (long)axis.Minimum.GetValueOrDefault());
            Assert.AreEqual(new DateTimeOffset(end).ToUnixTimeMilliseconds(), (long)axis.Maximum.GetValueOrDefault());
            Assert.AreEqual(start.ToOADate(), viewModel.XAxis.Minimum.GetValueOrDefault(), 1e-9);
            Assert.AreEqual(end.ToOADate(), viewModel.XAxis.Maximum.GetValueOrDefault(), 1e-9);
            Assert.AreEqual(TimeSpan.FromMinutes(30).TotalDays, viewModel.XAxis.Interval.GetValueOrDefault(), 1e-9);
        }

        [TestMethod]
        public void ManualXAxisUiHints_DescribeExpectedInputFormats()
        {
            MethodInfo captionMethod = typeof(MainForm).GetMethod("GetManualXAxisCaption", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo boundsHintMethod = typeof(MainForm).GetMethod("GetManualXAxisBoundsHint", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo stepHintMethod = typeof(MainForm).GetMethod("GetManualXAxisStepHint", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(captionMethod);
            Assert.IsNotNull(boundsHintMethod);
            Assert.IsNotNull(stepHintMethod);

            string overlayCaption = (string)captionMethod.Invoke(null, new object[] { true });
            string nonOverlayCaption = (string)captionMethod.Invoke(null, new object[] { false });
            string overlayBoundsHint = (string)boundsHintMethod.Invoke(null, new object[] { true });
            string nonOverlayBoundsHint = (string)boundsHintMethod.Invoke(null, new object[] { false });
            string overlayStepHint = (string)stepHintMethod.Invoke(null, new object[] { true });
            string nonOverlayStepHint = (string)stepHintMethod.Invoke(null, new object[] { false });

            StringAssert.Contains(overlayCaption, "hours");
            StringAssert.Contains(nonOverlayCaption, "date/time");
            StringAssert.Contains(nonOverlayBoundsHint, "dd.MM.yyyy HH:mm");
            StringAssert.Contains(nonOverlayStepHint, "minutes");
            StringAssert.Contains(overlayBoundsHint, "hours");
            StringAssert.Contains(overlayStepHint, "hours");
        }
    }
}
