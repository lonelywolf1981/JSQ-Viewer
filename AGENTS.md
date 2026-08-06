# Repository Guidelines

## Project Structure & Module Organization
`JSQViewer.csproj` is the single .NET Framework 4.8 WinForms application entry point. `Program.cs` starts the UI. Keep domain and file-parsing logic in `Core/`, persistence and JSON-backed user settings in `Settings/`, export code in `Export/`, and WinForms screens/widgets in `UI/`. Database access lives in `Infrastructure/Database/`; the application ports `IRecordingCatalog` and `IRecordingDataReader` live in `Application/Workspace/Ports/`. Npgsql dependencies live in `lib/npgsql/` and must be referenced from the project with `HintPath`; the binding redirects in `App.config` are mandatory for deployed builds. Manual test fixtures live in `testdata/refresh_suite/`, helper scripts in `tools/refresh_suite/`, and supporting notes in `doc/`.

## Build, Test, and Development Commands
Use these commands from the repository root:

```powershell
dotnet build .\JSQViewer.csproj -c Debug
dotnet build .\JSQViewer.csproj -c Release
.\bin\Debug\JSQViewer.exe
python .\tools\refresh_suite\build_refresh_suite.py
python .\tools\refresh_suite\switch_refresh_variant.py --variant v2
```

The Debug build is the normal local workflow. Release verifies the production binaries and output, not installer packaging. Installer verification additionally requires staging the exact payload, building it with Inno Setup, and manually checking the installed payload. Run the generated EXE to test the WinForms app manually. The Python scripts regenerate deterministic DBF fixtures and switch active refresh-test variants.

## Coding Style & Naming Conventions
Follow the existing C# style: 4-space indentation, braces on their own lines, `PascalCase` for types and public members, `_camelCase` for private fields, and explicit access modifiers. Match the current file placement: UI event handlers stay in `UI/`, stateful data logic stays in `Core/`, and persistence helpers stay in `Settings/`. Prefer small, focused methods over adding more logic to `MainForm`.

## Testing Guidelines
The automated test suite lives at `JSQViewer.Tests/JSQViewer.Tests.csproj`; run it with `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`. Validate changes with a successful `dotnet build` plus targeted manual checks. Database tests must use fakes and make no network calls; live database and package validation are manual and follow `doc/database_source_manual_checklist.md`. For refresh behavior, follow `doc/refresh_button_test_suite.md` and use `testdata/refresh_suite/` fixtures. Name new test notes after the feature under test, and keep deterministic sample data under `testdata/` with a short README when the setup is non-obvious.

## Commit & Pull Request Guidelines
Recent commits use short, imperative Russian summaries such as `Исправлена обрезка длительности серий...`. Keep commit messages concise, specific, and scoped to one change. Pull requests should include: what changed, why it changed, how it was validated (`dotnet build`, manual scenarios, scripts used), and screenshots for visible UI updates. Link the related issue when one exists.

## Configuration & Data Files
Do not commit machine-specific output from `bin/`, `obj/`, or ad hoc local data folders. Treat generated JSON settings and refresh-suite fixture changes as intentional only when they are part of the feature being reviewed.
