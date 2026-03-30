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

## Post-Migration Smoke Checks

After the Clean Architecture migration, also spot-check these flows in the same run:

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
