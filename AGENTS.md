# AGENTS.md

Guidance for coding agents working in `JSQViewer`.

## 1) Project Snapshot

- Stack: C# WinForms, classic `.csproj` (non-SDK style).
- Target framework: `.NET Framework v4.8` (`JSQViewer.csproj`).
- Entry point: `Program.cs`.
- Main modules compiled by project:
  - `Core/*.cs`
  - `Settings/*.cs`
  - `Export/*.cs`
  - `UI/*.cs`
- App type: desktop executable (`WinExe`).
- Main output (Debug): `bin/Debug/JSQViewer.exe`.

## 2) Build / Lint / Test Commands

Run commands from repository root: `C:\Users\a.baidenko\Downloads\JSQViewer`.

### Build

- Preferred cross-machine build:
  - `dotnet build "JSQViewer.csproj" -c Debug`
- Release build:
  - `dotnet build "JSQViewer.csproj" -c Release`
- Rebuild:
  - `dotnet build "JSQViewer.csproj" -c Debug -t:Rebuild`
- Legacy script (Windows-specific absolute path):
  - `build.bat`

### Run

- Build then run executable directly:
  - `bin\Debug\JSQViewer.exe`

### Lint / Formatting

- No dedicated lint config or formatter config is present (`.editorconfig`, StyleCop, Roslyn rulesets not found).
- Use build as baseline quality gate:
  - `dotnet build "JSQViewer.csproj" -c Debug`
- Optional stricter local check when needed:
  - `dotnet build "JSQViewer.csproj" -c Debug -p:TreatWarningsAsErrors=true`

### Tests

- There is currently no separate test project in this repository.
- `dotnet test "JSQViewer.csproj"` does not execute real unit tests.

If a test project is added later, use:

- Run all tests:
  - `dotnet test <TestsProject>.csproj -c Debug`
- Run a single test by fully qualified name:
  - `dotnet test <TestsProject>.csproj --filter "FullyQualifiedName=Namespace.ClassName.MethodName"`
- Run tests for one class:
  - `dotnet test <TestsProject>.csproj --filter "FullyQualifiedName~Namespace.ClassName"`
- Run tests by method substring:
  - `dotnet test <TestsProject>.csproj --filter "Name~MethodPart"`

## 3) Repository-Specific Conventions

### File and module boundaries

- Keep parsing/data operations in `Core/`.
- Keep persistence and JSON stores in `Settings/`.
- Keep template/export logic in `Export/`.
- Keep WinForms controls/forms/localization in `UI/`.
- Avoid pushing UI-specific behavior into `Core` unless it is pure data logic.

### Imports (`using`) conventions

- Place `using` directives at top of file.
- Order: BCL namespaces first (`System*`), then project namespaces (`JSQViewer.*`).
- Prefer one `using` per line.
- Remove unused usings.

### Naming conventions

- Types/methods/properties/events: `PascalCase`.
- Private fields: `_camelCase` (very common in this repo, especially UI code).
- Private static readonly fields: `PascalCase` or `_camelCase` exists; preserve local file style.
- Constants: `PascalCase` (e.g., `MaxEntries`).
- Local variables/parameters: `camelCase`.
- Keep externally persisted JSON schema property names unchanged when already snake_case
  (e.g., `row_mark`, `threshold_T`, `discharge_mark`).

### Formatting style

- Use 4 spaces indentation, braces on new lines (Allman style).
- Keep line lengths readable; split complex conditions and constructor args.
- Prefer explicit blocks for non-trivial `if/for/foreach` bodies.
- Preserve existing CRLF/newline style in touched files.

### Type usage

- Codebase is pre-nullable-reference-types style; add defensive null checks explicitly.
- Prefer explicit types when they improve clarity in non-obvious logic.
- `var` is acceptable when right-hand type is obvious.
- Use `StringComparer.OrdinalIgnoreCase` for case-insensitive dictionaries/sets storing channel codes/keys.
- Use `CultureInfo.InvariantCulture` for parsing/formatting persisted numeric/text protocol values.

### Error handling and diagnostics

- For recoverable UI actions: catch, log with `AppLogger.LogError(...)`, and show user-facing feedback.
- For core invariants or missing required data/files: throw specific exceptions (`InvalidDataException`, `FileNotFoundException`, etc.).
- Do not silently swallow exceptions in new code unless there is a strong UX reason.
- If swallowing is required, add at least debug/log trace.
- Keep message text actionable and concise.

### Logging

- Use `AppLogger` for application-level operational/error logging.
- Provide contextual message text that includes operation intent.
- Prefer logging at boundaries (load/export/save) rather than inside tight loops.

### Threading and UI safety

- WinForms controls must be accessed on UI thread.
- Heavy I/O/CPU work should avoid blocking UI thread where practical.
- Shared mutable state should be protected (`lock`, immutable snapshots, or existing cache/state patterns).
- Follow existing synchronization patterns in `AppState` and `SeriesCache`.

### Data/time and parsing rules

- Keep timestamp conversions consistent with existing `Unix ms` handling.
- Maintain DBF/metadata parsing compatibility with current data sources.
- Preserve encoding fallback behavior in file parsers unless explicitly changing requirements.

### Localization and UI text

- User-visible strings should go through `Loc.Get("...")` keys.
- When adding a new key, add both RU and EN entries in `UI/Localization.cs`.
- Keep key names stable and descriptive.

### Persistence and files

- JSON persistence should go through `JsonHelper` / `Persistence` / store classes.
- Use UTF-8 for text files unless file format dictates otherwise.
- Avoid changing persisted file names/locations without migration plan.

## 4) Rules Files Check (Cursor/Copilot)

- `.cursorrules`: not found.
- `.cursor/rules/`: not found.
- `.github/copilot-instructions.md`: not found.

No additional Cursor/Copilot repository rules are currently defined.

## 5) Practical Agent Workflow

- Before edits, inspect touched module for local style and naming choices.
- Make minimal, targeted changes; avoid broad refactors unless requested.
- Build after changes with `dotnet build "JSQViewer.csproj" -c Debug`.
- If adding tests in future, document exact single-test command in PR/notes.
- Do not alter JSON schema keys consumed by existing saved files unless migration is included.

## 6) Additional Agent Directives

- Always respond to the user in Russian.
- After any code/config/documentation change, create a commit in the current branch.
- Use Russian commit messages.
- Do not merge branches unless the user gives an explicit direct instruction to merge.
- Use the Context7 MCP server when external library/framework documentation is needed.
