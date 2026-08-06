# Database Source Manual Checklist

Automated database tests use fakes and make no network calls. Checks against a live PostgreSQL database are manual and must be performed with this checklist.

## Preparation

1. Confirm that a live test database is reachable at the default endpoint `192.168.66.100:5432` and contains database `jsq_db` for user `jsq_user`.
2. Open the database connection settings and verify the defaults: host `192.168.66.100`, port `5432`, database `jsq_db`, and user `jsq_user`.
3. Enter a valid password, click **Test Connection**, expect the **Connected** result, then click **OK** to save the settings automatically.
4. Restart JSQ Viewer, confirm that the password persisted, and reconnect without entering it again.

## Historical recording

5. Open the database recording source and confirm that the historical recording list loads from the live database.
6. Apply the available post, product, and date filters and confirm that every displayed recording matches all selected filters.
7. Select a historical recording and load it into the workspace.
8. Confirm that channel and status codes are normalized and displayed consistently with equivalent DBF data.
9. Compare the loaded series with its source data and confirm that sample gaps remain gaps rather than being silently filled or shifted.
10. Open the recording metadata and confirm that identifiers, post, product, start time, and duration describe the selected recording.
11. Export the historical recording and verify that the exported values, timestamps, channel names, and metadata match the loaded recording.

## Active recording

12. Load an active recording and confirm that it is shown in bold in the recording list.
13. Leave the recording open for three refresh intervals and confirm after each interval that newly committed samples appear without duplicating existing samples.
14. Disconnect the database network for 40 seconds and confirm that the workspace remains usable while the recording reports a lost connection.
15. Restore the network and confirm that the lost-connection indication clears and refresh resumes from the last loaded sample without duplicates or reordered data.
16. Mark or allow the recording to finish and confirm that automatic refresh stops after the completed tail is loaded.

## Mixed workspace

17. Load one DBF recording and one database recording into the same workspace and confirm that both remain independently selectable and render correctly.
18. Load database recordings from two different posts and confirm that their post identities and data do not become mixed.
19. Click **Refresh** and confirm that the DBF source, both database posts, selections, and visible series all remain correct.

## Package verification

20. Install the packaged application on a non-development machine without Visual Studio, the repository, or locally registered development dependencies, then start it from the installed shortcut.
21. Repeat connection, list, load, metadata, and export checks on the installed application. A `FileLoadException` mentioning `System.Runtime.CompilerServices.Unsafe` means that the package is missing the mandatory `JSQViewer.exe.config` binding redirects; correct the package before release.
