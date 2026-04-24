# Protocol Default Channel Order — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** При открытии записи каналы по умолчанию отображаются в порядке столбцов протокола (фиксированные ключи → остальные по натуральной сортировке) без каких-либо действий пользователя.

**Architecture:** Новый класс `ProtocolChannelOrder.Build()` вычисляет порядок каналов по алгоритму экспортёра. `MainForm.BindLoadedData` вызывает его как fallback, когда `LoadSavedOrder()` возвращает пустой список.

**Tech Stack:** C# / .NET 4.8, MSTest, проект `JSQViewer.Tests`

---

## File Map

| Действие | Файл |
|---|---|
| Создать | `Application/Channels/ProtocolChannelOrder.cs` |
| Создать | `JSQViewer.Tests/ProtocolChannelOrderTests.cs` |
| Изменить | `UI/MainForm.cs` (метод `BindLoadedData`, ~строка 1834) |

---

## Task 1: Failing tests для `ProtocolChannelOrder`

**Files:**
- Create: `JSQViewer.Tests/ProtocolChannelOrderTests.cs`

- [ ] **Шаг 1: Написать файл с тестами**

Создать `JSQViewer.Tests/ProtocolChannelOrderTests.cs`:

```csharp
using System.Collections.Generic;
using JSQViewer.Application.Channels;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ProtocolChannelOrderTests
    {
        // ── null / empty ──────────────────────────────────────────────────

        [TestMethod]
        public void Build_NullCols_ReturnsEmpty()
        {
            var result = ProtocolChannelOrder.Build(null, null);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void Build_EmptyCols_ReturnsEmpty()
        {
            var result = ProtocolChannelOrder.Build(new string[0], null);
            Assert.AreEqual(0, result.Count);
        }

        // ── fixed keys ────────────────────────────────────────────────────

        [TestMethod]
        public void Build_PlacesFixedKeysFirst_InDefinedOrder()
        {
            // W, Pc, T1, F — все фиксированные, порядок должен быть Pc, T1, F, W
            string[] cols = new[] { "W", "Pc", "T1", "F" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("Pc", result[0]);
            Assert.AreEqual("T1", result[1]);
            Assert.AreEqual("F",  result[2]);
            Assert.AreEqual("W",  result[3]);
        }

        [TestMethod]
        public void Build_MissingFixedKey_IsSkipped()
        {
            // Только T1 из фиксированных
            string[] cols = new[] { "T1", "X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("T1", result[0]);
            Assert.AreEqual("X1", result[1]);
        }

        // ── suffix resolution ─────────────────────────────────────────────

        [TestMethod]
        public void Build_SuffixMatch_APrefixWinsOverCPrefix()
        {
            // A-Pc и C-Pc — оба совпадают с ключом "Pc", A- должен быть выбран
            string[] cols = new[] { "C-Pc", "A-Pc", "X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual("A-Pc", result[0]); // выбран для Pc
            // C-Pc и X1 попадают в extras
            CollectionAssert.Contains(result, "C-Pc");
            CollectionAssert.Contains(result, "X1");
        }

        [TestMethod]
        public void Build_SuffixMatch_CPrefixWinsOverOthers()
        {
            // C-Pc и Z-Pc — C- должен быть выбран (A- отсутствует)
            string[] cols = new[] { "Z-Pc", "C-Pc", "X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual("C-Pc", result[0]);
        }

        [TestMethod]
        public void Build_APrefixWinsEvenOverExactMatch()
        {
            // "A-Pc" — суффиксное совпадение с A-префиксом;
            // "Pc"   — точное совпадение без префикса.
            // A- имеет наивысший приоритет → выбирается A-Pc, Pc уходит в extras.
            string[] cols = new[] { "A-Pc", "Pc" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual("A-Pc", result[0]); // A- выигрывает
            Assert.AreEqual("Pc",   result[1]); // Pc → extras
        }

        // ── extras sorting ────────────────────────────────────────────────

        [TestMethod]
        public void Build_SortsExtrasByChannelName()
        {
            string[] cols = new[] { "Z-sensor", "A-sensor" };
            var channels = new Dictionary<string, ChannelInfo>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["Z-sensor"] = new ChannelInfo { Code = "Z-sensor", Name = "Zebra" },
                ["A-sensor"] = new ChannelInfo { Code = "A-sensor", Name = "Alpha" }
            };
            var result = ProtocolChannelOrder.Build(cols, channels);
            Assert.AreEqual("A-sensor", result[0]); // "Alpha" < "Zebra"
            Assert.AreEqual("Z-sensor", result[1]);
        }

        [TestMethod]
        public void Build_SortsExtrasByCodeWhenNoChannelName()
        {
            // Нет ChannelInfo — сортировка по коду
            string[] cols = new[] { "X3", "X1", "X2" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual("X1", result[0]);
            Assert.AreEqual("X2", result[1]);
            Assert.AreEqual("X3", result[2]);
        }

        [TestMethod]
        public void Build_NaturalSortForExtras()
        {
            // Натуральная сортировка: X2 < X10
            string[] cols = new[] { "X10", "X2", "X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual("X1",  result[0]);
            Assert.AreEqual("X2",  result[1]);
            Assert.AreEqual("X10", result[2]);
        }

        // ── combined ──────────────────────────────────────────────────────

        [TestMethod]
        public void Build_FullScenario_FixedFirstThenSortedExtras()
        {
            string[] cols = new[] { "X2", "A-Pc", "T1", "X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            // Pc (mapped from A-Pc) first, T1 second, then extras X1, X2
            Assert.AreEqual("A-Pc", result[0]);
            Assert.AreEqual("T1",   result[1]);
            Assert.AreEqual("X1",   result[2]);
            Assert.AreEqual("X2",   result[3]);
        }

        [TestMethod]
        public void Build_AllColsIncluded_NoLostChannels()
        {
            string[] cols = new[] { "Pc", "T1", "Extra1", "Extra2" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual(cols.Length, result.Count);
        }
    }
}
```

- [ ] **Шаг 2: Убедиться что тесты не компилируются (класс не существует)**

```
dotnet build JSQViewer.Tests
```

Ожидаемо: ошибка компиляции — `ProtocolChannelOrder` не найден.

---

## Task 2: Реализация `ProtocolChannelOrder`

**Files:**
- Create: `Application/Channels/ProtocolChannelOrder.cs`

- [ ] **Шаг 1: Создать файл реализации**

Создать `Application/Channels/ProtocolChannelOrder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JSQViewer.Core;

namespace JSQViewer.Application.Channels
{
    public static class ProtocolChannelOrder
    {
        private static readonly Regex NaturalSplitRegex = new Regex("(\\d+)", RegexOptions.Compiled);

        private static readonly string[] FixedKeys = new[]
        {
            "Pc", "Pe", "T-sie", "UR-sie", "Tc", "Te",
            "T1", "T2", "T3", "T4", "T5", "T6", "T7",
            "I", "F", "V", "W"
        };

        public static List<string> Build(string[] cols, Dictionary<string, ChannelInfo> channels)
        {
            if (cols == null || cols.Length == 0)
                return new List<string>();

            var channelMap = channels ?? new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>(cols.Length);

            foreach (string key in FixedKeys)
            {
                string matched = ResolveKey(key, cols, used);
                if (!string.IsNullOrEmpty(matched))
                {
                    result.Add(matched);
                    used.Add(matched);
                }
            }

            var extras = cols
                .Where(c => !string.IsNullOrWhiteSpace(c) && !used.Contains(c))
                .OrderBy(c => GetDisplayName(c, channelMap), new NaturalComparer())
                .ThenBy(c => c, new NaturalComparer())
                .ToList();

            result.AddRange(extras);
            return result;
        }

        private static string ResolveKey(string key, string[] cols, HashSet<string> used)
        {
            var exact = new List<string>();
            var suffix = new List<string>();
            string suf = "-" + key;

            foreach (string c in cols)
            {
                if (used.Contains(c)) continue;
                if (string.Equals(c, key, StringComparison.OrdinalIgnoreCase))
                    exact.Add(c);
                else if (c.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                    suffix.Add(c);
            }

            var candidates = exact.Concat(suffix).ToList();
            if (candidates.Count == 0) return string.Empty;

            foreach (string pref in new[] { "A-", "C-" })
            {
                string byPref = candidates.FirstOrDefault(
                    c => c.StartsWith(pref, StringComparison.OrdinalIgnoreCase));
                if (byPref != null) return byPref;
            }

            return candidates[0];
        }

        private static string GetDisplayName(string code, Dictionary<string, ChannelInfo> channels)
        {
            ChannelInfo ch;
            if (channels.TryGetValue(code, out ch))
            {
                if (!string.IsNullOrWhiteSpace(ch.Name)) return ch.Name.Trim();
                if (!string.IsNullOrWhiteSpace(ch.Label)) return ch.Label.Trim();
            }
            return code ?? string.Empty;
        }

        private sealed class NaturalComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                string[] a = NaturalSplitRegex.Split(x ?? string.Empty);
                string[] b = NaturalSplitRegex.Split(y ?? string.Empty);
                int count = Math.Max(a.Length, b.Length);
                for (int i = 0; i < count; i++)
                {
                    if (i >= a.Length) return -1;
                    if (i >= b.Length) return 1;
                    int ai, bi;
                    bool aIsNum = int.TryParse(a[i], out ai);
                    bool bIsNum = int.TryParse(b[i], out bi);
                    int cmp = (aIsNum && bIsNum)
                        ? ai.CompareTo(bi)
                        : string.Compare(a[i], b[i], StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return cmp;
                }
                return 0;
            }
        }
    }
}
```

- [ ] **Шаг 2: Запустить тесты**

```
dotnet test JSQViewer.Tests --filter "ProtocolChannelOrderTests"
```

Ожидаемо: все тесты зелёные.

- [ ] **Шаг 3: Коммит**

```bash
git add Application/Channels/ProtocolChannelOrder.cs JSQViewer.Tests/ProtocolChannelOrderTests.cs
git commit -m "feat: add ProtocolChannelOrder for protocol-based default channel ordering"
```

---

## Task 3: Подключить `ProtocolChannelOrder` в `MainForm`

**Files:**
- Modify: `UI/MainForm.cs` (~строка 1834, метод `BindLoadedData`)

- [ ] **Шаг 1: Изменить `BindLoadedData`**

Найти в `UI/MainForm.cs` блок (строки ~1834–1838):

```csharp
            SourceWindowRefreshPlan refreshPlan = _channelWorkspacePresenter.BindData(
                data,
                LoadSavedOrder(),
                preferredCheckedCodes,
                preserveSourceWindowsLayout);
```

Заменить на:

```csharp
            List<string> initialOrder = LoadSavedOrder();
            if (initialOrder.Count == 0 && data != null)
            {
                initialOrder = ProtocolChannelOrder.Build(data.ColumnNames, data.Channels);
            }

            SourceWindowRefreshPlan refreshPlan = _channelWorkspacePresenter.BindData(
                data,
                initialOrder,
                preferredCheckedCodes,
                preserveSourceWindowsLayout);
```

- [ ] **Шаг 2: Добавить using в начало файла (если нет)**

Убедиться что в `UI/MainForm.cs` есть строка:

```csharp
using JSQViewer.Application.Channels;
```

Проверить grep-ом:
```
grep -n "using JSQViewer.Application.Channels" UI/MainForm.cs
```

Если нет — добавить в блок using-ов вверху файла.

- [ ] **Шаг 3: Собрать проект**

```
dotnet build JSQViewer.csproj
```

Ожидаемо: 0 ошибок, 0 предупреждений.

- [ ] **Шаг 4: Запустить все тесты**

```
dotnet test JSQViewer.Tests
```

Ожидаемо: все 56+ тестов зелёные.

- [ ] **Шаг 5: Коммит**

```bash
git add UI/MainForm.cs
git commit -m "feat: use protocol column order as default when opening a record"
```

---

## Проверка после реализации

1. Открыть запись, у которой нет `channel_order.json`
2. Каналы `Pc`, `Pe`, `T-sie`, `UR-sie`, `Tc`, `Te`, `T1`…`T7`, `I`, `F`, `V`, `W` (и их `A-`/`C-` варианты) должны идти первыми в том же порядке
3. Остальные каналы — после, отсортированы натурально по имени
4. Открыть запись, у которой есть `channel_order.json` — порядок должен совпадать с сохранённым (протокольный порядок не применяется)
5. При загрузке нескольких папок одновременно — порядок в каждом окне источника совпадает с главным
