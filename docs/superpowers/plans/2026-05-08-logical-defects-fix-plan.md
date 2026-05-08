# Logical Defects Fix Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix confirmed logical defects in loading, chart state, recent folders, root detection, and export validation while keeping the disputed protocol-order behavior under explicit verification.

**Architecture:** Treat each defect as a narrow behavior change with a regression test first. Avoid broad UI rewrites: add small state guards/helpers around existing WinForms flows, and keep data-import fixes inside workspace/import use cases. Do not change protocol ordering until a failing reproduction contradicts the observed 0.1.7 behavior.

**Tech Stack:** .NET Framework 4.8, WinForms, MSTest, existing `JSQViewer.Tests` project.

---

## File Structure

- Modify: `UI/MainForm.cs`
  - Guard async load completion.
  - Suppress recent-folder selection events during programmatic updates.
  - Clear stale chart selection/range state in user-driven reset flows.
- Modify: `Infrastructure/DataImport/FileSystemTestRootLocator.cs`
  - Align DBF discovery with actual reader expectations.
  - Make recursive candidate selection deterministic.
- Modify: `Application/Workspace/UseCases/LoadWorkspaceDataUseCase.cs`
  - Deduplicate resolved roots and return resolved roots/spec consistently.
- Modify: `Application/Workspace/UseCases/MergeLoadedSourcesUseCase.cs`
  - Avoid duplicate-root dictionary crashes if duplicate roots still reach merge.
- Modify: `Export/TemplateExportValidator.cs`
  - Validate formula rows starting from the actual exported data start row.
- Modify: `Export/TemplateExporter.cs`
  - Apply A/C priority to normalized base codes for source-qualified channels.
- Test: `JSQViewer.Tests/WorkspaceLoadingTests.cs`
- Test: `JSQViewer.Tests/ChannelWorkspaceTests.cs`
- Test: `JSQViewer.Tests/ChartViewUseCaseTests.cs`
- Test: `JSQViewer.Tests/TemplateExporterSmokeTests.cs`
- Test: `JSQViewer.Tests/ExportWorkflowTests.cs`

---

### Task 1: Freeze Protocol-Order Decision With Evidence

**Files:**
- Test: `JSQViewer.Tests/ProtocolChannelOrderTests.cs`
- Read-only fixture/manual check: the six-record scenario used to validate 0.1.7.

- [ ] **Step 1: Record current evidence**

Document in the task notes:
- 0.1.7 manually shows protocol order for all 6 open records.
- Current unit tests fail on protocol-order expectations.
- Therefore this is a test/expectation conflict until reproduced against real data.

- [ ] **Step 2: Add or update a targeted test only if real data contradicts 0.1.7**

If a real folder set is found where order is wrong, add a minimal test in `ProtocolChannelOrderTests.cs` with exactly those source-qualified codes.

- [ ] **Step 3: Run targeted tests**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter ProtocolChannelOrderTests
```

Expected:
- If no real repro exists, do not change production protocol-order code.
- If a real repro exists, test must fail before any code changes.

---

### Task 2: Prevent Older Async Loads From Overwriting Newer Loads

**Files:**
- Modify: `UI/MainForm.cs`
- Test: add focused tests where feasible around a presenter/service extraction, or document manual verification if direct WinForms async testing is impractical.

- [ ] **Step 1: Introduce a load generation field**

Add a private integer field, for example:

```csharp
private int _loadGeneration;
```

- [ ] **Step 2: Increment generation at the start of `LoadFolder`**

At the beginning of `LoadFolder`, after input validation, capture:

```csharp
int generation = ++_loadGeneration;
```

- [ ] **Step 3: Ignore stale results after `await Task.Run(...)`**

Immediately after the await, before assigning `_currentWorkspaceKey`, `_folderBox.Text`, `_viewerSession`, or calling `BindLoadedData`, add:

```csharp
if (generation != _loadGeneration || IsDisposed)
{
    return;
}
```

- [ ] **Step 4: Make `finally` not clear busy state for stale loads**

Only reset cursor/busy if `generation == _loadGeneration`.

- [ ] **Step 5: Verify manually**

Manual scenario:
- Start loading a large/slow folder.
- Quickly load another folder.
- Expected: the second folder remains displayed even if the first load finishes later.

Run:

```powershell
dotnet build .\JSQViewer.csproj -c Debug
```

Expected: build succeeds.

---

### Task 3: Clear Stale Chart Channels When User Clears Selection

**Files:**
- Modify: `UI/MainForm.cs`
- Test: `JSQViewer.Tests/ChartViewUseCaseTests.cs` or a new presenter-level test if chart state can be isolated.

- [ ] **Step 1: Write failing regression test or document UI test gap**

Preferred behavior:
- After channels are cleared, redraw should not fall back to previous `_lastSelectedCodes`.

If this cannot be tested without WinForms private state, record manual verification steps in the PR notes.

- [ ] **Step 2: Clear `_lastSelectedCodes` in `ClearChannelsButtonOnClick`**

Update `ClearChannelsButtonOnClick`:

```csharp
_channelWorkspacePresenter.ClearAllChannels();
_lastSelectedCodes.Clear();
```

- [ ] **Step 3: Verify manually**

Manual scenario:
- Load data.
- Select channels and show chart.
- Press Clear or Escape.
- Expected: chart hides or becomes empty; selection info shows zero.

- [ ] **Step 4: Run tests**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter Chart
```

---

### Task 4: Stop Programmatic Recent-Folder Updates From Triggering Reloads

**Files:**
- Modify: `UI/MainForm.cs`
- Test: add a small state test if recent-folder behavior can be extracted; otherwise manual WinForms verification.

- [ ] **Step 1: Add suppression field**

```csharp
private bool _suppressRecentFolderSelectionChanged;
```

- [ ] **Step 2: Guard `RecentFoldersBoxOnSelectedIndexChanged`**

At the start:

```csharp
if (_suppressRecentFolderSelectionChanged)
{
    return;
}
```

- [ ] **Step 3: Wrap `LoadRecentFolders` and `AddRecentFolder` selection changes**

Use `try/finally` around `Items.Clear()`, item population, and `SelectedIndex = 0`.

- [ ] **Step 4: Verify manually**

Manual scenario:
- Load a folder with `addToRecent = true`.
- Expected: exactly one load occurs, not a second load caused by the recent combo.

---

### Task 5: Fix Startup Ordering Between Recent Folders and Saved UI State

**Files:**
- Modify: `UI/MainForm.cs`

- [ ] **Step 1: Inspect constructor order**

Find current calls around `LoadRecentFolders()` and `LoadUiState()`.

- [ ] **Step 2: Prevent recent selection from auto-loading during initialization**

Either:
- keep the suppression flag active during `LoadRecentFolders()`, or
- load UI state before selecting a recent item.

Use the smaller change that preserves existing startup behavior.

- [ ] **Step 3: Verify manually**

Manual scenario:
- Save UI state with folder A.
- Ensure recent list first item is folder B.
- Restart app.
- Expected: app does not start loading B before saved state is applied.

---

### Task 6: Reset Chart Range on Close All and New Unrelated Loads

**Files:**
- Modify: `UI/MainForm.cs`

- [ ] **Step 1: Create helper to clear chart range state**

Add a private helper:

```csharp
private void ClearChartRangeState()
{
    _rangeStartOa = double.NaN;
    _rangeEndOa = double.NaN;
    _rangeLabel.Text = string.Empty;
    // Reset range trackbar using the existing control API.
}
```

Use the actual existing range-trackbar reset methods/properties in `MainForm.cs`.

- [ ] **Step 2: Call helper from `CloseAllButtonOnClick`**

Call it before clearing chart series or immediately after `_viewerSession.SetData(string.Empty, null)`.

- [ ] **Step 3: Call helper when loading a different workspace**

When a load is not `preserveSelection`/refresh-in-place and the workspace key changes, clear old range state.

- [ ] **Step 4: Verify manually**

Manual scenario:
- Load record A, select a narrow range.
- Close All.
- Load record B.
- Expected: chart is not clipped by A’s range.

---

### Task 7: Align Root Locator With Reader and Make Discovery Deterministic

**Files:**
- Modify: `Infrastructure/DataImport/FileSystemTestRootLocator.cs`
- Test: `JSQViewer.Tests/WorkspaceLoadingTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests for:
- folder containing `Prova_backup.dbf` is not accepted as a valid test root;
- parent with `A\Deep\Prova001.dbf` and `B\Prova001.dbf` selects deterministic shortest/root-nearest candidate.

- [ ] **Step 2: Run tests and confirm RED**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter WorkspaceLoadingTests
```

Expected: new tests fail.

- [ ] **Step 3: Use the same regex semantics as `DbfTestDataSourceReader`**

Change locator regex to accept only DBFs the reader can load:

```csharp
private static readonly Regex ProvaDbfRegex = new Regex(@"^Prova\d+\.dbf$", RegexOptions.IgnoreCase);
```

- [ ] **Step 4: Collect all candidates before choosing**

Do not stop at the first subtree. Gather candidates up to max depth, sort deterministically by depth/path, and select the best.

- [ ] **Step 5: Run tests and full build**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter WorkspaceLoadingTests
dotnet build .\JSQViewer.csproj -c Debug
```

---

### Task 8: Deduplicate Resolved Roots Before Merge

**Files:**
- Modify: `Application/Workspace/UseCases/LoadWorkspaceDataUseCase.cs`
- Modify defensively: `Application/Workspace/UseCases/MergeLoadedSourcesUseCase.cs`
- Test: `JSQViewer.Tests/WorkspaceLoadingTests.cs`

- [ ] **Step 1: Write failing test**

Create a test where two different input folder strings resolve to the same root.

Expected behavior:
- no duplicate-key exception;
- result contains one loaded root;
- normalized folder spec uses the resolved root once.

- [ ] **Step 2: Run test and confirm RED**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter WorkspaceLoadingTests
```

- [ ] **Step 3: Deduplicate after `FindRoot`**

In `LoadWorkspaceDataUseCase.Execute`, resolve all roots first, then `Distinct(StringComparer.OrdinalIgnoreCase)` before reading sources.

- [ ] **Step 4: Return resolved roots in `WorkspaceLoadResult`**

Use resolved roots for:
- `NormalizedFolderSpec`;
- `Folders`;
- downstream workspace key generation.

- [ ] **Step 5: Add defensive merge behavior**

If duplicate roots still reach `MergeLoadedSourcesUseCase`, avoid `ToDictionary` throwing by either deduplicating or using a unique source key consistently.

- [ ] **Step 6: Run tests**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter WorkspaceLoadingTests
```

---

### Task 9: Validate Single-Row Exports Correctly

**Files:**
- Modify: `Export/TemplateExportValidator.cs`
- Test: `JSQViewer.Tests/TemplateExporterSmokeTests.cs`

- [ ] **Step 1: Write failing test**

Add a test that exports a workbook with exactly one data row and validates it.

Expected:

```csharp
Assert.IsTrue(validation.Ok, validation.Message);
```

- [ ] **Step 2: Run test and confirm RED**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter TemplateExporterSmokeTests
```

- [ ] **Step 3: Update validator row threshold**

Allow formulas in row 4, matching `TemplateExporter` start row:

```csharp
rowIndex < 4
```

Rename local variable from `hasFormulaAfterRow4` to `hasFormulaInDataRows`.

- [ ] **Step 4: Run export tests**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter TemplateExporter
```

---

### Task 10: Normalize Source-Qualified Codes During Export A/C Priority

**Files:**
- Modify: `Export/TemplateExporter.cs`
- Test: `JSQViewer.Tests/TemplateExporterPreparationTests.cs` or `TemplateExporterSmokeTests.cs`

- [ ] **Step 1: Write failing test**

Use columns:

```csharp
new[] { "srcB::B-Pc", "srcA::A-Pc" }
```

Expected:
- template key `Pc` resolves to `srcA::A-Pc`, not first match.

- [ ] **Step 2: Run test and confirm RED**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter TemplateExporter
```

- [ ] **Step 3: Add local base-code normalization helper**

In `TemplateExporter`, add a helper equivalent to:

```csharp
private static string GetBaseCode(string code)
{
    if (string.IsNullOrWhiteSpace(code)) return string.Empty;
    int sep = code.IndexOf("::", StringComparison.Ordinal);
    string result = sep >= 0 ? code.Substring(sep + 2) : code;
    int hash = result.IndexOf('#');
    return hash > 0 ? result.Substring(0, hash) : result;
}
```

- [ ] **Step 4: Use base code in priority checks**

Replace:

```csharp
m.StartsWith(pref, StringComparison.OrdinalIgnoreCase)
```

with:

```csharp
GetBaseCode(m).StartsWith(pref, StringComparison.OrdinalIgnoreCase)
```

- [ ] **Step 5: Run export tests**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter TemplateExporter
```

---

### Task 11: Reconcile Source-Window Sort Tests With Desired UX

**Files:**
- Modify: `Presentation/WinForms/Presenters/SourceWindowCoordinator.cs` only if desired UX is source windows default to `User`.
- Test: `JSQViewer.Tests/ChannelWorkspaceTests.cs`

- [ ] **Step 1: Decide desired behavior**

Use the current product decision:
- source windows should default to `User`; or
- source windows should inherit main sort.

- [ ] **Step 2: If `User` is desired, restore that behavior**

In `BindRoots`, when layout is not preserved, set:

```csharp
state.SortMode = "User";
```

- [ ] **Step 3: If main-sort inheritance is desired, update tests**

Change tests at `ChannelWorkspaceTests.cs:278` and `ChannelWorkspaceTests.cs:305` to expected `Code`.

- [ ] **Step 4: Run focused tests**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter ChannelWorkspacePresenterTests
```

---

### Task 12: Reconcile Manual X-Axis Hint Test With Russian UI

**Files:**
- Modify: `JSQViewer.Tests/ChartViewUseCaseTests.cs` or `UI/MainForm.cs`

- [ ] **Step 1: Decide language requirement**

If UI is Russian, tests should expect Russian strings. If hints must include English unit words, production text should be localized via `Loc`.

- [ ] **Step 2: Update test or UI**

Recommended for current app: update test to expect:
- `часы`;
- `минут`;
- `dd.MM.yyyy HH:mm`.

- [ ] **Step 3: Run focused test**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore --filter ManualXAxisUiHints
```

---

### Task 13: Final Verification

**Files:**
- No new changes unless verification exposes regressions.

- [ ] **Step 1: Run full Debug build**

```powershell
dotnet build .\JSQViewer.csproj -c Debug
```

Expected: 0 errors.

- [ ] **Step 2: Run full test suite**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj -c Debug --no-restore
```

Expected: all tests pass, except any explicitly deferred protocol-order tests that were intentionally marked/updated after the UX decision.

- [ ] **Step 3: Manual smoke test**

Manual scenarios:
- load one record;
- load 6 records;
- switch folders quickly;
- clear selected channels while chart is open;
- close all, open another record;
- export one-row or very small dataset;
- export multi-source data with A/C-prefixed fixed channels.

- [ ] **Step 4: Update PR notes**

Include:
- defects fixed;
- tests added;
- manual scenarios;
- explicit note that protocol-order production behavior was not changed unless separately reproduced.

