# Clean Architecture Rebuild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild JSQ Viewer into a Clean Architecture structure by moving major functional blocks out of WinForms and static helpers into explicit domain, application, and infrastructure modules.

**Architecture:** Use a staged vertical migration. First create the target layer skeleton and platform ports. Then move large functional blocks one by one: workspace loading, session state, channel workspace, chart pipeline, export pipeline, and user persistence. Keep behavior stable while removing old paths after each block is proven.

**Tech Stack:** C# /.NET Framework 4.8, WinForms, System.IO, JavaScriptSerializer, OpenXML package manipulation, Python-based manual test data tools.

**Status (2026-04-24):** Mostly implemented through a staged refactor from `2026-03-30` to `2026-04-03`, with later cleanup/docs follow-up. `Application/`, `Infrastructure/`, and `Presentation/WinForms/` are in active use, but `Domain/` was not introduced, `UI/MainForm.cs` is still not a thin shell, and compatibility seams such as `Core/AppState.cs`, `Core/SeriesCache.cs`, and `UI/Localization.cs` remain.

Tail-closure wave additions (2026-04-24):
- `Application/UiState/UiShellStateService` extracted from `MainForm`; recent-folder and UI-state orchestration now fully behind an application service.
- `Application/Workspace/WorkspaceLoadOrchestrationService` extracted; `MainForm` no longer holds `_workspaceFolderSpecParser` or assembles load requests directly.
- `Application/Channels/WorkspaceLayoutStateService` added; `WorkspaceLayoutState` extended with `SourceEffectiveOrders`; workspace layout load/save delegated to the new service.

**Relevant commits:**
- `94e83d3` `refactor: establish clean architecture bootstrap`
- `0654a69` `refactor: move localization ownership into platform`
- `1094514` `refactor: inject ui notification and launch services`
- `c7c94b2` `refactor: extract user persistence repositories`
- `f9389c5` `refactor: extract workspace loading pipeline`
- `0d9632e` `refactor: replace app state with viewer session`
- `9aed4f5` `Вынесено рабочее пространство каналов`
- `2b86e3a` `refactor: extract chart pipeline and range semantics`
- `76584a7` `refactor: harden chart pipeline boundaries`
- `1be0a55` `refactor: extract export workflow services`
- `0efde63` `refactor: reduce presentation shell dependencies`
- `a91d97c` `docs: capture clean architecture migration notes`

---

## File Structure

The new structure should be introduced inside the existing project before deleting old code.

- Create: `Domain/`
- Create: `Application/`
- Create: `Infrastructure/`
- Create: `Presentation/WinForms/`
- Move or replace responsibilities from:
  - `UI/MainForm.cs`
  - `UI/SettingsDialog.cs`
  - `UI/Localization.cs`
  - `UI/ToastNotification.cs`
  - `Core/TestLoader.cs`
  - `Core/AppState.cs`
  - `Core/SeriesCache.cs`
  - `Core/CanaliParser.cs`
  - `Core/DbfReader.cs`
  - `Core/AppLogger.cs`
  - `Settings/*.cs`
  - `Export/*.cs`

## Stage 1: Establish the new architectural skeleton

**Files:**
- Create: `Domain/Abstractions/`
- Create: `Application/Abstractions/`
- Create: `Infrastructure/Composition/`
- Create: `Presentation/WinForms/Composition/`
- Modify: `JSQViewer.csproj`
- Modify: `Program.cs`

- [ ] Define top-level namespaces and folders for `Domain`, `Application`, `Infrastructure`, and `Presentation.WinForms`.
  Current repository state: `Application`, `Infrastructure`, and `Presentation/WinForms` are established and in use; `Domain/` is the remaining missing part of this original target.
- [x] Add the new folders to [JSQViewer.csproj](/C:/Users/a.baidenko/Downloads/JSQ-Laboratory/JSQ_Viewer/JSQViewer.csproj).
- [x] Create core application ports: `ILogger`, `IFileSystem`, `IAppPaths`, `ILocalizationService`, `INotificationService`, `IExternalProcessLauncher`.
- [x] Introduce a simple composition root in `Program.cs` that constructs concrete infrastructure services and passes them to the main presentation entry point.
- [x] Keep global exception handling behavior unchanged while routing logging through `ILogger`.
- [ ] Build the project and confirm no runtime behavior changed yet.

## Stage 2: Replace platform statics with injectable services

**Files:**
- Create: `Infrastructure/Platform/SystemFileSystem.cs`
- Create: `Infrastructure/Platform/ProjectRootPaths.cs`
- Create: `Infrastructure/Platform/FileLogger.cs`
- Create: `Infrastructure/Platform/DictionaryLocalizationService.cs`
- Create: `Infrastructure/Platform/ToastNotificationService.cs`
- Create: `Infrastructure/Platform/ProcessLauncher.cs`
- Modify: `Program.cs`
- Modify: `UI/Localization.cs`
- Modify: `UI/ToastNotification.cs`
- Modify: `Core/AppLogger.cs`

- [x] Implement `IFileSystem` over `System.IO`.
- [x] Implement `IAppPaths` and formalize all path conventions currently discovered implicitly in `MainForm`.
- [x] Implement `ILogger` using the current `app.log` format from [Core/AppLogger.cs](/C:/Users/a.baidenko/Downloads/JSQ-Laboratory/JSQ_Viewer/Core/AppLogger.cs).
- [x] Move localization dictionaries out of static global access into `ILocalizationService`.
- [x] Wrap toast notifications and external process launching behind interfaces.
- [x] Convert `Program.cs` and the main form bootstrap path to use injected platform services instead of static helpers.
- [x] Leave compatibility shims in place temporarily if needed, but mark them for deletion in later stages.

## Stage 3: Rebuild user persistence as infrastructure repositories

**Files:**
- Create: `Application/UserState/Ports/IPresetRepository.cs`
- Create: `Application/UserState/Ports/IChannelOrderRepository.cs`
- Create: `Application/UserState/Ports/IUiStateRepository.cs`
- Create: `Application/UserState/Ports/IRecentFoldersRepository.cs`
- Create: `Application/UserState/Ports/IViewerSettingsRepository.cs`
- Create: `Infrastructure/Persistence/JsonPresetRepository.cs`
- Create: `Infrastructure/Persistence/JsonChannelOrderRepository.cs`
- Create: `Infrastructure/Persistence/JsonUiStateRepository.cs`
- Create: `Infrastructure/Persistence/JsonRecentFoldersRepository.cs`
- Create: `Infrastructure/Persistence/JsonViewerSettingsRepository.cs`
- Modify: `Settings/PresetStore.cs`
- Modify: `Settings/OrderStore.cs`
- Modify: `Settings/ViewerSettings.cs`
- Modify: `Settings/JsonHelper.cs`
- Modify: `UI/MainForm.cs`

- [x] Preserve existing JSON formats and filenames for presets, orders, UI state, recent folders, and viewer settings.
- [x] Replace direct `JsonHelper` usage in [MainForm.cs](/C:/Users/a.baidenko/Downloads/JSQ-Laboratory/JSQ_Viewer/UI/MainForm.cs) with repository calls.
- [x] Move `LoadRecentFolders`, `AddRecentFolder`, `LoadUiState`, and form-closing persistence logic behind repositories and application-facing services.
- [x] Leave old `Settings/*` classes only as temporary wrappers if required for transition.
- [ ] Verify that presets, orders, recent folders, and UI state still load from the same on-disk locations.

## Stage 4: Rebuild workspace loading as application + import infrastructure

**Files:**
- Create: `Domain/Workspace/`
- Create: `Application/Workspace/Ports/ITestRootLocator.cs`
- Create: `Application/Workspace/Ports/ITestMetadataReader.cs`
- Create: `Application/Workspace/Ports/ICanaliDefinitionReader.cs`
- Create: `Application/Workspace/Ports/ITestDataSourceReader.cs`
- Create: `Application/Workspace/UseCases/LoadWorkspaceDataUseCase.cs`
- Create: `Application/Workspace/UseCases/RefreshWorkspaceDataUseCase.cs`
- Create: `Application/Workspace/UseCases/AnalyzeOverlapConflictsUseCase.cs`
- Create: `Application/Workspace/UseCases/MergeLoadedSourcesUseCase.cs`
- Create: `Infrastructure/DataImport/DbfTestDataSourceReader.cs`
- Create: `Infrastructure/DataImport/FileSystemTestRootLocator.cs`
- Create: `Infrastructure/DataImport/CanaliDefinitionReader.cs`
- Create: `Infrastructure/DataImport/ProvaMetadataReader.cs`
- Modify: `Core/TestLoader.cs`
- Modify: `Core/CanaliParser.cs`
- Modify: `Core/DbfReader.cs`
- Modify: `UI/MainForm.cs`

- [x] Extract folder spec parsing and workspace load requests away from the form.
- [x] Move root discovery, metadata reading, channel definition reading, and DBF parsing behind explicit ports.
- [x] Move overlap analysis and merge rules into application/domain services, not into infrastructure readers.
- [ ] Replace `TestLoader` direct calls in `MainForm` with `LoadWorkspaceDataUseCase` and `RefreshWorkspaceDataUseCase`.
- [x] Preserve current single-source and multi-source semantics, including overlap split behavior.
- [ ] Use the refresh-suite data and documented manual scenarios to validate the new path.

## Stage 5: Replace global session state and derived data cache

**Files:**
- Create: `Application/Session/IViewerSession.cs`
- Create: `Application/Session/ViewerSession.cs`
- Create: `Application/Charting/Ports/ISeriesSliceCache.cs`
- Create: `Application/Charting/Services/SeriesSliceService.cs`
- Create: `Infrastructure/Cache/MemorySeriesSliceCache.cs`
- Modify: `Core/AppState.cs`
- Modify: `Core/SeriesCache.cs`
- Modify: `UI/MainForm.cs`
- Modify: `Export/TemplateExporter.cs`

- [x] Introduce `IViewerSession` as the single current-workspace state holder.
- [x] Move `BuildSummary`, `NearestIndex`, `SliceByTime`, and timestamp utilities out of `AppState` into stateless services where appropriate.
- [x] Replace static `SeriesCache` with an injected cache decorator or service pair.
- [x] Remove direct `AppState` access from `MainForm`.
- [x] Remove `AppState` leakage from [TemplateExporter.cs](/C:/Users/a.baidenko/Downloads/JSQ-Laboratory/JSQ_Viewer/Export/TemplateExporter.cs).
- [ ] Keep data versioning and cache invalidation behavior equivalent to current behavior.

## Stage 6: Extract channel workspace and multi-window coordination

**Files:**
- Create: `Application/Channels/`
- Create: `Presentation/WinForms/Presenters/ChannelWorkspacePresenter.cs`
- Create: `Presentation/WinForms/Presenters/SourceWindowsCoordinator.cs`
- Create: `Presentation/WinForms/ViewModels/ChannelItemViewModel.cs`
- Modify: `UI/MainForm.cs`

- [x] Move channel projection, filtering, sorting, ordering, and selection rules into an application-backed channel workspace model.
- [x] Extract source-window synchronization and coordination from `MainForm` into a dedicated coordinator.
- [ ] Remove direct mutation of `_checkedCodes`, `_allChannels`, and source-window collections from ad hoc event code.
- [ ] Keep current multi-window behavior, including per-source lists and shared selection semantics.
- [ ] Validate channel selection preservation during refresh and multi-source use.

## Stage 7: Extract chart pipeline and range semantics

**Files:**
- Create: `Domain/Charting/`
- Create: `Application/Charting/UseCases/BuildChartViewUseCase.cs`
- Create: `Application/Charting/UseCases/BuildWorkspaceSummaryUseCase.cs`
- Create: `Presentation/WinForms/ViewModels/ChartViewModel.cs`
- Create: `Presentation/WinForms/Charting/ChartRenderer.cs`
- Modify: `UI/MainForm.cs`
- Modify: `UI/RangeTrackBar.cs`

- [ ] Move chart request building, step resolution, overlay rules, legend generation, and range semantics out of `MainForm`.
  Current repository state: partially implemented. Dedicated chart pipeline/view-model layers exist, but `MainForm` still assembles chart requests and retains part of the range/legend/overlay semantics.
- [ ] Decide whether `RangeTrackBar` stays OADate-specific inside presentation or becomes a neutral range control. Prefer presentation-only OADate behavior if possible.
- [x] Introduce chart view models that the form can render without owning chart calculations.
- [ ] Preserve current zoom, reset, overlay axis, and detached-chart behaviors.
- [ ] Validate chart output using the current manual refresh scenarios and spot-check performance with multi-source data.

## Stage 8: Extract export pipeline and settings workflow

**Files:**
- Create: `Application/Exporting/UseCases/ExportTemplateUseCase.cs`
- Create: `Application/Exporting/Ports/ITemplateExporter.cs`
- Create: `Application/Exporting/Ports/ITemplateExportValidator.cs`
- Create: `Presentation/WinForms/Presenters/ExportSettingsPresenter.cs`
- Modify: `Export/TemplateExporter.cs`
- Modify: `Export/TemplateExportValidator.cs`
- Modify: `UI/SettingsDialog.cs`
- Modify: `UI/MainForm.cs`

- [x] Wrap the current exporter and validator behind application ports without changing file format behavior.
- [x] Move export request assembly out of `MainForm`.
- [ ] Move settings validation and mapping logic out of `SettingsDialog` where it is business-significant.
- [ ] Keep the existing export template behavior, warning handling, and post-save open behavior.
- [x] Before deeper exporter refactoring, capture golden-file or structural validation checks for the produced XLSX.

## Stage 9: Rebuild presentation around presenters and remove legacy paths

**Files:**
- Create: `Presentation/WinForms/Views/`
- Create: `Presentation/WinForms/Presenters/`
- Modify: `UI/MainForm.cs`
- Modify: `UI/SettingsDialog.cs`
- Delete or archive legacy static helpers after replacement

- [ ] Refactor `MainForm` into a thin view shell that delegates to presenters and use cases.
- [ ] Remove direct infrastructure access from WinForms code.
- [ ] Remove legacy compatibility shims for `AppState`, `Loc`, `ToastNotification`, direct `JsonHelper` usage, and old static stores.
- [ ] Re-run build and manual scenario verification across loading, refresh, charting, presets, orders, localization, and export.

## Stage 10: Hardening, verification, and cleanup

**Files:**
- Modify: `doc/refresh_button_test_suite.md`
- Modify: `doc/clean_architecture_migration_notes.md`
- Modify: affected source files across all layers

- [x] Update repository documentation to describe the new architecture and composition model.
- [x] Add regression-oriented verification notes for loading, refresh, charting, export, presets, orders, and localization.
- [ ] Remove dead code and obsolete adapters only after all new paths are stable.
- [ ] Confirm `dotnet build` succeeds in Debug and Release.
- [ ] Run the refresh manual suite and any new architecture-focused smoke scenarios before closing the migration.
