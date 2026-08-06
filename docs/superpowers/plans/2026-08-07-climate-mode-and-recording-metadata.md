# Климатический режим и метаданные прогона — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Показать климатический режим прогона (25/60, 32/65, 40/40) в таблице выбора записей из БД и вывести модель, вид испытания и режим в заголовке рабочего пространства после открытия.

**Architecture:** Определение режима выносится в чистый класс `ClimateModeResolver` в `Application/Database/` — он не знает ни про SQL, ни про WinForms и принимает уже усреднённые значения. Средние по первым пяти окнам каналов `T-sie` и `UR-sie` считает PostgreSQL коррелированными подзапросами, что даёт 48 мс на всю таблицу. Инфраструктурный слой (`RecordingCatalogQueryBuilder`, `PostgresRecordingCatalog`, `PostgresRecordingDataSourceReader`) только передаёт значения в резолвер, UI показывает готовые метки.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WinForms, MSTest 3.5.2, Npgsql 6.0.13, PostgreSQL.

Спецификация: `docs/superpowers/specs/2026-08-06-climate-mode-and-recording-metadata-design.md`.

## Global Constraints

- Язык C# 7.3 — нельзя использовать `switch`-выражения, `record`, целевую типизацию `new()`, `??=`, интерполяцию в константах. Тернарный оператор с явным приведением `(DateTime?)null` — существующий приём в этом коде.
- Стиль: отступ 4 пробела, фигурные скобки на отдельной строке, `PascalCase` для типов и публичных членов, `_camelCase` для приватных полей, явные модификаторы доступа.
- Сравнение строковых ключей и корней источников — всегда `StringComparer.OrdinalIgnoreCase` / `StringComparison.OrdinalIgnoreCase`.
- Автотесты не обращаются к базе данных и к сети. SQL проверяется по тексту запроса, логика — на данных в памяти.
- Сборка: `dotnet build .\JSQViewer.csproj -c Debug` без ошибок и предупреждений. Тесты: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`.
- Форма SQL для средних значений — строго `ORDER BY a.window_start LIMIT 5` в подзапросе, `LIMIT @limit` применяется до вычисления средних. Замена на `row_number() OVER (…)` замедляет запрос с 48 мс до 10 с.
- Допуск определения режима по температуре — ровно 3.0 °C включительно.
- Идентификаторы режимов в БД: `25_60`, `32_65`, `40_40`. Метки для показа: `25/60`, `32/65`, `40/40`.
- Русские ключи метаданных в `TestData.Meta` пишутся так же, как существующие: «Модель оборудования», «Тип испытания», «Климатический режим».
- Один коммит на задачу, сообщение — короткое повелительное предложение на русском.
- В `Export/TemplateExporter.cs:17` объявлен `[assembly: InternalsVisibleTo("JSQViewer.Tests")]`, поэтому `internal` члены доступны тестам.

---

### Task 1: Резолвер климатического режима

**Files:**
- Create: `Application/Database/ClimateModeInfo.cs`
- Create: `Application/Database/ClimateModeResolver.cs`
- Test: `JSQViewer.Tests/ClimateModeResolverTests.cs`

Правка `JSQViewer.csproj` не нужна: строка 109 подключает каталог целиком — `<Compile Include="Application\**\*.cs" />`. Тестовый проект — SDK-style, файлы тоже подхватываются автоматически.

**Interfaces:**
- Consumes: ничего.
- Produces:
  - `enum ClimateModeSource { Unknown, FromRecord, FromChannels }`
  - `sealed class ClimateModeInfo` с конструктором `ClimateModeInfo(string label, ClimateModeSource source, double? temperatureCelsius, double? humidityPercent)`, свойствами `string Label`, `ClimateModeSource Source`, `double? TemperatureCelsius`, `double? HumidityPercent`, `bool IsKnown` и статическим полем `ClimateModeInfo.Unknown`.
  - `sealed class ClimateModeResolver` с константой `public const double ToleranceCelsius = 3.0;` и методом `public ClimateModeInfo Resolve(string climateModeId, double? temperatureCelsius, double? humidityPercent)`.

- [ ] **Step 1: Написать падающий тест**

Создать `JSQViewer.Tests/ClimateModeResolverTests.cs`:

```csharp
using JSQViewer.Application.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class ClimateModeResolverTests
    {
        [TestMethod]
        public void Resolve_WithModeIdFromRecord_UsesRecordValue()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve("32_65", 25.0, 60.0);

            Assert.AreEqual("32/65", info.Label);
            Assert.AreEqual(ClimateModeSource.FromRecord, info.Source);
            Assert.IsTrue(info.IsKnown);
        }

        [TestMethod]
        public void Resolve_WithUnknownModeId_FallsBackToTemperature()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve("18_50", 25.2, 59.7);

            Assert.AreEqual("25/60", info.Label);
            Assert.AreEqual(ClimateModeSource.FromChannels, info.Source);
        }

        [TestMethod]
        public void Resolve_WithoutModeId_ClassifiesEachKnownMode()
        {
            var resolver = new ClimateModeResolver();

            Assert.AreEqual("25/60", resolver.Resolve(null, 25.2, 59.7).Label);
            Assert.AreEqual("32/65", resolver.Resolve(null, 32.4, 64.6).Label);
            Assert.AreEqual("40/40", resolver.Resolve(null, 39.5, 41.2).Label);
        }

        [TestMethod]
        public void Resolve_IgnoresHumidityWhenClassifying()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve(null, 32.3, 55.1);

            Assert.AreEqual("32/65", info.Label);
        }

        [TestMethod]
        public void Resolve_AtToleranceBoundary_StillClassifies()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve(null, 35.0, null);

            Assert.AreEqual("32/65", info.Label);
        }

        [TestMethod]
        public void Resolve_BeyondTolerance_ReturnsUnknown()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve(null, 35.1, null);

            Assert.AreEqual(ClimateModeSource.Unknown, info.Source);
            Assert.AreEqual(string.Empty, info.Label);
            Assert.IsFalse(info.IsKnown);
        }

        [TestMethod]
        public void Resolve_WithoutTemperature_ReturnsUnknown()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve("   ", null, 64.0);

            Assert.AreEqual(ClimateModeSource.Unknown, info.Source);
            Assert.IsFalse(info.IsKnown);
        }

        [TestMethod]
        public void Resolve_KeepsMeasuredValuesForTooltip()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve(null, 32.34, 64.12);

            Assert.AreEqual(32.34, info.TemperatureCelsius.Value, 0.001);
            Assert.AreEqual(64.12, info.HumidityPercent.Value, 0.001);
        }
    }
}
```

- [ ] **Step 2: Запустить тест и убедиться, что он падает**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter ClimateModeResolverTests`
Expected: FAIL — ошибка компиляции, тип `ClimateModeResolver` не найден.

- [ ] **Step 3: Написать минимальную реализацию**

`Application/Database/ClimateModeInfo.cs`:

```csharp
using System;

namespace JSQViewer.Application.Database
{
    public enum ClimateModeSource
    {
        Unknown,
        FromRecord,
        FromChannels
    }

    public sealed class ClimateModeInfo
    {
        public static readonly ClimateModeInfo Unknown =
            new ClimateModeInfo(string.Empty, ClimateModeSource.Unknown, null, null);

        public ClimateModeInfo(
            string label,
            ClimateModeSource source,
            double? temperatureCelsius,
            double? humidityPercent)
        {
            Label = label ?? string.Empty;
            Source = source;
            TemperatureCelsius = temperatureCelsius;
            HumidityPercent = humidityPercent;
        }

        public string Label { get; private set; }

        public ClimateModeSource Source { get; private set; }

        public double? TemperatureCelsius { get; private set; }

        public double? HumidityPercent { get; private set; }

        public bool IsKnown
        {
            get { return Source != ClimateModeSource.Unknown && Label.Length > 0; }
        }
    }
}
```

`Application/Database/ClimateModeResolver.cs`:

```csharp
using System;

namespace JSQViewer.Application.Database
{
    public sealed class ClimateModeResolver
    {
        public const double ToleranceCelsius = 3.0;

        private static readonly string[] ModeIds = { "25_60", "32_65", "40_40" };
        private static readonly double[] ModeTemperatures = { 25.0, 32.0, 40.0 };

        public ClimateModeInfo Resolve(
            string climateModeId,
            double? temperatureCelsius,
            double? humidityPercent)
        {
            string recordLabel = TryGetLabel(climateModeId);
            if (recordLabel != null)
            {
                return new ClimateModeInfo(
                    recordLabel,
                    ClimateModeSource.FromRecord,
                    temperatureCelsius,
                    humidityPercent);
            }

            if (!temperatureCelsius.HasValue)
            {
                return ClimateModeInfo.Unknown;
            }

            int bestIndex = -1;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < ModeTemperatures.Length; i++)
            {
                double distance = Math.Abs(ModeTemperatures[i] - temperatureCelsius.Value);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestDistance > ToleranceCelsius)
            {
                return ClimateModeInfo.Unknown;
            }

            return new ClimateModeInfo(
                ToLabel(ModeIds[bestIndex]),
                ClimateModeSource.FromChannels,
                temperatureCelsius,
                humidityPercent);
        }

        private static string TryGetLabel(string climateModeId)
        {
            if (string.IsNullOrWhiteSpace(climateModeId))
            {
                return null;
            }

            string id = climateModeId.Trim();
            for (int i = 0; i < ModeIds.Length; i++)
            {
                if (string.Equals(ModeIds[i], id, StringComparison.OrdinalIgnoreCase))
                {
                    return ToLabel(ModeIds[i]);
                }
            }

            return null;
        }

        private static string ToLabel(string modeId)
        {
            return modeId.Replace('_', '/');
        }
    }
}
```

- [ ] **Step 4: Запустить тест и убедиться, что он проходит**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter ClimateModeResolverTests`
Expected: PASS, 8 тестов.

- [ ] **Step 5: Коммит**

```bash
git add Application/Database/ClimateModeInfo.cs Application/Database/ClimateModeResolver.cs JSQViewer.csproj JSQViewer.Tests/ClimateModeResolverTests.cs
git commit -m "Добавлено определение климатического режима"
```

---

### Task 2: Средние значения T-sie и UR-sie в запросе каталога

**Files:**
- Modify: `Infrastructure/Database/RecordingCatalogQueryBuilder.cs:46-58`
- Modify: `Application/Database/RecordingSummaryItem.cs:14`
- Modify: `Infrastructure/Database/PostgresRecordingCatalog.cs:47-63`
- Test: `JSQViewer.Tests/RecordingCatalogQueryBuilderTests.cs`

**Interfaces:**
- Consumes: `ClimateModeResolver.Resolve(string, double?, double?)`, `ClimateModeInfo` из задачи 1.
- Produces: свойство `ClimateModeInfo ClimateMode { get; set; }` у `RecordingSummaryItem`; заполняется в `PostgresRecordingCatalog.List`. Никогда не `null` — при отсутствии данных содержит `ClimateModeInfo.Unknown`.

- [ ] **Step 1: Написать падающие тесты**

Дописать в `JSQViewer.Tests/RecordingCatalogQueryBuilderTests.cs` внутрь класса:

```csharp
        [TestMethod]
        public void Build_SelectsFirstWindowAveragesForClimateChannels()
        {
            string sql = new RecordingCatalogQueryBuilder().Build(new RecordingCatalogFilter(), new List<string>());

            StringAssert.Contains(sql, "WITH page AS (");
            StringAssert.Contains(sql, "a.channel_id = 'T-sie'");
            StringAssert.Contains(sql, "a.channel_id = 'UR-sie'");
            StringAssert.Contains(sql, "ORDER BY a.window_start LIMIT 5");
            StringAssert.Contains(sql, "AS t_sie_avg");
            StringAssert.Contains(sql, "AS ur_sie_avg");
        }

        [TestMethod]
        public void Build_NeverUsesWindowFunctionForAverages()
        {
            string sql = new RecordingCatalogQueryBuilder().Build(new RecordingCatalogFilter(), new List<string>());

            Assert.IsFalse(
                sql.ToUpperInvariant().Contains("ROW_NUMBER"),
                "Оконная функция замедляет запрос с 48 мс до 10 с.");
        }

        [TestMethod]
        public void Build_AppliesLimitBeforeComputingAverages()
        {
            string sql = new RecordingCatalogQueryBuilder().Build(new RecordingCatalogFilter(), new List<string>());

            int limitIndex = sql.IndexOf("LIMIT @limit", StringComparison.Ordinal);
            int averageIndex = sql.IndexOf("t_sie_avg", StringComparison.Ordinal);
            Assert.IsTrue(limitIndex > 0, "Ожидался LIMIT @limit.");
            Assert.IsTrue(averageIndex > limitIndex, "Средние должны считаться после отбора страницы.");
        }

        [TestMethod]
        public void Build_SelectsClimateModeColumn()
        {
            string sql = new RecordingCatalogQueryBuilder().Build(new RecordingCatalogFilter(), new List<string>());

            StringAssert.Contains(sql, "r.climate_mode");
        }
```

- [ ] **Step 2: Запустить тесты и убедиться, что они падают**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingCatalogQueryBuilderTests`
Expected: FAIL — четыре новых теста падают на `StringAssert.Contains`, пять старых проходят.

- [ ] **Step 3: Переписать построитель запроса**

Заменить в `Infrastructure/Database/RecordingCatalogQueryBuilder.cs` блок формирования SQL (строки 46-58, от `var sql = new StringBuilder();` до `return sql.ToString();`) на:

```csharp
            var sql = new StringBuilder();
            sql.AppendLine("WITH page AS (");
            sql.AppendLine("    SELECT r.id, r.post_id, r.title, r.status, r.started_at, r.stopped_at,");
            sql.AppendLine("           r.equipment_model, r.experiment_type, r.climate_mode");
            sql.AppendLine("    FROM recordings r");
            if (conditions.Count > 0)
            {
                sql.AppendLine("    WHERE " + string.Join(" AND ", conditions.ToArray()));
            }

            sql.AppendLine("    ORDER BY r.started_at DESC NULLS LAST");
            sql.AppendLine("    LIMIT @limit)");
            sql.AppendLine("SELECT p.id, p.post_id, p.title, p.status, p.started_at, p.stopped_at,");
            sql.AppendLine("       p.equipment_model, p.experiment_type, p.climate_mode,");
            sql.AppendLine("       (SELECT avg(t.v) FROM (");
            sql.AppendLine("           SELECT a.avg_value v FROM recording_aggregates a");
            sql.AppendLine("           WHERE a.recording_id = p.id AND a.channel_id = 'T-sie' AND a.avg_value IS NOT NULL");
            sql.AppendLine("           ORDER BY a.window_start LIMIT 5) t) AS t_sie_avg,");
            sql.AppendLine("       (SELECT avg(u.v) FROM (");
            sql.AppendLine("           SELECT a.avg_value v FROM recording_aggregates a");
            sql.AppendLine("           WHERE a.recording_id = p.id AND a.channel_id = 'UR-sie' AND a.avg_value IS NOT NULL");
            sql.AppendLine("           ORDER BY a.window_start LIMIT 5) u) AS ur_sie_avg");
            sql.AppendLine("FROM page p");
            parameterNames.Add("limit");
            return sql.ToString();
```

Условия фильтров выше по коду не меняются: они по-прежнему ссылаются на `r.` и теперь применяются внутри CTE.

- [ ] **Step 4: Запустить тесты и убедиться, что они проходят**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingCatalogQueryBuilderTests`
Expected: PASS, 9 тестов. Старые тесты проходят, потому что `FROM recordings r`, `ORDER BY r.started_at DESC NULLS LAST` и `LIMIT @limit` остались в тексте запроса.

- [ ] **Step 5: Заполнить климатический режим в каталоге**

В `Application/Database/RecordingSummaryItem.cs` добавить после свойства `ExperimentType`:

```csharp
        public ClimateModeInfo ClimateMode { get; set; }
```

В `Infrastructure/Database/PostgresRecordingCatalog.cs` добавить поле рядом с `_queryBuilder`:

```csharp
        private readonly ClimateModeResolver _climateModeResolver;
```

и его инициализацию в конструкторе рядом с `_queryBuilder = new RecordingCatalogQueryBuilder();`:

```csharp
            _climateModeResolver = new ClimateModeResolver();
```

Заменить создание элемента в `List` (строки 51-61) на:

```csharp
                        result.Add(new RecordingSummaryItem
                        {
                            Id = ReadString(reader, 0),
                            PostId = ReadString(reader, 1),
                            Title = ReadString(reader, 2),
                            Status = ReadString(reader, 3),
                            StartedAt = ReadLocalDateTime(reader, 4),
                            StoppedAt = ReadLocalDateTime(reader, 5),
                            EquipmentModel = ReadString(reader, 6),
                            ExperimentType = ReadString(reader, 7),
                            ClimateMode = _climateModeResolver.Resolve(
                                ReadString(reader, 8),
                                ReadNullableDouble(reader, 9),
                                ReadNullableDouble(reader, 10))
                        });
```

Добавить рядом с `ReadLocalDateTime` вспомогательный метод:

```csharp
        private static double? ReadNullableDouble(IDataRecord record, int index)
        {
            return record.IsDBNull(index)
                ? (double?)null
                : Convert.ToDouble(record.GetValue(index), CultureInfo.InvariantCulture);
        }
```

Добавить в начало файла `using System.Globalization;` и `using JSQViewer.Application.Database;` — второй уже присутствует.

- [ ] **Step 6: Собрать и прогнать весь набор тестов**

Run: `dotnet build .\JSQViewer.csproj -c Debug` затем `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Expected: сборка без ошибок и предупреждений, все тесты проходят.

- [ ] **Step 7: Коммит**

```bash
git add Infrastructure/Database/RecordingCatalogQueryBuilder.cs Infrastructure/Database/PostgresRecordingCatalog.cs Application/Database/RecordingSummaryItem.cs JSQViewer.Tests/RecordingCatalogQueryBuilderTests.cs
git commit -m "Каталог прогонов отдаёт климатический режим"
```

---

### Task 3: Колонка «Режим» в диалоге выбора

**Files:**
- Modify: `UI/OpenFromDatabaseForm.cs:208-237` (`BindRecordings`), `:295-304` (`AddColumns`)
- Modify: `Infrastructure/Platform/DictionaryLocalizationService.cs` (русский словарь рядом со строкой 31, английский рядом со строкой 268)

**Interfaces:**
- Consumes: `RecordingSummaryItem.ClimateMode` (тип `ClimateModeInfo`) из задачи 2.
- Produces: ничего для последующих задач.

- [ ] **Step 1: Добавить строки локализации**

В русский словарь `DictionaryLocalizationService` после `{ "RecordingColumnExperiment", "Эксперимент" },`:

```csharp
            { "RecordingColumnClimateMode", "Режим" },
            { "ClimateModeFromRecord", "Из карточки прогона" },
            { "ClimateModeFromChannels", "Определён по T-sie: {0} °C, UR-sie: {1} %" },
            { "ClimateModeFromTemperature", "Определён по T-sie: {0} °C" },
```

В английский словарь после `{ "RecordingColumnExperiment", "Experiment" },`:

```csharp
            { "RecordingColumnClimateMode", "Mode" },
            { "ClimateModeFromRecord", "From the recording card" },
            { "ClimateModeFromChannels", "Derived from T-sie: {0} °C, UR-sie: {1} %" },
            { "ClimateModeFromTemperature", "Derived from T-sie: {0} °C" },
```

- [ ] **Step 2: Добавить колонку**

В `UI/OpenFromDatabaseForm.cs`, метод `AddColumns`, вставить между строками с `RecordingColumnExperiment` и `RecordingColumnDuration`:

```csharp
            _recordingsGrid.Columns.Add(CreateColumn(Loc.Get("RecordingColumnClimateMode"), 80));
```

- [ ] **Step 3: Заполнять колонку и подсказку**

В методе `BindRecordings` заменить вызов `_recordingsGrid.Rows.Add(...)` на вариант с климатическим режимом (шестым аргументом) и добавить подсказку после установки шрифта активной строки:

```csharp
                ClimateModeInfo climateMode = item.ClimateMode ?? ClimateModeInfo.Unknown;
                int index = _recordingsGrid.Rows.Add(
                    item.StartedAt.HasValue ? item.StartedAt.Value.ToString("g") : string.Empty,
                    item.PostId ?? string.Empty,
                    item.Title ?? string.Empty,
                    item.EquipmentModel ?? string.Empty,
                    item.ExperimentType ?? string.Empty,
                    climateMode.Label,
                    item.DurationHours.ToString("0.##"),
                    item.IsActive ? Loc.Get("RecordingStatusActive") : Loc.Get("RecordingStatusStopped"));
                DataGridViewRow row = _recordingsGrid.Rows[index];
                row.Tag = item;
                if (item.IsActive)
                {
                    row.DefaultCellStyle.Font = _activeRowFont;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        cell.ToolTipText = Loc.Get("RecordingLiveUpdating");
                    }
                }

                string climateTooltip = BuildClimateModeTooltip(climateMode);
                if (climateTooltip.Length > 0)
                {
                    row.Cells[5].ToolTipText = climateTooltip;
                }
```

Добавить в тот же класс приватный метод рядом с `BindRecordings`:

```csharp
        private static string BuildClimateModeTooltip(ClimateModeInfo climateMode)
        {
            if (climateMode == null || !climateMode.IsKnown)
            {
                return string.Empty;
            }

            if (climateMode.Source == ClimateModeSource.FromRecord)
            {
                return Loc.Get("ClimateModeFromRecord");
            }

            string temperature = climateMode.TemperatureCelsius.HasValue
                ? climateMode.TemperatureCelsius.Value.ToString("0.#")
                : string.Empty;
            if (temperature.Length == 0)
            {
                return string.Empty;
            }

            if (!climateMode.HumidityPercent.HasValue)
            {
                return string.Format(Loc.Get("ClimateModeFromTemperature"), temperature);
            }

            return string.Format(
                Loc.Get("ClimateModeFromChannels"),
                temperature,
                climateMode.HumidityPercent.Value.ToString("0.#"));
        }
```

Подсказка ставится после цикла по ячейкам, поэтому у активной записи ячейка режима показывает именно климатическую подсказку, а остальные ячейки — сообщение о живом обновлении.

- [ ] **Step 4: Собрать и прогнать тесты**

Run: `dotnet build .\JSQViewer.csproj -c Debug` затем `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Expected: сборка без ошибок и предупреждений, все тесты проходят. Индекс `row.Cells[5]` соответствует новой колонке: 0 — начало, 1 — пост, 2 — название, 3 — модель, 4 — эксперимент, 5 — режим, 6 — длительность, 7 — статус.

- [ ] **Step 5: Коммит**

```bash
git add UI/OpenFromDatabaseForm.cs Infrastructure/Platform/DictionaryLocalizationService.cs
git commit -m "Добавлена колонка климатического режима"
```

---

### Task 4: Общие каналы и метаданные прогона

**Files:**
- Modify: `Infrastructure/Database/PostgresRecordingDataSourceReader.cs:15-64` (константы SQL), `:150-186` (`ReadRecordingSnapshot`), `:188-215` (`ReadChannels`)
- Test: `JSQViewer.Tests/PostgresRecordingDataSourceReaderSqlTests.cs` (создать)

**Interfaces:**
- Consumes: `ClimateModeResolver`, `ClimateModeInfo` из задачи 1.
- Produces: ключ «Климатический режим» в `TestData.Meta` для источников `jsqdb://recording/<id>`; каналы `T-sie` и `UR-sie` в `TestData.Channels` и `TestData.Columns`.

- [ ] **Step 1: Написать падающий тест**

Создать `JSQViewer.Tests/PostgresRecordingDataSourceReaderSqlTests.cs`:

```csharp
using JSQViewer.Infrastructure.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class PostgresRecordingDataSourceReaderSqlTests
    {
        [TestMethod]
        public void ChannelsSql_IncludesCommonChannels()
        {
            StringAssert.Contains(
                PostgresRecordingDataSourceReader.ChannelsSql,
                "(post_id = @post OR is_common)");
        }

        [TestMethod]
        public void ChannelsSql_KeepsHiddenChannelsExcluded()
        {
            StringAssert.Contains(PostgresRecordingDataSourceReader.ChannelsSql, "NOT is_hidden");
        }

        [TestMethod]
        public void RowsSql_IncludesCommonChannels()
        {
            StringAssert.Contains(
                PostgresRecordingDataSourceReader.RowsSql,
                "(cc.post_id = r.post_id OR cc.is_common)");
        }

        [TestMethod]
        public void RowsSql_KeepsExclusionFilter()
        {
            StringAssert.Contains(
                PostgresRecordingDataSourceReader.RowsSql,
                "recording_aggregate_exclusions");
        }

        [TestMethod]
        public void RecordingSql_SelectsClimateModeAndFirstWindowAverages()
        {
            StringAssert.Contains(PostgresRecordingDataSourceReader.RecordingSql, "r.climate_mode");
            StringAssert.Contains(PostgresRecordingDataSourceReader.RecordingSql, "a.channel_id = 'T-sie'");
            StringAssert.Contains(PostgresRecordingDataSourceReader.RecordingSql, "a.channel_id = 'UR-sie'");
            StringAssert.Contains(
                PostgresRecordingDataSourceReader.RecordingSql,
                "ORDER BY a.window_start LIMIT 5");
        }
    }
}
```

- [ ] **Step 2: Запустить тест и убедиться, что он падает**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter PostgresRecordingDataSourceReaderSqlTests`
Expected: FAIL — ошибка компиляции, константы `private const string` недоступны тестам.

- [ ] **Step 3: Открыть константы и изменить запросы**

В `Infrastructure/Database/PostgresRecordingDataSourceReader.cs` заменить `private const string` на `internal const string` у `RecordingSql`, `ChannelsSql` и `RowsSql` (сборка уже помечена `InternalsVisibleTo("JSQViewer.Tests")` в `Export/TemplateExporter.cs:17`).

`ChannelsSql`:

```csharp
        internal const string ChannelsSql = @"
SELECT channel_id, alias, unit
FROM channel_config
WHERE (post_id = @post OR is_common)
  AND NOT is_hidden
ORDER BY channel_id";
```

В `RowsSql` заменить условие соединения с `channel_config`:

```csharp
        AND EXISTS (
              SELECT 1
              FROM recordings r
              JOIN channel_config cc
                ON cc.channel_id = a.channel_id
               AND (cc.post_id = r.post_id OR cc.is_common)
              WHERE r.id = @id
                AND NOT cc.is_hidden)
```

В `RecordingSql` добавить три колонки перед `FROM recordings r`, после строки с `notes`:

```sql
       r.climate_mode,
       (SELECT avg(t.v) FROM (
           SELECT a.avg_value v FROM recording_aggregates a
           WHERE a.recording_id = r.id AND a.channel_id = 'T-sie' AND a.avg_value IS NOT NULL
           ORDER BY a.window_start LIMIT 5) t) AS t_sie_avg,
       (SELECT avg(u.v) FROM (
           SELECT a.avg_value v FROM recording_aggregates a
           WHERE a.recording_id = r.id AND a.channel_id = 'UR-sie' AND a.avg_value IS NOT NULL
           ORDER BY a.window_start LIMIT 5) u) AS ur_sie_avg
```

Запятая после `to_jsonb(r) ->> 'notes' AS notes` обязательна.

- [ ] **Step 4: Записать режим в метаданные**

В `ReadRecordingSnapshot` после строки `AddMetadata(metadata, "Примечания", reader, 15);` добавить:

```csharp
                    ClimateModeInfo climateMode = ClimateModeResolverInstance.Resolve(
                        ReadString(reader, 16),
                        ReadNullableDouble(reader, 17),
                        ReadNullableDouble(reader, 18));
                    if (climateMode.IsKnown)
                    {
                        metadata["Климатический режим"] = climateMode.Label;
                    }
```

Добавить в класс статическое поле рядом с `_mapper`:

```csharp
        private static readonly ClimateModeResolver ClimateModeResolverInstance = new ClimateModeResolver();
```

и вспомогательный метод рядом с `ReadLocalDateTime`:

```csharp
        private static double? ReadNullableDouble(IDataRecord record, int index)
        {
            return record.IsDBNull(index)
                ? (double?)null
                : Convert.ToDouble(record.GetValue(index), CultureInfo.InvariantCulture);
        }
```

`using System.Globalization;` в файле уже есть.

- [ ] **Step 5: Запустить тесты и убедиться, что они проходят**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
Expected: PASS — новые 5 тестов и весь существующий набор.

- [ ] **Step 6: Коммит**

```bash
git add Infrastructure/Database/PostgresRecordingDataSourceReader.cs JSQViewer.Tests/PostgresRecordingDataSourceReaderSqlTests.cs
git commit -m "Загружаются общие каналы и климатический режим"
```

---

### Task 5: Расширенный заголовок рабочего пространства

**Files:**
- Modify: `Application/Workspace/WorkspaceTitleBuilder.cs:17-36`
- Create: `JSQViewer.Tests/WorkspaceTitleBuilderTests.cs` (файла ещё нет)

**Interfaces:**
- Consumes: ключ «Климатический режим» в `TestData.Meta` из задачи 4; существующие ключи «Модель оборудования» и «Тип испытания».
- Produces: изменённое поведение `WorkspaceTitleBuilder.Build(TestData, string)`. Публичная сигнатура не меняется, поэтому `MainForm.BuildChartWindowCaption` и `ApplyChartWindowTitles` править не нужно.

- [ ] **Step 1: Написать падающие тесты**

Создать `JSQViewer.Tests/WorkspaceTitleBuilderTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using JSQViewer.Application.Workspace;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class WorkspaceTitleBuilderTests
    {
        private static TestData CreateSingleRecording(Dictionary<string, string> meta)
        {
            return new TestData
            {
                Root = "jsqdb://recording/abc123",
                SourceOrder = new[] { "jsqdb://recording/abc123" },
                SourceDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "jsqdb://recording/abc123", "Post B 2026-08-06 14-33-12" }
                },
                Meta = meta
            };
        }

        [TestMethod]
        public void Build_WithSingleRecording_AppendsModelExperimentAndClimateMode()
        {
            TestData data = CreateSingleRecording(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Модель оборудования", "DINAMIC" },
                { "Тип испытания", "FUNC" },
                { "Климатический режим", "32/65" }
            });

            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(data, "запасной");

            Assert.AreEqual("Post B 2026-08-06 14-33-12 · DINAMIC · FUNC · 32/65", title);
        }

        [TestMethod]
        public void Build_WithSingleRecording_SkipsMissingParts()
        {
            TestData data = CreateSingleRecording(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Модель оборудования", "LIDER" },
                { "Тип испытания", "   " }
            });

            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(data, "запасной");

            Assert.AreEqual("Post B 2026-08-06 14-33-12 · LIDER", title);
        }

        [TestMethod]
        public void Build_WithFolderSource_KeepsPlainName()
        {
            var data = new TestData
            {
                Root = @"C:\tests\FORCE KA50",
                SourceOrder = new[] { @"C:\tests\FORCE KA50" },
                Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(data, "запасной");

            Assert.AreEqual("FORCE KA50", title);
        }

        [TestMethod]
        public void Build_WithSeveralSources_JoinsNamesWithoutMetadata()
        {
            var data = new TestData
            {
                Root = "jsqdb://recording/abc123",
                SourceOrder = new[] { "jsqdb://recording/abc123", "jsqdb://recording/def456" },
                SourceDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "jsqdb://recording/abc123", "Прогон 1" },
                    { "jsqdb://recording/def456", "Прогон 2" }
                },
                Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Модель оборудования", "DINAMIC" }
                }
            };

            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(data, "запасной");

            Assert.AreEqual("Прогон 1; Прогон 2", title);
        }

        [TestMethod]
        public void Build_WithoutSources_ReturnsFallback()
        {
            string title = new WorkspaceTitleBuilder(new SourceDisplayNameResolver()).Build(null, "запасной");

            Assert.AreEqual("запасной", title);
        }
    }
}
```

- [ ] **Step 2: Запустить тесты и убедиться, что они падают**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter WorkspaceTitleBuilderTests`
Expected: FAIL — первые два теста возвращают заголовок без метаданных, остальные проходят.

- [ ] **Step 3: Дополнить построитель заголовка**

В `Application/Workspace/WorkspaceTitleBuilder.cs` заменить хвост метода `Build` (`return titles.Count == 0 …`) на:

```csharp
            if (titles.Count == 0)
            {
                return fallback ?? string.Empty;
            }

            if (titles.Count > 1)
            {
                return string.Join("; ", titles);
            }

            var parts = new List<string> { titles[0] };
            AppendMetaPart(parts, data, "Модель оборудования");
            AppendMetaPart(parts, data, "Тип испытания");
            AppendMetaPart(parts, data, "Климатический режим");
            return string.Join(" · ", parts.ToArray());
        }

        private static void AppendMetaPart(ICollection<string> parts, TestData data, string metaKey)
        {
            if (data == null || data.Meta == null)
            {
                return;
            }

            string value;
            if (data.Meta.TryGetValue(metaKey, out value) && !string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value.Trim());
            }
        }
```

- [ ] **Step 4: Запустить тесты и убедиться, что они проходят**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter WorkspaceTitleBuilderTests`
Expected: PASS, все тесты класса.

- [ ] **Step 5: Коммит**

```bash
git add Application/Workspace/WorkspaceTitleBuilder.cs JSQViewer.Tests/WorkspaceTitleBuilderTests.cs
git commit -m "Заголовок показывает модель, испытание и режим"
```

---

### Task 6: Регрессия, ручной чек-лист и ревью

**Files:**
- Create: `doc/climate_mode_manual_checklist.md`

**Interfaces:**
- Consumes: результат всех предыдущих задач.
- Produces: подтверждение готовности к слиянию.

- [ ] **Step 1: Полная регрессия**

Run:
```powershell
dotnet build .\JSQViewer.csproj -c Debug
dotnet build .\JSQViewer.csproj -c Release
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj
```
Expected: обе сборки — 0 ошибок, 0 предупреждений; тесты — 0 упавших. Записать фактические числа пройденных и пропущенных тестов.

- [ ] **Step 2: Написать ручной чек-лист**

Создать `doc/climate_mode_manual_checklist.md` со следующими пунктами (проверка на рабочей базе, вручную):

1. Открыть «Из БД…» — в таблице видна колонка «Режим» между «Эксперимент» и «Длительность, ч».
2. У прогонов Post B и Post C за 2026-08-06 в колонке стоит `32/65`.
3. У прогонов за 2026-08-04 в колонке стоит `25/60`.
4. Подсказка на ячейке режима у прогона с заполненным `climate_mode` — «Из карточки прогона».
5. Подсказка на ячейке режима у прогона без `climate_mode` — «Определён по T-sie: … °C, UR-sie: … %».
6. Фильтры по посту, датам, эксперименту и названию работают как раньше.
7. Список из ~200 прогонов открывается без заметной задержки (ориентир — доли секунды).
8. Открыть один прогон — в заголовке окна графика видно `Название · Модель · Эксперимент · Режим`.
9. В списке каналов открытого прогона присутствуют `T-sie` и `UR-sie`.
10. Значения `T-sie` на графике соответствуют показанному режиму.
11. Добавить второй прогон через «Из БД…» — заголовок перечисляет названия через `; `, метаданные не добавляются.
12. Открыть папочный источник (DBF) — заголовок не изменился по сравнению с предыдущей версией.
13. Открыть активную запись (статус «Идёт запись») — живое обновление раз в 30 секунд работает, каналы `T-sie` и `UR-sie` дополняются.
14. Экспорт в шаблон отрабатывает без ошибок на прогоне с общими каналами.

Файл добавить в git принудительно: каталог `doc/` в `.gitignore`, но существующие чек-листы там уже отслеживаются.

- [ ] **Step 3: Коммит**

```bash
git add -f doc/climate_mode_manual_checklist.md
git commit -m "Добавлен чек-лист проверки климатического режима"
```

- [ ] **Step 4: Ревью**

Запросить ревью по скиллу `superpowers:requesting-code-review`: проверка соответствия спецификации и проверка качества. Замечания уровня Critical и Important исправить отдельным коммитом, повторно прогнав сборку и тесты.

---

## Проверка плана относительно спецификации

| Требование спецификации | Задача |
|---|---|
| `ClimateModeResolver`, приоритет `climate_mode`, допуск ±3 °C, влажность не участвует | 1 |
| Метки `25/60`, `32/65`, `40/40` из идентификаторов БД | 1 |
| SQL каталога с CTE и подзапросами `LIMIT 5`, запрет `row_number()` | 2 |
| `RecordingSummaryItem.ClimateMode` | 2 |
| Колонка «Режим» и подсказки об источнике | 3 |
| Ключ «Климатический режим» в `TestData.Meta` | 4 |
| Общие каналы `T-sie` и `UR-sie` (`is_common`) | 4 |
| Заголовок для одного источника через ` · `, для нескольких — как раньше | 5 |
| Полная регрессия, сборки Debug и Release, ручной чек-лист | 6 |
