using System.Collections.Generic;
using System.Drawing;
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
        public void Render_WhenTwoSourcesShareARole_SecondMaximumGoesToTheRight()
        {
            using (Chart chart = CreateChart())
            {
                new ChartRenderer().Render(chart, ViewModel(
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Maximum, Value = 33.0, Label = "a" },
                    new ChartLevelLineViewModel { SourceIndex = 1, Role = ChartSeriesRole.T8Maximum, Value = 32.2, Label = "b" }));

                StripLine[] strips = chart.ChartAreas[0].AxisY.StripLines.Cast<StripLine>().ToArray();

                // Первая подпись слева, вторая справа — иначе при разнице в градус
                // они полностью перекрывают друг друга. Обе остаются над линией.
                Assert.AreEqual(StringAlignment.Near, strips[0].TextAlignment);
                Assert.AreEqual(StringAlignment.Far, strips[1].TextAlignment);
                Assert.AreEqual(StringAlignment.Far, strips[0].TextLineAlignment);
                Assert.AreEqual(StringAlignment.Far, strips[1].TextLineAlignment);
            }
        }

        [TestMethod]
        public void Render_WhenTwoSourcesShareMinimum_SecondLabelGoesToTheRight()
        {
            using (Chart chart = CreateChart())
            {
                new ChartRenderer().Render(chart, ViewModel(
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Minimum, Value = 0.5, Label = "a" },
                    new ChartLevelLineViewModel { SourceIndex = 1, Role = ChartSeriesRole.T8Minimum, Value = 0.3, Label = "b" }));

                StripLine[] strips = chart.ChartAreas[0].AxisY.StripLines.Cast<StripLine>().ToArray();

                // Минимум ведёт себя так же, как остальные роли: вторая подпись вправо.
                Assert.AreEqual(StringAlignment.Near, strips[0].TextAlignment);
                Assert.AreEqual(StringAlignment.Far, strips[1].TextAlignment);
                Assert.AreEqual(StringAlignment.Far, strips[0].TextLineAlignment);
                Assert.AreEqual(StringAlignment.Far, strips[1].TextLineAlignment);
            }
        }

        [TestMethod]
        public void Render_PlacementIsPerRole_NotGlobal()
        {
            using (Chart chart = CreateChart())
            {
                new ChartRenderer().Render(chart, ViewModel(
                    new ChartLevelLineViewModel { SourceIndex = 1, Role = ChartSeriesRole.T8Average, Value = 7.0, Label = "b-avg" },
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Average, Value = 8.0, Label = "a-avg" },
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Maximum, Value = 20.0, Label = "a-max" }));

                StripLine[] strips = chart.ChartAreas[0].AxisY.StripLines.Cast<StripLine>().ToArray();

                // Порядок устойчив: сначала роль, затем источник. Счётчик мест
                // ведётся отдельно по каждой роли, поэтому единственный максимум
                // остаётся слева, хотя средних уже две.
                Assert.AreEqual("a-avg", strips[0].Text);
                Assert.AreEqual(StringAlignment.Near, strips[0].TextAlignment);
                Assert.AreEqual("b-avg", strips[1].Text);
                Assert.AreEqual(StringAlignment.Far, strips[1].TextAlignment);
                Assert.AreEqual("a-max", strips[2].Text);
                Assert.AreEqual(StringAlignment.Near, strips[2].TextAlignment);
                Assert.AreEqual(StringAlignment.Far, strips[2].TextLineAlignment);
            }
        }

        [TestMethod]
        public void Render_WhenLevelSitsBelowEveryShownSeries_ExtendsTheAxisToReachIt()
        {
            using (Chart chart = CreateChart())
            {
                // Воспроизводит случай с реального прогона: на графике показаны
                // только тёплые термопары, а минимум группы принадлежит скрытой
                // и лежит ниже нижней границы автоматической шкалы.
                new ChartRenderer().Render(chart, ViewModel(
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Minimum, Value = -4.0, Label = "T8+ мин -4.0 (T26)" }));

                Axis axisY = chart.ChartAreas[0].AxisY;

                Assert.IsFalse(double.IsNaN(axisY.Minimum), "нижняя граница осталась автоматической");
                Assert.IsTrue(axisY.Minimum < -4.0, "уровень -4.0 не попал в область, минимум оси " + axisY.Minimum);
                // Верхняя граница не тронута: подписи оси сохраняют круглые значения.
                Assert.IsTrue(double.IsNaN(axisY.Maximum));
            }
        }

        [TestMethod]
        public void Render_WhenLevelSitsAboveEveryShownSeries_ExtendsTheUpperBound()
        {
            using (Chart chart = CreateChart())
            {
                new ChartRenderer().Render(chart, ViewModel(
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Maximum, Value = 40.0, Label = "T8+ макс 40.0 (T27)" }));

                Axis axisY = chart.ChartAreas[0].AxisY;

                Assert.IsTrue(axisY.Maximum > 40.0, "уровень 40.0 не попал в область, максимум оси " + axisY.Maximum);
                Assert.IsTrue(double.IsNaN(axisY.Minimum));
            }
        }

        [TestMethod]
        public void Render_WhenLevelIsInsideSeriesRange_LeavesAxisAutomatic()
        {
            using (Chart chart = CreateChart())
            {
                // Серии в ViewModel идут от 5 до 6, уровень между ними.
                new ChartRenderer().Render(chart, ViewModel(
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Average, Value = 5.5, Label = "T8+ сред 5.5" }));

                Axis axisY = chart.ChartAreas[0].AxisY;

                Assert.IsTrue(double.IsNaN(axisY.Minimum));
                Assert.IsTrue(double.IsNaN(axisY.Maximum));
            }
        }

        [TestMethod]
        public void Render_WithManualYAxis_DoesNotOverrideTheUserBounds()
        {
            using (Chart chart = CreateChart())
            {
                ChartViewModel model = ViewModel(
                    new ChartLevelLineViewModel { SourceIndex = 0, Role = ChartSeriesRole.T8Minimum, Value = -4.0, Label = "a" });
                model.YAxis = new ChartAxisSettingsViewModel { IsManualEnabled = true, Minimum = 0d, Maximum = 10d };

                new ChartRenderer().Render(chart, model);

                Axis axisY = chart.ChartAreas[0].AxisY;

                // Заданные вручную границы важнее видимости уровня.
                Assert.AreEqual(0d, axisY.Minimum, 1e-9);
                Assert.AreEqual(10d, axisY.Maximum, 1e-9);
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
