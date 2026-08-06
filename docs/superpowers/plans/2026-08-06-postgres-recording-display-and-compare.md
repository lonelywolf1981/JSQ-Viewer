# Названия прогонов PostgreSQL и добавление для сравнения — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Показывать пользовательское название PostgreSQL-прогона вместо технического ID и позволять добавлять новые записи БД к текущему workspace для сравнения, не превышая общий лимит шести источников.

**Architecture:** Технический root `jsqdb://recording/<id>` остаётся единственным идентификатором. `TestData` получает отдельные `SourceDisplayNames` и `SourceOrder`; один resolver строит безопасные и различимые подписи, а UI, chart pipeline и forecast используют его как единственный источник отображаемых имён. Добавление записей оформляется чистой операцией orchestration-сервиса с исходами `Success`, `NoNewSources`, `LimitExceeded`, после чего `MainForm` сохраняет существующий rollback и single-source live-refresh.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WinForms, MSTest 3.5.2, Npgsql 6.0.13.

**Спецификация:** `docs/superpowers/specs/2026-08-06-postgres-recording-display-and-compare-design.md`

---

## Ограничения и инварианты

- Не менять формат `jsqdb://recording/<id>` и не использовать display name для запросов, дедупликации, recent sources, workspace key или live-refresh.
- Все сравнения source root выполнять через `StringComparer.OrdinalIgnoreCase`/`StringComparison.OrdinalIgnoreCase`.
- `SourceDisplayNames` и `SourceOrder` всегда инициализированы; при чтении старых/неполных `TestData` production-код всё равно безопасно обрабатывает `null`.
- Порядок workspace определяется `SourceOrder`; отсутствующие там root добавляются из `SourceColumns` только как fallback.
- Два разных root с одинаковым названием не сливаются. Для обоих показывается `Название [полный recordingId]`.
- Для файловых источников `Meta["Название"]` игнорируется: сохраняется прежнее отображение имени файла/папки.
- После дедупликации более шести уникальных root означают полный отказ операции без частичного добавления.
- Не присваивать `_folderBox.Text` до успешной загрузки: `LoadFolder` уже делает commit/rollback textbox, session и live-refresh.
- Основной проект использует C# 7.3: не применять nullable reference types, switch expressions, target-typed `new`, `using` declarations.

## Структура файлов

**Создаются:**

| Файл | Ответственность |
| --- | --- |
| `Application/Workspace/SourceDisplayNameResolver.cs` | Упорядочивание roots, fallback имён и различение коллизий title |
| `Application/Workspace/WorkspaceTitleBuilder.cs` | Заголовок одиночного/составного workspace |
| `Application/Workspace/WorkspaceSourceAdditionResult.cs` | Типизированный результат добавления sources |
| `Presentation/WinForms/Presenters/DynamicsForecastRoleItemBuilder.cs` | Тестируемые labels ролей прогноза на основе resolver |
| `Presentation/WinForms/Presenters/DatabaseRecordingSelectionPresenter.cs` | Преобразование результата current + selected в load либо локализуемый отказ |
| `Presentation/WinForms/Presenters/WorkspaceLoadRollbackCoordinator.cs` | Тестируемое решение и callbacks восстановления textbox/live-refresh после failed load |
| `Presentation/WinForms/ViewModels/DynamicsForecastRoleItemViewModel.cs` | Code/Label/DurationHours для combo box ролей |
| `JSQViewer.Tests/SourceDisplayNameResolverTests.cs` | Resolver и workspace-title builder |
| `JSQViewer.Tests/MergeLoadedSourcesUseCaseTests.cs` | Перенос display names/order через merge и коллизии roots |
| `JSQViewer.Tests/ForecastRoleItemBuilderTests.cs` | Labels ролей прогноза и техническая identity |
| `JSQViewer.Tests/DatabaseRecordingSelectionPresenterTests.cs` | Отказ не вызывает load; success передаёт полный spec |
| `JSQViewer.Tests/WorkspaceLoadRollbackCoordinatorTests.cs` | Failed add восстанавливает source text и live-refresh callbacks |
| `doc/postgres_recording_compare_manual_checklist.md` | Ручная проверка видимых UI-сценариев и rollback |

**Изменяются:**

| Файл | Что меняется |
| --- | --- |
| `Core/Models.cs` | `TestData.SourceDisplayNames`, `TestData.SourceOrder` и безопасные значения по умолчанию |
| `Application/Database/RecordingRowsToTestDataMapper.cs` | Запись trimmed title/ID и сохранение identity при append |
| `Application/Workspace/UseCases/LoadWorkspaceDataUseCase.cs` | Гарантия порядка одиночной загрузки |
| `Application/Workspace/UseCases/MergeLoadedSourcesUseCase.cs` | Merge имён и явного порядка |
| `Application/Workspace/UseCases/RemoveLoadedSourceUseCase.cs` | Фильтрация имён и порядка по оставшим root |
| `Application/Channels/ChannelWorkspaceModel.cs` | Source windows следуют `SourceOrder` |
| `Application/Workspace/WorkspaceLoadOrchestrationService.cs` | Чистая атомарная операция current + selected |
| `Application/Charting/ChartPipelineService.cs` | Legend prefixes через resolver |
| `Presentation/WinForms/Presenters/ChannelWorkspacePresenter.cs` | Передача разрешённых title coordinator’у |
| `Presentation/WinForms/Presenters/SourceWindowCoordinator.cs` | Хранение title отдельно от root; удаление `Path.GetFileName` |
| `UI/MainForm.cs` | Add-from-DB, chart/source titles, localization, forecast labels |
| `JSQViewer.Tests/RecordingRowsToTestDataMapperTests.cs` | Title, blank fallback, append identity |
| `JSQViewer.Tests/RecordingLiveRefreshTests.cs` | Сохранение display name/order при append |
| `JSQViewer.Tests/RecordingWorkspaceLoadingTests.cs` | Single DB load сохраняет identity metadata |
| `JSQViewer.Tests/WorkspaceLoadingTests.cs` | Source order одиночной/смешанной загрузки |
| `JSQViewer.Tests/RemoveLoadedSourceUseCaseTests.cs` | Удаление по root без потери второго одинакового title |
| `JSQViewer.Tests/ChannelWorkspaceTests.cs` | Порядок и title окон источников |
| `JSQViewer.Tests/ChartPipelineTests.cs` | DB title в legend и различение одинаковых названий |
| `JSQViewer.Tests/WorkspaceLoadOrchestrationServiceTests.cs` | Дубликаты, mixed workspace и атомарный лимит |

## Порядок выполнения

Задачи выполняются последовательно. Каждая начинается с красного теста и завершается отдельным коммитом. После задач 1–3 доступна полная модель identity/display; задачи 4–6 подключают её к UI и поведению сравнения; задача 7 — интеграционная проверка.

---

### Task 1: Identity metadata в `TestData` и PostgreSQL mapper

**Files:**
- Modify: `Core/Models.cs:23-51`
- Modify: `Application/Database/RecordingRowsToTestDataMapper.cs:20-96, 104-190`
- Test: `JSQViewer.Tests/RecordingRowsToTestDataMapperTests.cs`
- Test: `JSQViewer.Tests/RecordingLiveRefreshTests.cs`

- [ ] **Step 1: Написать failing tests конструктора и mapper**

Добавить проверки:

```csharp
[TestMethod]
public void TestData_InitializesSourceIdentityCollections()
{
    var data = new TestData();

    Assert.IsNotNull(data.SourceDisplayNames);
    Assert.IsNotNull(data.SourceOrder);
    data.SourceDisplayNames["ROOT"] = "Name";
    Assert.AreEqual("Name", data.SourceDisplayNames["root"]);
}

[TestMethod]
public void Map_UsesTrimmedRecordingTitleAsSourceDisplayName()
{
    TestData data = new RecordingRowsToTestDataMapper().Map(
        Source,
        "B",
        new List<RecordingAggregateRow>(),
        Channels(),
        new Dictionary<string, string> { { "Название", "  Испытание KA50  " } });

    Assert.AreEqual("Испытание KA50", data.SourceDisplayNames[Source]);
    CollectionAssert.AreEqual(new[] { Source }, data.SourceOrder);
}

[TestMethod]
public void Map_BlankRecordingTitleFallsBackToRecordingId()
{
    TestData data = new RecordingRowsToTestDataMapper().Map(
        Source,
        "B",
        new List<RecordingAggregateRow>(),
        Channels(),
        new Dictionary<string, string> { { "Название", "   " } });

    Assert.AreEqual("abc", data.SourceDisplayNames[Source]);
}
```

- [ ] **Step 2: Запустить mapper-тесты и подтвердить RED**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~RecordingRowsToTestDataMapperTests
```

Expected: FAIL — у `TestData` ещё нет `SourceDisplayNames`/`SourceOrder`.

- [ ] **Step 3: Добавить свойства и минимальный mapper**

В `TestData`:

```csharp
public Dictionary<string, string> SourceDisplayNames { get; set; }
public string[] SourceOrder { get; set; }
```

В конструкторе:

```csharp
SourceDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
SourceOrder = new string[0];
```

В mapper добавить helper и заполнение результата:

```csharp
private static string ResolveRecordingDisplayName(
    string source,
    IDictionary<string, string> metadata)
{
    string title;
    if (metadata != null
        && metadata.TryGetValue("Название", out title)
        && !string.IsNullOrWhiteSpace(title))
    {
        return title.Trim();
    }

    string recordingId;
    return RecordingSourceRef.TryParse(source, out recordingId)
        ? recordingId
        : source;
}
```

При `Map` создать case-insensitive словарь с одной парой `{ source, ResolveRecordingDisplayName(...) }` и `SourceOrder = new[] { source }`.

- [ ] **Step 4: Написать failing append-тесты**

```csharp
[TestMethod]
public void Append_WithNewRows_PreservesSourceIdentityMetadata()
{
    var mapper = new RecordingRowsToTestDataMapper();
    TestData initial = mapper.Map(
        Source,
        "B",
        new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0) },
        Channels(),
        new Dictionary<string, string> { { "Название", "Прогон 1" } });

    TestData result = mapper.Append(
        initial,
        "B",
        new List<RecordingAggregateRow> { Row("B-T1", 2000, 11.0) });

    Assert.AreEqual("Прогон 1", result.SourceDisplayNames[Source]);
    CollectionAssert.AreEqual(new[] { Source }, result.SourceOrder);
}
```

Расширить `GrowingRecordingReader_AppendsNewWindowsWithoutReplacingExistingRows`: metadata содержит название, после двух append оно и `SourceOrder` не меняются.

- [ ] **Step 5: Сохранить identity metadata в `Append`**

В создаваемом `TestData`:

```csharp
SourceDisplayNames = existing.SourceDisplayNames == null
    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    : new Dictionary<string, string>(existing.SourceDisplayNames, StringComparer.OrdinalIgnoreCase),
SourceOrder = existing.SourceOrder == null
    ? new string[0]
    : existing.SourceOrder.ToArray(),
```

Ветка без новых строк продолжает возвращать тот же instance.

- [ ] **Step 6: Запустить целевые тесты и закоммитить**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "FullyQualifiedName~RecordingRowsToTestDataMapperTests|FullyQualifiedName~RecordingLiveRefreshTests"
```

Expected: PASS.

```powershell
git add Core/Models.cs Application/Database/RecordingRowsToTestDataMapper.cs JSQViewer.Tests/RecordingRowsToTestDataMapperTests.cs JSQViewer.Tests/RecordingLiveRefreshTests.cs
git commit -m "Добавлены отображаемые имена прогонов"
```

---

### Task 2: Перенос имён и порядка через load, merge и remove

**Files:**
- Modify: `Application/Workspace/UseCases/LoadWorkspaceDataUseCase.cs:41-103`
- Modify: `Application/Workspace/UseCases/MergeLoadedSourcesUseCase.cs:24-290`
- Modify: `Application/Workspace/UseCases/RemoveLoadedSourceUseCase.cs:10-177`
- Create: `JSQViewer.Tests/MergeLoadedSourcesUseCaseTests.cs`
- Modify: `JSQViewer.Tests/RecordingWorkspaceLoadingTests.cs`
- Modify: `JSQViewer.Tests/WorkspaceLoadingTests.cs`
- Modify: `JSQViewer.Tests/RemoveLoadedSourceUseCaseTests.cs`

- [ ] **Step 1: Написать failing merge/order tests**

Создать `MergeLoadedSourcesUseCaseTests` с helpers для двух одноточечных `TestData`. Проверить:

```csharp
[TestMethod]
public void Execute_PreservesDisplayNamesAndInputSourceOrder()
{
    TestData first = CreateData("jsqdb://recording/a", "Прогон A", "A-T1", 10L);
    TestData second = CreateData("jsqdb://recording/b", "Прогон B", "B-T1", 20L);

    TestData result = new MergeLoadedSourcesUseCase().Execute(
        new[] { first, second },
        true);

    Assert.AreEqual("Прогон A", result.SourceDisplayNames[first.Root]);
    Assert.AreEqual("Прогон B", result.SourceDisplayNames[second.Root]);
    CollectionAssert.AreEqual(new[] { first.Root, second.Root }, result.SourceOrder);
}

[TestMethod]
public void Execute_SameRootUsesFirstNonBlankDisplayName()
{
    TestData blank = CreateData("jsqdb://recording/a", " ", "A-T1", 10L);
    TestData named = CreateData("JSQDB://RECORDING/A", "Прогон A", "A-T2", 20L);

    TestData result = new MergeLoadedSourcesUseCase().Execute(
        new[] { blank, named },
        true);

    Assert.AreEqual("Прогон A", result.SourceDisplayNames[blank.Root]);
    CollectionAssert.AreEqual(new[] { blank.Root }, result.SourceOrder);
}
```

Добавить тест с уже merged input: `SourceOrder` обрабатывается раньше `SourceColumns`, отсутствующие roots добавляются в конец, повторов по регистру нет.

- [ ] **Step 2: Написать failing remove и single-load tests**

В fixture `RemoveLoadedSourceUseCaseTests` добавить два разных URI с одинаковым названием и порядок. После удаления первого по root остаётся только второй display-name entry/order.

В `RecordingWorkspaceLoadingTests` проверить, что одиночный DB result имеет один `SourceOrder`. В `WorkspaceLoadingTests` проверить, что одиночный файловый reader с пустым order нормализуется до root, а смешанная загрузка сохраняет request order.

- [ ] **Step 3: Запустить тесты и подтвердить RED**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "FullyQualifiedName~MergeLoadedSourcesUseCaseTests|FullyQualifiedName~RemoveLoadedSourceUseCaseTests|FullyQualifiedName~RecordingWorkspaceLoadingTests|FullyQualifiedName~WorkspaceLoadingTests"
```

Expected: FAIL — новые поля не переносятся.

- [ ] **Step 4: Нормализовать single-load order**

После чтения каждого root в `LoadWorkspaceDataUseCase.Execute` вызвать один helper:

```csharp
private static TestData EnsureSourceOrder(TestData data, string fallbackRoot)
{
    if (data == null)
    {
        return data;
    }

    var order = new List<string>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (data.SourceOrder != null)
    {
        foreach (string root in data.SourceOrder)
        {
            if (!string.IsNullOrWhiteSpace(root) && seen.Add(root)) order.Add(root);
        }
    }
    if (data.SourceColumns != null)
    {
        foreach (string root in data.SourceColumns.Keys)
        {
            if (!string.IsNullOrWhiteSpace(root) && seen.Add(root)) order.Add(root);
        }
    }
    if (order.Count == 0 && !string.IsNullOrWhiteSpace(fallbackRoot)) order.Add(fallbackRoot);
    data.SourceOrder = order.ToArray();
    return data;
}
```

Не менять root и не создавать display name для файлового источника.

- [ ] **Step 5: Реализовать merge metadata до дедупликации data rows**

До `DeduplicateByRoot` собрать auxiliary state из исходного списка:

```csharp
Dictionary<string, string> sourceDisplayNames = BuildSourceDisplayNames(list);
string[] sourceOrder = BuildSourceOrder(list);
```

`BuildSourceDisplayNames` перебирает inputs в порядке аргументов, пропускает null/whitespace, делает `Trim()` и записывает только первое непустое значение каждого root. `BuildSourceOrder` сначала использует `data.SourceOrder`, затем недостающие `data.SourceColumns.Keys`; оба используют case-insensitive `HashSet`.

Важно: собирать эти поля до `DeduplicateByRoot`, чтобы сценарий «первый duplicate root без имени, второй с именем» сохранил первое непустое имя. Если после дедупликации остался один data source, вернуть его через helper, который присваивает новые cloned collections, не теряя найденное имя/order.

В итоговом `new TestData` добавить:

```csharp
SourceDisplayNames = sourceDisplayNames,
SourceOrder = sourceOrder,
```

- [ ] **Step 6: Реализовать remove filtering**

В результате `RemoveLoadedSourceUseCase.Execute`:

```csharp
SourceDisplayNames = FilterDisplayNames(data.SourceDisplayNames, remainingSourceColumns.Keys),
SourceOrder = FilterSourceOrder(data.SourceOrder, remainingSourceColumns.Keys),
```

Оба helper создают новые коллекции, сравнивают roots case-insensitive и никогда не используют display name как ключ удаления. Если старый `SourceOrder` пуст, `FilterSourceOrder` использует порядок `remainingSourceColumns.Keys` как fallback.

- [ ] **Step 7: Запустить целевые тесты и закоммитить**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "FullyQualifiedName~MergeLoadedSourcesUseCaseTests|FullyQualifiedName~RemoveLoadedSourceUseCaseTests|FullyQualifiedName~RecordingWorkspaceLoadingTests|FullyQualifiedName~WorkspaceLoadingTests"
```

Expected: PASS.

```powershell
git add Application/Workspace/UseCases/LoadWorkspaceDataUseCase.cs Application/Workspace/UseCases/MergeLoadedSourcesUseCase.cs Application/Workspace/UseCases/RemoveLoadedSourceUseCase.cs JSQViewer.Tests/MergeLoadedSourcesUseCaseTests.cs JSQViewer.Tests/RecordingWorkspaceLoadingTests.cs JSQViewer.Tests/WorkspaceLoadingTests.cs JSQViewer.Tests/RemoveLoadedSourceUseCaseTests.cs
git commit -m "Сохранены имена и порядок источников"
```

---

### Task 3: Единый resolver и заголовок workspace

**Files:**
- Create: `Application/Workspace/SourceDisplayNameResolver.cs`
- Create: `Application/Workspace/WorkspaceTitleBuilder.cs`
- Create: `JSQViewer.Tests/SourceDisplayNameResolverTests.cs`

- [ ] **Step 1: Написать полный набор failing resolver tests**

Покрыть:

- DB title с `Trim()`;
- whitespace title → ID;
- legacy single DB без `SourceDisplayNames` → `Meta["Название"]`;
- file root с `Meta["Название"]` → имя файла/папки, metadata игнорируется;
- `SourceOrder` задаёт порядок, missing roots из `SourceColumns` идут в конец;
- два URI с title, совпадающим без учёта регистра, оба становятся `Название [id]`;
- одинаковый title файла и DB не приводит к слиянию roots; ID-disambiguation применяется только DB collision;
- title builder: single, multi через `; `, empty data → fallback session folder.

Ключевой тест:

```csharp
[TestMethod]
public void ResolveAll_SameDatabaseTitlesKeepBothRootsAndAddIds()
{
    TestData data = CreateData(
        new[] { "jsqdb://recording/a", "jsqdb://recording/b" },
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "jsqdb://recording/a", "KA50" },
            { "jsqdb://recording/b", "ka50" }
        });

    IReadOnlyDictionary<string, string> result = new SourceDisplayNameResolver().ResolveAll(data);

    Assert.AreEqual("KA50 [a]", result["jsqdb://recording/a"]);
    Assert.AreEqual("ka50 [b]", result["jsqdb://recording/b"]);
    Assert.AreEqual(2, result.Count);
}
```

- [ ] **Step 2: Запустить resolver tests и подтвердить RED**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~SourceDisplayNameResolverTests
```

Expected: FAIL — классы отсутствуют.

- [ ] **Step 3: Реализовать `SourceDisplayNameResolver`**

Публичный контракт:

```csharp
public sealed class SourceDisplayNameResolver
{
    public IReadOnlyList<string> GetOrderedRoots(TestData data);
    public IReadOnlyDictionary<string, string> ResolveAll(TestData data);
    public string Resolve(TestData data, string sourceRoot);
}
```

Алгоритм `ResolveAll`:

1. Получить distinct roots из `SourceOrder`, затем `SourceColumns`.
2. Для каждого root выбрать базовое имя: `SourceDisplayNames[root].Trim()`; legacy `Meta["Название"]` только если root единственный и DB; DB ID; `Path.GetFileName(root.TrimEnd(...))`; root.
3. Сгруппировать базовые имена без учёта регистра.
4. В конфликтующей группе заменить подпись каждого DB-root на `baseName + " [" + recordingId + "]"`.
5. Вернуть новый case-insensitive dictionary, не удаляя элементы по совпадению values.

`Resolve(data, root)` вызывает `ResolveAll` для collision-aware результата и использует безопасный одиночный fallback, если root отсутствовал в order/columns.

- [ ] **Step 4: Реализовать `WorkspaceTitleBuilder`**

```csharp
public sealed class WorkspaceTitleBuilder
{
    private readonly SourceDisplayNameResolver _resolver;

    public WorkspaceTitleBuilder(SourceDisplayNameResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public string Build(TestData data, string fallback)
    {
        IReadOnlyList<string> roots = _resolver.GetOrderedRoots(data);
        IReadOnlyDictionary<string, string> names = _resolver.ResolveAll(data);
        string[] titles = roots
            .Where(root => names.ContainsKey(root) && !string.IsNullOrWhiteSpace(names[root]))
            .Select(root => names[root])
            .ToArray();
        return titles.Length == 0 ? (fallback ?? string.Empty) : string.Join("; ", titles);
    }

    public string BuildCaption(TestData data, string fallback, string format)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            string.IsNullOrWhiteSpace(format) ? "{0}" : format,
            Build(data, fallback));
    }
}
```

Тест `BuildCaption_DataAlreadyLoadedBeforeWindowCreation_UsesDatabaseTitle` передаёт format `"График — {0}"` и ожидает `"График — Прогон A"`. Этот pure метод используется как при создании host, так и при последующей локализации.

- [ ] **Step 5: Запустить тесты и закоммитить**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~SourceDisplayNameResolverTests
```

Expected: PASS.

```powershell
git add Application/Workspace/SourceDisplayNameResolver.cs Application/Workspace/WorkspaceTitleBuilder.cs JSQViewer.Tests/SourceDisplayNameResolverTests.cs
git commit -m "Добавлено разрешение названий источников"
```

---

### Task 4: Source windows, chart legends и forecast labels

**Files:**
- Modify: `Application/Channels/ChannelWorkspaceModel.cs:31-102`
- Modify: `Application/Charting/ChartPipelineService.cs:356-378`
- Modify: `Presentation/WinForms/Presenters/ChannelWorkspacePresenter.cs:10-55`
- Modify: `Presentation/WinForms/Presenters/SourceWindowCoordinator.cs:10-158`
- Create: `Presentation/WinForms/Presenters/DynamicsForecastRoleItemBuilder.cs`
- Create: `Presentation/WinForms/ViewModels/DynamicsForecastRoleItemViewModel.cs`
- Modify: `JSQViewer.Tests/ChannelWorkspaceTests.cs`
- Modify: `JSQViewer.Tests/ChartPipelineTests.cs`
- Create: `JSQViewer.Tests/ForecastRoleItemBuilderTests.cs`

- [ ] **Step 1: Написать failing presenter/order tests**

В `ChannelWorkspacePresenterTests`:

- `BindData_UsesSourceOrderInsteadOfDictionaryEnumeration`;
- `BindData_DatabaseSourceWindowUsesDisplayTitle`;
- `BindData_SameTitlesKeepTwoWindowsWithIdsAndTechnicalRoots`;
- stable roots refresh сохраняет layout, но применяет актуальные resolved titles.

Пример assertions:

```csharp
CollectionAssert.AreEqual(
    new[] { "jsqdb://recording/b", "jsqdb://recording/a" },
    windows.Select(window => window.SourceRoot).ToArray());
CollectionAssert.AreEqual(
    new[] { "KA50 [b]", "KA50 [a]" },
    windows.Select(window => window.Title).ToArray());
```

- [ ] **Step 2: Написать failing chart/forecast tests**

В `ChartPipelineTests` проверить multi-source DB legend `[Прогон A] T1`, collision `[KA50 [a]] T1`, а также неизменный `ChartPipelineSeries.SourceRoot == jsqdb://recording/a`. Single-source legend остаётся без prefix.

В `ForecastRoleItemBuilderTests` проверить те же resolved labels, порядок selected codes, duration по `SourceStartMs`/`SourceEndMs`, и что `Code` остаётся техническим channel code.

- [ ] **Step 3: Запустить тесты и подтвердить RED**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "FullyQualifiedName~ChannelWorkspacePresenterTests|FullyQualifiedName~ChartPipelineTests|FullyQualifiedName~ForecastRoleItemBuilderTests"
```

Expected: FAIL — UI consumers всё ещё используют `Path.GetFileName`.

- [ ] **Step 4: Подключить порядок и titles к source windows**

`ChannelWorkspaceModel.Load` строит `_sourceRoots` через `SourceDisplayNameResolver.GetOrderedRoots(data)` и для каждого root берёт columns из `SourceColumns`.

`ChannelWorkspacePresenter` получает resolver через private field/constructor, при `BindData` вычисляет `ResolveAll(data)` и вызывает:

```csharp
_sourceWindowCoordinator.BindRoots(
    _workspace.SourceRoots,
    resolvedTitles,
    _mainSortMode,
    preserveSourceWindowsLayout);
```

`SourceWindowCoordinator` хранит case-insensitive `_titles`, обновляет их при каждом bind и строит view model через `_titles[root]`. `HaveSameRoots` продолжает сравнивать только roots. Удалить `System.IO` и локальный `BuildTitle`.

Сохранить существующий parameterless constructor `ChannelWorkspacePresenter()` для composition; он делегирует constructor с `new SourceDisplayNameResolver()`.

- [ ] **Step 5: Подключить resolver к chart pipeline**

Добавить resolver dependency с совместимым default constructor. В `BuildSeriesLegendText` заменить локальный `Path.GetFileName`:

```csharp
string sourceName = _sourceDisplayNameResolver.Resolve(data, source);
return string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", sourceName, displayCode);
```

Если метод сейчас static, сделать instance method; production call sites уже находятся внутри instance `Execute`.

- [ ] **Step 6: Вынести forecast role item builder**

`DynamicsForecastRoleItemViewModel` содержит `Code`, `Label`, `DurationHours` и `ToString() => Label`.

Builder получает `SourceDisplayNameResolver` и для каждого selected code:

```csharp
string source = ResolveSourceRoot(data, code);
string sourceName = _resolver.Resolve(data, source);
string label = string.IsNullOrWhiteSpace(sourceName)
    ? NormalizeChannelCodeForDisplay(code)
    : string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", sourceName, NormalizeChannelCodeForDisplay(code));
```

Duration вычисляется по техническому source root. Перенести из `MainForm` только чистую логику; диалог выбора остаётся в UI.

- [ ] **Step 7: Запустить тесты и закоммитить**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "FullyQualifiedName~ChannelWorkspacePresenterTests|FullyQualifiedName~ChartPipelineTests|FullyQualifiedName~ForecastRoleItemBuilderTests"
```

Expected: PASS.

```powershell
git add Application/Channels/ChannelWorkspaceModel.cs Application/Charting/ChartPipelineService.cs Presentation/WinForms/Presenters/ChannelWorkspacePresenter.cs Presentation/WinForms/Presenters/SourceWindowCoordinator.cs Presentation/WinForms/Presenters/DynamicsForecastRoleItemBuilder.cs Presentation/WinForms/ViewModels/DynamicsForecastRoleItemViewModel.cs JSQViewer.Tests/ChannelWorkspaceTests.cs JSQViewer.Tests/ChartPipelineTests.cs JSQViewer.Tests/ForecastRoleItemBuilderTests.cs
git commit -m "Применены названия прогонов в окнах и графиках"
```

---

### Task 5: Атомарное добавление записей к текущему workspace

**Files:**
- Create: `Application/Workspace/WorkspaceSourceAdditionResult.cs`
- Modify: `Application/Workspace/WorkspaceLoadOrchestrationService.cs:24-76`
- Modify: `JSQViewer.Tests/WorkspaceLoadOrchestrationServiceTests.cs`

- [ ] **Step 1: Написать failing tests чистой операции**

Добавить сценарии:

1. folder + xlsx + выбранный DB URI сохраняют порядок;
2. duplicate текущего URI отбрасывается case-insensitive;
3. полностью duplicate selection → `NoNewSources`;
4. 5 current + duplicate + 1 new → `Success`, итог 6;
5. 5 current + 2 new → `LimitExceeded`, без частичного `FolderSpec`;
6. 6 current + selection → `LimitExceeded`;
7. два разных URI остаются двумя roots независимо от одинаковых display names вне операции.

```csharp
[TestMethod]
public void AddSources_FiveCurrentDuplicateAndNew_AddsOnlyNewToSix()
{
    WorkspaceLoadOrchestrationService service = CreateService();
    string current = "C:\\a ; C:\\b ; C:\\c ; C:\\d ; jsqdb://recording/old";

    WorkspaceSourceAdditionResult result = service.AddSources(
        current,
        new[] { "JSQDB://RECORDING/OLD", "jsqdb://recording/new" });

    Assert.AreEqual(WorkspaceSourceAdditionStatus.Success, result.Status);
    Assert.AreEqual(6, result.Sources.Count);
    Assert.AreEqual("jsqdb://recording/new", result.Sources[5]);
}
```

- [ ] **Step 2: Запустить orchestration tests и подтвердить RED**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~WorkspaceLoadOrchestrationServiceTests
```

Expected: FAIL — result type/метод отсутствуют.

- [ ] **Step 3: Реализовать result type**

```csharp
public enum WorkspaceSourceAdditionStatus
{
    Success,
    NoNewSources,
    LimitExceeded
}

public sealed class WorkspaceSourceAdditionResult
{
    public WorkspaceSourceAdditionResult(
        WorkspaceSourceAdditionStatus status,
        IReadOnlyList<string> sources,
        string folderSpec)
    {
        Status = status;
        Sources = sources ?? new string[0];
        FolderSpec = folderSpec ?? string.Empty;
    }

    public WorkspaceSourceAdditionStatus Status { get; private set; }
    public IReadOnlyList<string> Sources { get; private set; }
    public string FolderSpec { get; private set; }
}
```

- [ ] **Step 4: Реализовать `AddSources`**

Публичный контракт:

```csharp
public WorkspaceSourceAdditionResult AddSources(
    string currentSpec,
    IEnumerable<string> selectedSources)
```

Алгоритм: `_parser.Parse(currentSpec)`, добавить trimmed selected через case-insensitive `HashSet`, посчитать `addedCount`, затем сначала `combined.Count > MaxFolderCount => LimitExceeded`, затем `addedCount == 0 => NoNewSources`, иначе `Success`. Для rejection вернуть исходные parsed sources и исходный joined spec — не частичный combined.

Display names не являются аргументом этого API.

- [ ] **Step 5: Запустить тесты и закоммитить**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter FullyQualifiedName~WorkspaceLoadOrchestrationServiceTests
```

Expected: PASS.

```powershell
git add Application/Workspace/WorkspaceSourceAdditionResult.cs Application/Workspace/WorkspaceLoadOrchestrationService.cs JSQViewer.Tests/WorkspaceLoadOrchestrationServiceTests.cs
git commit -m "Добавлено атомарное объединение источников"
```

---

### Task 6: Интеграция добавления и дружественных заголовков в `MainForm`

**Files:**
- Modify: `UI/MainForm.cs:733-808, 977-1009, 1539-1624, 1861-1892, 2150-2180, 2406-2466, 3451-3472, 3698-3890, 5065-5071`
- Create: `Presentation/WinForms/Presenters/DatabaseRecordingSelectionPresenter.cs`
- Create: `Presentation/WinForms/Presenters/WorkspaceLoadRollbackCoordinator.cs`
- Create: `JSQViewer.Tests/DatabaseRecordingSelectionPresenterTests.cs`
- Create: `JSQViewer.Tests/WorkspaceLoadRollbackCoordinatorTests.cs`
- Modify: `JSQViewer.Tests/RecordingLiveRefreshTests.cs`
- Create: `doc/postgres_recording_compare_manual_checklist.md`

- [ ] **Step 1: Зафиксировать pure live-refresh policy тестом**

Извлечь из `UpdateLiveRefreshState` private static helper `TryGetSingleLiveRecordingId(WorkspaceLoadOrchestrationService service, string spec, out string recordingId)` либо эквивалентный Application helper. Reflection-тесты подтверждают:

- один canonical DB URI → `true` и ID;
- два DB URI → `false`;
- folder + DB → `false`;
- строка, содержащая DB URI не как единственный source → `false`.

Не тестировать настоящий WinForms timer в unit suite.

- [ ] **Step 2: Написать failing tests UI selection presenter**

`DatabaseRecordingSelectionPresenterTests` использует настоящий `WorkspaceLoadOrchestrationService`, но fake callbacks `loadedSpecs` и `notificationKeys`. Проверить:

- `Success` вызывает load ровно один раз с current + selected spec и не вызывает notification;
- `NoNewSources` вызывает только `SourceAlreadyAdded`;
- `LimitExceeded` вызывает только `TooManyFolders`;
- обе rejection-ветки не вызывают load, поэтому не могут инициировать изменение `_folderBox` через `LoadFolder`;
- presenter вообще не получает textbox/control, следовательно единственная разрешённая UI-мутация — переданный load callback на success.

```csharp
[TestMethod]
public void ApplySelection_LimitExceeded_NotifiesWithoutLoading()
{
    var loadedSpecs = new List<string>();
    var notificationKeys = new List<string>();
    var presenter = new DatabaseRecordingSelectionPresenter(CreateWorkspaceService());

    presenter.ApplySelection(
        "C:\\a ; C:\\b ; C:\\c ; C:\\d ; C:\\e",
        new[] { "jsqdb://recording/one", "jsqdb://recording/two" },
        loadedSpecs.Add,
        notificationKeys.Add);

    Assert.AreEqual(0, loadedSpecs.Count);
    CollectionAssert.AreEqual(new[] { "TooManyFolders" }, notificationKeys);
}
```

- [ ] **Step 3: Реализовать selection presenter и подключить handler**

Публичный метод presenter:

```csharp
public void ApplySelection(
    string currentSpec,
    IEnumerable<string> selectedSources,
    Action<string> loadFolder,
    Action<string> notifyByLocalizationKey)
{
    if (loadFolder == null) throw new ArgumentNullException(nameof(loadFolder));
    if (notifyByLocalizationKey == null) throw new ArgumentNullException(nameof(notifyByLocalizationKey));

    WorkspaceSourceAdditionResult result = _service.AddSources(currentSpec, selectedSources);
    if (result.Status == WorkspaceSourceAdditionStatus.NoNewSources)
    {
        notifyByLocalizationKey("SourceAlreadyAdded");
        return;
    }
    if (result.Status == WorkspaceSourceAdditionStatus.LimitExceeded)
    {
        notifyByLocalizationKey("TooManyFolders");
        return;
    }
    loadFolder(result.FolderSpec);
}
```

`OpenFromDatabaseButtonOnClick` должен выполнять только early full-workspace check, modal selection и вызов presenter:

```csharp
List<string> current = ParseFolderSpec(_folderBox.Text);
if (current.Count >= WorkspaceFolderSpecParser.MaxFolderCount)
{
    NotifyError(Loc.Get("TooManyFolders"));
    return;
}

using (var form = new OpenFromDatabaseForm(
    _recordingCatalog,
    WorkspaceFolderSpecParser.MaxFolderCount))
{
    if (form.ShowDialog(this) != DialogResult.OK) return;

    _databaseRecordingSelectionPresenter.ApplySelection(
        _folderBox.Text,
        form.SelectedSources,
        spec => LoadFolder(spec, true),
        key => NotifyError(Loc.Get(key)));
}
```

Не менять `_folderBox.Text` перед `LoadFolder`. Диалог получает max 6, а не свободное количество: так `5 current + duplicate + new` может пройти дедупликацию.

- [ ] **Step 4: Подключить единый chart title**

Добавить fields `SourceDisplayNameResolver`, `WorkspaceTitleBuilder`, `DynamicsForecastRoleItemBuilder`, `DatabaseRecordingSelectionPresenter`, `WorkspaceLoadRollbackCoordinator` с production defaults.

Один helper:

```csharp
private string GetWorkspaceDisplayTitle()
{
    return _workspaceTitleBuilder.Build(
        _viewerSession.Data,
        _viewerSession.Folder ?? Loc.Get("AppTitle"));
}

private void ApplyChartWindowTitles()
{
    string title = _workspaceTitleBuilder.BuildCaption(
        _viewerSession.Data,
        _viewerSession.Folder ?? Loc.Get("AppTitle"),
        Loc.Get("ChartWindowTitle"));
    if (_chartHostForm != null && !_chartHostForm.IsDisposed) _chartHostForm.Text = title;
    foreach (DetachedChartState state in _detachedCharts)
    {
        if (state != null && state.Form != null && !state.Form.IsDisposed) state.Form.Text = title;
    }
}
```

Вызывать после `BindLoadedData`, при создании detached form и из `ApplyLocalization`. В `EnsureChartHostForm` сразу после создания `_chartHostForm` обязательно вызвать `ApplyChartWindowTitles()` (либо присвоить caption через `GetWorkspaceDisplayTitle()`), потому что данные могут быть bound до первого открытия графика. Начальный пустой host использует AppTitle fallback.

Сценарий «данные уже находятся в session, host создаётся впервые» покрыт pure-тестом `WorkspaceTitleBuilder.BuildCaption` из Task 3; реальное окно в unit test не создаётся.

- [ ] **Step 5: Исправить source-window localization**

В `ApplyLocalization` заменить `Path.GetFileName(sw.SourceRoot...)` на:

```csharp
string sourceTitle = sw.ViewModel == null ? sw.SourceRoot : sw.ViewModel.Title;
sw.Form.Text = string.Format(Loc.Get("ChannelsForSource"), sourceTitle);
```

Остальные refresh/rebuild paths уже применяют `SourceChannelWindowViewModel.Title`.

- [ ] **Step 6: Подключить forecast builder и убрать расходящуюся логику**

`BuildForecastRoleItems` делегирует `DynamicsForecastRoleItemBuilder.Build(_viewerSession.Data, selectedCodes)`. Combo box и preselection используют новый view model. Удалить private `ForecastRoleItem`, `GetSourceDisplayName` и дублирующий неиспользуемый `MainForm.BuildSeriesLegendText`, если `rg` подтверждает отсутствие вызовов.

- [ ] **Step 7: Написать failing rollback coordinator tests**

Coordinator не владеет WinForms controls или timer. Он принимает snapshot identity и callbacks, поэтому тесты проверяют фактическое решение UI orchestration:

```csharp
[TestMethod]
public void RestoreAfterFailure_UnchangedSession_RestoresTextAndLiveRefresh()
{
    var calls = new List<string>();
    var data = new TestData();
    var coordinator = new WorkspaceLoadRollbackCoordinator();

    coordinator.RestoreAfterFailure(
        false,
        true,
        data,
        "jsqdb://recording/a",
        data,
        "jsqdb://recording/a",
        () => calls.Add("text"),
        () => calls.Add("live"));

    CollectionAssert.AreEqual(new[] { "text", "live" }, calls);
}
```

Дополнительно проверить: success не вызывает callbacks; failure после смены generation/session identity не восстанавливает stale state. Параметр `isCurrentGeneration` передаётся явно.

- [ ] **Step 8: Реализовать rollback coordinator и сохранить поведение `LoadFolder`**

Контракт:

```csharp
public void RestoreAfterFailure(
    bool loadSucceeded,
    bool isCurrentGeneration,
    TestData previousData,
    string previousFolder,
    TestData currentData,
    string currentFolder,
    Action restoreSourceText,
    Action restoreLiveRefresh)
```

Callbacks вызываются по порядку только когда `!loadSucceeded`, generation актуален, `ReferenceEquals(previousData, currentData)` и folders совпадают ordinal. В `LoadFolder.finally` заменить условие на вызов coordinator; callbacks остаются существующими:

```csharp
() => _folderBox.Text = previousSourceText,
() => RestoreLiveRefreshState(previousLiveRefreshState)
```

`UpdateLiveRefreshState` использует извлечённую pure policy. Успех 1→2 sources вызывает `StopLiveRefresh` и не запускает timer. Ошибка `LoadFolder` проходит coordinator и возвращает `_folderBox`, session data/folder и captured timer state.

Не добавлять multi-source refresh.

- [ ] **Step 9: Запустить целевые тесты и Debug build**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "FullyQualifiedName~RecordingLiveRefreshTests|FullyQualifiedName~ChannelWorkspacePresenterTests|FullyQualifiedName~SourceDisplayNameResolverTests|FullyQualifiedName~ForecastRoleItemBuilderTests|FullyQualifiedName~DatabaseRecordingSelectionPresenterTests|FullyQualifiedName~WorkspaceLoadRollbackCoordinatorTests"
dotnet build .\JSQViewer.csproj -c Debug
```

Expected: tests PASS; build 0 errors.

- [ ] **Step 10: Написать manual checklist и закоммитить**

Checklist должен включать:

- одиночный DB title в main chart и source window;
- второе нажатие «Из БД…» сохраняет первый прогон;
- одинаковые titles различаются `[recordingId]`;
- duplicate-only ничего не перезагружает;
- 5 + duplicate + new даёт 6;
- 5 + 2 new отклоняется целиком;
- успешный 1→2 останавливает live-refresh;
- намеренно неудачная загрузка второго source возвращает textbox/session/timer;
- folder/XLSX названия не изменились;
- смена языка обновляет main, detached и source-window captions без возврата ID.

```powershell
git add UI/MainForm.cs Presentation/WinForms/Presenters/DatabaseRecordingSelectionPresenter.cs Presentation/WinForms/Presenters/WorkspaceLoadRollbackCoordinator.cs JSQViewer.Tests/DatabaseRecordingSelectionPresenterTests.cs JSQViewer.Tests/WorkspaceLoadRollbackCoordinatorTests.cs JSQViewer.Tests/RecordingLiveRefreshTests.cs doc/postgres_recording_compare_manual_checklist.md
git commit -m "Исправлено добавление прогонов для сравнения"
```

---

### Task 7: Полная регрессия и независимое ревью

**Files:**
- Modify only if verification exposes a defect.

- [ ] **Step 1: Запустить полный тестовый набор**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj
```

Expected: все не-skipped тесты PASS; ранее известные skipped integration tests остаются skipped.

- [ ] **Step 2: Собрать Debug и Release**

Run:

```powershell
dotnet build .\JSQViewer.csproj -c Debug
dotnet build .\JSQViewer.csproj -c Release
```

Expected: 0 errors. Известные предупреждения test-project dependency resolution не считать новыми production-регрессиями; сравнить с baseline.

- [ ] **Step 3: Выполнить ручную проверку**

Запустить:

```powershell
.\bin\Debug\JSQViewer.exe
```

Пройти `doc/postgres_recording_compare_manual_checklist.md` на доступной PostgreSQL базе. Если база недоступна, явно записать непроверенные пункты в итоговый отчёт, не объявляя их пройденными.

- [ ] **Step 4: Проверить diff и артефакты**

Run:

```powershell
git diff --check
git status --short
git diff --stat master...HEAD
```

Expected: нет whitespace errors; в commit scope нет `bin/`, `obj/`, локальных настроек, screenshots или пользовательских untracked файлов.

- [ ] **Step 5: Запросить двухэтапное code review**

Использовать `superpowers:requesting-code-review`: сначала проверка соответствия спецификации/плану, затем качества реализации. Исправления снова проводить через failing test → minimal fix → verification.

- [ ] **Step 6: Финальный commit при необходимости**

Если ревью потребовало исправлений:

```powershell
git add <только-файлы-исправления>
git commit -m "Устранены замечания по сравнению прогонов"
```

После последнего commit повторить полный `dotnet test` и обе сборки перед сообщением о завершении.
