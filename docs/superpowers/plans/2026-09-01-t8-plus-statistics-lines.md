# Линии статистики T8+ на графике — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Показать на графике три линии — минимум, среднее и максимум по массиву термопар T8…T\<макс\> каждого источника, — включаемые по отдельности из окна источника и однозначно различимые при сравнении нескольких прогонов.

**Architecture:** Расчёт, который сегодня живёт внутри `GetRecordingInfoUseCase.CalculateT8PlusStats` и схлопывается в скаляры, выносится в два класса без состояния — `T8PlusChannelSelector` (отбор колонок) и `T8PlusSeriesBuilder` (три временных ряда). Карточка «i» и график становятся двумя потребителями одного расчёта. Пайплайн графика получает по одному запросу на источник, строит из рядов дополнительные `ChartPipelineSeries` с ролью и корнем источника, прореживает их тем же шагом, что и каналы. Рендерер красит их по источнику и различает стилем линии по роли.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WinForms, System.Windows.Forms.DataVisualization (MS Chart), MSTest.

**Spec:** `docs/superpowers/specs/2026-09-01-t8-plus-statistics-lines-design.md`

## Global Constraints

- Язык C# 7.3, целевая платформа net48. Синтаксис новее C# 7.3 не использовать (нет `switch`-выражений, target-typed `new`, записей).
- Стиль по `AGENTS.md`: 4 пробела, фигурные скобки на своей строке, `PascalCase` для типов и открытых членов, `_camelCase` для приватных полей, явные модификаторы доступа.
- Слой `Application/` не ссылается на WinForms. Всё, что знает про `System.Windows.Forms` и `System.Drawing`, живёт в `Presentation/WinForms/` или `UI/`.
- Порог валидности температуры один на приложение: `RecordingTemperatureValueFilter.IsValidTemperature` — значение строго больше −90. Новых порогов не заводить.
- Номер первого канала группы — константа 8. Настраиваемым не делать.
- Сообщения коммитов — короткие русские в повелительном наклонении, один коммит на задачу.
- Оба проекта собирают исходники по маскам каталогов, поэтому новые файлы в `.csproj` вписывать не нужно.
- Полный прогон тестов: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`. Отдельный класс тестов: тот же вызов с `--filter FullyQualifiedName~ИмяКласса`.
- В конце каждой задачи сборка Debug обязана проходить без ошибок и предупреждений: `dotnet build .\JSQViewer.csproj -c Debug`.

---

## Файловая структура

| Файл | Ответственность |
|---|---|
| `Application/Charting/T8PlusChannelSelector.cs` | Создать. Разбор имени канала, номера T-канала, отбор колонок T≥N по источнику |
| `Application/Charting/T8PlusSeries.cs` | Создать. Результат построителя: три массива `double?[]` и признак наличия каналов |
| `Application/Charting/T8PlusSeriesBuilder.cs` | Создать. Расчёт трёх рядов по `TestData` и корню источника |
| `Application/Charting/T8PlusSeriesRequest.cs` | Создать. Запрос на линии одного источника: корень и три флага |
| `Application/Charting/ChartSeriesRole.cs` | Создать. Перечисление ролей серии |
| `Application/Charting/RecordingTemperatureValueFilter.cs` | Изменить. Убрать дублирующий разбор имени, делегировать селектору |
| `Application/Charting/ChartPipelineSeries.cs` | Изменить. Добавить `Role` и `SourceIndex` |
| `Application/Charting/ChartPipelineRequest.cs` | Изменить. Добавить необязательный параметр с запросами T8+ |
| `Application/Charting/ChartPipelineService.cs` | Изменить. Построение и прореживание линий T8+, правило легенды, кэш рядов |
| `Application/Recording/GetRecordingInfoUseCase.cs` | Изменить. Перейти на построитель, убрать свою копию расчёта и разбора имён |
| `Application/Channels/WorkspaceLayoutState.cs` | Изменить. Хранение флагов T8+ по источнику |
| `Application/Channels/WorkspaceLayoutStateService.cs` | Изменить. Сохранение и чтение флагов T8+ |
| `Presentation/WinForms/Charting/SourceColorPalette.cs` | Создать. Контрастная палитра цветов источников |
| `Presentation/WinForms/ViewModels/ChartSeriesViewModel.cs` | Изменить. Добавить `Role` и `SourceIndex` |
| `Presentation/WinForms/Charting/ChartViewModelFactory.cs` | Изменить. Перенос новых полей |
| `Presentation/WinForms/Charting/ChartRenderer.cs` | Изменить. Порядок добавления, цвет, стиль, толщина |
| `Infrastructure/Platform/DictionaryLocalizationService.cs` | Изменить. Ключи подписей и легенды, RU и EN |
| `UI/MainForm.cs` | Изменить. Галки в окне источника, проводка в запрос, восстановление толщины при подсветке |
| `doc/t8_plus_lines_manual_checklist.md` | Создать. Ручная проверка отрисовки |

---

### Task 1: Отбор каналов группы T8+

Сейчас разбор имени канала и номера T-канала существует в двух дословных копиях: приватные `NormalizeChannelName` и `TryGetTChannelNumber` в `GetRecordingInfoUseCase` и такие же приватные в `RecordingTemperatureValueFilter`. Задача сводит их в один открытый класс.

**Files:**
- Create: `Application/Charting/T8PlusChannelSelector.cs`
- Modify: `Application/Charting/RecordingTemperatureValueFilter.cs`
- Test: `JSQViewer.Tests/T8PlusChannelSelectorTests.cs`

**Interfaces:**
- Consumes: `JSQViewer.Core.TestData` (поля `SourceColumns`, `ColumnNames`).
- Produces:
  - `public static class T8PlusChannelSelector`
  - `public const int DefaultMinimumNumber = 8;`
  - `public static string NormalizeChannelName(string columnName)`
  - `public static bool TryGetChannelNumber(string columnName, out int number)`
  - `public static List<string> SelectColumns(TestData data, string sourceRoot, int minimumNumber)`

- [ ] **Step 1: Написать падающий тест**

Создать `JSQViewer.Tests/T8PlusChannelSelectorTests.cs`:

```csharp
using System.Collections.Generic;
using JSQViewer.Application.Charting;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class T8PlusChannelSelectorTests
    {
        [TestMethod]
        public void TryGetChannelNumber_ParsesPlainAndDecoratedNames()
        {
            int number;

            Assert.IsTrue(T8PlusChannelSelector.TryGetChannelNumber("T8", out number));
            Assert.AreEqual(8, number);

            Assert.IsTrue(T8PlusChannelSelector.TryGetChannelNumber("C:\\run::T10", out number));
            Assert.AreEqual(10, number);

            Assert.IsTrue(T8PlusChannelSelector.TryGetChannelNumber("T12#2", out number));
            Assert.AreEqual(12, number);

            Assert.IsFalse(T8PlusChannelSelector.TryGetChannelNumber("T-sie", out number));
            Assert.IsFalse(T8PlusChannelSelector.TryGetChannelNumber("W", out number));
            Assert.IsFalse(T8PlusChannelSelector.TryGetChannelNumber(null, out number));
        }

        [TestMethod]
        public void SelectColumns_TakesOnlyOwnSourceAndNumbersAtOrAboveThreshold()
        {
            var data = new TestData();
            data.SourceColumns["A"] = new[] { "T1", "T7", "T8", "T10", "T-sie", "W" };
            data.SourceColumns["B"] = new[] { "T9" };

            List<string> columns = T8PlusChannelSelector.SelectColumns(data, "A", 8);

            CollectionAssert.AreEqual(new[] { "T8", "T10" }, columns);
        }

        [TestMethod]
        public void SelectColumns_FallsBackToColumnNamesForSingleSource()
        {
            var data = new TestData();
            data.ColumnNames = new[] { "T7", "T8", "T9" };

            List<string> columns = T8PlusChannelSelector.SelectColumns(data, "unknown", 8);

            CollectionAssert.AreEqual(new[] { "T8", "T9" }, columns);
        }

        [TestMethod]
        public void SelectColumns_WithoutMatchingChannels_ReturnsEmpty()
        {
            var data = new TestData();
            data.SourceColumns["A"] = new[] { "T1", "T-sie" };

            Assert.AreEqual(0, T8PlusChannelSelector.SelectColumns(data, "A", 8).Count);
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~T8PlusChannelSelectorTests`
Ожидается: ошибка компиляции — тип `T8PlusChannelSelector` не найден.

- [ ] **Step 3: Создать селектор**

Создать `Application/Charting/T8PlusChannelSelector.cs`:

```csharp
using System;
using System.Collections.Generic;
using JSQViewer.Core;

namespace JSQViewer.Application.Charting
{
    /// <summary>
    /// Отбор каналов массива термопар источника и разбор имён T-каналов.
    /// Единственное место в приложении, где имя канала разбирается на префикс,
    /// суффикс источника и номер: до выделения этого класса разбор существовал
    /// в двух дословных копиях.
    /// </summary>
    public static class T8PlusChannelSelector
    {
        public const int DefaultMinimumNumber = 8;

        public static List<string> SelectColumns(TestData data, string sourceRoot, int minimumNumber)
        {
            var result = new List<string>();
            if (data == null)
            {
                return result;
            }

            string[] columns;
            if (data.SourceColumns != null
                && sourceRoot != null
                && data.SourceColumns.TryGetValue(sourceRoot, out columns)
                && columns != null)
            {
                AddColumns(result, columns, minimumNumber);
                return result;
            }

            if (data.ColumnNames != null && (data.SourceColumns == null || data.SourceColumns.Count <= 1))
            {
                AddColumns(result, data.ColumnNames, minimumNumber);
            }

            return result;
        }

        public static bool TryGetChannelNumber(string columnName, out int number)
        {
            number = 0;
            if (string.IsNullOrEmpty(columnName))
            {
                return false;
            }

            string name = NormalizeChannelName(columnName);
            if (name.Length < 2 || (name[0] != 'T' && name[0] != 't'))
            {
                return false;
            }

            string digits = name.Substring(1);
            if (digits.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < digits.Length; i++)
            {
                if (!char.IsDigit(digits[i]))
                {
                    return false;
                }
            }

            return int.TryParse(digits, out number);
        }

        public static string NormalizeChannelName(string columnName)
        {
            if (columnName == null)
            {
                return string.Empty;
            }

            string name = columnName.Trim();
            int separator = name.LastIndexOf("::", StringComparison.Ordinal);
            if (separator >= 0)
            {
                name = name.Substring(separator + 2);
            }

            int hash = name.LastIndexOf('#');
            if (hash > 0)
            {
                string hashPart = name.Substring(hash + 1);
                bool allDigits = hashPart.Length > 0;
                for (int i = 0; i < hashPart.Length; i++)
                {
                    if (!char.IsDigit(hashPart[i]))
                    {
                        allDigits = false;
                        break;
                    }
                }

                if (allDigits)
                {
                    name = name.Substring(0, hash);
                }
            }

            if (name.Length >= 3 && name[1] == '-')
            {
                name = name.Substring(2);
            }

            return name;
        }

        private static void AddColumns(List<string> result, string[] columns, int minimumNumber)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                int number;
                if (TryGetChannelNumber(columns[i], out number) && number >= minimumNumber)
                {
                    result.Add(columns[i]);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Свернуть дубликат в фильтре температур**

Заменить всё содержимое `Application/Charting/RecordingTemperatureValueFilter.cs` на:

```csharp
namespace JSQViewer.Application.Charting
{
    internal static class RecordingTemperatureValueFilter
    {
        public static bool IsTemperatureChannel(string channelCode)
        {
            int number;
            return T8PlusChannelSelector.TryGetChannelNumber(channelCode, out number);
        }

        public static bool IsValidTemperature(double value)
        {
            return value > -90.0;
        }
    }
}
```

- [ ] **Step 5: Прогнать тесты**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Ожидается: все тесты проходят, включая существующие про фильтр температур и карточку «i».

- [ ] **Step 6: Собрать**

Выполнить: `dotnet build .\JSQViewer.csproj -c Debug`
Ожидается: 0 ошибок, 0 предупреждений.

- [ ] **Step 7: Коммит**

```bash
git add Application/Charting/T8PlusChannelSelector.cs Application/Charting/RecordingTemperatureValueFilter.cs JSQViewer.Tests/T8PlusChannelSelectorTests.cs
git commit -m "Выделен отбор каналов группы T8+ в отдельный класс"
```

---

### Task 2: Построитель временных рядов T8+

**Files:**
- Create: `Application/Charting/T8PlusSeries.cs`
- Create: `Application/Charting/T8PlusSeriesBuilder.cs`
- Test: `JSQViewer.Tests/T8PlusSeriesBuilderTests.cs`

**Interfaces:**
- Consumes: `T8PlusChannelSelector.SelectColumns`, `RecordingTemperatureValueFilter.IsValidTemperature` (последний `internal`, тесты видят его через `InternalsVisibleTo`? — нет, тесты его не трогают напрямую).
- Produces:
  - `public sealed class T8PlusSeries` со свойствами `bool HasChannels`, `double?[] Minimum`, `double?[] Average`, `double?[] Maximum` и `public static readonly T8PlusSeries Empty`
  - `public sealed class T8PlusSeriesBuilder` с методом `public T8PlusSeries Build(TestData data, string sourceRoot)`

- [ ] **Step 1: Написать падающий тест**

Создать `JSQViewer.Tests/T8PlusSeriesBuilderTests.cs`:

```csharp
using JSQViewer.Application.Charting;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class T8PlusSeriesBuilderTests
    {
        private static TestData BuildData()
        {
            var data = new TestData();
            data.TimestampsMs = new[] { 1000L, 2000L, 3000L };
            data.RowCount = 3;
            data.SourceColumns["A"] = new[] { "T1", "T8", "T9", "T10" };
            data.Columns["T1"] = new double?[] { 100.0, 100.0, 100.0 };
            data.Columns["T8"] = new double?[] { 10.0, null, -95.0 };
            data.Columns["T9"] = new double?[] { 20.0, 4.0, -95.0 };
            data.Columns["T10"] = new double?[] { 30.0, 6.0, null };
            return data;
        }

        [TestMethod]
        public void Build_ComputesMinimumAverageAndMaximumAcrossChannels()
        {
            T8PlusSeries series = new T8PlusSeriesBuilder().Build(BuildData(), "A");

            Assert.IsTrue(series.HasChannels);
            Assert.AreEqual(10.0, series.Minimum[0].Value, 1e-9);
            Assert.AreEqual(20.0, series.Average[0].Value, 1e-9);
            Assert.AreEqual(30.0, series.Maximum[0].Value, 1e-9);
        }

        [TestMethod]
        public void Build_IgnoresMissingValuesWhenAveraging()
        {
            T8PlusSeries series = new T8PlusSeriesBuilder().Build(BuildData(), "A");

            // На втором отсчёте T8 пуст, среднее считается по T9 и T10.
            Assert.AreEqual(4.0, series.Minimum[1].Value, 1e-9);
            Assert.AreEqual(5.0, series.Average[1].Value, 1e-9);
            Assert.AreEqual(6.0, series.Maximum[1].Value, 1e-9);
        }

        [TestMethod]
        public void Build_WhenSampleHasNoValidValues_YieldsNullInAllThreeSeries()
        {
            T8PlusSeries series = new T8PlusSeriesBuilder().Build(BuildData(), "A");

            // На третьем отсчёте T8 и T9 ниже порога валидности, T10 пуст.
            Assert.IsFalse(series.Minimum[2].HasValue);
            Assert.IsFalse(series.Average[2].HasValue);
            Assert.IsFalse(series.Maximum[2].HasValue);
        }

        [TestMethod]
        public void Build_IgnoresChannelsOfOtherSources()
        {
            TestData data = BuildData();
            data.SourceColumns["B"] = new[] { "T20" };
            data.Columns["T20"] = new double?[] { -50.0, -50.0, -50.0 };

            T8PlusSeries series = new T8PlusSeriesBuilder().Build(data, "A");

            Assert.AreEqual(10.0, series.Minimum[0].Value, 1e-9);
        }

        [TestMethod]
        public void Build_WithoutT8Channels_ReturnsEmptySeries()
        {
            var data = new TestData();
            data.TimestampsMs = new[] { 1000L };
            data.RowCount = 1;
            data.SourceColumns["A"] = new[] { "T1", "T-sie" };

            T8PlusSeries series = new T8PlusSeriesBuilder().Build(data, "A");

            Assert.IsFalse(series.HasChannels);
            Assert.AreEqual(0, series.Average.Length);
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~T8PlusSeriesBuilderTests`
Ожидается: ошибка компиляции — типы `T8PlusSeries` и `T8PlusSeriesBuilder` не найдены.

- [ ] **Step 3: Создать тип результата**

Создать `Application/Charting/T8PlusSeries.cs`:

```csharp
namespace JSQViewer.Application.Charting
{
    /// <summary>
    /// Три временных ряда по массиву термопар источника, выровненные
    /// по <see cref="JSQViewer.Core.TestData.TimestampsMs"/>.
    /// </summary>
    public sealed class T8PlusSeries
    {
        public static readonly T8PlusSeries Empty = new T8PlusSeries(false, new double?[0], new double?[0], new double?[0]);

        public T8PlusSeries(bool hasChannels, double?[] minimum, double?[] average, double?[] maximum)
        {
            HasChannels = hasChannels;
            Minimum = minimum ?? new double?[0];
            Average = average ?? new double?[0];
            Maximum = maximum ?? new double?[0];
        }

        public bool HasChannels { get; private set; }

        public double?[] Minimum { get; private set; }

        public double?[] Average { get; private set; }

        public double?[] Maximum { get; private set; }
    }
}
```

- [ ] **Step 4: Создать построитель**

Создать `Application/Charting/T8PlusSeriesBuilder.cs`:

```csharp
using System.Collections.Generic;
using JSQViewer.Core;

namespace JSQViewer.Application.Charting
{
    /// <summary>
    /// Считает минимум, среднее и максимум по массиву термопар источника
    /// на каждом отсчёте времени. Без состояния: кэширование — забота вызывающего.
    /// </summary>
    public sealed class T8PlusSeriesBuilder
    {
        public T8PlusSeries Build(TestData data, string sourceRoot)
        {
            if (data == null || data.TimestampsMs == null || data.TimestampsMs.Length == 0)
            {
                return T8PlusSeries.Empty;
            }

            List<string> columns = T8PlusChannelSelector.SelectColumns(
                data, sourceRoot, T8PlusChannelSelector.DefaultMinimumNumber);
            if (columns.Count == 0)
            {
                return T8PlusSeries.Empty;
            }

            var values = new List<double?[]>(columns.Count);
            for (int c = 0; c < columns.Count; c++)
            {
                double?[] column;
                if (data.Columns != null && data.Columns.TryGetValue(columns[c], out column) && column != null)
                {
                    values.Add(column);
                }
            }

            if (values.Count == 0)
            {
                return T8PlusSeries.Empty;
            }

            int length = data.TimestampsMs.Length;
            var minimum = new double?[length];
            var average = new double?[length];
            var maximum = new double?[length];

            for (int i = 0; i < length; i++)
            {
                double sum = 0d;
                double min = 0d;
                double max = 0d;
                int count = 0;

                for (int c = 0; c < values.Count; c++)
                {
                    double?[] column = values[c];
                    if (i >= column.Length || !column[i].HasValue)
                    {
                        continue;
                    }

                    double value = column[i].Value;
                    if (!RecordingTemperatureValueFilter.IsValidTemperature(value))
                    {
                        continue;
                    }

                    if (count == 0 || value < min)
                    {
                        min = value;
                    }

                    if (count == 0 || value > max)
                    {
                        max = value;
                    }

                    sum += value;
                    count++;
                }

                if (count == 0)
                {
                    continue;
                }

                minimum[i] = min;
                average[i] = sum / count;
                maximum[i] = max;
            }

            return new T8PlusSeries(true, minimum, average, maximum);
        }
    }
}
```

- [ ] **Step 5: Прогнать тесты**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~T8PlusSeriesBuilderTests`
Ожидается: PASS, 5 тестов.

- [ ] **Step 6: Коммит**

```bash
git add Application/Charting/T8PlusSeries.cs Application/Charting/T8PlusSeriesBuilder.cs JSQViewer.Tests/T8PlusSeriesBuilderTests.cs
git commit -m "Добавлен построитель рядов статистики T8+"
```

---

### Task 3: Карточка «i» переходит на общий расчёт

Сейчас `CalculateT8PlusStats` считает те же величины самостоятельно. После задачи расчёт остаётся один, а карточка сводит его в скаляры. Поведение обязано совпасть до последней цифры — существующие тесты правиться не должны.

**Files:**
- Modify: `Application/Recording/GetRecordingInfoUseCase.cs:279-424` (метод `CalculateT8PlusStats`), удаление приватных `FindTColumns`, `AddTColumns`, `TryGetTChannelNumber`, `NormalizeChannelName`
- Test: `JSQViewer.Tests/GetRecordingInfoUseCaseTests.cs` (файл существует; новых тестов не требуется, кроме шага 1)

**Interfaces:**
- Consumes: `T8PlusSeriesBuilder.Build(TestData, string)`, `T8PlusSeries`.
- Produces: публичный контракт `GetRecordingInfoUseCase` не меняется.

- [ ] **Step 1: Написать тест, фиксирующий эквивалентность**

Добавить метод в существующий класс тестов в `JSQViewer.Tests/GetRecordingInfoUseCaseTests.cs`. Если в файле ещё нет `using JSQViewer.Application.Charting;`, добавить его:

```csharp
        [TestMethod]
        public void Execute_T8PlusStats_MatchesSeriesBuilderOutput()
        {
            var data = new TestData();
            data.TimestampsMs = new[] { 0L, 60_000L, 120_000L };
            data.RowCount = 3;
            data.SourceColumns["A"] = new[] { "T8", "T9" };
            data.Columns["T8"] = new double?[] { 20.0, 8.0, 4.0 };
            data.Columns["T9"] = new double?[] { 22.0, 10.0, 6.0 };
            data.SourceStartMs["A"] = 0L;
            data.SourceEndMs["A"] = 120_000L;

            var useCase = new GetRecordingInfoUseCase(new TimestampRangeService());
            RecordingInfoResult result = useCase.Execute(data, "A");

            T8PlusSeries series = new T8PlusSeriesBuilder().Build(data, "A");

            Assert.IsTrue(result.T8PlusStats.HasChannels);
            // Наименьшее среднее по отсчётам — последний отсчёт: (4 + 6) / 2.
            Assert.AreEqual(series.Average[2].Value, result.T8PlusStats.AverageValue.Value, 1e-9);
            Assert.AreEqual(series.Minimum[2].Value, result.T8PlusStats.MinimumValue.Value, 1e-9);
            Assert.AreEqual(series.Maximum[2].Value, result.T8PlusStats.MaximumValue.Value, 1e-9);
        }
```

- [ ] **Step 2: Прогнать тест до правки**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~GetRecordingInfoUseCaseTests`
Ожидается: PASS. Тест закрепляет текущее поведение перед переносом расчёта — это страховочная сеть, а не красная фаза.

- [ ] **Step 3: Перевести расчёт на построитель**

В `Application/Recording/GetRecordingInfoUseCase.cs` добавить `using JSQViewer.Application.Charting;` (уже есть) и приватное поле рядом с остальными:

```csharp
        private readonly T8PlusSeriesBuilder _t8PlusSeriesBuilder = new T8PlusSeriesBuilder();
```

Заменить тело `CalculateT8PlusStats` на свод по готовым рядам. Отбор колонок и поканальный цикл уходят полностью; порядок обхода, пороги и правила «лучшего» значения сохраняются дословно, включая то, что лучший максимум ищется как **наименьший** из максимумов:

```csharp
        private T8PlusTemperatureStats CalculateT8PlusStats(
            TestData data,
            string sourceRoot,
            int i0,
            int i1,
            long startMs,
            T8PlusTemperatureThresholds thresholds)
        {
            T8PlusSeries series = _t8PlusSeriesBuilder.Build(data, sourceRoot);
            if (!series.HasChannels)
            {
                return null;
            }

            var stats = new T8PlusTemperatureStats { HasChannels = true };
            double bestAverage = double.MaxValue;
            long bestAverageTimestampMs = 0L;
            bool hasAverage = false;
            double? firstAverage = null;
            long firstAverageTimestampMs = 0L;
            double bestMinimum = double.MaxValue;
            long bestMinimumTimestampMs = 0L;
            bool hasMinimum = false;
            double bestMaximum = double.MaxValue;
            long bestMaximumTimestampMs = 0L;
            bool hasMaximum = false;

            for (int i = i0; i < i1; i++)
            {
                if (i >= series.Average.Length || !series.Average[i].HasValue)
                {
                    continue;
                }

                long timestampMs = data.TimestampsMs[i];
                double average = series.Average[i].Value;
                double min = series.Minimum[i].Value;
                double max = series.Maximum[i].Value;

                if (!firstAverage.HasValue)
                {
                    firstAverage = average;
                    firstAverageTimestampMs = timestampMs;
                }

                if (average < bestAverage)
                {
                    bestAverage = average;
                    bestAverageTimestampMs = timestampMs;
                    hasAverage = true;
                }

                if (min < bestMinimum)
                {
                    bestMinimum = min;
                    bestMinimumTimestampMs = timestampMs;
                    hasMinimum = true;
                }

                // Именно «меньше»: карточка ищет момент, когда самый тёплый
                // датчик опустился ниже всего, а не глобальный максимум прогона.
                if (max < bestMaximum)
                {
                    bestMaximum = max;
                    bestMaximumTimestampMs = timestampMs;
                    hasMaximum = true;
                }

                if (!stats.AverageReached && average <= thresholds.AverageThreshold)
                {
                    stats.AverageReached = true;
                    stats.AverageValue = average;
                    stats.AverageElapsedMs = timestampMs - startMs;
                    stats.AverageTime = _timestampRangeService.UnixMsToLocalDateTime(timestampMs);
                }

                if (!stats.MinimumReached && min <= thresholds.MinimumThreshold)
                {
                    stats.MinimumReached = true;
                    stats.MinimumValue = min;
                    stats.MinimumElapsedMs = timestampMs - startMs;
                    stats.MinimumTime = _timestampRangeService.UnixMsToLocalDateTime(timestampMs);
                }

                if (!stats.MaximumReached && max <= thresholds.MaximumThreshold)
                {
                    stats.MaximumReached = true;
                    stats.MaximumValue = max;
                    stats.MaximumElapsedMs = timestampMs - startMs;
                    stats.MaximumTime = _timestampRangeService.UnixMsToLocalDateTime(timestampMs);
                }
            }

            if (!stats.AverageReached && hasAverage)
            {
                stats.AverageValue = bestAverage;
                stats.AverageElapsedMs = bestAverageTimestampMs - startMs;
                stats.AverageTime = _timestampRangeService.UnixMsToLocalDateTime(bestAverageTimestampMs);
            }

            if (firstAverage.HasValue && hasAverage)
            {
                double durationMin = (bestAverageTimestampMs - firstAverageTimestampMs) / 60_000.0;
                if (durationMin > 0)
                {
                    stats.AverageDropRatePerMinute = (bestAverage - firstAverage.Value) / durationMin;
                }
            }

            if (!stats.MinimumReached && hasMinimum)
            {
                stats.MinimumValue = bestMinimum;
                stats.MinimumElapsedMs = bestMinimumTimestampMs - startMs;
                stats.MinimumTime = _timestampRangeService.UnixMsToLocalDateTime(bestMinimumTimestampMs);
            }

            if (!stats.MaximumReached && hasMaximum)
            {
                stats.MaximumValue = bestMaximum;
                stats.MaximumElapsedMs = bestMaximumTimestampMs - startMs;
                stats.MaximumTime = _timestampRangeService.UnixMsToLocalDateTime(bestMaximumTimestampMs);
            }

            return stats;
        }
```

- [ ] **Step 4: Удалить осиротевшие приватные методы**

Из `GetRecordingInfoUseCase` удалить `FindTColumns` и `AddTColumns` целиком. `TryGetTChannelNumber` и `NormalizeChannelName` оставить **только** если на них ещё ссылаются `FindT1Column`/`FindT1InArray`/`IsWChannel`; в этом случае перевести их на `T8PlusChannelSelector.TryGetChannelNumber` и `T8PlusChannelSelector.NormalizeChannelName` и удалить приватные копии. Компилятор укажет оставшиеся ссылки.

- [ ] **Step 5: Прогнать весь набор**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Ожидается: все тесты проходят. Ни один существующий тест карточки «i» править нельзя — если он упал, расхождение в переносе, а не в тесте.

- [ ] **Step 6: Собрать**

Выполнить: `dotnet build .\JSQViewer.csproj -c Debug`
Ожидается: 0 ошибок, 0 предупреждений.

- [ ] **Step 7: Коммит**

```bash
git add Application/Recording/GetRecordingInfoUseCase.cs JSQViewer.Tests/GetRecordingInfoUseCaseTests.cs
git commit -m "Карточка прогона считает статистику T8+ общим построителем"
```

---

### Task 4: Линии T8+ в пайплайне графика

**Files:**
- Create: `Application/Charting/ChartSeriesRole.cs`
- Create: `Application/Charting/T8PlusSeriesRequest.cs`
- Modify: `Application/Charting/ChartPipelineSeries.cs`
- Modify: `Application/Charting/ChartPipelineRequest.cs:80-125` (сигнатура и тело `ForChart`)
- Modify: `Application/Charting/ChartPipelineService.cs`
- Test: `JSQViewer.Tests/T8PlusChartPipelineTests.cs`

**Interfaces:**
- Consumes: `T8PlusSeriesBuilder.Build`, `T8PlusSeries`, `SourceDisplayNameResolver.Resolve(TestData, string)`.
- Produces:
  - `public enum ChartSeriesRole { Channel, T8Minimum, T8Average, T8Maximum }`
  - `public sealed class T8PlusSeriesRequest` со свойствами `string SourceRoot`, `bool ShowMinimum`, `bool ShowAverage`, `bool ShowMaximum` и конструктором `T8PlusSeriesRequest(string sourceRoot, bool showMinimum, bool showAverage, bool showMaximum)`
  - `ChartPipelineSeries.Role` типа `ChartSeriesRole`, `ChartPipelineSeries.SourceIndex` типа `int`
  - `ChartPipelineRequest.T8PlusSeries` типа `IReadOnlyList<T8PlusSeriesRequest>`
  - у `ForChart` новый последний необязательный параметр `IReadOnlyList<T8PlusSeriesRequest> t8PlusSeries = null`

- [ ] **Step 1: Написать падающие тесты**

Создать `JSQViewer.Tests/T8PlusChartPipelineTests.cs`:

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

        private static ChartPipelineRequest Request(TestData data, IReadOnlyList<T8PlusSeriesRequest> t8)
        {
            return ChartPipelineRequest.ForChart(
                data, new[] { "T1" }, false, 1, false, 1, 1000, 1,
                double.NaN, double.NaN, null, null, false, null, t8);
        }

        [TestMethod]
        public void Execute_WithAllThreeFlags_AddsThreeSeriesWithRolesAndSourceRoot()
        {
            TestData data = BuildData();
            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };

            ChartPipelineResult result = CreateService().Execute(Request(data, t8));

            List<ChartPipelineSeries> extra = result.Series
                .Where(s => s.Role != ChartSeriesRole.Channel)
                .ToList();

            Assert.AreEqual(3, extra.Count);
            CollectionAssert.AreEquivalent(
                new[] { ChartSeriesRole.T8Minimum, ChartSeriesRole.T8Average, ChartSeriesRole.T8Maximum },
                extra.Select(s => s.Role).ToArray());
            Assert.IsTrue(extra.All(s => s.SourceRoot == "A"));
            Assert.IsTrue(extra.All(s => s.SourceIndex == 0));
        }

        [TestMethod]
        public void Execute_WithSingleFlag_AddsOnlyThatSeriesWithExpectedValues()
        {
            TestData data = BuildData();
            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };

            ChartPipelineResult result = CreateService().Execute(Request(data, t8));

            ChartPipelineSeries average = result.Series.Single(s => s.Role == ChartSeriesRole.T8Average);

            Assert.AreEqual(4, average.YValues.Length);
            Assert.AreEqual(15.0, average.YValues[0], 1e-9);
            Assert.AreEqual(9.0, average.YValues[3], 1e-9);
        }

        [TestMethod]
        public void Execute_WithoutFlags_AddsNothing()
        {
            TestData data = BuildData();

            ChartPipelineResult result = CreateService().Execute(Request(data, null));

            Assert.IsTrue(result.Series.All(s => s.Role == ChartSeriesRole.Channel));
        }

        [TestMethod]
        public void Execute_WithoutT8Channels_AddsNothing()
        {
            var data = new TestData();
            data.Root = "A";
            data.TimestampsMs = new[] { 0L, 1000L };
            data.RowCount = 2;
            data.SourceOrder = new[] { "A" };
            data.SourceColumns["A"] = new[] { "T1" };
            data.CodeSources["T1"] = "A";
            data.Columns["T1"] = new double?[] { 1.0, 1.0 };
            var t8 = new[] { new T8PlusSeriesRequest("A", true, true, true) };

            ChartPipelineResult result = CreateService().Execute(Request(data, t8));

            Assert.IsTrue(result.Series.All(s => s.Role == ChartSeriesRole.Channel));
        }

        [TestMethod]
        public void Execute_RespectsDecimationStep()
        {
            TestData data = BuildData();
            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };
            ChartPipelineRequest request = ChartPipelineRequest.ForChart(
                data, new[] { "T1" }, false, 1, false, 2, 1000, 1,
                double.NaN, double.NaN, null, null, false, null, t8);

            ChartPipelineResult result = CreateService().Execute(request);

            ChartPipelineSeries average = result.Series.Single(s => s.Role == ChartSeriesRole.T8Average);
            ChartPipelineSeries channel = result.Series.Single(s => s.Role == ChartSeriesRole.Channel);

            Assert.AreEqual(2, result.Step);
            Assert.AreEqual(channel.XValues.Length, average.XValues.Length);
            Assert.AreEqual(15.0, average.YValues[0], 1e-9);
            Assert.AreEqual(11.0, average.YValues[1], 1e-9);
        }

        [TestMethod]
        public void Execute_WhenManyChannels_KeepsOnlyT8LinesInLegend()
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

            var t8 = new[] { new T8PlusSeriesRequest("A", false, true, false) };
            ChartPipelineResult result = CreateService().Execute(
                ChartPipelineRequest.ForChart(
                    data, columns, false, 1, false, 1, 1000, columns.Count,
                    double.NaN, double.NaN, null, null, false, null, t8));

            Assert.IsTrue(result.ShowLegend);
            Assert.IsTrue(result.Series.Where(s => s.Role == ChartSeriesRole.Channel).All(s => !s.IsVisibleInLegend));
            Assert.IsTrue(result.Series.Where(s => s.Role != ChartSeriesRole.Channel).All(s => s.IsVisibleInLegend));
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тесты падают**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~T8PlusChartPipelineTests`
Ожидается: ошибка компиляции — типы `ChartSeriesRole` и `T8PlusSeriesRequest` не найдены.

- [ ] **Step 3: Создать перечисление ролей**

Создать `Application/Charting/ChartSeriesRole.cs`:

```csharp
namespace JSQViewer.Application.Charting
{
    public enum ChartSeriesRole
    {
        Channel = 0,
        T8Minimum = 1,
        T8Average = 2,
        T8Maximum = 3
    }
}
```

- [ ] **Step 4: Создать запрос линий источника**

Создать `Application/Charting/T8PlusSeriesRequest.cs`:

```csharp
namespace JSQViewer.Application.Charting
{
    public sealed class T8PlusSeriesRequest
    {
        public T8PlusSeriesRequest(string sourceRoot, bool showMinimum, bool showAverage, bool showMaximum)
        {
            SourceRoot = sourceRoot ?? string.Empty;
            ShowMinimum = showMinimum;
            ShowAverage = showAverage;
            ShowMaximum = showMaximum;
        }

        public string SourceRoot { get; private set; }

        public bool ShowMinimum { get; private set; }

        public bool ShowAverage { get; private set; }

        public bool ShowMaximum { get; private set; }

        public bool HasAny
        {
            get { return ShowMinimum || ShowAverage || ShowMaximum; }
        }
    }
}
```

- [ ] **Step 5: Расширить серию пайплайна**

В `Application/Charting/ChartPipelineSeries.cs` добавить два свойства после `IsForecast`:

```csharp
        public ChartSeriesRole Role { get; set; }

        public int SourceIndex { get; set; }
```

- [ ] **Step 6: Расширить запрос пайплайна**

В `Application/Charting/ChartPipelineRequest.cs` добавить в конструктор инициализацию, свойство и параметр.

В приватном конструкторе после `YAxis = ChartAxisSettings.Automatic();` добавить:

```csharp
            T8PlusSeries = new T8PlusSeriesRequest[0];
```

Рядом с `DynamicsForecastRoleSelection` добавить свойство:

```csharp
        public IReadOnlyList<T8PlusSeriesRequest> T8PlusSeries { get; private set; }
```

В сигнатуру `ForChart` добавить последним параметром:

```csharp
            IReadOnlyList<T8PlusSeriesRequest> t8PlusSeries = null)
```

В создаваемом объекте после `DynamicsForecastRoleSelection = dynamicsForecastRoleSelection` добавить:

```csharp
                T8PlusSeries = t8PlusSeries == null
                    ? (IReadOnlyList<T8PlusSeriesRequest>)new T8PlusSeriesRequest[0]
                    : new List<T8PlusSeriesRequest>(t8PlusSeries)
```

- [ ] **Step 7: Строить линии в пайплайне**

В `Application/Charting/ChartPipelineService.cs` добавить поля рядом с существующими:

```csharp
        private readonly T8PlusSeriesBuilder _t8PlusSeriesBuilder = new T8PlusSeriesBuilder();
        private readonly Dictionary<string, T8PlusSeries> _t8PlusCache =
            new Dictionary<string, T8PlusSeries>(StringComparer.OrdinalIgnoreCase);
        private int _t8PlusCacheDataVersion = int.MinValue;
```

В `Execute`, сразу после блока построения прогноза (`if (overlayMode && request.IncludeDynamicsForecast) { ... }`) и **до** расчёта `dataMin`/`dataMax`, вставить:

```csharp
            int t8PlusCount = AppendT8PlusSeries(request, data, timestamps, step, series);
            if (t8PlusCount > 0)
            {
                // Линии T8+ обязаны оставаться подписанными даже там, где легенда
                // каналов скрыта из-за их количества, — иначе при сравнении
                // источников их невозможно опознать.
                for (int i = 0; i < series.Count; i++)
                {
                    if (series[i].Role == ChartSeriesRole.Channel)
                    {
                        series[i].IsVisibleInLegend = showLegend;
                    }
                }

                showLegend = true;
            }
```

Добавить приватные методы:

```csharp
        private int AppendT8PlusSeries(
            ChartPipelineRequest request,
            TestData data,
            long[] timestamps,
            int step,
            List<ChartPipelineSeries> series)
        {
            IReadOnlyList<T8PlusSeriesRequest> requests = request.T8PlusSeries;
            if (requests == null || requests.Count == 0 || timestamps.Length == 0)
            {
                return 0;
            }

            EnsureT8PlusCacheVersion(request.DataVersion);

            int added = 0;
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

                int sourceIndex = ResolveSourceIndex(data, item.SourceRoot);
                string sourceName = _sourceDisplayNameResolver.Resolve(data, item.SourceRoot);

                if (item.ShowMinimum)
                {
                    series.Add(BuildT8PlusSeries(
                        data, item.SourceRoot, sourceIndex, sourceName,
                        ChartSeriesRole.T8Minimum, built.Minimum, timestamps, step, request.OverlayMode));
                    added++;
                }

                if (item.ShowAverage)
                {
                    series.Add(BuildT8PlusSeries(
                        data, item.SourceRoot, sourceIndex, sourceName,
                        ChartSeriesRole.T8Average, built.Average, timestamps, step, request.OverlayMode));
                    added++;
                }

                if (item.ShowMaximum)
                {
                    series.Add(BuildT8PlusSeries(
                        data, item.SourceRoot, sourceIndex, sourceName,
                        ChartSeriesRole.T8Maximum, built.Maximum, timestamps, step, request.OverlayMode));
                    added++;
                }
            }

            return added;
        }

        private void EnsureT8PlusCacheVersion(int dataVersion)
        {
            if (_t8PlusCacheDataVersion == dataVersion)
            {
                return;
            }

            _t8PlusCache.Clear();
            _t8PlusCacheDataVersion = dataVersion;
        }

        private static ChartPipelineSeries BuildT8PlusSeries(
            TestData data,
            string sourceRoot,
            int sourceIndex,
            string sourceName,
            ChartSeriesRole role,
            double?[] values,
            long[] timestamps,
            int step,
            bool overlayMode)
        {
            // Срез каналов строится от первого отсчёта с шагом step, поэтому
            // индекс i-й точки среза в полном массиве равен i * step.
            var xList = new List<double>(timestamps.Length);
            var yList = new List<double>(timestamps.Length);
            long baseMs = overlayMode ? ResolveSourceBaseMs(data, sourceRoot, timestamps[0]) : timestamps[0];

            for (int i = 0; i < timestamps.Length; i++)
            {
                long index = (long)i * step;
                if (index >= values.Length)
                {
                    break;
                }

                double? value = values[(int)index];
                if (!value.HasValue)
                {
                    continue;
                }

                long relativeMs = Math.Max(0L, timestamps[i] - baseMs);
                xList.Add(overlayMode ? relativeMs / 3600000.0 : timestamps[i]);
                yList.Add(value.Value);
            }

            return new ChartPipelineSeries
            {
                Code = sourceRoot,
                LegendText = BuildT8PlusLegendText(sourceName, role),
                SourceRoot = sourceRoot,
                SourceIndex = sourceIndex,
                Role = role,
                XValues = xList.ToArray(),
                YValues = yList.ToArray(),
                BorderWidth = role == ChartSeriesRole.T8Average ? 3 : 2,
                IsVisibleInLegend = true
            };
        }

        private static string BuildT8PlusLegendText(string sourceName, ChartSeriesRole role)
        {
            string suffix;
            if (role == ChartSeriesRole.T8Minimum)
            {
                suffix = "T8+ мин";
            }
            else if (role == ChartSeriesRole.T8Maximum)
            {
                suffix = "T8+ макс";
            }
            else
            {
                suffix = "T8+ сред";
            }

            return string.IsNullOrWhiteSpace(sourceName)
                ? suffix
                : string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", sourceName, suffix);
        }

        private static long ResolveSourceBaseMs(TestData data, string sourceRoot, long fallbackMs)
        {
            long startMs;
            if (data != null
                && data.SourceStartMs != null
                && !string.IsNullOrWhiteSpace(sourceRoot)
                && data.SourceStartMs.TryGetValue(sourceRoot, out startMs))
            {
                return startMs;
            }

            return fallbackMs;
        }

        private static int ResolveSourceIndex(TestData data, string sourceRoot)
        {
            if (data == null || data.SourceOrder == null)
            {
                return 0;
            }

            for (int i = 0; i < data.SourceOrder.Length; i++)
            {
                if (string.Equals(data.SourceOrder[i], sourceRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }
```

- [ ] **Step 8: Прогнать новые тесты**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~T8PlusChartPipelineTests`
Ожидается: PASS, 6 тестов.

- [ ] **Step 9: Прогнать весь набор**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Ожидается: все тесты проходят. Существующие тесты `ChartPipelineTests` вызывают `ForChart` без нового параметра — он необязательный, правки не требуются.

- [ ] **Step 10: Коммит**

```bash
git add Application/Charting/ChartSeriesRole.cs Application/Charting/T8PlusSeriesRequest.cs Application/Charting/ChartPipelineSeries.cs Application/Charting/ChartPipelineRequest.cs Application/Charting/ChartPipelineService.cs JSQViewer.Tests/T8PlusChartPipelineTests.cs
git commit -m "Пайплайн графика строит линии статистики T8+"
```

---

### Task 5: Палитра источников и отрисовка линий

**Files:**
- Create: `Presentation/WinForms/Charting/SourceColorPalette.cs`
- Modify: `Presentation/WinForms/ViewModels/ChartSeriesViewModel.cs`
- Modify: `Presentation/WinForms/Charting/ChartViewModelFactory.cs:22-36`
- Modify: `Presentation/WinForms/Charting/ChartRenderer.cs:33-45`
- Test: `JSQViewer.Tests/SourceColorPaletteTests.cs`

**Interfaces:**
- Consumes: `ChartSeriesRole`, `ChartPipelineSeries.Role`, `ChartPipelineSeries.SourceIndex`.
- Produces:
  - `public static class SourceColorPalette` с `public static Color ForSourceIndex(int index)` и `public static int Count { get; }`
  - `ChartSeriesViewModel.Role` типа `ChartSeriesRole`, `ChartSeriesViewModel.SourceIndex` типа `int`

- [ ] **Step 1: Написать падающий тест**

Создать `JSQViewer.Tests/SourceColorPaletteTests.cs`:

```csharp
using System.Drawing;
using JSQViewer.Presentation.WinForms.Charting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class SourceColorPaletteTests
    {
        [TestMethod]
        public void ForSourceIndex_GivesDistinctColorsToNeighbouringSources()
        {
            Assert.AreNotEqual(SourceColorPalette.ForSourceIndex(0), SourceColorPalette.ForSourceIndex(1));
            Assert.AreNotEqual(SourceColorPalette.ForSourceIndex(1), SourceColorPalette.ForSourceIndex(2));
        }

        [TestMethod]
        public void ForSourceIndex_WrapsAroundAndHandlesNegativeIndex()
        {
            Assert.AreEqual(SourceColorPalette.ForSourceIndex(0), SourceColorPalette.ForSourceIndex(SourceColorPalette.Count));
            Assert.AreEqual(SourceColorPalette.ForSourceIndex(0), SourceColorPalette.ForSourceIndex(-1));
        }

        [TestMethod]
        public void ForSourceIndex_ReturnsOpaqueColors()
        {
            for (int i = 0; i < SourceColorPalette.Count; i++)
            {
                Color color = SourceColorPalette.ForSourceIndex(i);
                Assert.AreEqual(255, color.A);
            }
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~SourceColorPaletteTests`
Ожидается: ошибка компиляции — тип `SourceColorPalette` не найден.

- [ ] **Step 3: Создать палитру**

Создать `Presentation/WinForms/Charting/SourceColorPalette.cs`:

```csharp
using System.Drawing;

namespace JSQViewer.Presentation.WinForms.Charting
{
    /// <summary>
    /// Цвета линий статистики T8+ по номеру источника. Своя палитра, а не палитра
    /// каналов: та назначается MS Chart автоматически, занимать её нельзя.
    /// </summary>
    public static class SourceColorPalette
    {
        private static readonly Color[] Colors =
        {
            Color.FromArgb(255, 20, 20, 20),
            Color.FromArgb(255, 200, 30, 30),
            Color.FromArgb(255, 20, 90, 200),
            Color.FromArgb(255, 20, 130, 60),
            Color.FromArgb(255, 150, 40, 170),
            Color.FromArgb(255, 200, 110, 0)
        };

        public static int Count
        {
            get { return Colors.Length; }
        }

        public static Color ForSourceIndex(int index)
        {
            int normalized = index % Colors.Length;
            if (normalized < 0)
            {
                normalized += Colors.Length;
            }

            return Colors[normalized];
        }
    }
}
```

- [ ] **Step 4: Пронести роль до модели представления**

В `Presentation/WinForms/ViewModels/ChartSeriesViewModel.cs` добавить `using JSQViewer.Application.Charting;` и два свойства после `IsForecast`:

```csharp
        public ChartSeriesRole Role { get; set; }

        public int SourceIndex { get; set; }
```

В `ChartViewModelFactory.Create`, в создаваемом `ChartSeriesViewModel`, после `IsForecast = item.IsForecast` добавить:

```csharp
                    Role = item.Role,
                    SourceIndex = item.SourceIndex
```

- [ ] **Step 5: Отрисовать линии**

В `Presentation/WinForms/Charting/ChartRenderer.cs` заменить цикл добавления серий на двухпроходный: сначала канальные серии (их цвет по-прежнему раздаёт MS Chart), затем линии T8+ с явным цветом. Порядок важен: серия с уже заданным цветом не должна вклиниваться в раздачу палитры каналов.

Заменить блок `for (int i = 0; i < viewModel.Series.Count; i++) { ... }` на:

```csharp
                for (int i = 0; i < viewModel.Series.Count; i++)
                {
                    ChartSeriesViewModel model = viewModel.Series[i];
                    if (model.Role != ChartSeriesRole.Channel)
                    {
                        continue;
                    }

                    chart.Series.Add(CreateSeries(chart, viewModel, model, i));
                }

                for (int i = 0; i < viewModel.Series.Count; i++)
                {
                    ChartSeriesViewModel model = viewModel.Series[i];
                    if (model.Role == ChartSeriesRole.Channel)
                    {
                        continue;
                    }

                    Series series = CreateSeries(chart, viewModel, model, i);
                    series.Color = SourceColorPalette.ForSourceIndex(model.SourceIndex);
                    series.BorderDashStyle = ResolveT8PlusDashStyle(model.Role);
                    chart.Series.Add(series);
                }
```

Добавить приватные методы в тот же класс:

```csharp
        private static Series CreateSeries(Chart chart, ChartViewModel viewModel, ChartSeriesViewModel model, int index)
        {
            // Имя серии в MS Chart обязано быть уникальным, а Code у линий T8+
            // совпадает с корнем источника и повторяется трижды.
            var series = new Series(model.Code + "|" + index.ToString(CultureInfo.InvariantCulture));
            series.ChartType = SeriesChartType.FastLine;
            series.XValueType = viewModel.OverlayMode ? ChartValueType.Double : ChartValueType.DateTime;
            series.BorderWidth = model.BorderWidth;
            series.BorderDashStyle = model.IsForecast ? ChartDashStyle.Dash : ChartDashStyle.Solid;
            series.IsVisibleInLegend = model.IsVisibleInLegend;
            series.LegendText = model.LegendText;
            series.Points.DataBindXY(model.XValues ?? new double[0], model.YValues ?? new double[0]);
            return series;
        }

        private static ChartDashStyle ResolveT8PlusDashStyle(ChartSeriesRole role)
        {
            if (role == ChartSeriesRole.T8Minimum)
            {
                return ChartDashStyle.Dot;
            }

            if (role == ChartSeriesRole.T8Maximum)
            {
                // Не Dash: штриховой стиль уже занят линией прогноза динамики.
                return ChartDashStyle.DashDot;
            }

            return ChartDashStyle.Solid;
        }
```

Добавить в начало файла `using System.Globalization;` и `using JSQViewer.Application.Charting;`.

- [ ] **Step 6: Прогнать тесты**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Ожидается: все тесты проходят.

- [ ] **Step 7: Собрать**

Выполнить: `dotnet build .\JSQViewer.csproj -c Debug`
Ожидается: 0 ошибок, 0 предупреждений.

- [ ] **Step 8: Коммит**

```bash
git add Presentation/WinForms/Charting/SourceColorPalette.cs Presentation/WinForms/ViewModels/ChartSeriesViewModel.cs Presentation/WinForms/Charting/ChartViewModelFactory.cs Presentation/WinForms/Charting/ChartRenderer.cs JSQViewer.Tests/SourceColorPaletteTests.cs
git commit -m "Линии T8+ рисуются цветом источника и стилем по роли"
```

---

### Task 6: Хранение флагов T8+ в раскладке рабочего пространства

**Files:**
- Modify: `Application/Channels/WorkspaceLayoutState.cs`
- Modify: `Application/Channels/WorkspaceLayoutStateService.cs`
- Test: `JSQViewer.Tests/WorkspaceLayoutT8PlusTests.cs`

**Interfaces:**
- Consumes: `WorkspaceLayoutState.NormalizeSourceRoot`, `IWorkspaceLayoutRepository`.
- Produces:
  - `public sealed class T8PlusLineSelection` со свойствами `bool ShowMinimum`, `bool ShowAverage`, `bool ShowMaximum`, конструктором `T8PlusLineSelection(bool showMinimum, bool showAverage, bool showMaximum)`, свойством `bool HasAny` и `public static readonly T8PlusLineSelection None`
  - `WorkspaceLayoutState.SourceT8PlusLines` типа `Dictionary<string, T8PlusLineSelection>`
  - `WorkspaceLayoutStateService.GetSourceT8PlusLines(WorkspaceLayoutState state, string sourceRoot)` → `T8PlusLineSelection`
  - `WorkspaceLayoutStateService.SaveSourceT8PlusLines(string workspaceKey, WorkspaceLayoutState state, string sourceRoot, T8PlusLineSelection selection)` → `WorkspaceLayoutState`

- [ ] **Step 1: Написать падающий тест**

Создать `JSQViewer.Tests/WorkspaceLayoutT8PlusTests.cs`:

```csharp
using System.Collections.Generic;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Channels;
using JSQViewer.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class WorkspaceLayoutT8PlusTests
    {
        private sealed class FakeLayoutRepository : IWorkspaceLayoutRepository
        {
            public Dictionary<string, WorkspaceLayoutState> Saved =
                new Dictionary<string, WorkspaceLayoutState>();

            public WorkspaceLayoutState Load(string workspaceKey)
            {
                WorkspaceLayoutState state;
                return Saved.TryGetValue(workspaceKey, out state) ? state : null;
            }

            public bool Save(string workspaceKey, WorkspaceLayoutState state)
            {
                Saved[workspaceKey] = state;
                return true;
            }
        }

        private sealed class FakeOrderRepository : IOrderRepository
        {
            public List<ChannelOrderModel> List() { return new List<ChannelOrderModel>(); }
            public ChannelOrderModel Load(string keyOrName) { return null; }
            public bool Exists(string keyOrName) { return false; }
            public ChannelOrderModel Save(string name, IList<string> order) { return null; }
            public bool Delete(string keyOrName) { return true; }
            public List<string> LoadLegacyOrder() { return new List<string>(); }
            public bool SaveLegacyOrder(IList<string> order) { return true; }
        }

        [TestMethod]
        public void SaveSourceT8PlusLines_RoundTripsThroughRepository()
        {
            var repository = new FakeLayoutRepository();
            var service = new WorkspaceLayoutStateService(repository, new FakeOrderRepository());

            WorkspaceLayoutState state = service.SaveSourceT8PlusLines(
                "ws", new WorkspaceLayoutState(), "C:\\runs\\A\\",
                new T8PlusLineSelection(true, false, true));

            Assert.IsTrue(repository.Saved.ContainsKey("ws"));

            T8PlusLineSelection restored = service.GetSourceT8PlusLines(state, "C:\\runs\\A");

            Assert.IsTrue(restored.ShowMinimum);
            Assert.IsFalse(restored.ShowAverage);
            Assert.IsTrue(restored.ShowMaximum);
        }

        [TestMethod]
        public void GetSourceT8PlusLines_ForUnknownSource_ReturnsNone()
        {
            var service = new WorkspaceLayoutStateService(new FakeLayoutRepository(), new FakeOrderRepository());

            T8PlusLineSelection restored = service.GetSourceT8PlusLines(new WorkspaceLayoutState(), "C:\\runs\\Z");

            Assert.IsFalse(restored.HasAny);
        }

        [TestMethod]
        public void SaveSourceT8PlusLines_WithNothingSelected_DropsTheEntry()
        {
            var service = new WorkspaceLayoutStateService(new FakeLayoutRepository(), new FakeOrderRepository());

            WorkspaceLayoutState state = service.SaveSourceT8PlusLines(
                "ws", new WorkspaceLayoutState(), "C:\\runs\\A",
                new T8PlusLineSelection(true, true, true));
            state = service.SaveSourceT8PlusLines("ws", state, "C:\\runs\\A", T8PlusLineSelection.None);

            Assert.AreEqual(0, state.SourceT8PlusLines.Count);
        }
    }
}
```

Подделки написаны по фактическим объявлениям `Application/Abstractions/IWorkspaceLayoutRepository.cs` и `Application/Abstractions/IOrderRepository.cs`; `ChannelOrderModel` живёт в namespace `JSQViewer.Settings`.

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~WorkspaceLayoutT8PlusTests`
Ожидается: ошибка компиляции — тип `T8PlusLineSelection` не найден.

- [ ] **Step 3: Добавить тип выбора и поле состояния**

В `Application/Channels/WorkspaceLayoutState.cs` добавить в тот же namespace:

```csharp
    public sealed class T8PlusLineSelection
    {
        public static readonly T8PlusLineSelection None = new T8PlusLineSelection(false, false, false);

        public T8PlusLineSelection(bool showMinimum, bool showAverage, bool showMaximum)
        {
            ShowMinimum = showMinimum;
            ShowAverage = showAverage;
            ShowMaximum = showMaximum;
        }

        public bool ShowMinimum { get; set; }

        public bool ShowAverage { get; set; }

        public bool ShowMaximum { get; set; }

        public bool HasAny
        {
            get { return ShowMinimum || ShowAverage || ShowMaximum; }
        }
    }
```

Свойства сделаны изменяемыми, потому что состояние сериализуется тем же JSON-хранилищем, что и остальная раскладка, а оно требует открытых сеттеров и конструктора без параметров. Добавить конструктор без параметров:

```csharp
        public T8PlusLineSelection()
        {
        }
```

В `WorkspaceLayoutState` добавить свойство и его инициализацию:

```csharp
        public Dictionary<string, T8PlusLineSelection> SourceT8PlusLines { get; set; }
```

В конструкторе:

```csharp
            SourceT8PlusLines = new Dictionary<string, T8PlusLineSelection>(StringComparer.OrdinalIgnoreCase);
```

В `EnsureInitialized`:

```csharp
            SourceT8PlusLines = NormalizeT8PlusLines(SourceT8PlusLines);
```

И приватный нормализатор рядом с остальными:

```csharp
        private static Dictionary<string, T8PlusLineSelection> NormalizeT8PlusLines(
            Dictionary<string, T8PlusLineSelection> source)
        {
            var result = new Dictionary<string, T8PlusLineSelection>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, T8PlusLineSelection> pair in source)
            {
                string key = NormalizeSourceRoot(pair.Key);
                if (key.Length == 0 || pair.Value == null || !pair.Value.HasAny)
                {
                    continue;
                }

                result[key] = pair.Value;
            }

            return result;
        }
```

- [ ] **Step 4: Добавить чтение и сохранение в сервис**

В `Application/Channels/WorkspaceLayoutStateService.cs` добавить два метода рядом с `SaveSourceEffectiveOrder`:

```csharp
        public T8PlusLineSelection GetSourceT8PlusLines(WorkspaceLayoutState state, string sourceRoot)
        {
            WorkspaceLayoutState workspaceState = EnsureState(state);
            string normalizedRoot = WorkspaceLayoutState.NormalizeSourceRoot(sourceRoot);

            T8PlusLineSelection selection;
            if (workspaceState.SourceT8PlusLines.TryGetValue(normalizedRoot, out selection) && selection != null)
            {
                return selection;
            }

            return T8PlusLineSelection.None;
        }

        public WorkspaceLayoutState SaveSourceT8PlusLines(
            string workspaceKey,
            WorkspaceLayoutState state,
            string sourceRoot,
            T8PlusLineSelection selection)
        {
            WorkspaceLayoutState workspaceState = EnsureState(state);
            string normalizedRoot = WorkspaceLayoutState.NormalizeSourceRoot(sourceRoot);
            if (normalizedRoot.Length == 0)
            {
                return workspaceState;
            }

            if (selection == null || !selection.HasAny)
            {
                workspaceState.SourceT8PlusLines.Remove(normalizedRoot);
            }
            else
            {
                workspaceState.SourceT8PlusLines[normalizedRoot] = selection;
            }

            Save(workspaceKey, workspaceState);
            return workspaceState;
        }
```

- [ ] **Step 5: Прогнать тесты**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Ожидается: все тесты проходят.

- [ ] **Step 6: Коммит**

```bash
git add Application/Channels/WorkspaceLayoutState.cs Application/Channels/WorkspaceLayoutStateService.cs JSQViewer.Tests/WorkspaceLayoutT8PlusTests.cs
git commit -m "Флаги линий T8+ хранятся в раскладке по источникам"
```

---

### Task 7: Галки в окне источника и проводка в график

**Files:**
- Modify: `Infrastructure/Platform/DictionaryLocalizationService.cs`
- Modify: `UI/MainForm.cs` (класс `SourceWindowState` около строки 5032; построение окна источника около строки 2620; построение запроса около строки 3228; подсветка ближайшей серии около строки 1390)
- Test: ручная проверка (Task 8); автоматических тестов на WinForms в проекте нет

**Interfaces:**
- Consumes: `T8PlusLineSelection`, `WorkspaceLayoutStateService.GetSourceT8PlusLines`/`SaveSourceT8PlusLines`, `T8PlusSeriesRequest`, `T8PlusChannelSelector.SelectColumns`.
- Produces: пользовательский интерфейс; новых открытых API нет.

- [ ] **Step 1: Добавить строки локализации**

В `Infrastructure/Platform/DictionaryLocalizationService.cs` в русский словарь рядом с `{ "SelectedOnly", "Выбранные" }`:

```csharp
            { "T8PlusGroup", "T8+:" },
            { "T8PlusMinimum", "мин" },
            { "T8PlusAverage", "сред" },
            { "T8PlusMaximum", "макс" },
            { "TipT8PlusLines", "Линии минимума, среднего и максимума по каналам T8 и старше" },
            { "TipT8PlusUnavailable", "У источника нет каналов T8 и старше" },
```

В английский словарь рядом с `{ "SelectedOnly", "Selected" }`:

```csharp
            { "T8PlusGroup", "T8+:" },
            { "T8PlusMinimum", "min" },
            { "T8PlusAverage", "avg" },
            { "T8PlusMaximum", "max" },
            { "TipT8PlusLines", "Minimum, average and maximum across channels T8 and above" },
            { "TipT8PlusUnavailable", "This source has no channels T8 or above" },
```

- [ ] **Step 2: Расширить состояние окна источника**

В `UI/MainForm.cs` в `private sealed class SourceWindowState` добавить после `public Form InfoForm { get; set; }`:

```csharp
            public CheckBox T8PlusMinimumCheck { get; set; }
            public CheckBox T8PlusAverageCheck { get; set; }
            public CheckBox T8PlusMaximumCheck { get; set; }
```

- [ ] **Step 3: Добавить галки в окно источника**

В построении окна источника, после блока с `infoButton` и до `var list = new CheckedListBox();`, вставить вторую панель:

```csharp
                var t8Panel = new FlowLayoutPanel();
                t8Panel.Dock = DockStyle.Top;
                t8Panel.AutoSize = true;
                t8Panel.WrapContents = false;
                t8Panel.Padding = new Padding(4, 0, 4, 4);

                var t8Label = new Label();
                t8Label.Text = Loc.Get("T8PlusGroup");
                t8Label.AutoSize = true;
                t8Label.Padding = new Padding(0, 4, 4, 0);
                t8Panel.Controls.Add(t8Label);

                var t8Minimum = new CheckBox { Text = Loc.Get("T8PlusMinimum"), AutoSize = true };
                var t8Average = new CheckBox { Text = Loc.Get("T8PlusAverage"), AutoSize = true };
                var t8Maximum = new CheckBox { Text = Loc.Get("T8PlusMaximum"), AutoSize = true };
                t8Panel.Controls.Add(t8Minimum);
                t8Panel.Controls.Add(t8Average);
                t8Panel.Controls.Add(t8Maximum);
```

В создаваемый `SourceWindowState` добавить:

```csharp
                    T8PlusMinimumCheck = t8Minimum,
                    T8PlusAverageCheck = t8Average,
                    T8PlusMaximumCheck = t8Maximum,
```

После присвоений обработчиков `selectedOnly.CheckedChanged` и прочих добавить:

```csharp
                t8Minimum.CheckedChanged += delegate { T8PlusSelectionChanged(state); };
                t8Average.CheckedChanged += delegate { T8PlusSelectionChanged(state); };
                t8Maximum.CheckedChanged += delegate { T8PlusSelectionChanged(state); };
```

В добавление контролов в форму (`form.Controls.Add(list); form.Controls.Add(bottom); form.Controls.Add(top);`) вставить панель между списком и верхней панелью, чтобы она встала под ней:

```csharp
                form.Controls.Add(list);
                form.Controls.Add(bottom);
                form.Controls.Add(t8Panel);
                form.Controls.Add(top);
```

Сразу после создания состояния восстановить сохранённый выбор и включить или погасить галки:

```csharp
                ApplyT8PlusAvailability(state);
                RestoreT8PlusSelection(state);
```

- [ ] **Step 4: Добавить обработчики и построение запроса**

В `UI/MainForm.cs` добавить приватные методы рядом с `SourceWindowOptionsChanged`:

```csharp
        private void ApplyT8PlusAvailability(SourceWindowState state)
        {
            if (state == null) return;
            TestData data = _viewerSession.Data;
            bool available = data != null
                && T8PlusChannelSelector.SelectColumns(
                        data, state.SourceRoot, T8PlusChannelSelector.DefaultMinimumNumber).Count > 0;

            string tip = Loc.Get(available ? "TipT8PlusLines" : "TipT8PlusUnavailable");
            CheckBox[] checks = { state.T8PlusMinimumCheck, state.T8PlusAverageCheck, state.T8PlusMaximumCheck };
            for (int i = 0; i < checks.Length; i++)
            {
                if (checks[i] == null) continue;
                checks[i].Enabled = available;
                if (!available)
                {
                    checks[i].Checked = false;
                }

                _toolTip.SetToolTip(checks[i], tip);
            }
        }

        private void RestoreT8PlusSelection(SourceWindowState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(_currentWorkspaceKey)) return;

            EnsureWorkspaceLayoutState();
            T8PlusLineSelection selection = _workspaceLayoutStateService.GetSourceT8PlusLines(
                _workspaceLayoutState, state.SourceRoot);

            _syncingChannelWorkspaceOptions = true;
            try
            {
                if (state.T8PlusMinimumCheck != null && state.T8PlusMinimumCheck.Enabled)
                    state.T8PlusMinimumCheck.Checked = selection.ShowMinimum;
                if (state.T8PlusAverageCheck != null && state.T8PlusAverageCheck.Enabled)
                    state.T8PlusAverageCheck.Checked = selection.ShowAverage;
                if (state.T8PlusMaximumCheck != null && state.T8PlusMaximumCheck.Enabled)
                    state.T8PlusMaximumCheck.Checked = selection.ShowMaximum;
            }
            finally
            {
                _syncingChannelWorkspaceOptions = false;
            }
        }

        private void T8PlusSelectionChanged(SourceWindowState state)
        {
            if (state == null) return;
            if (_syncingChannelWorkspaceOptions) return;

            if (!string.IsNullOrWhiteSpace(_currentWorkspaceKey))
            {
                EnsureWorkspaceLayoutState();
                _workspaceLayoutState = _workspaceLayoutStateService.SaveSourceT8PlusLines(
                    _currentWorkspaceKey,
                    _workspaceLayoutState,
                    state.SourceRoot,
                    BuildT8PlusSelection(state));
            }

            RedrawChart();
        }

        private static T8PlusLineSelection BuildT8PlusSelection(SourceWindowState state)
        {
            return new T8PlusLineSelection(
                state.T8PlusMinimumCheck != null && state.T8PlusMinimumCheck.Checked,
                state.T8PlusAverageCheck != null && state.T8PlusAverageCheck.Checked,
                state.T8PlusMaximumCheck != null && state.T8PlusMaximumCheck.Checked);
        }

        private List<T8PlusSeriesRequest> BuildT8PlusSeriesRequests()
        {
            var requests = new List<T8PlusSeriesRequest>();
            foreach (KeyValuePair<string, SourceWindowState> pair in _sourceWindows)
            {
                SourceWindowState state = pair.Value;
                if (state == null) continue;

                T8PlusLineSelection selection = BuildT8PlusSelection(state);
                if (!selection.HasAny) continue;

                requests.Add(new T8PlusSeriesRequest(
                    state.SourceRoot, selection.ShowMinimum, selection.ShowAverage, selection.ShowMaximum));
            }

            return requests;
        }
```

Все использованные здесь члены `MainForm` уже существуют и проверены: поле подсказок `_toolTip` (строка 96), перерисовка `RedrawChart()` (строка 3185), словарь окон источников `_sourceWindows` типа `Dictionary<string, SourceWindowState>` (строка 124), флаг `_syncingChannelWorkspaceOptions`, поле `_currentWorkspaceKey` и метод `EnsureWorkspaceLayoutState()`. Новых сущностей вместо них не заводить.

Добавить в начало `UI/MainForm.cs` `using JSQViewer.Application.Charting;`, если его там ещё нет.

- [ ] **Step 5: Передать запросы в пайплайн**

В `UI/MainForm.cs` в вызове `ChartPipelineRequest.ForChart` (около строки 3228) добавить последним аргументом:

```csharp
                  forecastRoles,
                  BuildT8PlusSeriesRequests());
```

- [ ] **Step 6: Сохранить толщину линий при подсветке**

В обработчике подсказки под курсором подсветка сейчас выполняет `s.BorderWidth = s == closestSeries ? 2 : 1;` и затирает толщину 3 и 2 у линий T8+. Заменить на восстановление исходной толщины, запомненной в теге серии:

```csharp
            if (closestSeries != _lastHighlightedSeries)
            {
                _lastHighlightedSeries = closestSeries;
                foreach (Series s in chart.Series)
                {
                    if (!(s.Tag is int))
                    {
                        s.Tag = s.BorderWidth;
                    }

                    int baseWidth = (int)s.Tag;
                    s.BorderWidth = s == closestSeries ? baseWidth + 1 : baseWidth;
                }
            }
```

- [ ] **Step 7: Собрать и прогнать тесты**

Выполнить: `dotnet build .\JSQViewer.csproj -c Debug` и `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Ожидается: 0 ошибок, 0 предупреждений; все тесты проходят.

- [ ] **Step 8: Коммит**

```bash
git add Infrastructure/Platform/DictionaryLocalizationService.cs UI/MainForm.cs
git commit -m "Линии T8+ включаются галками в окне источника"
```

---

### Task 8: Ручная проверка и сборка Release

**Files:**
- Create: `doc/t8_plus_lines_manual_checklist.md`

**Interfaces:**
- Consumes: всё построенное ранее.
- Produces: чек-лист ручной проверки.

- [ ] **Step 1: Написать чек-лист**

Создать `doc/t8_plus_lines_manual_checklist.md`:

```markdown
# Чек-лист проверки линий статистики T8+

Автоматические тесты покрывают отбор каналов, расчёт рядов, пайплайн и палитру. Отрисовка в MS Chart и поведение окон WinForms проверяются вручную. Пункт, который не удалось выполнить, фиксируется отдельно, а не отмечается пройденным.

1. Открыть один прогон. В окне источника под строкой «Выбранные» видна строка «T8+:» с галками «мин», «сред», «макс», все сняты.
2. График до включения галок выглядит ровно как до этой работы.
3. Включить «сред» — на графике появляется сплошная толстая линия, в легенде строка «[Имя источника] T8+ сред».
4. Включить «мин» и «макс» — добавляются точечная и штрих-пунктирная линии того же цвета.
5. Цвета каналов при включении и выключении галок не меняются.
6. Значения линий совпадают с окном «i»: наименьшее среднее на графике равно полю среднего в карточке прогона.
7. Снять галки с каналов T8…Tmax — линии остаются на месте.
8. Открыть источник без каналов T8 и старше — галки неактивны, подсказка «У источника нет каналов T8 и старше».
9. Добавить второй прогон, включить линии у обоих — цвета линий у источников разные, стили внутри источника различаются, легенда перечисляет оба источника.
10. При тридцати с лишним выбранных каналах легенда содержит только линии T8+.
11. Навести курсор на график — подсказка показывает значения линий T8+, толщина линий после наведения не «худеет» до единицы.
12. Переключить режим наложения — линии каждого источника выравниваются по началу своего прогона.
13. Изменить шаг прореживания — линии перестраиваются, точек столько же, сколько у каналов.
14. Сохранить раскладку, закрыть и открыть прогон заново — состояние галок восстановилось.
15. Открепить график в отдельное окно — линии и легенда воспроизводятся в нём.
16. Сохранить изображение графика — линии T8+ есть на картинке.
17. Экспорт по шаблону отрабатывает без ошибок и по содержимому не изменился.
```

- [ ] **Step 2: Собрать Release**

Выполнить: `dotnet build .\JSQViewer.csproj -c Release`
Ожидается: 0 ошибок, 0 предупреждений.

- [ ] **Step 3: Полный прогон тестов**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Ожидается: все тесты проходят, ни одного упавшего.

- [ ] **Step 4: Коммит**

Каталог `doc/` в `.gitignore`, поэтому файл добавляется принудительно.

```bash
git add -f doc/t8_plus_lines_manual_checklist.md
git commit -m "Добавлен чек-лист ручной проверки линий T8+"
```

- [ ] **Step 5: Пройти чек-лист**

Запустить `.\bin\Debug\JSQViewer.exe` и пройти пункты 1–17 на реальном прогоне. Пункт 5 (цвета каналов не сдвигаются при добавлении линий с явным цветом) — главный риск задачи: MS Chart раздаёт палитру всем сериям с пустым цветом, и порядок добавления серий здесь значим. Если цвета всё-таки сдвигаются, зафиксировать это и вернуться к Task 5, а не подгонять чек-лист.

---

## Самопроверка плана

**Покрытие спеки:** раздел 1 спеки — задачи 1–3; раздел 2 — задача 4; раздел 3 — задачи 4 и 5; раздел 4 — задачи 6 и 7; раздел 5 — задача 7 (подсветка) и явное указание, что экспорт не трогается; тестирование — задачи 1–6 плюс чек-лист в задаче 8.

**Согласованность имён:** `T8PlusChannelSelector.SelectColumns`, `T8PlusChannelSelector.TryGetChannelNumber`, `T8PlusChannelSelector.NormalizeChannelName`, `T8PlusChannelSelector.DefaultMinimumNumber`, `T8PlusSeriesBuilder.Build`, `T8PlusSeries.HasChannels/Minimum/Average/Maximum`, `ChartSeriesRole`, `T8PlusSeriesRequest.HasAny`, `ChartPipelineSeries.Role/SourceIndex`, `ChartPipelineRequest.T8PlusSeries`, `SourceColorPalette.ForSourceIndex/Count`, `T8PlusLineSelection.HasAny/None`, `WorkspaceLayoutState.SourceT8PlusLines`, `WorkspaceLayoutStateService.GetSourceT8PlusLines/SaveSourceT8PlusLines` — используются в задачах согласованно.

**Опора на существующий код — проверено по репозиторию перед написанием плана:** `MainForm._toolTip` (строка 96), `MainForm.RedrawChart()` (строка 3185), `MainForm._sourceWindows` (строка 124), файл `JSQViewer.Tests/GetRecordingInfoUseCaseTests.cs`, сигнатуры `IWorkspaceLayoutRepository` и `IOrderRepository`. Догадок в плане не осталось.

**Главный риск:** порядок раздачи цветов в MS Chart (задача 5, пункт 5 чек-листа). Он не проверяется автотестами и подтверждается только на живом графике.
