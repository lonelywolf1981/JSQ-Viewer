using System;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Charting.UseCases;
using JSQViewer.Core;
using JSQViewer.Infrastructure.Cache;
using JSQViewer.Presentation.WinForms.Charting;
using JSQViewer.Presentation.WinForms.ViewModels;
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
    }
}
