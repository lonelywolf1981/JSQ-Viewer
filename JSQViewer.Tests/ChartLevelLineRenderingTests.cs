using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using JSQViewer.Application.Charting;
using JSQViewer.Presentation.WinForms.Charting;
using JSQViewer.Presentation.WinForms.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class ChartLevelLineRenderingTests
    {
        private static Chart CreateChart()
        {
            var chart = new Chart();
            chart.ChartAreas.Add(new ChartArea("main"));
            chart.Legends.Add(new Legend("legend"));
            return chart;
        }

        private static ChartViewModel ViewModel(params ChartLevelLineViewModel[] levels)
        {
            return new ChartViewModel
            {
                HasData = true,
                ShowLegend = true,
                Step = 1,
                Series = new List<ChartSeriesViewModel>
                {
                    new ChartSeriesViewModel
                    {
                        Code = "T1",
                        LegendText = "T1",
                        XValues = new[] { 1d, 2d },
                        YValues = new[] { 5d, 6d },
                        BorderWidth = 1,
                        IsVisibleInLegend = true
                    }
                },
                LevelLines = levels
            };
        }

        [TestMethod]
        public void Render_AddsOneStripLinePerLevel_WithValueAndLabel()
        {
            using (Chart chart = CreateChart())
            {
                new ChartRenderer().Render(chart, ViewModel(
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Average, Value = 4.5, Label = "T8+ сред 4.5" }));

                StripLine strip = chart.ChartAreas[0].AxisY.StripLines.Single();
                Assert.AreEqual(4.5, strip.IntervalOffset, 1e-9);
                Assert.AreEqual(0d, strip.StripWidth, 1e-9);
                Assert.AreEqual("T8+ сред 4.5", strip.Text);
            }
        }

        [TestMethod]
        public void Render_LevelsAreNotSeries_AndDoNotEnterLegend()
        {
            using (Chart chart = CreateChart())
            {
                new ChartRenderer().Render(chart, ViewModel(
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Minimum, Value = 1d, Label = "T8+ мин 1.0" },
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Maximum, Value = 9d, Label = "T8+ макс 9.0" }));

                Assert.AreEqual(1, chart.Series.Count);
                Assert.AreEqual("T1", chart.Series[0].Name);
                Assert.AreEqual(2, chart.ChartAreas[0].AxisY.StripLines.Count);
            }
        }

        [TestMethod]
        public void Render_UsesDashStyleByRoleAndNeverDash()
        {
            using (Chart chart = CreateChart())
            {
                new ChartRenderer().Render(chart, ViewModel(
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Minimum, Value = 1d, Label = "a" },
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Average, Value = 2d, Label = "b" },
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Maximum, Value = 3d, Label = "c" }));

                StripLine[] strips = chart.ChartAreas[0].AxisY.StripLines.Cast<StripLine>().ToArray();

                Assert.AreEqual(ChartDashStyle.Dot, strips[0].BorderDashStyle);
                Assert.AreEqual(ChartDashStyle.Solid, strips[1].BorderDashStyle);
                Assert.AreEqual(ChartDashStyle.DashDot, strips[2].BorderDashStyle);
                // Dash закреплён за линией прогноза динамики.
                Assert.IsFalse(strips.Any(s => s.BorderDashStyle == ChartDashStyle.Dash));
            }
        }

        [TestMethod]
        public void Render_ColorsLevelBySourceIndex()
        {
            using (Chart chart = CreateChart())
            {
                new ChartRenderer().Render(chart, ViewModel(
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Average, Value = 1d, Label = "a" },
                    new ChartLevelLineViewModel { SourceIndex = 1, Role = ChartSeriesRole.T8Average, Value = 2d, Label = "b" }));

                StripLine[] strips = chart.ChartAreas[0].AxisY.StripLines.Cast<StripLine>().ToArray();

                Assert.AreEqual(SourceColorPalette.ForSourceIndex(0), strips[0].BorderColor);
                Assert.AreEqual(SourceColorPalette.ForSourceIndex(1), strips[1].BorderColor);
                Assert.AreNotEqual(strips[0].BorderColor, strips[1].BorderColor);
            }
        }

        [TestMethod]
        public void Render_WithoutLevels_LeavesNoStripLines()
        {
            using (Chart chart = CreateChart())
            {
                chart.ChartAreas[0].AxisY.StripLines.Add(new StripLine { IntervalOffset = 42d });

                new ChartRenderer().Render(chart, ViewModel());

                // Прошлые полосы обязаны очищаться, иначе они накапливаются при каждой перерисовке.
                Assert.AreEqual(0, chart.ChartAreas[0].AxisY.StripLines.Count);
            }
        }
    }
}
