# Recording Info Button — Design Spec

## Goal

Add an «i» button to each source window that opens a metadata window showing computed T1 statistics and all key-value pairs from the recording's `.dat` file.

## Architecture

A new `GetRecordingInfoUseCase` in the Application layer computes T1 stats from `TestData` and returns all `.dat` metadata. `MainForm` adds the «i» button to each source window's filter panel, calls the use case on click, and displays a new `RecordingInfoForm`. The source window minimum width is increased to fit all controls without overlap.

**Tech Stack:** C# .NET Framework 4.8, WinForms, MSTest

---

## Components

### 1. `Application/Recording/RecordingInfoResult.cs` (new)

Result object returned by the use case:

```csharp
public sealed class RecordingInfoResult
{
    public string SourceRoot { get; set; }

    // T1 stats — null if T1 channel not found
    public double? T1Min { get; set; }
    public DateTime? T1MinTime { get; set; }
    public double? T1DropRatePerMinute { get; set; }

    // All key-value pairs from the .dat file, in file order
    public IReadOnlyList<KeyValuePair<string, string>> Meta { get; set; }
}
```

### 2. `Application/Recording/GetRecordingInfoUseCase.cs` (new)

```csharp
public sealed class GetRecordingInfoUseCase
{
    public RecordingInfoResult Execute(TestData data, string sourceRoot)
}
```

**T1 channel resolution** (in priority order):
- Look in `data.SourceColumns[sourceRoot]` for a column whose name, after stripping prefix (`A-`, `B-`, `C-`), equals `"T1"` (OrdinalIgnoreCase)
- Fallback: look in `data.ColumnNames` directly for `T1`, `A-T1`, `B-T1`, `C-T1`
- If not found: `T1Min = null`, `T1MinTime = null`, `T1DropRatePerMinute = null`

**T1 stats computation:**
- Rows for this source: indices where `TimestampsMs` falls within `data.SourceStartMs[sourceRoot]..data.SourceEndMs[sourceRoot]` (inclusive)
- `T1Min` = minimum non-null value in `data.Columns[t1Name][sourceStart..sourceEnd]`
- `T1MinTime` = `UnixMsToLocalDateTime(TimestampsMs[indexOfMin])`
- `T1DropRatePerMinute` = `(T1Min - firstNonNullValue) / totalDurationMinutes`
  - `firstNonNullValue` = first non-null value in the source range
  - `totalDurationMinutes` = `(SourceEndMs - SourceStartMs) / 60000.0`
  - If duration ≤ 0 or first value unavailable: `null`

**Meta:** directly return `data.Meta` as ordered list of `KeyValuePair<string,string>`. `TestData.Meta` is already populated by `ProvaMetadataReader` during load — no additional file I/O needed.

**Timestamp conversion:** use existing `TimestampRangeService.UnixMsToLocalDateTime(long)`.

### 3. `UI/RecordingInfoForm.cs` (new)

WinForms `Form` (not modal):

- `FormBorderStyle = FixedSingle`
- `MaximizeBox = false`, `MinimizeBox = false`
- Title: path of source root (truncated to last two path components if too long)
- Layout: `TableLayoutPanel` with 2 columns (label / value), auto-row height

**Structure:**
```
┌─────────────────────────────────────┐
│ C:\Data\Test_001               [✕]  │
├─────────────────────────────────────┤
│  ТЕМПЕРАТУРА T1                     │
│  Минимум          │  -38.4 °C       │
│  Время минимума   │  14.03.26 02:14 │
│  Скорость падения │  -0.47 °C/мин   │
├─────────────────────────────────────┤
│  МЕТАДАННЫЕ                         │
│  <Key1>           │  <Value1>       │
│  <Key2>           │  <Value2>       │
│  ...              │  ...            │
├─────────────────────────────────────┤
│              [ Закрыть ]            │
└─────────────────────────────────────┘
```

- Section headers («ТЕМПЕРАТУРА T1», «МЕТАДАННЫЕ»): `Label` spanning both columns, bold, uppercase, small font
- If T1 not found: show single row «T1 не найден» in place of the three stats rows
- Values formatted:
  - Temperature: `F1` format + ` °C`
  - DateTime: `dd.MM.yy HH:mm:ss`
  - Rate: `F2` format + ` °C/мин`; prefix `−` for negative
- «Закрыть» button at bottom, `DialogResult = None`, calls `Close()`
- Window height: calculated from row count (no scrolling — all rows visible)

### 4. `UI/MainForm.cs` — changes

**A. Source window minimum width fix**

In `RefreshSourceChannels` (~line 2121), increase `form.MinimumSize` / initial `Width` so all controls in the top panel fit without overlap. Current controls: FilterBox + SortModeBox + SelectedOnlyCheck + SelectAllButton + ClearButton + new InfoButton. Calculate required width from sum of control widths + gaps + margins.

**B. «i» button in filter panel**

In the top panel construction loop (~lines 2129–2161), add after the last existing button:

```csharp
var infoButton = new Button
{
    Text = "i",
    Width = 24,
    Font = new Font(panel.Font, FontStyle.Bold | FontStyle.Italic),
    // style consistent with existing panel buttons
};
```

**C. Click handler**

```csharp
infoButton.Click += (s, e) =>
{
    // Close existing info window for this source if open
    state.InfoForm?.Close();
    state.InfoForm = null;

    var result = _getRecordingInfoUseCase.Execute(
        _viewerSession.Data, state.SourceRoot);

    var form = new RecordingInfoForm(result);
    state.InfoForm = form;
    form.FormClosed += (_, __) => state.InfoForm = null;
    form.Show(this);
};
```

**D. `SourceWindowState` additions**

Add `InfoForm` field to `SourceWindowState`:
```csharp
public Form InfoForm { get; set; }
```

Close `InfoForm` when the source window is closed:
```csharp
form.FormClosed += (s, e) =>
{
    state.InfoForm?.Close();
    // existing cleanup...
};
```

### 5. `GetRecordingInfoUseCase` wiring

Instantiate `GetRecordingInfoUseCase` in `Presentation/WinForms/Composition/` (or wherever `MainForm` is composed) and inject into `MainForm` via constructor or property.

---

## Tests

**File:** `JSQViewer.Tests/GetRecordingInfoUseCaseTests.cs`

| Test | What it verifies |
|------|-----------------|
| `Execute_WithT1Channel_ReturnsMinAndTime` | Correct minimum value and timestamp index |
| `Execute_WithT1Channel_ReturnsDropRate` | Rate = (min - first) / duration correctly |
| `Execute_WithPrefixedT1_FindsChannel` | `A-T1` resolved as T1 channel |
| `Execute_WithNoT1Channel_ReturnsNullStats` | All three T1 fields null |
| `Execute_ReturnsMeta_InOrder` | Meta list matches TestData.Meta key order |
| `Execute_SingleRow_DropRateIsNull` | Duration ≤ 0 → rate is null |

---

## Out of Scope

- Exporting info window content
- Showing stats for channels other than T1
- Filtering or searching metadata keys
- Persisting window position between sessions
