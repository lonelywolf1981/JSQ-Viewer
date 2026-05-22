# Exported Protocol Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let JSQ Viewer open exported `.xlsx` protocol files as normal data sources and mix them with existing `dat/dbf` recordings.

**Architecture:** Add an XLSX protocol reader that returns `TestData`, then extend workspace loading so source specs can point either to a data folder or an exported protocol file. Keep merging in `MergeLoadedSourcesUseCase` and make WinForms browse/add paths able to select either folders or `.xlsx` files.

**Tech Stack:** .NET Framework 4.8, WinForms, MSTest, OpenXML package reading through `System.IO.Packaging`/XML APIs already referenced by the project.

---

### Task 1: XLSX Reader

**Files:**
- Create: `Infrastructure/DataImport/ExportedProtocolDataSourceReader.cs`
- Test: `JSQViewer.Tests/ExportedProtocolDataSourceReaderTests.cs`

- [ ] **Step 1: Write failing tests**

Test the sample file `06.03.26 FORCE KA50 90G FULL 4040.xlsx`:

- reader returns `RowCount > 0`;
- fixed channel `Pc` exists;
- an extra channel from column `Z` exists with a unit parsed from `[°C]`;
- `Root`, `CodeSources`, `SourceColumns`, `SourceStartMs`, `SourceEndMs` point to the `.xlsx`;
- timestamps are monotonic.

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter ExportedProtocolDataSourceReaderTests
```

Expected: FAIL because `ExportedProtocolDataSourceReader` does not exist.

- [ ] **Step 3: Implement minimal reader**

Implement direct OpenXML ZIP/XML reading:

- load shared strings;
- stream `sheet1.xml`;
- read row 1 metadata, row 3 headers, rows 4+ values;
- map fixed columns `D:T` to current template codes;
- read extras from `Z` onward using header text as code when no original code exists;
- convert Excel day fractions to milliseconds;
- build sorted `TestData`.

- [ ] **Step 4: Run tests and verify GREEN**

Run the same filtered test and fix until it passes.

### Task 2: Workspace Loading Integration

**Files:**
- Modify: `Application/Workspace/UseCases/LoadWorkspaceDataUseCase.cs`
- Modify: `Infrastructure/Composition/WorkspaceLoadingComposition.cs`
- Test: `JSQViewer.Tests/WorkspaceLoadingTests.cs`

- [ ] **Step 1: Write failing tests**

Add a fake XLSX reader test showing:

- `LoadWorkspaceDataUseCase` loads a single `.xlsx` path without asking folder readers;
- it merges a folder source and `.xlsx` source through existing merge logic.

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter WorkspaceLoadingTests
```

Expected: FAIL because `LoadWorkspaceDataUseCase` only accepts folders.

- [ ] **Step 3: Implement source-type branching**

Add an optional `ITestDataSourceReader` for exported protocols. If a parsed source path ends with `.xlsx` and exists as a file, read it through the XLSX reader. Otherwise keep the existing folder resolution and DBF path.

- [ ] **Step 4: Run tests and verify GREEN**

Run the filtered workspace tests.

### Task 3: UI Source Selection

**Files:**
- Modify: `Application/Workspace/WorkspaceLoadOrchestrationService.cs`
- Modify: `UI/MainForm.cs`
- Modify: `Infrastructure/Platform/DictionaryLocalizationService.cs`
- Test: `JSQViewer.Tests/WorkspaceLoadOrchestrationServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Update orchestration validation tests so `.xlsx` paths are valid when `FileExists` returns true, while missing files remain invalid.

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter WorkspaceLoadOrchestrationServiceTests
```

Expected: FAIL because validation accepts directories only.

- [ ] **Step 3: Implement UI and validation changes**

Allow valid specs to contain existing directories or `.xlsx` files. Replace folder-only browse/add selection with a small dialog that offers folder selection or `.xlsx` file selection. Update tooltips and error text to mention files.

- [ ] **Step 4: Run tests and verify GREEN**

Run the filtered orchestration tests.

### Task 4: Full Verification

**Files:**
- No new code unless verification finds defects.

- [ ] **Step 1: Run full test suite**

```powershell
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj
```

- [ ] **Step 2: Run Debug build**

```powershell
dotnet build .\JSQViewer.csproj -c Debug
```

- [ ] **Step 3: Review git diff**

Confirm only intended files changed and no unrelated untracked files were added.

