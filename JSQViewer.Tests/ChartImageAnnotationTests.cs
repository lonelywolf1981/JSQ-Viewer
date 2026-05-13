using System.Collections.Generic;
using System.Drawing;
using JSQViewer.Presentation.WinForms.Charting;
using JSQViewer.Presentation.WinForms.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ChartImageAnnotationTests
    {
        [TestMethod]
        public void CollectSourcePaths_ReturnsDistinctDisplayedSeriesPathsInOrder()
        {
            var viewModel = new ChartViewModel
            {
                Series = new[]
                {
                    new ChartSeriesViewModel { Code = "A", SourceRoot = @"Y:\Record A", XValues = new[] { 1d }, YValues = new[] { 10d } },
                    new ChartSeriesViewModel { Code = "B", SourceRoot = @"Y:\Record B", XValues = new[] { 1d }, YValues = new[] { 20d } },
                    new ChartSeriesViewModel { Code = "C", SourceRoot = @"Y:\Record A", XValues = new[] { 1d }, YValues = new[] { 30d } },
                    new ChartSeriesViewModel { Code = "D", SourceRoot = @"Y:\Empty", XValues = new double[0], YValues = new double[0] }
                }
            };

            IReadOnlyList<string> paths = ChartImageAnnotation.CollectSourcePaths(viewModel);

            CollectionAssert.AreEqual(new[] { @"Y:\Record A", @"Y:\Record B" }, (System.Collections.ICollection)paths);
        }

        [TestMethod]
        public void ResolvePngExportSize_UsesFullHdWhenChartIsSmaller()
        {
            Size size = ChartImageAnnotation.ResolvePngExportSize(new Size(900, 500));

            Assert.AreEqual(new Size(1920, 1080), size);
        }

        [TestMethod]
        public void ResolvePngExportSize_KeepsLargerChartSize()
        {
            Size size = ChartImageAnnotation.ResolvePngExportSize(new Size(2560, 1440));

            Assert.AreEqual(new Size(2560, 1440), size);
        }
    }
}
