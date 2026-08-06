# PostgreSQL Recording Names And Compare Manual Checklist

Automated tests cover the pure display-name resolution and the source-addition policy. Behaviour that depends on a live PostgreSQL database and on real WinForms windows is manual and must be verified with this checklist. Record every item that could not be executed instead of marking it as passed.

## Preparation

1. Confirm that a live test database is reachable and that at least three recordings exist: two with distinct titles, and two more that share the same title (case-insensitively).
2. Confirm that at least one recording has a blank or whitespace-only title.
3. Have one DBF measurement folder and one exported XLSX protocol available for the mixed-workspace checks.

## Single database recording

4. Load a single recording through **Из БД…** and confirm that the main chart window caption shows the recording title, not `jsqdb://recording/<id>`.
5. Confirm that the source channel window caption shows the same title.
6. Load the recording whose title is blank and confirm that the caption falls back to the recording identifier.
7. Open the recording info dialog and confirm that the technical identifiers are still shown there unchanged.

## Adding recordings for comparison

8. With one recording loaded, press **Из БД…** again, select a second recording, and confirm that the first recording remains in the workspace and both appear in the source list.
9. Confirm that the chart legend prefixes series with the resolved titles and that both source windows are present.
10. Add the two recordings that share the same title and confirm that both windows and both legend prefixes are disambiguated as `Название [recordingId]`.
11. Select only recordings that are already loaded and confirm that the workspace is not reloaded and that the «источник уже добавлен» message is shown.
12. With five sources loaded, select one already-loaded recording plus one new recording, and confirm that the workspace loads with exactly six sources.
13. With five sources loaded, select two new recordings and confirm that the operation is rejected as a whole with the «слишком много источников» message and that no source was added.
14. With six sources loaded, press **Из БД…** and confirm that the limit message appears immediately without opening the selection dialog.

## Live refresh and rollback

15. Load a single active recording, confirm live refresh is running, then add a second source and confirm that live refresh stops and does not restart for the multi-source workspace.
16. Provoke a failed load of a second source (for example, stop the database before confirming the selection) and confirm that the source textbox, the loaded data, and the live-refresh timer are restored to their previous state.

## Files and localization

17. Load a DBF folder and an exported XLSX protocol and confirm that their captions still show the folder/file name and are not affected by any database title metadata.
18. In a mixed workspace, confirm that the source order in the channel windows matches the order of the sources in the source specification.
19. Switch the interface language and confirm that the main chart window, every detached chart window, and every source window caption are re-localized while still showing the resolved titles rather than recording identifiers.
