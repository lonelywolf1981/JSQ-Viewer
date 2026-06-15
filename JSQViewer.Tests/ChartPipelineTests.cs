using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Charting;
using JSQViewer.Core;
using JSQViewer.Infrastructure.Cache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ChartPipelineTests
    {
        [TestMethod]
        public void DynamicsForecastService_BuildsFuncBasedFullForecastFromStart()
        {
            var service = new DynamicsForecastService();
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::A-01",
                LegendText = "[old FUNC] A-01",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 1d, 2d, 3d },
                YValues = new[] { 20d, 15d, 10d, 5d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::A-01",
                LegendText = "[old FULL] A-01",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 1d, 2d, 3d },
                YValues = new[] { 20d, 17d, 14d, 12d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::A-01",
                LegendText = "[new FUNC] A-01",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 1d, 2d, 3d },
                YValues = new[] { 20d, 15d, 10d, 5d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::A-01", "old-full::A-01", "new-func::A-01"));

            Assert.IsNotNull(forecast);
            Assert.IsTrue(forecast.IsForecast);
            CollectionAssert.AreEqual(new[] { 0d, 1d, 2d, 3d }, forecast.XValues);
            Assert.AreEqual(20d, forecast.YValues[0], 1e-9);
            Assert.AreEqual(17d, forecast.YValues[1], 1e-9);
            Assert.AreEqual(14d, forecast.YValues[2], 1e-9);
            Assert.AreEqual(12d, forecast.YValues[3], 1e-9);
            StringAssert.Contains(forecast.LegendText, "Прогноз");
        }

        [TestMethod]
        public void DynamicsForecastService_StopsAtFirstFullMinimum()
        {
            var service = new DynamicsForecastService();
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 1d, 2d, 3d, 4d, 5d, 6d },
                YValues = new[] { 10d, 8d, 7d, 6d, 6d, 6d, 6d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 1d, 2d, 3d, 4d, 5d, 6d },
                YValues = new[] { 20d, 15d, 13d, 10d, 8d, 11d, 8d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 11d, 10d, 9d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            Assert.AreEqual(5, forecast.YValues.Length);
            Assert.AreEqual(-1d, forecast.YValues[forecast.YValues.Length - 1], 1e-9);
        }

        [TestMethod]
        public void DynamicsForecastService_NormalizesOldPairStartTemperatures()
        {
            var service = new DynamicsForecastService();
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 25d, 20d, 15d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 32d, 27d, 22d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 31d, 21d, 11d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            CollectionAssert.AreEqual(new[] { 0d, 1d, 2d }, forecast.XValues);
            Assert.AreEqual(31d, forecast.YValues[0], 1e-9);
            Assert.AreEqual(26d, forecast.YValues[1], 1e-9);
            Assert.AreEqual(21d, forecast.YValues[2], 1e-9);
        }

        [TestMethod]
        public void DynamicsForecastService_ShiftsFullTemplateToNewFuncStartWithoutTimeCompression()
        {
            var service = new DynamicsForecastService(1.0);
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 30d, 15d, 5d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 10d, 20d },
                YValues = new[] { 33d, 18d, 8d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 4d },
                YValues = new[] { 31d, 5d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            CollectionAssert.AreEqual(new[] { 0d, 24d, 40d }, forecast.XValues);
            Assert.AreEqual(31d, forecast.YValues[0], 1e-9);
            Assert.AreEqual(16d, forecast.YValues[1], 1e-9);
            Assert.AreEqual(6d, forecast.YValues[2], 1e-9);
        }

        [TestMethod]
        public void DynamicsForecastService_MapsT1FullTimeByFuncToFullProgressRatio()
        {
            var service = new DynamicsForecastService(1.0);
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 30d, 20d, 10d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 5d, 10d },
                YValues = new[] { 30d, 20d, 10d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 2d, 4d },
                YValues = new[] { 32d, 22d, 12d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            CollectionAssert.AreEqual(new[] { 0d, 10d, 20d }, forecast.XValues);
            Assert.AreEqual(32d, forecast.YValues[0], 1e-9);
            Assert.AreEqual(22d, forecast.YValues[1], 1e-9);
            Assert.AreEqual(12d, forecast.YValues[2], 1e-9);
        }

        [TestMethod]
        public void DynamicsForecastService_WarnsWhenReferenceFuncStartAndDurationMismatch()
        {
            var service = new DynamicsForecastService();
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 25d, 15d, 5d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 32d, 20d, 10d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 2.5d, 5d },
                YValues = new[] { 31d, 16d, 6d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            Assert.IsNotNull(forecast.ForecastWarnings);
            var codes = forecast.ForecastWarnings.Select(w => w.Code).ToList();
            CollectionAssert.Contains(codes, DynamicsForecastWarningCode.ReferenceFuncStartTemperatureMismatch);
            CollectionAssert.Contains(codes, DynamicsForecastWarningCode.ReferenceFuncDurationMismatch);
        }

        [TestMethod]
        public void DynamicsForecastService_NoWarningsForComparableReferenceFunc()
        {
            var service = new DynamicsForecastService();
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 30d, 20d, 10d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 32d, 22d, 12d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 31d, 21d, 11d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            Assert.IsNotNull(forecast.ForecastWarnings);
            Assert.AreEqual(0, forecast.ForecastWarnings.Count);
        }

        [TestMethod]
        public void DynamicsForecastService_DampingZeroKeepsFullTemplateTimeline()
        {
            var service = new DynamicsForecastService(0.0);
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 30d, 20d, 10d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 5d, 10d },
                YValues = new[] { 30d, 20d, 10d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 2d, 4d },
                YValues = new[] { 32d, 22d, 12d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            // With damping 0 the FUNC speed ratio (here 2x slower) is ignored and the
            // forecast keeps the old FULL timeline instead of stretching it.
            CollectionAssert.AreEqual(new[] { 0d, 5d, 10d }, forecast.XValues);
        }

        [TestMethod]
        public void DynamicsForecastService_DefaultDampingAppliesPartialFuncWarpExponent()
        {
            var service = new DynamicsForecastService();
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 30d, 20d, 10d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 5d, 10d },
                YValues = new[] { 30d, 20d, 10d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 2d, 4d },
                YValues = new[] { 32d, 22d, 12d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            // FUNC speed ratio = 2; default damping 0.45 -> warp factor 2^0.45 (~1.366)
            // instead of the raw 2.0, so the old FULL timeline (0,5,10) is stretched by
            // 2^0.45 rather than doubled.
            double factor = Math.Pow(2d, 0.45);
            Assert.AreEqual(0d, forecast.XValues[0], 1e-9);
            Assert.AreEqual(5d * factor, forecast.XValues[1], 1e-6);
            Assert.AreEqual(10d * factor, forecast.XValues[2], 1e-6);
        }

        [TestMethod]
        public void DynamicsForecastService_StartsAtNewFuncTemperatureWhenOldFullStartsHigherThanOldFunc()
        {
            var service = new DynamicsForecastService();
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 25d, 15d, 5d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 5d, 10d },
                YValues = new[] { 32d, 22d, 12d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 2d, 4d },
                YValues = new[] { 31d, 21d, 11d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            Assert.AreEqual(31d, forecast.YValues[0], 1e-9);
            Assert.AreEqual(21d, forecast.YValues[1], 1e-9);
            Assert.AreEqual(11d, forecast.YValues[2], 1e-9);
        }

        [TestMethod]
        public void DynamicsForecastService_ContinuesFromFullWhenOldFuncIsShorterThanTarget()
        {
            var service = new DynamicsForecastService(1.0);
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 0.1d, 0.2d },
                YValues = new[] { 30d, 12d, 5d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 0.1d, 0.2d, 1d, 2d, 3d, 4d },
                YValues = new[] { 25d, 22d, 21d, 18d, 16d, 19d, 15d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 1d, 2d, 3d, 4d },
                YValues = new[] { 32d, 12d, 6d, 4d, 3d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            Assert.AreEqual(7, forecast.XValues.Length);
            Assert.AreEqual(80d, forecast.XValues[6], 1e-9);
            Assert.AreEqual(32d, forecast.YValues[0], 1e-9);
            Assert.IsTrue(forecast.YValues[5] > forecast.YValues[4]);
            Assert.IsTrue(forecast.YValues[5] > forecast.YValues[6]);
        }

        [TestMethod]
        public void DynamicsForecastService_ShiftsLoadedTemplateToNewFuncStartAndPreservesTimeScale()
        {
            var service = new DynamicsForecastService(1.0);
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old empty FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 30d, 20d, 10d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old loaded FULL",
                XValues = new[] { 0d, 1d, 2d, 3d, 4d },
                YValues = new[] { 25d, 21d, 17d, 20d, 16d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 2d, 4d },
                YValues = new[] { 30d, 26d, 22d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            CollectionAssert.AreEqual(new[] { 0d, 2d, 4d, 6d, 8d }, forecast.XValues);
            Assert.AreEqual(30d, forecast.YValues[0], 1e-9);
            Assert.AreEqual(26d, forecast.YValues[1], 1e-9);
            Assert.AreEqual(22d, forecast.YValues[2], 1e-9);
            Assert.AreEqual(25d, forecast.YValues[3], 1e-9);
            Assert.AreEqual(21d, forecast.YValues[4], 1e-9);
            Assert.IsTrue(forecast.YValues[3] > forecast.YValues[2]);
            Assert.IsTrue(forecast.YValues[3] > forecast.YValues[4]);
        }

        [TestMethod]
        public void DynamicsForecastService_PreservesFullTemplateTimeWhenFuncRatesDiffer()
        {
            var service = new DynamicsForecastService(1.0);
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old empty FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 30d, 20d, 10d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old loaded FULL",
                XValues = new[] { 0d, 1d, 2d, 3d, 4d },
                YValues = new[] { 24d, 20d, 16d, 19d, 15d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 3d, 4d },
                YValues = new[] { 30d, 20d, 10d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            CollectionAssert.AreEqual(new[] { 0d, 3d, 4.25d, 6.125d, 8d }, forecast.XValues);
            Assert.AreEqual(30d, forecast.YValues[0], 1e-9);
            Assert.AreEqual(26d, forecast.YValues[1], 1e-9);
            Assert.AreEqual(22d, forecast.YValues[2], 1e-9);
            Assert.AreEqual(25d, forecast.YValues[3], 1e-9);
            Assert.AreEqual(21d, forecast.YValues[4], 1e-9);
        }

        [TestMethod]
        public void DynamicsForecastService_SpreadsDefrostReboundAcrossTimeWithoutDuplicateX()
        {
            var service = new DynamicsForecastService(1.0);
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old empty FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 30d, 20d, 10d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old loaded FULL",
                XValues = new[] { 0d, 1d, 2d, 3d, 4d },
                YValues = new[] { 25d, 21d, 17d, 20d, 16d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 2d, 4d },
                YValues = new[] { 30d, 26d, 22d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            // Defrost rebound point (index 3) must spread across time instead of
            // collapsing onto the previous X, while keeping the temperature peak.
            CollectionAssert.AreEqual(new[] { 0d, 2d, 4d, 6d, 8d }, forecast.XValues);
            for (int i = 1; i < forecast.XValues.Length; i++)
            {
                Assert.IsTrue(
                    forecast.XValues[i] > forecast.XValues[i - 1],
                    "Forecast X values must be strictly increasing (no vertical artifacts).");
            }

            Assert.AreEqual(25d, forecast.YValues[3], 1e-9);
            Assert.IsTrue(forecast.YValues[3] > forecast.YValues[2]);
            Assert.IsTrue(forecast.YValues[3] > forecast.YValues[4]);
        }

        [TestMethod]
        public void DynamicsForecastService_FollowsNewFuncInItsRegionAndKeepsLaterFullDefrosts()
        {
            var service = new DynamicsForecastService();
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old FUNC",
                XValues = new[] { 0d, 1d, 2d, 3d },
                YValues = new[] { 30d, 20d, 10d, 0d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old FULL",
                XValues = new[] { 0d, 1d, 2d, 3d, 4d, 5d, 6d },
                // Early rebound (index 2: 24 -> 26) lands inside the new FUNC region.
                // Late rebound (index 5: 12 -> 14) lands beyond it.
                YValues = new[] { 30d, 24d, 26d, 18d, 12d, 14d, 6d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 1d, 2d, 3d },
                YValues = new[] { 30d, 20d, 10d, 0d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            CollectionAssert.AreEqual(new[] { 0d, 1d, 2d, 3d, 4d, 5d, 6d }, forecast.XValues);

            // Inside the new FUNC region (X <= 3) the early FULL rebound is suppressed:
            // the forecast falls monotonically instead of humping up.
            CollectionAssert.AreEqual(new[] { 30d, 24d, 24d, 18d }, forecast.YValues.Take(4).ToArray());
            for (int i = 1; i <= 3; i++)
            {
                Assert.IsTrue(
                    forecast.YValues[i] <= forecast.YValues[i - 1] + 1e-9,
                    "Forecast must fall monotonically while the new FUNC has data.");
            }

            // Beyond the new FUNC region the FULL defrost is preserved.
            Assert.AreEqual(14d, forecast.YValues[5], 1e-9);
            Assert.IsTrue(forecast.YValues[5] > forecast.YValues[4]);
            Assert.IsTrue(forecast.YValues[5] > forecast.YValues[6]);
        }

        [TestMethod]
        public void DynamicsForecastService_DecaysStartOffsetByLoadedCoolingProgress()
        {
            var service = new DynamicsForecastService();
            var oldFunc = new ChartPipelineSeries
            {
                Code = "old-func::C-T1",
                LegendText = "[old FUNC] C-T1",
                SourceRoot = "C:\\tests\\old empty FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 30d, 20d, 10d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "old-full::C-T1",
                LegendText = "[old FULL] C-T1",
                SourceRoot = "C:\\tests\\old loaded FULL",
                XValues = new[] { 0d, 1d, 2d, 3d },
                YValues = new[] { 25d, 15d, 5d, 7d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "new-func::C-T1",
                LegendText = "[new FUNC] C-T1",
                SourceRoot = "C:\\tests\\new FUNC",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 35d, 25d, 15d }
            };

            ChartPipelineSeries forecast = service.BuildForecast(
                new[] { oldFunc, oldFull, newFunc },
                new DynamicsForecastRoleSelection("old-func::C-T1", "old-full::C-T1", "new-func::C-T1"));

            Assert.IsNotNull(forecast);
            Assert.AreEqual(3, forecast.YValues.Length);
            Assert.AreEqual(35d, forecast.YValues[0], 1e-9);
            Assert.AreEqual(25d, forecast.YValues[1], 1e-9);
            Assert.AreEqual(15d, forecast.YValues[2], 1e-9);
        }

        [TestMethod]
        public void DynamicsForecastService_UsesExplicitRoleCodesInsteadOfNameDetection()
        {
            var service = new DynamicsForecastService();
            var oldFunc = new ChartPipelineSeries
            {
                Code = "series-a",
                LegendText = "[recording A] C-T1",
                SourceRoot = "C:\\tests\\recording A",
                XValues = new[] { 0d, 1d, 2d, 3d },
                YValues = new[] { 10d, 8d, 7d, 6d }
            };
            var oldFull = new ChartPipelineSeries
            {
                Code = "series-b",
                LegendText = "[recording B] C-T1",
                SourceRoot = "C:\\tests\\recording B",
                XValues = new[] { 0d, 1d, 2d, 3d },
                YValues = new[] { 20d, 15d, 13d, 10d }
            };
            var newFunc = new ChartPipelineSeries
            {
                Code = "series-c",
                LegendText = "[recording C] C-T1",
                SourceRoot = "C:\\tests\\recording C",
                XValues = new[] { 0d, 1d, 2d },
                YValues = new[] { 11d, 10d, 9d }
            };
            var roles = new DynamicsForecastRoleSelection("series-a", "series-b", "series-c");

            ChartPipelineSeries forecast = service.BuildForecast(new[] { oldFunc, oldFull, newFunc }, roles);

            Assert.IsNotNull(forecast);
            Assert.AreEqual("series-c::forecast", forecast.Code);
            Assert.AreEqual(4, forecast.XValues.Length);
            Assert.AreEqual(0d, forecast.XValues[0], 1e-9);
        }

        [TestMethod]
        public void ResolveStep_UsesManualValueWhenAutoStepIsDisabled()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var request = ChartPipelineRequest.ForChart(
                SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L, 2000L, 3000L }),
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 3,
                targetPoints: 5000,
                selectedChannelCount: 1);

            ChartPipelineResult result = service.Execute(request);

            Assert.AreEqual(3, result.Step);
        }

        [TestMethod]
        public void ResolveStep_CapsTargetPointsByChannelCount()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var request = ChartPipelineRequest.ForChart(
                SessionAndChartingTestData.CreateData(Enumerable.Range(0, 50000).Select(i => (long)i).ToArray()),
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: true,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 12);

            ChartPipelineResult result = service.Execute(request);

            Assert.AreEqual(12, result.Step);
        }

        [TestMethod]
        public void ResolveStep_ForcesStepOneForMultiSourceSelections()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var data = SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L, 2000L, 3000L });
            data.SourceColumns["source-a"] = new[] { "A-01" };
            data.SourceColumns["source-b"] = new[] { "B-01" };
            data.CodeSources["A-01"] = "C:\\tests\\source-a\\";
            data.CodeSources["B-01"] = "C:\\tests\\source-b\\";
            data.SourceStartMs["C:\\tests\\source-a\\"] = 0L;
            data.SourceEndMs["C:\\tests\\source-a\\"] = 3000L;
            data.SourceStartMs["C:\\tests\\source-b\\"] = 0L;
            data.SourceEndMs["C:\\tests\\source-b\\"] = 3000L;

            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01", "B-01" },
                overlayMode: true,
                dataVersion: 1,
                autoStepEnabled: true,
                manualStep: 5,
                targetPoints: 5000,
                selectedChannelCount: 2);

            ChartPipelineResult result = service.Execute(request);

            Assert.AreEqual(1, result.Step);
        }

        [TestMethod]
        public void Execute_AppendsFuncBasedForecastSeries_WhenRequestedInOverlayMode()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var data = SessionAndChartingTestData.CreateData(
                new long[] { 0L, 3600000L, 7200000L, 10800000L },
                new Dictionary<string, double?[]>
                {
                    ["old-func::A-01"] = new double?[] { 20d, 15d, 10d, 5d },
                    ["old-full::A-01"] = new double?[] { 20d, 17d, 14d, 12d },
                    ["new-func::A-01"] = new double?[] { 20d, 15d, 10d, 5d }
                });
            data.SourceColumns["old FUNC"] = new[] { "old-func::A-01" };
            data.SourceColumns["old FULL"] = new[] { "old-full::A-01" };
            data.SourceColumns["new FUNC"] = new[] { "new-func::A-01" };
            data.CodeSources["old-func::A-01"] = "old FUNC";
            data.CodeSources["old-full::A-01"] = "old FULL";
            data.CodeSources["new-func::A-01"] = "new FUNC";
            data.SourceStartMs["old FUNC"] = 0L;
            data.SourceEndMs["old FUNC"] = 10800000L;
            data.SourceStartMs["old FULL"] = 0L;
            data.SourceEndMs["old FULL"] = 10800000L;
            data.SourceStartMs["new FUNC"] = 0L;
            data.SourceEndMs["new FUNC"] = 7200000L;

            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "old-func::A-01", "old-full::A-01", "new-func::A-01" },
                overlayMode: true,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 2,
                includeDynamicsForecast: true);

            ChartPipelineResult result = service.Execute(request);

            Assert.AreEqual(4, result.Series.Count);
            ChartPipelineSeries forecast = result.Series.Single(series => series.IsForecast);
            CollectionAssert.AreEqual(new[] { 0d, 1d, 2d, 3d }, forecast.XValues);
            Assert.AreEqual(20d, forecast.YValues[0], 1e-9);
            Assert.AreEqual(17d, forecast.YValues[1], 1e-9);
            Assert.AreEqual(14d, forecast.YValues[2], 1e-9);
            Assert.AreEqual(12d, forecast.YValues[3], 1e-9);
        }

        [TestMethod]
        public void Execute_PassesExplicitForecastRolesToForecastService()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var data = SessionAndChartingTestData.CreateData(
                new long[] { 0L, 3600000L, 7200000L, 10800000L },
                new Dictionary<string, double?[]>
                {
                    ["source-a::C-T1"] = new double?[] { 10d, 8d, 7d, 6d },
                    ["source-b::C-T1"] = new double?[] { 20d, 15d, 13d, 10d },
                    ["source-c::C-T1"] = new double?[] { 11d, 10d, 9d, null }
                });

            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "source-a::C-T1", "source-b::C-T1", "source-c::C-T1" },
                overlayMode: true,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 3,
                includeDynamicsForecast: true,
                dynamicsForecastRoleSelection: new DynamicsForecastRoleSelection("source-a::C-T1", "source-b::C-T1", "source-c::C-T1"));

            ChartPipelineResult result = service.Execute(request);

            Assert.IsTrue(result.Series.Any(series => series.IsForecast));
        }

        [TestMethod]
        public void Execute_OverlayMode_UsesSourceStartAsRelativeZero()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var data = SessionAndChartingTestData.CreateData(
                new long[] { 0L, 3600000L, 7200000L },
                new Dictionary<string, double?[]>
                {
                    ["A-01"] = new double?[] { null, 10d, 20d }
                });
            string source = "C:\\tests\\root\\";
            data.SourceColumns[source] = new[] { "A-01" };
            data.CodeSources["A-01"] = source;
            data.SourceStartMs[source] = 0L;
            data.SourceEndMs[source] = 7200000L;

            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01" },
                overlayMode: true,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1);

            ChartPipelineResult result = service.Execute(request);

            Assert.AreEqual(2, result.Series.Single().XValues.Length);
            Assert.AreEqual(1d, result.Series.Single().XValues[0], 1e-9);
            Assert.AreEqual(2d, result.Series.Single().XValues[1], 1e-9);
            Assert.AreEqual(2d, result.DataMaximum, 1e-9);
        }

        [TestMethod]
        public void ResolveLegendText_UsesSourceNameWhenMultipleSourcesAreLoaded()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var data = SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L });
            data.SourceColumns["source-a"] = new[] { "A-01" };
            data.SourceColumns["source-b"] = new[] { "B-01" };
            data.CodeSources["A-01"] = "C:\\tests\\source-a\\";

            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1);

            ChartPipelineResult result = service.Execute(request);

            Assert.AreEqual("[source-a] A-01", result.Series.Single().LegendText);
        }

        [TestMethod]
        public void ResolveLegendText_RemovesMergedSourcePrefixFromDisplayCode()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var data = SessionAndChartingTestData.CreateData(
                new long[] { 0L, 1000L },
                new Dictionary<string, double?[]>
                {
                    ["source-a::A-01"] = new double?[] { 1d, 2d }
                });
            data.SourceColumns["source-a"] = new[] { "source-a::A-01" };
            data.SourceColumns["source-b"] = new[] { "source-b::A-01" };
            data.CodeSources["source-a::A-01"] = "C:\\tests\\source-a\\";

            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "source-a::A-01" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1);

            ChartPipelineResult result = service.Execute(request);

            Assert.AreEqual("[source-a] A-01", result.Series.Single().LegendText);
        }

        [TestMethod]
        public void Execute_UsesCachedSeriesSliceService()
        {
            var cache = new CountingSeriesSliceCache();
            var sliceService = new SeriesSliceService(cache, new TimestampRangeService());
            var service = new ChartPipelineService(sliceService);
            var data = SessionAndChartingTestData.CreateData(
                new long[] { 0L, 1000L, 2000L },
                new Dictionary<string, double?[]>
                {
                    ["A-01"] = new double?[] { 1d, 2d, 3d }
                });
            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 7,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1);

            ChartPipelineResult first = service.Execute(request);
            ChartPipelineResult second = service.Execute(request);

            Assert.AreEqual(0d, first.Series.Single().XValues[0]);
            Assert.AreEqual(first.Series.Single().XValues[0], second.Series.Single().XValues[0]);
            Assert.AreEqual(1, cache.SetCount);
            Assert.IsTrue(cache.HitCount >= 1);
        }

        [TestMethod]
        public void Execute_UsesThinOnePixelLineForRenderedSeries()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var data = SessionAndChartingTestData.CreateData(
                new long[] { 0L, 1000L },
                new Dictionary<string, double?[]>
                {
                    ["A-01"] = new double?[] { 1d, 2d },
                    ["A-02"] = new double?[] { 3d, 4d }
                });
            var request = ChartPipelineRequest.ForChart(
                data,
                new[] { "A-01", "A-02" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 2);

            ChartPipelineResult result = service.Execute(request);

            Assert.IsTrue(result.Series.All(series => series.BorderWidth == 1));
        }

        [TestMethod]
        public void Execute_PropagatesManualXAxisSettings_WhenEnabled()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var request = ChartPipelineRequest.ForChart(
                SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L, 2000L, 3000L }),
                new[] { "A-01" },
                overlayMode: true,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1,
                xAxisSettings: ChartAxisSettings.ForManual(minimum: 0.5, maximum: 2.5, interval: 0.25));

            ChartPipelineResult result = service.Execute(request);

            Assert.IsTrue(result.XAxis.IsManualEnabled);
            Assert.AreEqual(0.5, result.XAxis.Minimum.GetValueOrDefault(), 1e-9);
            Assert.AreEqual(2.5, result.XAxis.Maximum.GetValueOrDefault(), 1e-9);
            Assert.AreEqual(0.25, result.XAxis.Interval.GetValueOrDefault(), 1e-9);
        }

        [TestMethod]
        public void Execute_PropagatesManualYAxisSettings_WhenEnabled()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var request = ChartPipelineRequest.ForChart(
                SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L, 2000L, 3000L }),
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1,
                yAxisSettings: ChartAxisSettings.ForManual(minimum: 10.0, maximum: 40.0, interval: 5.0));

            ChartPipelineResult result = service.Execute(request);

            Assert.IsTrue(result.YAxis.IsManualEnabled);
            Assert.AreEqual(10.0, result.YAxis.Minimum.GetValueOrDefault(), 1e-9);
            Assert.AreEqual(40.0, result.YAxis.Maximum.GetValueOrDefault(), 1e-9);
            Assert.AreEqual(5.0, result.YAxis.Interval.GetValueOrDefault(), 1e-9);
        }

        [TestMethod]
        public void Execute_KeepsAutomaticAxisBehavior_WhenManualAxisModeIsDisabled()
        {
            var service = new ChartPipelineService(new SeriesSliceService(new MemorySeriesSliceCache(), new TimestampRangeService()));
            var request = ChartPipelineRequest.ForChart(
                SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L, 2000L, 3000L }),
                new[] { "A-01" },
                overlayMode: false,
                dataVersion: 1,
                autoStepEnabled: false,
                manualStep: 1,
                targetPoints: 5000,
                selectedChannelCount: 1,
                xAxisSettings: ChartAxisSettings.ForManual(minimum: 1.0, maximum: 2.0, interval: 0.5).Disable(),
                yAxisSettings: ChartAxisSettings.ForManual(minimum: 10.0, maximum: 20.0, interval: 1.0).Disable());

            ChartPipelineResult result = service.Execute(request);

            Assert.IsFalse(result.XAxis.IsManualEnabled);
            Assert.IsFalse(result.XAxis.Minimum.HasValue);
            Assert.IsFalse(result.XAxis.Maximum.HasValue);
            Assert.IsFalse(result.XAxis.Interval.HasValue);
            Assert.IsFalse(result.YAxis.IsManualEnabled);
            Assert.IsFalse(result.YAxis.Minimum.HasValue);
            Assert.IsFalse(result.YAxis.Maximum.HasValue);
            Assert.IsFalse(result.YAxis.Interval.HasValue);
        }
    }

    internal sealed class CountingSeriesSliceCache : ISeriesSliceCache
    {
        private readonly Dictionary<string, SeriesSlice> _items = new Dictionary<string, SeriesSlice>(StringComparer.OrdinalIgnoreCase);

        public int HitCount { get; private set; }

        public int SetCount { get; private set; }

        public bool TryGet(string key, out SeriesSlice slice)
        {
            bool found = _items.TryGetValue(key, out slice);
            if (found)
            {
                HitCount++;
            }

            return found;
        }

        public void Set(string key, SeriesSlice slice)
        {
            _items[key] = slice;
            SetCount++;
        }

        public void Clear()
        {
            _items.Clear();
        }
    }
}
