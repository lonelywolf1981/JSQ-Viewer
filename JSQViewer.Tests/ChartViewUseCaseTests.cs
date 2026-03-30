using System;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Charting.UseCases;
using JSQViewer.Presentation.WinForms.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1,
                rangeStartOa: 0.5,
                rangeEndOa: 1.5);
            var useCase = new BuildChartViewUseCase(new ChartPipelineService(new TimestampRangeService()));

            ChartViewModel viewModel = useCase.Execute(request, "Overlay hours");

            Assert.IsTrue(viewModel.HasData);
            Assert.AreEqual("0.##", viewModel.XAxisLabelFormat);
            Assert.AreEqual("Overlay hours", viewModel.XAxisTitle);
            Assert.IsTrue(viewModel.Range.IsActive);
            Assert.AreEqual(0.5, viewModel.Range.Start);
            Assert.AreEqual(1.5, viewModel.Range.End);
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
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1);
            var useCase = new BuildChartViewUseCase(new ChartPipelineService(new TimestampRangeService()));

            ChartViewModel viewModel = useCase.Execute(request, "ignored");

            Assert.AreEqual("HH:mm\ndd.MM", viewModel.XAxisLabelFormat);
            Assert.AreEqual(string.Empty, viewModel.XAxisTitle);
            Assert.IsFalse(viewModel.Range.IsActive);
            Assert.AreEqual(1, viewModel.Series.Count);
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

            WorkspaceSummaryViewModel summary = useCase.Execute(data);

            Assert.AreEqual(3, summary.PointCount);
            Assert.AreEqual(timestampRangeService.UnixMsToLocalDateTime(1000L), summary.Start);
            Assert.AreEqual(timestampRangeService.UnixMsToLocalDateTime(3000L), summary.End);
        }
    }
}
