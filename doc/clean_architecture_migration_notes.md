# Clean Architecture Migration Notes

## Target Structure

- `Application/`
  Use cases and contracts for workspace loading, charting, export, user state, and session state.
- `Infrastructure/`
  File system, persistence, data import, export adapters, and platform services.
- `Presentation/WinForms/`
  Presenters, chart rendering, composition helpers, and UI-facing view models.
- `UI/`
  Thin WinForms shells that bind controls to presenters and use cases.

## Migrated Blocks

- Workspace loading moved behind `LoadWorkspaceDataUseCase` and `Infrastructure/DataImport/*`.
- Global session/cache logic moved into `ViewerSession`, `SeriesSliceService`, and chart use cases.
- Channel projection and source-window coordination moved into `ChannelWorkspacePresenter`.
- Chart request building and rendering now flow through `BuildChartViewUseCase`, `ChartPipelineService`, `ChartViewModelFactory`, and `ChartRenderer`.
- Export orchestration moved behind `ExportTemplateUseCase` with infrastructure adapters for XLSX rendering and validation.
- Settings normalization now runs through `ViewerSettingsSanitizer`.
- Recent folders and UI state orchestration moved into `Application/UiState/UiShellStateService`; `MainForm` no longer calls repositories directly for these concerns.
- Folder spec normalization, load request assembly, and workspace key derivation moved into `Application/Workspace/WorkspaceLoadOrchestrationService`; `MainForm` no longer holds `_workspaceFolderSpecParser`.
- Workspace layout persistence extended: `WorkspaceLayoutState` now carries `SourceEffectiveOrders`; load/save orchestration delegated to `Application/Channels/WorkspaceLayoutStateService`; `FileWorkspaceLayoutRepository` persists the expanded model.
- Manual chart axis controls: `ChartAxisSettingsViewModel` added in `Presentation/WinForms/ViewModels`; `ChartPipelineRequest`/`ChartPipelineResult` carry `XAxis`/`YAxis` settings; `ChartPipelineService` normalizes them; `ChartRenderer` applies them via `ApplyXAxis()`/`ApplyAxis()`; `ChartViewModelFactory` maps them into view state; `MainForm` wires `_manualXAxisCheck`, `_manualYAxisCheck`, and min/max/step fields.

## Remaining Compatibility Seams

- `Core/AppState.cs`
- `Core/SeriesCache.cs`
- `UI/Localization.cs`
- `Settings/PresetStore.cs`
- `Settings/OrderStore.cs`
- `Settings/ViewerSettings.cs`

These files are no longer primary runtime orchestration paths, but they remain as compatibility wrappers or storage helpers.

## Verification Snapshot

- Automated:
  - `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj` — **96 tests passing** as of 2026-04-24 (up from 69 before the tail-closure wave)
  - `dotnet build .\JSQViewer.csproj -c Debug`
  - `dotnet build .\JSQViewer.csproj -c Release`
- Manual:
  - `doc/refresh_button_test_suite.md` — includes Tail Closure Wave verification scenarios (TC-AXIS-01/02, TC-LAYOUT-01/02, TC-ORCH-01)
  - export open/save warning flow
  - chart range sync in detached window
  - RU/EN localization pass across main window and settings dialog

## Tail Closure Wave Documentation Baseline

This note tracks documentation for the 2026-04-24 tail-closure wave.

**Wave completion status (2026-04-24):** Tasks 1 (Manual Chart Axis Controls), 3 (Workspace-Scoped Source Order Persistence), 4 (UiShellStateService), and 5 (WorkspaceLoadOrchestrationService) are implemented and passing. Task 2 (Documentation Baseline Prep) and Task 6 (Final Documentation Sync and Verification Alignment) are the current documentation-finishing steps; they do not require additional runtime changes.

### Verification Focus For This Wave

- Explicit chart open policy:
  verify the intended open/visibility behavior in `MainForm` and the detached chart once runtime wiring lands.
- Manual axis controls:
  verify manual X/Y configuration paths only after the corresponding chart pipeline and renderer work is present.
- Workspace layout persistence:
  verify source-window order persistence as workspace-scoped state, including restore and safe fallback behavior.
- 4..6 source loading:
  verify that larger multi-source workspaces remain stable after the orchestration cleanup in this wave.
- Protocol template mode:
  verify that template-oriented flows still behave correctly after chart/layout changes.
- Remaining orchestration cleanup:
  verify that `MainForm` continues moving toward UI wiring only, with orchestration delegated to application/presentation services.

### Terminology Alignment

- Use `MainForm` when referring to the WinForms shell.
- Use `detached chart` when referring to the separate chart window.
- Use `workspace layout` for persisted UI/layout state tied to the current workspace.
- Use `protocol template mode` for the template-oriented protocol/export flow.

### Manual Verification Baseline

The authoritative manual checklist for this wave lives in `doc/refresh_button_test_suite.md`.
Until the runtime slices are merged, keep those sections as preparation/checklist scaffolding rather than pass/fail claims.

### Current Compatibility Focus

The following areas remain worth checking during this wave even if their runtime ownership is being reduced:

- `MainForm` still contains some orchestration that should continue shrinking without changing user-visible behavior.
- Compatibility wrappers and storage helpers may still be exercised indirectly by workspace, chart, and template-mode flows.
- Manual verification should capture whether remaining seams are observable in behavior, not just whether files still exist.

## Known Follow-Up

- Remove `Loc` shim entirely by pushing localization into explicit presentation services.
- Retire compatibility facades once external/tests no longer depend on them.
- Add UI-level smoke coverage for WinForms chart rendering and settings dialog interactions.
