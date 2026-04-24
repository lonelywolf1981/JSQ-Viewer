# Test Suite: Refresh Button

This test suite verifies the new `Refresh` button behavior in the main window.

## Scope

- Reload data from currently selected folders.
- Preserve selected channels.
- Preserve selected chart display mode (including overlay compare mode).
- Redraw chart with newly loaded data.

## Preconditions

1. Build the app:

```bash
dotnet build "JSQViewer.csproj" -c Debug
```

2. Generate deterministic test data:

```bash
python tools/refresh_suite/build_refresh_suite.py
```

3. Ensure active data variant is `v1`:

```bash
python tools/refresh_suite/switch_refresh_variant.py --variant v1
```

4. Run the app:

```bash
bin\Debug\JSQViewer.exe
```

5. Use these folders in the UI:

- `testdata\refresh_suite\source_a`
- `testdata\refresh_suite\source_b`

## Data Expectations

- `v1` values in `A-01` are around `10..14` (source_a) and `20..24` (source_b).
- `v2` values in `A-01` are around `30..34` (source_a) and `40..44` (source_b).
- Switching from `v1` to `v2` must produce visible chart shift after clicking `Refresh`.

## Test Cases

### TC-01 Single-folder refresh keeps selected channels

1. Load `source_a` only.
2. Select channels `A-01` and `C-01`.
3. Switch data on disk to `v2`:

```bash
python tools/refresh_suite/switch_refresh_variant.py --variant v2
```

4. Click `Refresh`.

Expected:

- `A-01` and `C-01` stay selected.
- Chart updates to new values (around `30..34` for `A-01`).
- No need to reselect channels manually.

### TC-02 Single-folder refresh keeps non-overlay mode

1. Load `source_a` only.
2. Ensure compare overlay mode is OFF.
3. Click `Refresh`.

Expected:

- Compare overlay checkbox remains OFF.
- Chart remains in normal absolute-time axis mode.

### TC-03 Multi-folder refresh keeps selected channels in source windows

1. Load `source_a`, then add `source_b`.
2. In source windows, select any 2-3 channels.
3. Switch to `v1` or `v2` (opposite of current).
4. Click `Refresh`.

Expected:

- Channel selection stays checked in source windows.
- Main chart shows the same selected set of channels.

### TC-04 Multi-folder refresh keeps overlay mode

1. Load `source_a` and `source_b`.
2. Enable compare overlay mode.
3. Select channels and note chart shape.
4. Switch active variant (`v1` <-> `v2`).
5. Click `Refresh`.

Expected:

- Compare overlay mode remains ON.
- Chart redraws with new data values.
- No forced mode reset unless overlay is unavailable.

### TC-05 Refresh with no folder selected

1. Start app with empty folder field.
2. Click `Refresh`.

Expected:

- User sees validation error (`SelectFolder`).
- App does not crash.

### TC-06 Refresh when one configured folder is missing

1. Load valid folder(s).
2. Rename one source folder in Explorer (temporarily).
3. Click `Refresh`.

Expected:

- Error is shown for missing directory.
- Previous app state remains stable.

### TC-07 Busy-state lock on controls during refresh

1. Load two folders.
2. Click `Refresh` and immediately try clicking `Browse/AddData/Refresh` again.

Expected:

- Busy overlay appears.
- `Browse`, `AddData`, `Refresh` are disabled during reload.
- Controls re-enable after completion.

### TC-08 Repeated refresh stability

1. Load two folders and select channels.
2. Click `Refresh` 3-5 times (switching `v1`/`v2` between clicks).

Expected:

- No exceptions or freezes.
- Selection remains stable.
- Chart always reflects active variant.

### TC-09 Close all then refresh

1. Load data.
2. Click `Close All`.
3. Click `Refresh`.

Expected:

- No crash.
- Proper validation message about missing folder/data.

### TC-10 Localization visibility

1. Switch language RU/EN.
2. Check Refresh button caption and tooltip.

Expected:

- RU: `Обновить` + RU tooltip.
- EN: `Refresh` + EN tooltip.

## Pass Criteria

- All test cases pass without crashes.
- Refresh reliably reloads data from current folders.
- Channel selection and chart mode are preserved as specified.

## Tail Closure Wave Baseline

Use this section as the manual verification checklist for the 2026-04-24 tail-closure wave.
This is a documentation baseline only. Fill in observed results after the corresponding runtime tasks land.

### Terminology

- `MainForm` is the primary attached-chart shell.
- `detached chart` is the separate chart window opened from the main UI.
- `workspace layout` means persisted UI state scoped to the current workspace/folder set.
- `protocol template mode` means the template-oriented protocol/export workflow already present in the app.

### Verification Matrix

| Area | Scenario status | Notes to verify after runtime work lands |
| --- | --- | --- |
| Explicit chart open policy | Pending implementation | Confirm when a chart should open automatically vs only after explicit user action. Cover both `MainForm` and detached chart flows. |
| Manual axis controls | Pending implementation | Cover attached chart and detached chart behavior for manual X/Y settings, reset-to-auto behavior, and redraw consistency. |
| Source-window order persistence | Pending implementation | Confirm workspace layout restores source-specific order only in the intended cases and does not override named order selections. |
| 4..6 source loading | Pending implementation | Confirm multi-source loading remains stable when the workspace contains four, five, or six sources. |
| Protocol template mode | Pending implementation | Re-run the protocol template workflow after this wave to confirm chart/layout changes do not regress template-specific behavior. |
| Remaining clean-architecture cleanup | Pending implementation | Confirm `MainForm` stays focused on UI wiring while orchestration paths move behind application/presentation services. |

### Manual Verification Sections To Complete

#### Explicit Chart Open Policy

Preparation:

- Reuse the standard refresh-suite fixtures unless a later runtime task requires a different dataset.
- Record whether the chart is already visible in `MainForm` before each action.

Checks to complete after implementation:

- Verify the attached chart open/refresh behavior for the default workspace flow.
- Verify whether opening a detached chart changes the attached-chart policy.
- Verify that no unintended chart window appears during workspace load, refresh, or source-window interactions.

#### Manual Axis Controls

Preparation:

- Use a dataset where axis changes are visually obvious.
- Exercise both attached-chart and detached-chart rendering paths.

Checks to complete after implementation:

- Verify manual X-axis inputs.
- Verify manual Y-axis inputs.
- Verify returning to automatic axis behavior.
- Verify redraw consistency after refresh, range change, and detached-chart reopen.

#### Source-Window Order Persistence

Preparation:

- Use at least two sources so source-window ordering is observable.
- Record whether a named order is selected before reordering.

Checks to complete after implementation:

- Verify persisted effective order for workspace-scoped source windows.
- Verify restore behavior when no named order is selected.
- Verify named order precedence when a named order is selected.
- Verify safe degradation if persisted order no longer matches the current channel set.

#### 4..6 Source Loading

Preparation:

- Assemble deterministic fixtures covering four, five, and six sources.
- Keep folder naming stable so workspace layout comparisons stay readable.

Checks to complete after implementation:

- Verify load success and UI responsiveness with four sources.
- Verify load success and UI responsiveness with five sources.
- Verify load success and UI responsiveness with six sources.
- Verify chart/source-window interactions still work after refresh and reopen operations.

#### Protocol Template Mode

Preparation:

- Start from a workspace and template configuration already known to work before this wave.

Checks to complete after implementation:

- Verify template-mode chart interactions still behave as expected.
- Verify export/protocol actions still use the intended template-mode state.
- Verify no workspace-layout or chart-policy change leaks into unrelated template-mode flows.

#### Remaining Clean-Architecture Cleanup

Preparation:

- Compare the observed UI behavior with the responsibilities documented in `doc/clean_architecture_migration_notes.md`.

Checks to complete after implementation:

- Verify user-visible behavior is unchanged where the wave only moves orchestration.
- Verify regressions are tracked as orchestration/presentation issues rather than undocumented `MainForm` behavior.
- Record any remaining manual checks that still depend on compatibility seams.

## Post-Migration Smoke Checks

Keep these checks in the same manual session when the related behavior is available:

1. Chart pipeline:
   - Select channels, change range, open detached chart, and confirm main/detached range bars stay synchronized.
   - In overlay mode, confirm X-axis title is overlay-specific and range labels show elapsed time.

2. Export pipeline:
   - Export an `.xlsx` after selecting a non-full range in normal mode.
   - Confirm file is created, validation warning does not appear on the happy path, and the file opens after save.

3. Settings workflow:
   - Open Styles dialog, enter invalid scale bounds, apply, reopen dialog, and confirm values are normalized rather than crashing the app.

4. Localization:
   - Switch RU/EN and confirm `Refresh`, chart context menu items, and Styles dialog captions all update consistently.

## Tail Closure Wave — Manual Verification Scenarios

### TC-AXIS-01 Manual X axis range

1. Load any test data and click **Show Chart**.
2. Enable manual X axis checkbox.
3. Enter Min/Max/Step values.
4. Click **Show Chart** or trigger redraw.
5. **Expected**: chart X axis shows the specified range; values outside range are clipped.
6. Detach the chart window. Verify the same axis settings are preserved in the detached window.

### TC-AXIS-02 Manual Y axis range

1. Load any test data and click **Show Chart**.
2. Enable manual Y axis checkbox, set Min/Max/Step.
3. **Expected**: chart Y axis respects the manual settings.
4. Disable the manual Y axis checkbox.
5. **Expected**: chart reverts to automatic Y scaling.

### TC-LAYOUT-01 Source window order persists across sessions

1. Load 2–3 source folders.
2. Drag-reorder channels in one of the source windows.
3. Close the app and reopen.
4. Reload the same folder combination.
5. **Expected**: source window shows the saved channel order (not alphabetical/protocol default).

### TC-LAYOUT-02 Named order overrides persisted effective order

1. Load data, select a named order from the order dropdown.
2. Close and reopen; reload same folders.
3. **Expected**: the named order is restored, not the drag-reorder from a previous session.

### TC-ORCH-01 4–6 source loading

1. Load 4, then 5, then 6 different source folders one at a time using **Add Data**.
2. **Expected**: each load succeeds without error or UI freeze.
3. Attempt to add a 7th folder.
4. **Expected**: error is shown, load is rejected.
