# Recording Info Button Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Добавить кнопку «i» в каждое окно записи, открывающую окно с вычисленной статистикой T1 и всеми метаданными из `.dat`-файла.

**Architecture:** Новый use case `GetRecordingInfoUseCase` в Application-слое принимает `TestData` + `sourceRoot`, вычисляет минимум T1 и скорость падения, возвращает `RecordingInfoResult`. Новая `RecordingInfoForm` в UI-слое отображает результат. `MainForm` добавляет кнопку «i» в верхнюю панель каждого окна записи и управляет жизненным циклом окна метаданных.

**Tech Stack:** C# .NET Framework 4.8, WinForms, MSTest 3.5.2

---

## Файлы

| Действие | Файл | Что делает |
|----------|------|-----------|
| Create | `Application/Recording/RecordingInfoResult.cs` | DTO с результатом: T1-статистика + мета |
| Create | `Application/Recording/GetRecordingInfoUseCase.cs` | Вычисляет статистику T1, берёт мета из TestData |
| Create | `JSQViewer.Tests/GetRecordingInfoUseCaseTests.cs` | Unit-тесты use case |
| Create | `UI/RecordingInfoForm.cs` | WinForms Form с таблицей статистики и мета |
| Modify | `UI/MainForm.cs:4174–4198` | Добавить `InfoForm` в `SourceWindowState` |
| Modify | `UI/MainForm.cs:2121–2261` | Кнопка «i», ширина окна, обработчик клика |
| Modify | `UI/MainForm.cs:130–218` | Поле `_getRecordingInfoUseCase`, инициализация в конструкторе |

---

## Task 1: RecordingInfoResult — DTO результата

**Files:**
- Create: `Application/Recording/RecordingInfoResult.cs`

- [ ] **Step 1: Создать файл**

```csharp
// Application/Recording/RecordingInfoResult.cs
using System;
using System.Collections.Generic;

namespace JSQViewer.Application.Recording
{
    public sealed class RecordingInfoResult
    {
        public string SourceRoot { get; set; }

        // null если канал T1 не найден в данном источнике
        public double? T1Min { get; set; }
        public DateTime? T1MinTime { get; set; }
        public double? T1DropRatePerMinute { get; set; }

        // Все пары ключ-значение из .dat, в порядке перечисления Meta
        public IReadOnlyList<KeyValuePair<string, string>> Meta { get; set; }
    }
}
```

- [ ] **Step 2: Убедиться что проект компилируется**

```
dotnet build JSQViewer.csproj -c Debug 2>&1 | tail -5
```
Ожидание: `Сборка успешно завершена. Предупреждений: 0, Ошибок: 0`

- [ ] **Step 3: Коммит**

```
git add Application/Recording/RecordingInfoResult.cs
git commit -m "feat: add RecordingInfoResult DTO"
```

---

## Task 2: GetRecordingInfoUseCase — вычисление статистики T1

**Files:**
- Create: `Application/Recording/GetRecordingInfoUseCase.cs`
- Create: `JSQViewer.Tests/GetRecordingInfoUseCaseTests.cs`

- [ ] **Step 1: Написать тесты (все сразу, до реализации)**

```csharp
// JSQViewer.Tests/GetRecordingInfoUseCaseTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Recording;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class GetRecordingInfoUseCaseTests
    {
        private static readonly string Root = @"C:\Data\Test";

        // Создаёт TestData с одним источником и одним каналом T1
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

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsCorrectMin()
        {
            // startMs = 0, endMs = 60000 (1 минута), 4 значения
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { -20.0, -35.0, -38.4, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1Min);
            Assert.AreEqual(-38.4, r.T1Min.Value, 0.001);
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsCorrectMinTime()
        {
            // timestamps[2] соответствует минимуму -38.4
            long startMs = 1_000_000_000L;
            long endMs   = 1_000_060_000L;
            var data = MakeData(Root, startMs, endMs, "T1",
                new double?[] { -20.0, -35.0, -38.4, -30.0 });
            var ts = new TimestampRangeService();
            var uc = new GetRecordingInfoUseCase(ts);

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1MinTime);
            // timestamps[2] = startMs + 2*(60000/3) = startMs + 40000
            DateTime expected = ts.UnixMsToLocalDateTime(startMs + 40_000);
            Assert.AreEqual(expected, r.T1MinTime.Value);
        }

        [TestMethod]
        public void Execute_WithT1Channel_ReturnsDropRate()
        {
            // first=-20, min=-38.4, duration=1 мин → rate = (-38.4 - (-20)) / 1 = -18.4
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { -20.0, -35.0, -38.4, -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.T1DropRatePerMinute);
            Assert.AreEqual(-18.4, r.T1DropRatePerMinute.Value, 0.01);
        }

        [TestMethod]
        public void Execute_WithPrefixedT1_FindsChannel()
        {
            // Канал называется A-T1 (с префиксом источника)
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
        public void Execute_ReturnsMeta()
        {
            var data = MakeData(Root, 0, 60_000, "T1",
                new double?[] { -10.0, -20.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNotNull(r.Meta);
            Assert.IsTrue(r.Meta.Any(kv => kv.Key == "Модель" && kv.Value == "KA140"));
            Assert.IsTrue(r.Meta.Any(kv => kv.Key == "Хладагент" && kv.Value == "R600a"));
        }

        [TestMethod]
        public void Execute_SingleRow_DropRateIsNull()
        {
            // Один ряд → длительность 0 → скорость не определена
            var data = MakeData(Root, 0, 0, "T1",
                new double?[] { -30.0 });
            var uc = new GetRecordingInfoUseCase(new TimestampRangeService());

            RecordingInfoResult r = uc.Execute(data, Root);

            Assert.IsNull(r.T1DropRatePerMinute);
        }
    }
}
```

- [ ] **Step 2: Убедиться что тесты не компилируются (GetRecordingInfoUseCase не существует)**

```
dotnet test "JSQViewer.Tests\JSQViewer.Tests.csproj" --verbosity quiet 2>&1 | tail -8
```
Ожидание: ошибка компиляции `error CS0246: Тип или пространство имён "GetRecordingInfoUseCase" не найдено`

- [ ] **Step 3: Реализовать use case**

```csharp
// Application/Recording/GetRecordingInfoUseCase.cs
using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Charting;
using JSQViewer.Core;

namespace JSQViewer.Application.Recording
{
    public sealed class GetRecordingInfoUseCase
    {
        private readonly TimestampRangeService _timestampRangeService;

        public GetRecordingInfoUseCase(TimestampRangeService timestampRangeService)
        {
            if (timestampRangeService == null)
                throw new ArgumentNullException(nameof(timestampRangeService));
            _timestampRangeService = timestampRangeService;
        }

        public RecordingInfoResult Execute(TestData data, string sourceRoot)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (sourceRoot == null) throw new ArgumentNullException(nameof(sourceRoot));

            var result = new RecordingInfoResult
            {
                SourceRoot = sourceRoot,
                Meta = data.Meta != null
                    ? data.Meta.ToList()
                    : new List<KeyValuePair<string, string>>()
            };

            string t1Column = FindT1Column(data, sourceRoot);
            if (t1Column == null)
                return result;

            double?[] values;
            if (!data.Columns.TryGetValue(t1Column, out values) || values == null)
                return result;

            long startMs, endMs;
            if (!data.SourceStartMs.TryGetValue(sourceRoot, out startMs))
                startMs = data.TimestampsMs.Length > 0 ? data.TimestampsMs[0] : 0;
            if (!data.SourceEndMs.TryGetValue(sourceRoot, out endMs))
                endMs = data.TimestampsMs.Length > 0 ? data.TimestampsMs[data.TimestampsMs.Length - 1] : 0;

            var slice = _timestampRangeService.SliceByTime(data.TimestampsMs, startMs, endMs);
            int i0 = slice.Item1;
            int i1 = slice.Item2;
            if (i1 <= i0)
                return result;

            // Найти минимум и его индекс
            int minIdx = -1;
            double minVal = double.MaxValue;
            double? firstVal = null;
            for (int i = i0; i < i1; i++)
            {
                if (!values[i].HasValue) continue;
                if (firstVal == null) firstVal = values[i];
                if (values[i].Value < minVal)
                {
                    minVal = values[i].Value;
                    minIdx = i;
                }
            }

            if (minIdx < 0) return result;

            result.T1Min = minVal;
            result.T1MinTime = _timestampRangeService.UnixMsToLocalDateTime(
                data.TimestampsMs[minIdx]);

            double durationMin = (endMs - startMs) / 60_000.0;
            if (durationMin > 0 && firstVal.HasValue)
                result.T1DropRatePerMinute = (minVal - firstVal.Value) / durationMin;

            return result;
        }

        // Ищет в sourceColumns источника колонку, являющуюся T1
        // Поддерживает имена: T1, A-T1, B-T1, C-T1 (однобуквенный префикс)
        private static string FindT1Column(TestData data, string sourceRoot)
        {
            string[] cols;
            if (!data.SourceColumns.TryGetValue(sourceRoot, out cols) || cols == null)
                return null;

            foreach (string col in cols)
            {
                if (col == null) continue;
                if (string.Equals(col, "T1", StringComparison.OrdinalIgnoreCase))
                    return col;
                // Формат: X-T1, где X — одна буква
                if (col.Length >= 4 && col[1] == '-' &&
                    string.Equals(col.Substring(2), "T1", StringComparison.OrdinalIgnoreCase))
                    return col;
            }
            return null;
        }
    }
}
```

- [ ] **Step 4: Запустить тесты use case**

```
dotnet test "JSQViewer.Tests\JSQViewer.Tests.csproj" --filter "GetRecordingInfoUseCaseTests" --verbosity normal 2>&1 | tail -15
```
Ожидание: все 6 тестов PASS.

- [ ] **Step 5: Запустить весь набор**

```
dotnet test "JSQViewer.Tests\JSQViewer.Tests.csproj" --verbosity quiet 2>&1 | tail -4
```
Ожидание: 127/127 PASS (121 старых + 6 новых).

- [ ] **Step 6: Коммит**

```
git add Application/Recording/GetRecordingInfoUseCase.cs JSQViewer.Tests/GetRecordingInfoUseCaseTests.cs
git commit -m "feat: add GetRecordingInfoUseCase with T1 stats and meta"
```

---

## Task 3: RecordingInfoForm — окно метаданных

**Files:**
- Create: `UI/RecordingInfoForm.cs`

- [ ] **Step 1: Создать форму**

```csharp
// UI/RecordingInfoForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using JSQViewer.Application.Recording;

namespace JSQViewer.UI
{
    public sealed class RecordingInfoForm : Form
    {
        public RecordingInfoForm(RecordingInfoResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            Text = result.SourceRoot ?? string.Empty;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            StartPosition = FormStartPosition.Manual;
            Font = new Font("Microsoft Sans Serif", 9f);
            Padding = new Padding(10);

            var table = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            int row = 0;

            // --- Секция T1 ---
            AddHeader(table, "ТЕМПЕРАТУРА T1", ref row, topPad: 0);

            if (result.T1Min.HasValue)
            {
                AddRow(table, "Минимум",
                    result.T1Min.Value.ToString("F1") + " °C", ref row);
                AddRow(table, "Время минимума",
                    result.T1MinTime.HasValue
                        ? result.T1MinTime.Value.ToString("dd.MM.yy HH:mm:ss")
                        : "—", ref row);
                string rate = result.T1DropRatePerMinute.HasValue
                    ? result.T1DropRatePerMinute.Value.ToString("F2") + " °C/мин"
                    : "—";
                AddRow(table, "Скорость падения", rate, ref row);
            }
            else
            {
                AddRow(table, "T1 не найден", "—", ref row);
            }

            // --- Секция метаданных ---
            if (result.Meta != null && result.Meta.Count > 0)
            {
                AddHeader(table, "МЕТАДАННЫЕ", ref row, topPad: 8);
                foreach (KeyValuePair<string, string> kv in result.Meta)
                    AddRow(table, kv.Key, kv.Value ?? string.Empty, ref row);
            }

            Controls.Add(table);
        }

        private static void AddHeader(TableLayoutPanel table, string text,
            ref int row, int topPad)
        {
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold),
                ForeColor = SystemColors.GrayText,
                AutoSize = true,
                Padding = new Padding(0, topPad, 0, 2),
                Margin = new Padding(0)
            };
            table.Controls.Add(lbl, 0, row);
            table.SetColumnSpan(lbl, 2);
            row++;
        }

        private static void AddRow(TableLayoutPanel table, string key, string value,
            ref int row)
        {
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var keyLbl = new Label
            {
                Text = key,
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(0, 1, 20, 1),
                Margin = new Padding(0)
            };
            var valLbl = new Label
            {
                Text = value,
                AutoSize = true,
                Padding = new Padding(0, 1, 0, 1),
                Margin = new Padding(0)
            };
            table.Controls.Add(keyLbl, 0, row);
            table.Controls.Add(valLbl, 1, row);
            row++;
        }
    }
}
```

- [ ] **Step 2: Скомпилировать**

```
dotnet build JSQViewer.csproj -c Debug 2>&1 | tail -5
```
Ожидание: `Сборка успешно завершена. Предупреждений: 0, Ошибок: 0`

- [ ] **Step 3: Коммит**

```
git add UI/RecordingInfoForm.cs
git commit -m "feat: add RecordingInfoForm"
```

---

## Task 4: Подключение в MainForm — кнопка «i», поле, ширина окна

**Files:**
- Modify: `UI/MainForm.cs`

Читай файл перед каждым изменением. Изменений несколько — делай по одному.

### 4A: Добавить поле `_getRecordingInfoUseCase` и `using`

- [ ] **Step 1: Добавить using в начало файла**

В `UI/MainForm.cs` найти блок using (~строка 1). После строки:
```csharp
using JSQViewer.Application.Workspace.UseCases;
```
Добавить:
```csharp
using JSQViewer.Application.Recording;
```

- [ ] **Step 2: Добавить поле в класс**

Найти строку (~144):
```csharp
        private readonly LoadWorkspaceDataUseCase _loadWorkspaceDataUseCase;
```
После неё добавить:
```csharp
        private readonly GetRecordingInfoUseCase _getRecordingInfoUseCase;
```

- [ ] **Step 3: Инициализировать поле в конструкторе**

Найти строку (~218):
```csharp
            _removeLoadedSourceUseCase = new RemoveLoadedSourceUseCase();
```
После неё добавить:
```csharp
            _getRecordingInfoUseCase = new GetRecordingInfoUseCase(_timestampRangeService);
```

### 4B: Добавить `InfoForm` в `SourceWindowState`

- [ ] **Step 4: Расширить `SourceWindowState`**

Найти класс `SourceWindowState` (~строка 4174). Найти последнее поле:
```csharp
            public List<ChannelItem> Items { get; set; }
```
После него добавить:
```csharp
            public Form InfoForm { get; set; }
```

### 4C: Добавить кнопку «i» и увеличить ширину окна записи

- [ ] **Step 5: Увеличить ширину формы**

Найти строку (~2123):
```csharp
                form.Width = 560;
```
Заменить на:
```csharp
                form.Width = 610;
```

- [ ] **Step 6: Добавить кнопку «i» после кнопки Clear**

Найти блок (~2158–2161):
```csharp
                var clear = new Button();
                clear.Text = Loc.Get("Clear");
                clear.AutoSize = true;
                top.Controls.Add(clear);
```
После него добавить:
```csharp
                var infoButton = new Button();
                infoButton.Text = "i";
                infoButton.Width = 24;
                infoButton.Font = new Font(top.Font, FontStyle.Bold | FontStyle.Italic);
                top.Controls.Add(infoButton);
```

- [ ] **Step 7: Добавить обработчик клика**

Найти блок (~2215–2227) с обработчиками событий (строки вида `filterBox.TextChanged += ...`).
После строки:
```csharp
                clear.Click += delegate { ClearAllInSource(state); };
```
Добавить:
```csharp
                infoButton.Click += delegate
                {
                    if (state.InfoForm != null && !state.InfoForm.IsDisposed)
                    {
                        state.InfoForm.Close();
                        state.InfoForm = null;
                        return;
                    }
                    TestData data = _viewerSession.Data;
                    if (data == null) return;
                    RecordingInfoResult info = _getRecordingInfoUseCase.Execute(data, state.SourceRoot);
                    var infoForm = new RecordingInfoForm(info);
                    state.InfoForm = infoForm;
                    infoForm.FormClosed += delegate { state.InfoForm = null; };
                    // Позиционировать окно справа от окна записи
                    infoForm.Location = new Point(
                        state.Form.Right + 8,
                        state.Form.Top);
                    infoForm.Show(this);
                };
```

- [ ] **Step 8: Закрывать InfoForm при закрытии окна записи**

Найти `form.FormClosed += delegate` (~строка 2232). Внутри обработчика, в самом конце (перед или после `_sourceWindows.Remove(sourceRoot);`), добавить:
```csharp
                    if (state.InfoForm != null && !state.InfoForm.IsDisposed)
                    {
                        state.InfoForm.Close();
                        state.InfoForm = null;
                    }
```

- [ ] **Step 9: Скомпилировать**

```
dotnet build JSQViewer.csproj -c Debug 2>&1 | tail -5
```
Ожидание: `Сборка успешно завершена. Предупреждений: 0, Ошибок: 0`

- [ ] **Step 10: Запустить тесты**

```
dotnet test "JSQViewer.Tests\JSQViewer.Tests.csproj" --verbosity quiet 2>&1 | tail -4
```
Ожидание: все 127 тестов PASS.

- [ ] **Step 11: Коммит**

```
git add UI/MainForm.cs
git commit -m "feat: add info button to source windows with recording stats popup"
```

---

## Итоговый прогон

```
dotnet build JSQViewer.csproj -c Release 2>&1 | tail -5
dotnet test "JSQViewer.Tests\JSQViewer.Tests.csproj" --verbosity quiet 2>&1 | tail -4
```

Ожидание: Release-сборка без ошибок, все 127 тестов PASS.
