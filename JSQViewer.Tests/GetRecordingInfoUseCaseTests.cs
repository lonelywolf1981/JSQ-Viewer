using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Recording;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class GetRecordingInfoUseCaseTests
    {
        private static readonly string Root = @"C:\Data\Test";

        // Fake ITestMetadataReader — возвращает заранее заданные метаданные
        private sealed class FakeMetadataReader : ITestMetadataReader
        {
            private readonly Dictionary<string, string> _meta;
            public FakeMetadataReader(Dictionary<string, string> meta)
            {
                _meta = meta ?? new Dictionary<string, string>();
            }
            public Dictionary<string, string> Read(string root)
            {
                return _meta;
            }
        }

        private static TestData MakeData(string root, long startMs, long endMs,
            string columnName, double?[] values)
        {
            int count = values.Length;
            long[] timestamps = new long[count];
            for (int i = 0; i < count; i++)
                timestamps[i] = startMs + (count > 1 ? (long)((endMs - startMs) * i / (double)(count - 1)) : 0);

            return new TestData
            {
                RowCount = count,
                TimestampsMs = timestamps,
                ColumnNames = new[] { columnName },
                Columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    { [columnName] = values },
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                    { [root] = new[] { columnName } },
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                    { [root] = startMs },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                    { [root] = endMs },
                Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    { ["Модель"] = "KA140", ["Хладагент"] = "R600a" }
            };
        }

        private static TestData MakeData(string root, long[] timestamps,
            Dictionary<string, double?[]> columns, string[] sourceColumns = null)
        {
            return new TestData
            {
                RowCount = timestamps.Length,
                TimestampsMs = timestamps,
                ColumnNames = columns.Keys.ToArray(),
                Columns = new Dictionary<string, double?[]>(columns, StringComparer.OrdinalIgnoreCase),
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                    { [root] = sourceColumns ?? columns.Keys.ToArray() },
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                    { [root] = timestamps[0] },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                    { [root] = timestamps[timestamps.Length - 1] },
                Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsCorrectMin()
        {
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { -20.0, -35.0, -38.4, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1Min);
            Assert.AreEqual(-38.4, r.T1Min.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsInitialTemperatureFromFirstValidPoint()
        {
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { null, 31.5, 20.0, 10.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.AreEqual(31.5, r.T1InitialTemperature.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsFirstCoolingMinimumBeforeLaterGlobalMinimum()
        {
            long[] timestamps = Enumerable.Range(0, 31)
                .Select(i => i * 10 * 60_000L)
                .ToArray();
            double?[] values =
            {
                32.0, 28.0, 24.0, 24.8, 20.0, 16.0, 12.0, 8.0,
                6.0, 7.2, 6.5, 7.4, 6.6, 7.5, 6.7, 7.6,
                6.8, 7.7, 6.9, 7.8, 6.7, 7.6, 6.6, 7.5,
                6.4, 7.4, 6.2, 7.2, 6.0, 7.0, 5.0
            };
            var data = MakeData(Root, timestamps, new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["T1"] = values
            });
            var ts = new TimestampRangeService();
            var uc = new GetRecordingInfoUseCase(ts);

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.AreEqual(5.0, r.T1Min.Value, 0.001);
            Assert.AreEqual(300 * 60_000L, r.T1MinElapsedMs.Value);

            Assert.IsNotNull(r.T1FirstCoolingMin);
            Assert.AreEqual(6.0, r.T1FirstCoolingMin.Value, 0.001);
            Assert.AreEqual(80 * 60_000L, r.T1FirstCoolingMinElapsedMs.Value);
            Assert.AreEqual(ts.UnixMsToLocalDateTime(80 * 60_000L), r.T1FirstCoolingMinTime.Value);
        }

        [TestMethod]
        public void Execute_WithT1FirstCoolingMinimum_ReturnsLowestPointInsideFirstReboundWindow()
        {
            long[] timestamps = Enumerable.Range(0, 31)
                .Select(i => i * 5 * 60_000L)
                .ToArray();
            double?[] values =
            {
                32.0, 20.0, 5.23, 5.20, 5.10, 4.96, 5.00, 5.20,
                5.80, 6.00, 6.10, 6.00, 5.90, 5.80, 5.70, 5.60,
                5.50, 5.40, 5.30, 5.20, 5.10, 5.00, 4.95, 5.10,
                5.20, 5.30, 5.40, 5.50, 5.20, 5.00, 4.80
            };
            var data = MakeData(Root, timestamps, new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["T1"] = values
            });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.AreEqual(4.80, r.T1Min.Value, 0.001);
            Assert.AreEqual(4.96, r.T1FirstCoolingMin.Value, 0.001);
            Assert.AreEqual(25 * 60_000L, r.T1FirstCoolingMinElapsedMs.Value);
        }

        [TestMethod]
        public void Execute_WithT1FirstCoolingMinimum_DropRateUsesFirstCoolingMinimum()
        {
            long[] timestamps = Enumerable.Range(0, 31)
                .Select(i => i * 10 * 60_000L)
                .ToArray();
            double?[] values =
            {
                32.0, 28.0, 24.0, 24.8, 20.0, 16.0, 12.0, 8.0,
                6.0, 7.2, 6.5, 7.4, 6.6, 7.5, 6.7, 7.6,
                6.8, 7.7, 6.9, 7.8, 6.7, 7.6, 6.6, 7.5,
                6.4, 7.4, 6.2, 7.2, 6.0, 7.0, 5.0
            };
            var data = MakeData(Root, timestamps, new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["T1"] = values
            });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.AreEqual(5.0, r.T1Min.Value, 0.001);
            Assert.AreEqual(6.0, r.T1FirstCoolingMin.Value, 0.001);
            Assert.AreEqual(-0.325, r.T1DropRatePerMinute.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithWChannel_ReturnsEnergyToFirstCoolingMinimum()
        {
            long[] timestamps = Enumerable.Range(0, 31)
                .Select(i => i * 10 * 60_000L)
                .ToArray();
            double?[] t1Values =
            {
                32.0, 28.0, 24.0, 24.8, 20.0, 16.0, 12.0, 8.0,
                6.0, 7.2, 6.5, 7.4, 6.6, 7.5, 6.7, 7.6,
                6.8, 7.7, 6.9, 7.8, 6.7, 7.6, 6.6, 7.5,
                6.4, 7.4, 6.2, 7.2, 6.0, 7.0, 5.0
            };
            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["A-T1"] = t1Values,
                ["A-W"] = Enumerable.Repeat<double?>(120.0, timestamps.Length).ToArray()
            };
            var data = MakeData(Root, timestamps, columns);
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.AreEqual(80 * 60_000L, r.T1EnergyTargetElapsedMs.Value);
            Assert.AreEqual(0.16, r.T1EnergyToTargetKWh.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithWChannel_WhenFirstCoolingMinimumMissing_ReturnsEnergyToGlobalMinimum()
        {
            long[] timestamps = new[] { 0L, 60 * 60_000L, 120 * 60_000L };
            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["C-T1"] = new double?[] { 40.0, 35.0, 25.0 },
                ["C-W"] = new double?[] { 100.0, 200.0, 300.0 }
            };
            var data = MakeData(Root, timestamps, columns);
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNull(r.T1FirstCoolingMin);
            Assert.AreEqual(120 * 60_000L, r.T1EnergyTargetElapsedMs.Value);
            Assert.AreEqual(0.4, r.T1EnergyToTargetKWh.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsCorrectMinTime()
        {
            long startMs = 1_000_000_000L;
            long endMs   = 1_000_060_000L;
            var data = MakeData(Root, startMs, endMs, "T1",
                new double?[] { -20.0, -35.0, -38.4, -30.0 });
            var ts = new TimestampRangeService();
            var uc = new GetRecordingInfoUseCase(ts);

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1MinTime);
            DateTime expected = ts.UnixMsToLocalDateTime(startMs + 40_000);
            Assert.AreEqual(expected, r.T1MinTime.Value);
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsLocalSourceStartTime()
        {
            long startMs = 1_000_000_000L;
            long endMs   = 1_000_060_000L;
            var data = MakeData(Root, startMs, endMs, "T1",
                new double?[] { -20.0, -35.0, -38.4, -30.0 });
            var ts = new TimestampRangeService();
            var uc = new GetRecordingInfoUseCase(ts);

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.SourceStartTime);
            Assert.AreEqual(ts.UnixMsToLocalDateTime(startMs), r.SourceStartTime.Value);
            Assert.AreEqual(DateTimeKind.Local, r.SourceStartTime.Value.Kind);
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsElapsedMs()
        {
            // минимум на позиции [2] — через 40000 мс после старта записи
            long startMs = 1_000_000_000L;
            long endMs   = 1_000_060_000L;
            var data = MakeData(Root, startMs, endMs, "T1",
                new double?[] { -20.0, -35.0, -38.4, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1MinElapsedMs);
            // elapsed = timestamps[2] - startMs = (startMs + 40000) - startMs = 40000
            Assert.AreEqual(40_000L, r.T1MinElapsedMs.Value);
        }

        [TestMethod]
        public void Execute_WithLeadingNullT1_ElapsedFromSourceStart()
        {
            // Первые два значения null (инициализация датчика), первое валидное на позиции [2]
            // Минимум на позиции [4]
            long startMs = 0L;
            long endMs   = 80_000L;
            var data = MakeData(Root, startMs, endMs, "T1",
                new double?[] { null, null, -10.0, -35.0, -40.0, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1MinElapsedMs);
            // timestamps[4] = 0 + 4*(80000/5) = 64000 (минимум)
            // elapsed = 64000 - SourceStartMs = 64000 мс — от старта открытой записи
            Assert.AreEqual(64_000L, r.T1MinElapsedMs.Value,
                "elapsed должен считаться от старта открытой записи, как подписано в окне информации");
        }

        [TestMethod]
        public void Execute_WithDoubleColonT1Format_FindsChannel()
        {
            // Формат слитых источников: "basename::T1"
            var data = MakeData(Root, 0, 60_000, "JSQ:28 ACTIVE LARGE KA140::T1",
                new double?[] { -10.0, -40.0, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1Min, "basename::T1 должен распознаваться как T1");
            Assert.AreEqual(-40.0, r.T1Min.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithDoubleColonPrefixedT1Format_FindsChannel()
        {
            // Формат слитых источников с однобуквенным префиксом: "basename::C-T1"
            var data = MakeData(Root, 0, 60_000, "JSQ:28 ACTIVE LARGE KA140::C-T1",
                new double?[] { -10.0, -40.0, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1Min, "basename::C-T1 должен распознаваться как T1");
            Assert.AreEqual(-40.0, r.T1Min.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithDuplicateSuffixT1Format_FindsChannel()
        {
            // Слияние без split, дубликат суффикса: "C-T1#2"
            var data = MakeData(Root, 0, 60_000, "C-T1#2",
                new double?[] { -10.0, -40.0, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1Min, "C-T1#2 должен распознаваться как T1");
            Assert.AreEqual(-40.0, r.T1Min.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsDropRate()
        {
            // first=-20, min=-38.4 at 40 seconds from start → rate = (-38.4 - (-20)) / (40/60) = -27.6
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { -20.0, -35.0, -38.4, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1DropRatePerMinute);
            Assert.AreEqual(-27.6, r.T1DropRatePerMinute.Value, 0.01);
        }

        [TestMethod]
        public void Execute_WithLeadingNullT1_DropRateUsesTimeFromSourceStartToMinimum()
        {
            // first valid T1=-10, min=-40 at 80000 ms from source start -> rate=-22.5 °C/мин.
            // Если ошибочно делить на всю длительность источника, получится -15.0.
            var data = MakeData(Root, 0, 120_000, "T1",
                new double?[] { null, -10.0, -40.0, -20.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1DropRatePerMinute);
            Assert.AreEqual(-22.5, r.T1DropRatePerMinute.Value, 0.01);
        }

        [TestMethod]
        public void Execute_WithPrefixedT1_FindsChannel()
        {
            var data = MakeData(Root, 0, 60_000, "A-T1",
                new double?[] { -10.0, -25.0, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1Min, "A-T1 должен распознаваться как T1");
            Assert.AreEqual(-30.0, r.T1Min.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithNoT1Channel_ReturnsNullStats()
        {
            var data = MakeData(Root, 0, 60_000, "P1",
                new double?[] { 1.0, 2.0, 3.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNull(r.T1Min);
            Assert.IsNull(r.T1MinTime);
            Assert.IsNull(r.T1DropRatePerMinute);
        }

        [TestMethod]
        public void Execute_ReturnsMeta_FallbackToDataMeta_WhenNoReader()
        {
            // Без reader — используется data.Meta (совместимость)
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { -10.0, -20.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.Meta);
            Assert.IsTrue(r.Meta.Any(kv => kv.Key == "Модель" && kv.Value == "KA140"));
            Assert.IsTrue(r.Meta.Any(kv => kv.Key == "Хладагент" && kv.Value == "R600a"));
        }

        [TestMethod]
        public void Execute_ReturnsMeta_FromReader_WhenReaderProvided()
        {
            // С reader — используются данные именно этого источника, не слитые
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { -10.0, -20.0 });
            // data.Meta содержит "Модель"="KA140" (источник A)
            // reader возвращает данные источника B: "Модель"="KA200"
            var sourceSpecificMeta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Модель"] = "KA200",
                ["Хладагент"] = "R290"
            };
            var uc = new GetRecordingInfoUseCase(
                new TimestampRangeService(),
                new FakeMetadataReader(sourceSpecificMeta));

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.Meta);
            Assert.IsTrue(r.Meta.Any(kv => kv.Key == "Модель" && kv.Value == "KA200"),
                "reader должен возвращать метаданные конкретного источника, не слитые");
        }

        [TestMethod]
        public void Execute_FiltersRecordingStartAndStopMetadata()
        {
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { -10.0, -20.0 });
            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Operator"] = "Administrator",
                ["StartedAt"] = "2026-05-04 10:13:33.926783+00:00",
                ["StoppedAt"] = "2026-05-05 03:40:59.500322+00:00"
            };
            var uc = new GetRecordingInfoUseCase(
                new TimestampRangeService(),
                new FakeMetadataReader(meta));

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsTrue(r.Meta.Any(kv => kv.Key == "Operator"));
            Assert.IsFalse(r.Meta.Any(kv => string.Equals(kv.Key, "StartedAt", StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(r.Meta.Any(kv => string.Equals(kv.Key, "StoppedAt", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void Execute_SingleRow_DropRateIsNull()
        {
            var data = MakeData(Root, 0, 0, "T1",
                new double?[] { -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNull(r.T1DropRatePerMinute);
        }

        [TestMethod]
        public void Execute_FallbackToColumnNames_WhenSourceColumnsEmpty()
        {
            // SourceColumns does NOT contain the root — fallback to ColumnNames
            var data = new TestData
            {
                RowCount = 3,
                TimestampsMs = new long[] { 0, 30_000, 60_000 },
                ColumnNames = new[] { "T1" },
                Columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    { ["T1"] = new double?[] { -10.0, -25.0, -20.0 } },
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),  // empty!
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                    { [Root] = 0L },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                    { [Root] = 60_000L },
                Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1Min, "должен найти T1 через ColumnNames fallback");
            Assert.AreEqual(-25.0, r.T1Min.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithT8PlusChannels_ReturnsThresholdTimesAndValues()
        {
            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["T1"] = new double?[] { -10.0, -20.0, -30.0 },
                ["T7"] = new double?[] { -100.0, -100.0, -100.0 },
                ["C-T8"] = new double?[] { 10.0, 4.0, 0.0 },
                ["C-T9#2"] = new double?[] { 8.0, 6.0, 2.0 },
                ["source::C-T10"] = new double?[] { 12.0, 8.0, 4.0 }
            };
            var data = MakeData(Root, new long[] { 0L, 60_000L, 120_000L }, columns);
            var ts = new TimestampRangeService();
            var uc = new GetRecordingInfoUseCase(ts);

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T8PlusStats);
            Assert.IsTrue(r.T8PlusStats.AverageReached);
            Assert.AreEqual(2.0, r.T8PlusStats.AverageValue.Value, 0.001);
            Assert.AreEqual(120_000L, r.T8PlusStats.AverageElapsedMs.Value);
            Assert.AreEqual(ts.UnixMsToLocalDateTime(120_000L), r.T8PlusStats.AverageTime.Value);

            Assert.IsTrue(r.T8PlusStats.MinimumReached);
            Assert.AreEqual(0.0, r.T8PlusStats.MinimumValue.Value, 0.001);
            Assert.AreEqual(120_000L, r.T8PlusStats.MinimumElapsedMs.Value);

            Assert.IsTrue(r.T8PlusStats.MaximumReached);
            Assert.AreEqual(8.0, r.T8PlusStats.MaximumValue.Value, 0.001);
            Assert.AreEqual(60_000L, r.T8PlusStats.MaximumElapsedMs.Value);
        }

        [TestMethod]
        public void Execute_WithCustomT8PlusThresholds_RecalculatesReachedFlags()
        {
            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["T1"] = new double?[] { 30.0, 20.0, 10.0 },
                ["C-T8"] = new double?[] { 10.0, 4.0, 0.0 },
                ["C-T9"] = new double?[] { 8.0, 6.0, 2.0 },
                ["C-T10"] = new double?[] { 12.0, 8.0, 4.0 }
            };
            var data = MakeData(Root, new long[] { 0L, 60_000L, 120_000L }, columns);
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(
                data,
                Root,
                new T8PlusTemperatureThresholds(7.0, 1.0, 9.0));

            Assert.IsNotNull(r.T8PlusStats);
            Assert.IsTrue(r.T8PlusStats.AverageReached);
            Assert.AreEqual(6.0, r.T8PlusStats.AverageValue.Value, 0.001);
            Assert.AreEqual(60_000L, r.T8PlusStats.AverageElapsedMs.Value);
        }

        [TestMethod]
        public void Execute_WithT8PlusChannels_WhenThresholdsNotReached_ReturnsBestObservedValuesAndTimes()
        {
            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["T1"] = new double?[] { -10.0, -20.0, -30.0 },
                ["C-T8"] = new double?[] { 12.0, 7.0, 6.0 },
                ["C-T9"] = new double?[] { 14.0, 11.0, 10.0 }
            };
            var data = MakeData(Root, new long[] { 0L, 60_000L, 120_000L }, columns);
            var ts = new TimestampRangeService();
            var uc = new GetRecordingInfoUseCase(ts);

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T8PlusStats);
            Assert.IsFalse(r.T8PlusStats.AverageReached);
            Assert.AreEqual(8.0, r.T8PlusStats.AverageValue.Value, 0.001);
            Assert.AreEqual(120_000L, r.T8PlusStats.AverageElapsedMs.Value);
            Assert.AreEqual(ts.UnixMsToLocalDateTime(120_000L), r.T8PlusStats.AverageTime.Value);

            Assert.IsFalse(r.T8PlusStats.MinimumReached);
            Assert.AreEqual(6.0, r.T8PlusStats.MinimumValue.Value, 0.001);
            Assert.AreEqual(120_000L, r.T8PlusStats.MinimumElapsedMs.Value);

            Assert.IsFalse(r.T8PlusStats.MaximumReached);
            Assert.AreEqual(10.0, r.T8PlusStats.MaximumValue.Value, 0.001);
            Assert.AreEqual(120_000L, r.T8PlusStats.MaximumElapsedMs.Value);
        }

        [TestMethod]
        public void Execute_WithT8PlusChannels_IgnoresMinus99SentinelValues()
        {
            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["T1"] = new double?[] { -10.0, -20.0, -30.0 },
                ["A-T8"] = new double?[] { 30.0, 20.0, 10.0 },
                ["A-T9"] = new double?[] { 32.0, 22.0, 12.0 },
                ["C-T23"] = new double?[] { -99.0, -99.0, -99.0 },
                ["C-T24"] = new double?[] { -99.0, -99.0, -99.0 }
            };
            var data = MakeData(Root, new long[] { 0L, 60_000L, 120_000L }, columns);
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T8PlusStats);
            Assert.IsFalse(r.T8PlusStats.AverageReached);
            Assert.AreEqual(11.0, r.T8PlusStats.AverageValue.Value, 0.001);
            Assert.AreEqual(120_000L, r.T8PlusStats.AverageElapsedMs.Value);

            Assert.IsFalse(r.T8PlusStats.MinimumReached);
            Assert.AreEqual(10.0, r.T8PlusStats.MinimumValue.Value, 0.001);
            Assert.AreEqual(120_000L, r.T8PlusStats.MinimumElapsedMs.Value);

            Assert.IsFalse(r.T8PlusStats.MaximumReached);
            Assert.AreEqual(12.0, r.T8PlusStats.MaximumValue.Value, 0.001);
            Assert.AreEqual(120_000L, r.T8PlusStats.MaximumElapsedMs.Value);
        }

        [TestMethod]
        public void Execute_WithT8PlusChannels_ReturnsAverageDropRateFromStartToMinimumAverage()
        {
            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["T1"] = new double?[] { 30.0, 20.0, 10.0 },
                ["A-T8"] = new double?[] { 20.0, 14.0, 12.0 },
                ["A-T9"] = new double?[] { 22.0, 16.0, 14.0 }
            };
            var data = MakeData(Root, new long[] { 0L, 60_000L, 120_000L }, columns);
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T8PlusStats);
            Assert.AreEqual(-4.0, r.T8PlusStats.AverageDropRatePerMinute.Value, 0.001);
        }
    }
}
