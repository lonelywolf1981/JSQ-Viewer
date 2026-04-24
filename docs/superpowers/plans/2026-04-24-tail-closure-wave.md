# Tail Closure Wave Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the remaining reviewed tails around chart axis controls, workspace-scoped source layout persistence, `MainForm` orchestration cleanup, and manual verification docs.

**Status (2026-04-24):** Tasks 1, 3, 4, 5 implemented. Task 2 and Task 6 documentation steps in progress.

**Architecture:** Execute this wave in one safe parallel slice and then two sequential runtime slices. First add manual chart axis controls and prepare documentation scaffolding in parallel. Then extend workspace layout persistence and finally extract the remaining persistence/load-request orchestration out of `MainForm` before finishing the docs and verification alignment.

**Tech Stack:** C# / .NET Framework 4.8, WinForms, MSTest, existing `Application/*`, `Infrastructure/*`, `Presentation/WinForms/*`, JSON persistence.

---

## File Structure

- Modify: `UI/MainForm.cs`
  Keep WinForms event/control wiring here, but remove remaining orchestration tails and add axis-control wiring.
- Create: `Presentation/WinForms/ViewModels/ChartAxisSettingsViewModel.cs`
  UI-facing axis state model for manual X/Y settings.
- Modify: `Application/Charting/ChartPipelineRequest.cs`
  Carry normalized manual axis input into the pipeline.
- Modify: `Application/Charting/ChartPipelineResult.cs`
  Carry effective axis output required by rendering.
- Modify: `Application/Charting/UseCases/BuildChartViewUseCase.cs`
  Execute the pipeline with axis settings included.
- Modify: `Application/Charting/ChartPipelineService.cs`
  Normalize and propagate effective axis state.
- Modify: `Presentation/WinForms/Charting/ChartViewModelFactory.cs`
  Map effective axis state into renderable chart view state.
- Modify: `Presentation/WinForms/Charting/ChartRenderer.cs`
  Apply manual axis settings to attached and detached charts.
- Modify: `Presentation/WinForms/ViewModels/ChartViewModel.cs`
  Carry effective axis values used by renderer.

- Modify: `Application/Channels/WorkspaceLayoutState.cs`
  Extend persisted workspace state with source-specific effective order payload.
- Create: `Application/Channels/WorkspaceLayoutStateService.cs`
  Application-facing orchestration for workspace layout load/save and effective source-order persistence.
- Modify: `Infrastructure/Persistence/FileWorkspaceLayoutRepository.cs`
  Persist and load the expanded workspace layout model.
- Modify: `Presentation/WinForms/Presenters/ChannelWorkspacePresenter.cs`
  Expose runtime order state needed for source-window persistence and restore.
- Modify: `Application/Channels/ChannelWorkspaceModel.cs`
  Restore and report source-specific effective order.
- Modify: `Program.cs`
  Construct and inject the workspace layout application-facing service.

- Create: `Application/UiState/UiShellStateService.cs`
  Application-facing orchestration for recent folders and UI state load/save.
- Create: `Application/Workspace/WorkspaceLoadOrchestrationService.cs`
  Application-facing orchestration for folder-spec normalization/validation and load request assembly.
- Modify: `Infrastructure/Composition/WorkspaceLoadingComposition.cs`
  Wire the new workspace orchestration service dependencies.
- Modify: `Program.cs`
  Construct and inject the new application-facing services.

- Modify: `doc/refresh_button_test_suite.md`
  Add/update manual verification scenarios for this wave.
- Modify: `doc/clean_architecture_migration_notes.md`
  Reflect completed cleanup and new manual verification focus.

- Modify: `JSQViewer.Tests/ChartPipelineTests.cs`
- Modify: `JSQViewer.Tests/ChartViewUseCaseTests.cs`
- Modify: `JSQViewer.Tests/ChartDisplayBehaviorTests.cs`
- Modify: `JSQViewer.Tests/WorkspaceLayoutStateTests.cs`
- Modify: `JSQViewer.Tests/ChannelWorkspaceTests.cs`
- Create: `JSQViewer.Tests/UiShellStateServiceTests.cs`
- Create: `JSQViewer.Tests/WorkspaceLoadOrchestrationServiceTests.cs`

---

## Task 1: Manual Chart Axis Controls

**Files:**
- Create: `Presentation/WinForms/ViewModels/ChartAxisSettingsViewModel.cs`
- Modify: `Application/Charting/ChartPipelineRequest.cs`
- Modify: `Application/Charting/ChartPipelineResult.cs`
- Modify: `Application/Charting/ChartPipelineService.cs`
- Modify: `Application/Charting/UseCases/BuildChartViewUseCase.cs`
- Modify: `Presentation/WinForms/ViewModels/ChartViewModel.cs`
- Modify: `Presentation/WinForms/Charting/ChartViewModelFactory.cs`
- Modify: `Presentation/WinForms/Charting/ChartRenderer.cs`
- Modify: `UI/MainForm.cs`
- Modify: `JSQViewer.Tests/ChartPipelineTests.cs`
- Modify: `JSQViewer.Tests/ChartViewUseCaseTests.cs`

- [x] **Step 1: Write the failing pipeline and renderer tests**

Add tests covering:
- manual X min/max/step flows through the pipeline and renderer
- manual Y min/max/step flows through the pipeline and renderer
- disabled manual axis mode keeps automatic behavior
- effective axis settings survive detached-chart redraw

- [ ] **Step 2: Run targeted axis tests to verify they fail**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "ChartPipeline|ChartViewUseCase"`
Expected: FAIL because manual axis settings do not exist yet.

- [x] **Step 3: Add axis settings models and contracts**

Implement:
- `ChartAxisSettingsViewModel`
- axis settings payload in `ChartPipelineRequest`
- effective axis payload in `ChartPipelineResult`
- view-model fields needed by the renderer

- [x] **Step 4: Implement minimal pipeline and renderer support**

Update:
- `ChartPipelineService`
- `BuildChartViewUseCase`
- `ChartViewModelFactory`
- `ChartRenderer`

Ensure both attached and detached charts read the same effective axis state.

- [x] **Step 5: Wire `MainForm` controls for manual X/Y axis state**

Add compact WinForms controls for:
- enable manual X axis
- X min/max/step
- enable manual Y axis
- Y min/max/step

Translate UI values into normalized request state without moving axis semantics back into `MainForm`.

- [ ] **Step 6: Run targeted axis tests to verify they pass**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "ChartPipeline|ChartViewUseCase"`
Expected: PASS.

- [ ] **Step 7: Run focused chart regression tests**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "ChartDisplayBehavior|SessionAndCharting"`
Expected: PASS.

- [x] **Step 8: Commit**

```bash
git add Presentation/WinForms/ViewModels/ChartAxisSettingsViewModel.cs Application/Charting/ChartPipelineRequest.cs Application/Charting/ChartPipelineResult.cs Application/Charting/ChartPipelineService.cs Application/Charting/UseCases/BuildChartViewUseCase.cs Presentation/WinForms/ViewModels/ChartViewModel.cs Presentation/WinForms/Charting/ChartViewModelFactory.cs Presentation/WinForms/Charting/ChartRenderer.cs UI/MainForm.cs JSQViewer.Tests/ChartPipelineTests.cs JSQViewer.Tests/ChartViewUseCaseTests.cs
git commit -m "feat: add manual chart axis controls"
```

---

## Task 2: Documentation Baseline Prep

**Files:**
- Modify: `doc/refresh_button_test_suite.md`
- Modify: `doc/clean_architecture_migration_notes.md`

- [x] **Step 1: Restructure verification docs with placeholders for this wave**

Prepare sections for:
- explicit chart open policy
- manual axis controls
- source-window order persistence
- 4..6 source loading
- template mode verification
- remaining clean-architecture cleanup verification

- [x] **Step 2: Review docs for consistency with current repository terminology**

Check naming against:
- `MainForm`
- chart host / detached chart
- workspace layout
- protocol template mode

- [ ] **Step 3: Commit**

```bash
git add doc/refresh_button_test_suite.md doc/clean_architecture_migration_notes.md
git commit -m "docs: prepare verification notes for tail closure wave"
```

---

## Task 3: Workspace-Scoped Source Order Persistence

**Files:**
- Modify: `Application/Channels/WorkspaceLayoutState.cs`
- Create: `Application/Channels/WorkspaceLayoutStateService.cs`
- Modify: `Infrastructure/Persistence/FileWorkspaceLayoutRepository.cs`
- Modify: `Application/Channels/ChannelWorkspaceModel.cs`
- Modify: `Presentation/WinForms/Presenters/ChannelWorkspacePresenter.cs`
- Modify: `Program.cs`
- Modify: `UI/MainForm.cs`
- Modify: `JSQViewer.Tests/WorkspaceLayoutStateTests.cs`
- Modify: `JSQViewer.Tests/ChannelWorkspaceTests.cs`
- Create: `JSQViewer.Tests/WorkspaceLayoutStateServiceTests.cs`

- [x] **Step 1: Write the failing persistence tests**

Add tests covering:
- source-specific effective order is persisted in workspace layout state
- persisted effective order restores when no named order is selected
- named order still wins over persisted effective order
- persisted order degrades safely when channels differ after reload
- workspace layout load/save decisions are delegated through an application-facing service

- [ ] **Step 2: Run targeted workspace-layout tests to verify they fail**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "WorkspaceLayoutState|WorkspaceLayoutStateService|ChannelWorkspace"`
Expected: FAIL because effective source order is not persisted/restored yet.

- [x] **Step 3: Extend workspace layout persistence model**

Add source-specific effective order payload keyed by normalized source root.

- [x] **Step 4: Implement `WorkspaceLayoutStateService`**

Move workspace-layout orchestration behind an application-facing service:
- load workspace layout for the current workspace key
- persist main selected order key
- persist source selected order key
- persist source effective order for user-mode source windows
- provide safe restore behavior when persisted state no longer matches current channels

- [x] **Step 5: Teach runtime model/presenter to restore and report effective source order**

Update:
- `ChannelWorkspaceModel`
- `ChannelWorkspacePresenter`

Expose enough state for the service-backed restore/persist path:
- load workspace layout through `WorkspaceLayoutStateService` during workspace bind
- restore effective source order when no named order overrides it
- report user-mode source-window reorder results for persistence

- [x] **Step 6: Wire `Program` and `MainForm` to the service**

Inject `WorkspaceLayoutStateService` from composition/bootstrap and remove direct repository orchestration from `MainForm` for:
- workspace-layout load during bind
- main/source selected-order persistence
- source effective-order persistence

- [x] **Step 7: Delegate source-window persistence from `MainForm` through the service**

Persist effective source order when:
- source-window drag/drop completes in user mode
- workspace layout state is already active for the current workspace

- [ ] **Step 8: Run targeted workspace-layout tests to verify they pass**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "WorkspaceLayoutState|WorkspaceLayoutStateService|ChannelWorkspace"`
Expected: PASS.

- [x] **Step 9: Commit**

```bash
git add Application/Channels/WorkspaceLayoutState.cs Application/Channels/WorkspaceLayoutStateService.cs Infrastructure/Persistence/FileWorkspaceLayoutRepository.cs Application/Channels/ChannelWorkspaceModel.cs Presentation/WinForms/Presenters/ChannelWorkspacePresenter.cs Program.cs UI/MainForm.cs JSQViewer.Tests/WorkspaceLayoutStateTests.cs JSQViewer.Tests/WorkspaceLayoutStateServiceTests.cs JSQViewer.Tests/ChannelWorkspaceTests.cs
git commit -m "feat: persist workspace source window layout state"
```

---

## Task 4: Extract UI-State And Recent-Folder Orchestration

**Files:**
- Create: `Application/UiState/UiShellStateService.cs`
- Modify: `UI/MainForm.cs`
- Create: `JSQViewer.Tests/UiShellStateServiceTests.cs`
- Modify: `Program.cs`

- [x] **Step 1: Write the failing service tests**

Add tests covering:
- load recent folders through repository-backed orchestration
- add recent folder preserves expected ordering and deduplication
- load/save UI state through one application-facing service

- [ ] **Step 2: Run targeted service tests to verify they fail**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter UiShellStateService`
Expected: FAIL because the service does not exist yet.

- [x] **Step 3: Implement `UiShellStateService`**

Move orchestration for:
- recent-folder load
- recent-folder update
- UI-state load
- UI-state save

Keep repositories as infrastructure details.

- [x] **Step 4: Integrate `MainForm` and `Program` with the new service**

Replace direct orchestration in `MainForm` while keeping WinForms control updates in the form.

- [ ] **Step 5: Run targeted service tests to verify they pass**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter UiShellStateService`
Expected: PASS.

- [x] **Step 6: Commit**

```bash
git add Application/UiState/UiShellStateService.cs UI/MainForm.cs JSQViewer.Tests/UiShellStateServiceTests.cs Program.cs
git commit -m "refactor: extract ui shell state orchestration"
```

---

## Task 5: Extract Workspace Load Request Orchestration

**Files:**
- Create: `Application/Workspace/WorkspaceLoadOrchestrationService.cs`
- Modify: `Application/Workspace/WorkspaceFolderSpecParser.cs`
- Modify: `Infrastructure/Composition/WorkspaceLoadingComposition.cs`
- Modify: `Program.cs`
- Modify: `UI/MainForm.cs`
- Create: `JSQViewer.Tests/WorkspaceLoadOrchestrationServiceTests.cs`
- Modify: `JSQViewer.Tests/WorkspaceLoadingTests.cs`

- [x] **Step 1: Write the failing orchestration tests**

Add tests covering:
- folder spec normalization/validation through the new service
- `WorkspaceLoadRequest` assembly outside `MainForm`
- shared max-folder-count behavior remains unchanged

- [ ] **Step 2: Run targeted orchestration tests to verify they fail**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "WorkspaceLoadOrchestrationService|WorkspaceLoading"`
Expected: FAIL because the orchestration service does not exist yet.

- [x] **Step 3: Implement `WorkspaceLoadOrchestrationService`**

Move orchestration for:
- parse/join/validate folder spec
- normalized folder spec output
- load request assembly
- current workspace key derivation inputs

- [x] **Step 4: Integrate `MainForm` with the new workspace load path**

Keep only control/event wiring in `MainForm`; route spec orchestration and request construction through the new service.

- [ ] **Step 5: Run targeted orchestration tests to verify they pass**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "WorkspaceLoadOrchestrationService|WorkspaceLoading"`
Expected: PASS.

- [x] **Step 6: Commit**

```bash
git add Application/Workspace/WorkspaceLoadOrchestrationService.cs Application/Workspace/WorkspaceFolderSpecParser.cs Infrastructure/Composition/WorkspaceLoadingComposition.cs Program.cs UI/MainForm.cs JSQViewer.Tests/WorkspaceLoadOrchestrationServiceTests.cs JSQViewer.Tests/WorkspaceLoadingTests.cs
git commit -m "refactor: extract workspace load orchestration"
```

---

## Task 6: Final Documentation Sync And Verification Alignment

**Files:**
- Modify: `doc/refresh_button_test_suite.md`
- Modify: `doc/clean_architecture_migration_notes.md`
- Modify: `docs/superpowers/plans/2026-03-30-ui-behavior-and-multi-test-plan.md`
- Modify: `docs/superpowers/plans/2026-03-30-clean-architecture-rebuild.md`

- [ ] **Step 1: Update manual verification docs with completed scenarios**

Document:
- explicit chart-open checks
- manual axis control checks
- source-window layout persistence checks
- 4..6 source loading checks
- template mode checks
- reduced `MainForm` orchestration verification notes

- [ ] **Step 2: Align normalized plans with completed runtime work**

Update only the plan items that this wave actually completes. Do not close unverifiable full-build/full-suite/manual-run items unless executed and observed.

- [ ] **Step 3: Run focused automated verification for this wave**

Run: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter "Chart|Workspace|UiShellStateService|WorkspaceLoadOrchestrationService"`
Expected: PASS.

- [ ] **Step 4: Run build verification**

Run:
- `dotnet build .\JSQViewer.csproj -c Debug`
- `dotnet build .\JSQViewer.csproj -c Release`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add doc/refresh_button_test_suite.md doc/clean_architecture_migration_notes.md docs/superpowers/plans/2026-03-30-ui-behavior-and-multi-test-plan.md docs/superpowers/plans/2026-03-30-clean-architecture-rebuild.md
git commit -m "docs: align verification notes and plans for tail closure wave"
```
