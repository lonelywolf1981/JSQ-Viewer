# Дефолтный порядок каналов по логике протокола

**Дата:** 2026-04-24  
**Статус:** Утверждён  

## Цель

При открытии записи каналы по умолчанию отображаются в том же порядке, что и столбцы в протоколе экспорта — без каких-либо действий со стороны пользователя. Применяется к главному списку и ко всем окнам источников.

## Контекст

Сейчас дефолтный порядок берётся из `channel_order.json` (legacy), а при его отсутствии — из `data.ColumnNames` (порядок из DBF/CSV). Порядок столбцов в протоколе (`TemplateExporter.KeyToColumn` + `NaturalDisplayComparer`) при этом не учитывается.

## Архитектура

Два изолированных изменения:

```
Application/Channels/ProtocolChannelOrder.cs   ← новый класс
UI/MainForm.cs                                  ← 1 место изменений в BindLoadedData
```

### Поток данных

```
BindLoadedData(data)
  → LoadSavedOrder()  →  пусто?
      → да: ProtocolChannelOrder.Build(data.ColumnNames, data.Channels)
      → нет: использовать сохранённый порядок как раньше
  → _channelWorkspacePresenter.BindData(data, initialOrder, ...)
  → ChannelWorkspaceModel.Load → ApplySavedOrder → _channels в протокольном порядке
  → _sourceOrders строятся из _channels → все окна источников тоже в протокольном порядке
```

## Новый класс: `ProtocolChannelOrder`

**Файл:** `Application/Channels/ProtocolChannelOrder.cs`  
**Namespace:** `JSQViewer.Application.Channels`

```csharp
public static class ProtocolChannelOrder
{
    public static List<string> Build(string[] cols, Dictionary<string, ChannelInfo> channels)
}
```

### Алгоритм Build

**Шаг 1 — Фиксированные ключи** (в этом порядке):

`Pc → Pe → T-sie → UR-sie → Tc → Te → T1 → T2 → T3 → T4 → T5 → T6 → T7 → I → F → V → W`

Для каждого ключа выбирается один канал из `cols`:
- Точное совпадение кода с ключом (case-insensitive)
- Затем совпадение по суффиксу: код заканчивается на `-{key}` (напр. `A-Pc`, `C-Pc`)
- Приоритет среди кандидатов: `A-` > `C-` > прочие

Если совпадений нет — ключ пропускается. Один канал не может попасть в список дважды.

**Шаг 2 — Остальные каналы** (не вошедшие в шаг 1):
Натуральная сортировка по display name: `ChannelInfo.Name` → `ChannelInfo.Label` → код.

**Результат:** `[fixed_matched...] + [sorted_extras...]` — полный список всех каналов из `cols`.

## Изменение MainForm

**Файл:** `UI/MainForm.cs`, метод `BindLoadedData`

```csharp
// Было:
SourceWindowRefreshPlan refreshPlan = _channelWorkspacePresenter.BindData(
    data, LoadSavedOrder(), preferredCheckedCodes, preserveSourceWindowsLayout);

// Стало:
List<string> initialOrder = LoadSavedOrder();
if (initialOrder.Count == 0 && data != null)
{
    initialOrder = ProtocolChannelOrder.Build(data.ColumnNames, data.Channels);
}
SourceWindowRefreshPlan refreshPlan = _channelWorkspacePresenter.BindData(
    data, initialOrder, preferredCheckedCodes, preserveSourceWindowsLayout);
```

## Граничные случаи

| Ситуация | Поведение |
|---|---|
| `channel_order.json` существует | Протокольный порядок не применяется |
| Workspace layout содержит `MainSelectedOrderKey` / `SourceSelectedOrderKeys` | `ApplyWorkspaceLayoutSelections()` применяет их поверх (существующая логика) |
| Ни один канал не совпал с фиксированными ключами | Все каналы попадают в «остальные» и сортируются по display name |
| `data.Channels` равен null | `Build` работает с пустым словарём, сортировка по коду |
| Multi-source (несколько папок) | Все окна источников получают протокольный порядок автоматически через `_sourceOrders` |

## Что НЕ меняется

- Логика `TemplateExporter` — без изменений
- `ChannelWorkspaceModel.ApplySavedOrder` — без изменений
- Сохранённые пользовательские порядки и workspace layout — имеют приоритет над дефолтом
- Режимы сортировки в UI — без изменений
