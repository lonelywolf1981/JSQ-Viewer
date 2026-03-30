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
  - `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`
  - `dotnet build .\JSQViewer.csproj -c Debug`
  - `dotnet build .\JSQViewer.csproj -c Release`
- Manual:
  - `doc/refresh_button_test_suite.md`
  - export open/save warning flow
  - chart range sync in detached window
  - RU/EN localization pass across main window and settings dialog

## Known Follow-Up

- Remove `Loc` shim entirely by pushing localization into explicit presentation services.
- Retire compatibility facades once external/tests no longer depend on them.
- Add UI-level smoke coverage for WinForms chart rendering and settings dialog interactions.
