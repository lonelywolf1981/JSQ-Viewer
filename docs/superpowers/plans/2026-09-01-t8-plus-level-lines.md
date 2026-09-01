# Горизонтальные уровни T8+ — план переделки

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Заменить три временных ряда T8+ на три горизонтальные опорные линии, показывающие минимум, среднее и максимум по массиву термопар на момент последней отображаемой точки графика.

**Architecture:** Расчёт (`T8PlusChannelSelector`, `T8PlusSeriesBuilder`), хранение флагов и UI уже готовы и не меняются. Пайплайн перестаёт строить серии и начинает отдавать список уровней `ChartLevelLine`, вычисляя для каждого источника индекс последнего видимого отсчёта. Рендерер рисует уровни как `StripLine` по оси Y — не как серии, поэтому они не трогают палитру, легенду, подсказку и подсветку.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WinForms, System.Windows.Forms.DataVisualization (MS Chart), MSTest.

**Spec:** `docs/superpowers/specs/2026-09-01-t8-plus-statistics-lines-design.md` (ревизия 2)

## Global Constraints

- C# 7.3, целевая платформа net48. Синтаксис новее C# 7.3 не использовать.
- Стиль по `AGENTS.md`: 4 пробела, фигурные скобки на своей строке, `PascalCase` для типов и открытых членов, `_camelCase` для приватных полей, явные модификаторы доступа.
- Слой `Application/` не ссылается на WinForms. `Presentation/WinForms/` и `UI/` — могут.
- Порог валидности температуры один на приложение: `RecordingTemperatureValueFilter.IsValidTemperature`, строго больше −90.
- Штриховой пунктир `ChartDashStyle.Dash` зарезервирован за линией прогноза динамики и для уровней не используется.
- Сообщения коммитов — короткие русские в повелительном наклонении, один коммит на задачу.
- Оба проекта собирают исходники по маскам каталогов, новые файлы в `.csproj` вписывать не нужно.
- `UI/MainForm.cs` очень большой: только точечные правки, без переформатирования и без смены окончаний строк ни в одном файле.
- Полный прогон: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`. Отдельный класс: тот же вызов с `--filter FullyQualifiedName~ИмяКласса`.
- Сборки Debug и Release обязаны завершаться с 0 ошибок и 0 предупреждений.

---

## Файловая структура

| Файл | Ответственность |
|---|---|
| `Application/Charting/ChartLevelLine.cs` | Создать. Описание одного уровня: источник, роль, значение, подпись |
| `Application/Charting/VisibleRangeEdgeResolver.cs` | Создать. Индекс последнего видимого отсчёта для источника |
| `Application/Charting/ChartPipelineResult.cs` | Изменить. Свойство `LevelLines` |
| `Application/Charting/ChartPipelineService.cs` | Изменить. Уровни вместо серий; откат легенды и вклада в ось наложения |
| `Application/Charting/ChartPipelineSeries.cs` | Изменить. Убрать `Role` и `SourceIndex` |
| `Presentation/WinForms/ViewModels/ChartLevelLineViewModel.cs` | Создать. Модель представления уровня |
| `Presentation/WinForms/ViewModels/ChartViewModel.cs` | Изменить. Коллекция уровней |
| `Presentation/WinForms/ViewModels/ChartSeriesViewModel.cs` | Изменить. Убрать `Role` и `SourceIndex` |
| `Presentation/WinForms/Charting/ChartViewModelFactory.cs` | Изменить. Перенос уровней |
| `Presentation/WinForms/Charting/ChartRenderer.cs` | Изменить. Откат двух проходов и имён; отрисовка полос |

---

### Task 1: Тип уровня и определение правого края

**Files:**
- Create: `Application/Charting/ChartLevelLine.cs`
- Create: `Application/Charting/VisibleRangeEdgeResolver.cs`
- Test: `JSQViewer.Tests/VisibleRangeEdgeResolverTests.cs`

**Interfaces:**
- Consumes: `JSQViewer.Core.TestData`, `JSQViewer.Application.Charting.ChartSeriesRole` (уже существует со значениями `Channel = 0`, `T8Minimum = 1`, `T8Average = 2`, `T8Maximum = 3`).
- Produces:
  - `public sealed class ChartLevelLine` с конструктором `ChartLevelLine(string sourceRoot, int sourceIndex, ChartSeriesRole role, double value, string label)` и свойствами `SourceRoot`, `SourceIndex`, `Role`, `Value`, `Label` (все `{ get; private set; }`)
  - `public static class VisibleRangeEdgeResolver` с `public static int ResolveIndex(long[] timestampsMs, long edgeMs)`

`ResolveIndex` возвращает индекс последнего элемента `timestampsMs`, значение которого не превышает `edgeMs`. Массив отсортирован по возрастанию. Если все элементы больше `edgeMs`, либо массив пуст или равен `null` — возвращается `-1`.

- [ ] **Step 1: Написать падающий тест**

Создать `JSQViewer.Tests/VisibleRangeEdgeResolverTests.cs`:

```csharp
using JSQViewer.Application.Charting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class VisibleRangeEdgeResolverTests
    {
        private static readonly long[] Timestamps = { 0L, 1000L, 2000L, 3000L };

        [TestMethod]
        public void ResolveIndex_EdgeExactlyOnSample_ReturnsThatSample()
        {
            Assert.AreEqual(2, VisibleRangeEdgeResolver.ResolveIndex(Timestamps, 2000L));
        }

        [TestMethod]
        public void ResolveIndex_EdgeBetweenSamples_ReturnsEarlierSample()
        {
            Assert.AreEqual(1, VisibleRangeEdgeResolver.ResolveIndex(Timestamps, 1500L));
        }

        [TestMethod]
        public void ResolveIndex_EdgeBeyondLastSample_ReturnsLastSample()
        {
            Assert.AreEqual(3, VisibleRangeEdgeResolver.ResolveIndex(Timestamps, 99999L));
        }

        [TestMethod]
        public void ResolveIndex_EdgeBeforeFirstSample_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, VisibleRangeEdgeResolver.ResolveIndex(Timestamps, -1L));
        }

        [TestMethod]
        public void ResolveIndex_WithEmptyOrNullInput_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, VisibleRangeEdgeResolver.ResolveIndex(new long[0], 1000L));
            Assert.AreEqual(-1, VisibleRangeEdgeResolver.ResolveIndex(null, 1000L));
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~VisibleRangeEdgeResolverTests`
Ожидается: ошибка компиляции — тип `VisibleRangeEdgeResolver` не найден.

- [ ] **Step 3: Создать тип уровня**

Создать `Application/Charting/ChartLevelLine.cs`:

```csharp
namespace JSQViewer.Application.Charting
{
    /// <summary>
    /// Горизонтальная опорная линия на графике: значение статистики T8+
    /// на момент последней отображаемой точки одного источника.
    /// </summary>
    public sealed class ChartLevelLine
    {
        public ChartLevelLine(string sourceRoot, int sourceIndex, ChartSeriesRole role, double value, string label)
        {
            SourceRoot = sourceRoot ?? string.Empty;
            SourceIndex = sourceIndex;
            Role = role;
            Value = value;
            Label = label ?? string.Empty;
        }

        public string SourceRoot { get; private set; }

        public int SourceIndex { get; private set; }

        public ChartSeriesRole Role { get; private set; }

        public double Value { get; private set; }

        public string Label { get; private set; }
    }
}
```

- [ ] **Step 4: Создать определитель края**

Создать `Application/Charting/VisibleRangeEdgeResolver.cs`:

```csharp
namespace JSQViewer.Application.Charting
{
    /// <summary>
    /// Поиск последнего отсчёта, попадающего в видимый участок графика.
    /// Отдельный класс, потому что правый край задаётся тремя разными путями
    /// (ползунок диапазона, ручные границы оси, конец данных), а правило выбора
    /// отсчёта у них общее.
    /// </summary>
    public static class VisibleRangeEdgeResolver
    {
        public static int ResolveIndex(long[] timestampsMs, long edgeMs)
        {
            if (timestampsMs == null || timestampsMs.Length == 0)
            {
                return -1;
            }

            int low = 0;
            int high = timestampsMs.Length - 1;
            int result = -1;

            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                if (timestampsMs[middle] <= edgeMs)
                {
                    result = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return result;
        }
    }
}
```

- [ ] **Step 5: Прогнать тесты**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~VisibleRangeEdgeResolverTests`
Ожидается: PASS, 5 тестов.

- [ ] **Step 6: Собрать**

Выполнить: `dotnet build .\JSQViewer.csproj -c Debug`
Ожидается: 0 ошибок, 0 предупреждений.

- [ ] **Step 7: Коммит**

```bash
git add Application/Charting/ChartLevelLine.cs Application/Charting/VisibleRangeEdgeResolver.cs JSQViewer.Tests/VisibleRangeEdgeResolverTests.cs
git commit -m "Добавлены тип уровня T8+ и поиск последнего видимого отсчёта"
```

---

### Task 2: Пайплайн отдаёт уровни вместо серий

Это ядро переделки. Строится на типах из Task 1.

**Files:**
- Modify: `Application/Charting/ChartPipelineResult.cs`
- Modify: `Application/Charting/ChartPipelineSeries.cs` (удалить `Role` и `SourceIndex`)
- Modify: `Application/Charting/ChartPipelineService.cs`
- Modify: `JSQViewer.Tests/T8PlusChartPipelineTests.cs` (переписать под уровни)

**Interfaces:**
- Consumes: `ChartLevelLine`, `VisibleRangeEdgeResolver.ResolveIndex`, `T8PlusSeriesBuilder.Build`, `T8PlusSeries`, `T8PlusSeriesRequest`, `SourceDisplayNameResolver.Resolve`.
- Produces: `ChartPipelineResult.LevelLines` типа `IReadOnlyList<ChartLevelLine>`.

**Что удаляется из `ChartPipelineService` целиком:** `AppendT8PlusSeries`, `BuildT8PlusSeries`, `BuildT8PlusLegendText`, вызов на строке ~170 вместе с блоком `if (overlayMode && t8PlusMaxDurationMs > maxOverlayDurationMs)` и блоком `if (t8PlusCount > 0) { ... showLegend = true; }`. Приватные `ResolveSourceBaseMs`, `ResolveSourceIndex`, поля `_t8PlusSeriesBuilder`, `_t8PlusCache`, `_t8PlusCacheDataVersion` и метод `EnsureT8PlusCacheVersion` **остаются** — они нужны новому коду.

- [ ] **Step 1: Переписать тесты пайплайна**

Заменить содержимое `JSQViewer.Tests/T8PlusChartPipelineTests.cs` целиком:

```csharp
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Charting;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class T8PlusChartPipelineTests
    {
        private static ChartPipelineService CreateService()
        {
            return new ChartPipelineService(new SeriesSliceService(null, new TimestampRangeService()));
        }

        private static TestData BuildData()
        {
            var data = new TestData();
            data.Root = "A";
            data.TimestampsMs = new[] { 0L, 1000L, 2000L, 3000L };
            data.RowCount = 4;
            data.SourceOrder = new[] { "A" };
            data.SourceColumns["A"] = new[] { "T1", "T8", "T9" };
            data.SourceStartMs["A"] = 0L;
            data.SourceEndMs["A"] = 3000L;
            data.CodeSources["T1"] = "A";
            data.CodeSources["T8"] = "A";
            data.CodeSources["T9"] = "A";
            data.Columns["T1"] = new double?[] { 1.0, 1.0, 1.0, 1.0 };
            data.Columns["T8"] = new double?[] { 10.0, 8.0, 6.0, 4.0 };
            data.Columns["T9"] = new double?[] { 20.0, 18.0, 16.0, 14.0 };
            return data;
        }

        private static ChartPipelineRequest Request(
            TestData data,
            IReadOnlyList<T8PlusSeriesRequest> t8,
            bool overlayMode = false,
            double rangeStart = double.NaN,
            double rangeEnd = double.NaN)
        {
            return ChartPipelineRequest.ForChart(
                data, new[] { "T1" }, overlayMode, 1, false, 1, 1000, 1,
                rangeStart, rangeEnd, null, null, false, null, t8);
        }

        [TestMethod]
        public void Execute_WithAllThreeFlags_ProducesThreeLevelsAtLastSample()
        {
            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };

            ChartPipelineResult result = CreateService().Execute(Request(BuildData(), t8));

            Assert.AreEqual(3, result.LevelLines.Count);
            // Последний отсчёт: T8=4, T9=14.
            Assert.AreEqual(4.0, result.LevelLines.Single(l => l.Role == ChartSeriesRole.T8Minimum).Value, 1e-9);
            Assert.AreEqual(9.0, result.LevelLines.Single(l => l.Role == ChartSeriesRole.T8Average).Value, 1e-9);
            Assert.AreEqual(14.0, result.LevelLines.Single(l => l.Role == ChartSeriesRole.T8Maximum).Value, 1e-9);
            Assert.IsTrue(result.LevelLines.All(l => l.SourceRoot == "A" && l.SourceIndex == 0));
        }

        [TestMethod]
        public void Execute_WithNarrowedRange_UsesLastSampleInsideRange()
        {
            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };

            ChartPipelineResult result = CreateService().Execute(
                Request(BuildData(), t8, false, 0d, 1500d));

            // Правый край 1500 мс: последний попавший отсчёт — 1000 мс, T8=8, T9=18.
            Assert.AreEqual(13.0, result.LevelLines.Single().Value, 1e-9);
        }

        [TestMethod]
        public void Execute_WhenEdgeSampleHasNoValue_StepsBackToNearestValidSample()
        {
            TestData data = BuildData();
            data.Columns["T8"] = new double?[] { 10.0, 8.0, 6.0, null };
            data.Columns["T9"] = new double?[] { 20.0, 18.0, 16.0, null };
            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };

            ChartPipelineResult result = CreateService().Execute(Request(data, t8));

            // На последнем отсчёте значений нет, берётся предыдущий: (6 + 16) / 2.
            Assert.AreEqual(11.0, result.LevelLines.Single().Value, 1e-9);
        }

        [TestMethod]
        public void Execute_WhenNoValidSampleInRange_ProducesNoLevels()
        {
            TestData data = BuildData();
            data.Columns["T8"] = new double?[] { null, null, null, null };
            data.Columns["T9"] = new double?[] { null, null, null, null };
            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };

            ChartPipelineResult result = CreateService().Execute(Request(data, t8));

            Assert.AreEqual(0, result.LevelLines.Count);
        }

        [TestMethod]
        public void Execute_WithoutFlagsOrWithoutT8Channels_ProducesNoLevels()
        {
            Assert.AreEqual(0, CreateService().Execute(Request(BuildData(), null)).LevelLines.Count);

            var data = new TestData();
            data.Root = "A";
            data.TimestampsMs = new[] { 0L, 1000L };
            data.RowCount = 2;
            data.SourceOrder = new[] { "A" };
            data.SourceColumns["A"] = new[] { "T1" };
            data.CodeSources["T1"] = "A";
            data.Columns["T1"] = new double?[] { 1.0, 1.0 };
            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };

            Assert.AreEqual(0, CreateService().Execute(Request(data, t8)).LevelLines.Count);
        }

        [TestMethod]
        public void Execute_InOverlayMode_ResolvesEdgePerSourceStart()
        {
            var data = new TestData();
            data.Root = "A";
            data.TimestampsMs = new[] { 0L, 1000L, 2000L, 3000L, 4000L };
            data.RowCount = 5;
            data.SourceOrder = new[] { "A", "B" };
            data.SourceColumns["A"] = new[] { "T8" };
            data.SourceColumns["B"] = new[] { "T9" };
            data.SourceStartMs["A"] = 0L;
            data.SourceEndMs["A"] = 4000L;
            data.SourceStartMs["B"] = 2000L;
            data.SourceEndMs["B"] = 4000L;
            data.CodeSources["T8"] = "A";
            data.CodeSources["T9"] = "B";
            data.Columns["T8"] = new double?[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
            data.Columns["T9"] = new double?[] { 11.0, 21.0, 31.0, 41.0, 51.0 };

            var t8 = new[]
            {
                new T8PlusSeriesRequest("A", false, true, false),
                new T8PlusSeriesRequest("B", false, true, false)
            };

            // В наложении ось — часы от начала своего прогона. Край в 1 час
            // для A это абсолютные 3600000 мс, для B — 2000 + 3600000 мс;
            // оба за пределами данных, поэтому берётся последний отсчёт каждого.
            ChartPipelineResult result = CreateService().Execute(
                Request(data, t8, true, 0d, 1d));

            ChartLevelLine a = result.LevelLines.Single(l => l.SourceRoot == "A");
            ChartLevelLine b = result.LevelLines.Single(l => l.SourceRoot == "B");

            Assert.AreEqual(0, a.SourceIndex);
            Assert.AreEqual(1, b.SourceIndex);
            Assert.AreEqual(50.0, a.Value, 1e-9);
            Assert.AreEqual(51.0, b.Value, 1e-9);
        }

        [TestMethod]
        public void Execute_WithSingleSource_LabelOmitsSourceName()
        {
            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };

            ChartPipelineResult result = CreateService().Execute(Request(BuildData(), t8));

            string label = result.LevelLines.Single().Label;
            Assert.IsTrue(label.Contains("T8+"), label);
            Assert.IsFalse(label.Contains("["), label);
        }

        [TestMethod]
        public void Execute_WithLevelsEnabled_DoesNotChangeSeriesOrLegend()
        {
            var data = new TestData();
            data.Root = "A";
            data.TimestampsMs = new[] { 0L, 1000L };
            data.RowCount = 2;
            data.SourceOrder = new[] { "A" };
            var columns = new List<string>();
            for (int i = 1; i <= 30; i++)
            {
                string code = "C" + i.ToString();
                columns.Add(code);
                data.CodeSources[code] = "A";
                data.Columns[code] = new double?[] { i, i };
            }
            columns.Add("T8");
            data.CodeSources["T8"] = "A";
            data.Columns["T8"] = new double?[] { 5.0, 5.0 };
            data.SourceColumns["A"] = columns.ToArray();

            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };
            ChartPipelineResult result = CreateService().Execute(
                ChartPipelineRequest.ForChart(
                    data, columns, false, 1, false, 1, 1000, columns.Count,
                    double.NaN, double.NaN, null, null, false, null, t8));

            // Уровни не являются сериями: набор серий и правило легенды не меняются.
            Assert.AreEqual(columns.Count, result.Series.Count);
            Assert.IsFalse(result.ShowLegend);
            Assert.AreEqual(3, result.LevelLines.Count);
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тесты падают**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~T8PlusChartPipelineTests`
Ожидается: ошибка компиляции — у `ChartPipelineResult` нет свойства `LevelLines`.

- [ ] **Step 3: Добавить свойство результата**

В `Application/Charting/ChartPipelineResult.cs` в конструкторе добавить:

```csharp
            LevelLines = new ChartLevelLine[0];
```

и свойство рядом с `Series`:

```csharp
        public IReadOnlyList<ChartLevelLine> LevelLines { get; set; }
```

- [ ] **Step 4: Убрать роль с серии**

Из `Application/Charting/ChartPipelineSeries.cs` удалить свойства `Role` и `SourceIndex` — уровни больше не серии, и на серии эти поля не нужны.

- [ ] **Step 5: Заменить построение серий на построение уровней**

В `Application/Charting/ChartPipelineService.cs` удалить методы `AppendT8PlusSeries`, `BuildT8PlusSeries`, `BuildT8PlusLegendText`. Удалить из `Execute` блок, начинающийся с `long t8PlusMaxDurationMs;` и заканчивающийся закрывающей скобкой блока `if (t8PlusCount > 0)`, включая оба `if`. Ниже, в возвращаемом `ChartPipelineResult`, добавить `LevelLines = levelLines`.

Перед формированием результата вставить:

```csharp
            IReadOnlyList<ChartLevelLine> levelLines = BuildT8PlusLevels(request, data, overlayMode);
```

Добавить приватные методы:

```csharp
        private IReadOnlyList<ChartLevelLine> BuildT8PlusLevels(
            ChartPipelineRequest request,
            TestData data,
            bool overlayMode)
        {
            var levels = new List<ChartLevelLine>();
            IReadOnlyList<T8PlusSeriesRequest> requests = request.T8PlusSeries;
            if (requests == null || requests.Count == 0
                || data.TimestampsMs == null || data.TimestampsMs.Length == 0)
            {
                return levels;
            }

            EnsureT8PlusCacheVersion(request.DataVersion);

            bool multipleSources = data.SourceColumns != null && data.SourceColumns.Count > 1;

            for (int i = 0; i < requests.Count; i++)
            {
                T8PlusSeriesRequest item = requests[i];
                if (item == null || !item.HasAny || string.IsNullOrWhiteSpace(item.SourceRoot))
                {
                    continue;
                }

                T8PlusSeries built;
                if (!_t8PlusCache.TryGetValue(item.SourceRoot, out built))
                {
                    built = _t8PlusSeriesBuilder.Build(data, item.SourceRoot);
                    _t8PlusCache[item.SourceRoot] = built;
                }

                if (!built.HasChannels)
                {
                    continue;
                }

                long edgeMs = ResolveVisibleEdgeMs(request, data, item.SourceRoot, overlayMode);
                int edgeIndex = VisibleRangeEdgeResolver.ResolveIndex(data.TimestampsMs, edgeMs);
                if (edgeIndex < 0)
                {
                    continue;
                }

                long startMs = ResolveVisibleStartMs(request, data, item.SourceRoot, overlayMode);
                int valueIndex = FindLastValidIndex(built.Average, edgeIndex, data.TimestampsMs, startMs);
                if (valueIndex < 0)
                {
                    continue;
                }

                int sourceIndex = ResolveSourceIndex(data, item.SourceRoot);
                string sourceName = multipleSources
                    ? _sourceDisplayNameResolver.Resolve(data, item.SourceRoot)
                    : null;

                if (item.ShowMinimum)
                {
                    AddLevel(levels, built.Minimum, valueIndex, item.SourceRoot, sourceIndex, sourceName, ChartSeriesRole.T8Minimum);
                }

                if (item.ShowAverage)
                {
                    AddLevel(levels, built.Average, valueIndex, item.SourceRoot, sourceIndex, sourceName, ChartSeriesRole.T8Average);
                }

                if (item.ShowMaximum)
                {
                    AddLevel(levels, built.Maximum, valueIndex, item.SourceRoot, sourceIndex, sourceName, ChartSeriesRole.T8Maximum);
                }
            }

            return levels;
        }

        private static void AddLevel(
            List<ChartLevelLine> levels,
            double?[] values,
            int index,
            string sourceRoot,
            int sourceIndex,
            string sourceName,
            ChartSeriesRole role)
        {
            if (values == null || index >= values.Length || !values[index].HasValue)
            {
                return;
            }

            double value = values[index].Value;
            levels.Add(new ChartLevelLine(
                sourceRoot, sourceIndex, role, value, BuildLevelLabel(sourceName, role, value)));
        }

        private static string BuildLevelLabel(string sourceName, ChartSeriesRole role, double value)
        {
            string roleText;
            if (role == ChartSeriesRole.T8Minimum)
            {
                roleText = "T8+ мин";
            }
            else if (role == ChartSeriesRole.T8Maximum)
            {
                roleText = "T8+ макс";
            }
            else
            {
                roleText = "T8+ сред";
            }

            string valueText = value.ToString("0.0", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(sourceName)
                ? string.Format(CultureInfo.InvariantCulture, "{0} {1}", roleText, valueText)
                : string.Format(CultureInfo.InvariantCulture, "[{0}] {1} {2}", sourceName, roleText, valueText);
        }

        /// <summary>
        /// Ищет ближайший к правому краю отсчёт с валидным значением, не выходя
        /// за левую границу видимого участка. Без этого отступа линия пропадала бы
        /// на любом пропуске в данных ровно на крае.
        /// </summary>
        private static int FindLastValidIndex(double?[] values, int edgeIndex, long[] timestampsMs, long startMs)
        {
            if (values == null)
            {
                return -1;
            }

            for (int i = Math.Min(edgeIndex, values.Length - 1); i >= 0; i--)
            {
                if (timestampsMs[i] < startMs)
                {
                    return -1;
                }

                if (values[i].HasValue)
                {
                    return i;
                }
            }

            return -1;
        }

        private static long ResolveVisibleEdgeMs(
            ChartPipelineRequest request,
            TestData data,
            string sourceRoot,
            bool overlayMode)
        {
            long lastMs = data.TimestampsMs[data.TimestampsMs.Length - 1];

            double edge = request.SelectedRangeEnd;
            if (double.IsNaN(edge) && request.XAxis != null && request.XAxis.IsManualEnabled && request.XAxis.Maximum.HasValue)
            {
                edge = request.XAxis.Maximum.Value;
            }

            if (double.IsNaN(edge))
            {
                return lastMs;
            }

            if (!overlayMode)
            {
                return (long)edge;
            }

            // В наложении ось задана в часах от начала своего прогона.
            long baseMs = ResolveSourceBaseMs(data, sourceRoot, data.TimestampsMs[0]);
            return baseMs + (long)(edge * 3600000.0);
        }

        private static long ResolveVisibleStartMs(
            ChartPipelineRequest request,
            TestData data,
            string sourceRoot,
            bool overlayMode)
        {
            double start = request.SelectedRangeStart;
            if (double.IsNaN(start) && request.XAxis != null && request.XAxis.IsManualEnabled && request.XAxis.Minimum.HasValue)
            {
                start = request.XAxis.Minimum.Value;
            }

            if (double.IsNaN(start))
            {
                return long.MinValue;
            }

            if (!overlayMode)
            {
                return (long)start;
            }

            long baseMs = ResolveSourceBaseMs(data, sourceRoot, data.TimestampsMs[0]);
            return baseMs + (long)(start * 3600000.0);
        }
```

- [ ] **Step 6: Прогнать тесты пайплайна**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~T8PlusChartPipelineTests`
Ожидается: PASS, 8 тестов.

- [ ] **Step 7: Прогнать весь набор**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Ожидается: все проходят. Тесты, ссылавшиеся на удалённые `ChartPipelineSeries.Role` и `SourceIndex`, надо привести в соответствие — но только те, что относятся к T8+; тесты каналов и прогноза править нельзя.

- [ ] **Step 8: Собрать**

Выполнить: `dotnet build .\JSQViewer.csproj -c Debug`
Ожидается: 0 ошибок, 0 предупреждений.

- [ ] **Step 9: Коммит**

```bash
git add Application/Charting/ChartPipelineResult.cs Application/Charting/ChartPipelineSeries.cs Application/Charting/ChartPipelineService.cs JSQViewer.Tests/T8PlusChartPipelineTests.cs
git commit -m "Пайплайн отдаёт горизонтальные уровни T8+ вместо временных рядов"
```

---

### Task 3: Отрисовка уровней полосами оси Y

**Files:**
- Create: `Presentation/WinForms/ViewModels/ChartLevelLineViewModel.cs`
- Modify: `Presentation/WinForms/ViewModels/ChartViewModel.cs`
- Modify: `Presentation/WinForms/ViewModels/ChartSeriesViewModel.cs`
- Modify: `Presentation/WinForms/Charting/ChartViewModelFactory.cs`
- Modify: `Presentation/WinForms/Charting/ChartRenderer.cs`
- Test: `JSQViewer.Tests/ChartLevelLineRenderingTests.cs`

**Interfaces:**
- Consumes: `ChartLevelLine`, `ChartSeriesRole`, `SourceColorPalette.ForSourceIndex(int)`.
- Produces: `ChartLevelLineViewModel` со свойствами `SourceIndex`, `Role`, `Value`, `Label`; `ChartViewModel.LevelLines` типа `IReadOnlyList<ChartLevelLineViewModel>`.

- [ ] **Step 1: Написать падающий тест**

Создать `JSQViewer.Tests/ChartLevelLineRenderingTests.cs`:

```csharp
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
```

- [ ] **Step 2: Убедиться, что тесты падают**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~ChartLevelLineRenderingTests`
Ожидается: ошибка компиляции — тип `ChartLevelLineViewModel` не найден.

- [ ] **Step 3: Создать модель представления**

Создать `Presentation/WinForms/ViewModels/ChartLevelLineViewModel.cs`:

```csharp
using JSQViewer.Application.Charting;

namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class ChartLevelLineViewModel
    {
        public int SourceIndex { get; set; }

        public ChartSeriesRole Role { get; set; }

        public double Value { get; set; }

        public string Label { get; set; }
    }
}
```

- [ ] **Step 4: Провести уровни через модель графика**

В `Presentation/WinForms/ViewModels/ChartViewModel.cs` добавить в существующий конструктор, рядом с `Series = new ChartSeriesViewModel[0];`:

```csharp
            LevelLines = new ChartLevelLineViewModel[0];
```

и свойство рядом с `Series`:

```csharp
        public IReadOnlyList<ChartLevelLineViewModel> LevelLines { get; set; }
```

Из `Presentation/WinForms/ViewModels/ChartSeriesViewModel.cs` удалить свойства `Role` и `SourceIndex`.

В `ChartViewModelFactory.Create` убрать перенос `Role` и `SourceIndex` на серию и добавить в создаваемый `ChartViewModel`:

```csharp
                LevelLines = BuildLevelLines(result.LevelLines),
```

и приватный метод:

```csharp
        private static IReadOnlyList<ChartLevelLineViewModel> BuildLevelLines(IReadOnlyList<ChartLevelLine> levels)
        {
            if (levels == null || levels.Count == 0)
            {
                return new ChartLevelLineViewModel[0];
            }

            var result = new List<ChartLevelLineViewModel>(levels.Count);
            for (int i = 0; i < levels.Count; i++)
            {
                ChartLevelLine level = levels[i];
                result.Add(new ChartLevelLineViewModel
                {
                    SourceIndex = level.SourceIndex,
                    Role = level.Role,
                    Value = level.Value,
                    Label = level.Label
                });
            }

            return result;
        }
```

- [ ] **Step 5: Откатить двухпроходную вставку и нарисовать полосы**

В `Presentation/WinForms/Charting/ChartRenderer.cs` вернуть добавление серий одним циклом, как было до линий T8+: серии создаются в порядке `viewModel.Series`, имя серии равно `model.Code`, цвет не задаётся. Удалить `ResolveT8PlusDashStyle` в прежнем виде — он переезжает под полосы.

После добавления серий, внутри того же `try`, вставить отрисовку уровней:

```csharp
                if (chart.ChartAreas.Count > 0)
                {
                    ApplyLevelLines(chart.ChartAreas[0], viewModel);
                }
```

Добавить приватные методы:

```csharp
        private static void ApplyLevelLines(ChartArea area, ChartViewModel viewModel)
        {
            // Полосы накапливались бы при каждой перерисовке, поэтому чистим всегда,
            // даже когда уровней нет.
            area.AxisY.StripLines.Clear();

            IReadOnlyList<ChartLevelLineViewModel> levels = viewModel.LevelLines;
            if (levels == null)
            {
                return;
            }

            for (int i = 0; i < levels.Count; i++)
            {
                ChartLevelLineViewModel level = levels[i];
                var strip = new StripLine();
                strip.IntervalOffset = level.Value;
                strip.Interval = 0d;
                strip.StripWidth = 0d;
                strip.BorderColor = SourceColorPalette.ForSourceIndex(level.SourceIndex);
                strip.BorderWidth = 2;
                strip.BorderDashStyle = ResolveLevelDashStyle(level.Role);
                strip.Text = level.Label ?? string.Empty;
                strip.TextAlignment = StringAlignment.Far;
                strip.TextLineAlignment = StringAlignment.Near;
                area.AxisY.StripLines.Add(strip);
            }
        }

        private static ChartDashStyle ResolveLevelDashStyle(ChartSeriesRole role)
        {
            if (role == ChartSeriesRole.T8Minimum)
            {
                return ChartDashStyle.Dot;
            }

            if (role == ChartSeriesRole.T8Maximum)
            {
                // Не Dash: штриховой стиль занят линией прогноза динамики.
                return ChartDashStyle.DashDot;
            }

            return ChartDashStyle.Solid;
        }
```

Файл уже содержит `using System;`, `using System.Collections.Generic;`, `using System.Windows.Forms.DataVisualization.Charting;`, `using JSQViewer.Application.Charting;` и `using JSQViewer.Presentation.WinForms.ViewModels;`. Дополнительно нужен только `using System.Drawing;` — ради `StringAlignment`.

- [ ] **Step 6: Прогнать тесты отрисовки**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~ChartLevelLineRenderingTests`
Ожидается: PASS, 5 тестов.

- [ ] **Step 7: Прогнать весь набор**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Ожидается: все проходят, включая `ChartViewUseCaseTests`, где серия ищется по имени `forecast` — откат имён обязан её сохранить.

- [ ] **Step 8: Собрать обе конфигурации**

Выполнить: `dotnet build .\JSQViewer.csproj -c Debug` и `dotnet build .\JSQViewer.csproj -c Release`
Ожидается: 0 ошибок, 0 предупреждений в обеих.

- [ ] **Step 9: Коммит**

```bash
git add Presentation/WinForms/ViewModels/ChartLevelLineViewModel.cs Presentation/WinForms/ViewModels/ChartViewModel.cs Presentation/WinForms/ViewModels/ChartSeriesViewModel.cs Presentation/WinForms/Charting/ChartViewModelFactory.cs Presentation/WinForms/Charting/ChartRenderer.cs JSQViewer.Tests/ChartLevelLineRenderingTests.cs
git commit -m "Уровни T8+ рисуются полосами оси Y"
```

---

### Task 4: Чек-лист и версия

**Files:**
- Modify: `doc/t8_plus_lines_manual_checklist.md`
- Modify: `Properties/AssemblyInfo.cs`
- Modify: `installer/JSQViewer.iss`

- [ ] **Step 1: Переписать чек-лист**

Заменить содержимое `doc/t8_plus_lines_manual_checklist.md`:

```markdown
# Чек-лист проверки уровней статистики T8+

Автоматические тесты покрывают отбор каналов, расчёт рядов, выбор отсчёта на крае видимого участка, состав уровней и их отрисовку полосами. Внешний вид на живом графике и поведение окон WinForms проверяются вручную. Пункт, который не удалось выполнить, фиксируется отдельно, а не отмечается пройденным.

1. Открыть один прогон. В окне источника под строкой «Выбранные» видна строка «T8+:» с галками «мин», «сред», «макс», все сняты.
2. График до включения галок выглядит ровно как до этой работы; цвета каналов прежние.
3. Включить «сред» — появляется горизонтальная линия с подписью вида «T8+ сред 4.7».
4. Включить «мин» и «макс» — добавляются точечная и штрих-пунктирная горизонтали того же цвета.
5. Цвета каналов при включении и выключении галок не меняются, легенда не меняется.
6. Значение уровня совпадает со значением каналов T8…Tmax в конце видимого участка.
7. Сдвинуть правый край ползунка диапазона влево — уровни пересчитались под новый конец.
8. Задать ручной максимум оси X — уровни пересчитались под него.
9. Зум колесом мыши уровни не пересчитывает — это ожидаемое поведение, не дефект.
10. Снять галки с каналов T8…Tmax — уровни остаются на месте.
11. Открыть источник без каналов T8 и старше — галки неактивны, подсказка «У источника нет каналов T8 и старше».
12. Добавить второй прогон, включить уровни у обоих — цвета уровней у источников разные, подписи содержат имя источника.
13. Режим наложения с двумя прогонами разной длительности — уровень каждого взят по его собственному концу.
14. Навести курсор на график — подсказка перечисляет только каналы, уровни в неё не попадают; толщина линий после наведения не «худеет» до единицы.
15. Переключать галки несколько раз подряд — полос не накапливается, их ровно столько, сколько включено.
16. Сохранить раскладку, закрыть и открыть прогон заново — состояние галок восстановилось.
17. Открепить график в отдельное окно — уровни воспроизводятся в нём.
18. Сохранить изображение графика — уровни есть на картинке.
19. Экспорт по шаблону отрабатывает без ошибок и по содержимому не изменился.
```

- [ ] **Step 2: Поднять версию до 0.6.1**

В `Properties/AssemblyInfo.cs` заменить `0.6.0.0` на `0.6.1.0` в `AssemblyVersion` и `AssemblyFileVersion`, и `0.6.0` на `0.6.1` в `AssemblyInformationalVersion`. В `installer/JSQViewer.iss` заменить `#define MyAppVersion "0.6.0"` на `"0.6.1"`.

- [ ] **Step 3: Собрать Release и прогнать тесты**

Выполнить: `dotnet build .\JSQViewer.csproj -c Release` и `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Ожидается: 0 ошибок, 0 предупреждений; все тесты проходят.

- [ ] **Step 4: Коммит**

Каталог `doc/` в `.gitignore`, поэтому чек-лист добавляется принудительно.

```bash
git add -f doc/t8_plus_lines_manual_checklist.md
git add Properties/AssemblyInfo.cs installer/JSQViewer.iss
git commit -m "Обновлён чек-лист уровней T8+ и поднята версия до 0.6.1"
```

---

## Самопроверка плана

**Покрытие спеки:** раздел 1 спеки (уровни вместо серий) — задачи 1 и 2; раздел 2 (отрисовка полосами) — задача 3; раздел 3 (откат ненужного) — задачи 2 и 3; раздел 4 (переключатели и хранение) — уже реализован, не трогается; тестирование — задачи 1–3 плюс чек-лист в задаче 4.

**Согласованность имён:** `ChartLevelLine(sourceRoot, sourceIndex, role, value, label)`, `VisibleRangeEdgeResolver.ResolveIndex(long[], long)`, `ChartPipelineResult.LevelLines`, `ChartLevelLineViewModel.SourceIndex/Role/Value/Label`, `ChartViewModel.LevelLines`, `SourceColorPalette.ForSourceIndex(int)` — используются согласованно во всех задачах.

**Что остаётся от предыдущей работы и не трогается:** `T8PlusChannelSelector`, `T8PlusSeries`, `T8PlusSeriesBuilder`, `T8PlusSeriesRequest`, `ChartSeriesRole`, `SourceColorPalette`, `T8PlusLineSelection`, `WorkspaceLayoutState.SourceT8PlusLines`, `GetRecordingInfoUseCase`, галки в `UI/MainForm.cs` и локализация, починка толщины линий через `Series.Tag`.

**Главный риск:** `StripLine.TextAlignment` и положение подписи на живом графике — проверяется только вручную, пункты 3 и 12 чек-листа.
