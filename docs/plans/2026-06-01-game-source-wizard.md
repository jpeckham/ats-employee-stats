# Game Source Wizard Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the raw source path editor with a guided first-run ATS/ETS2 source wizard that validates install and save paths, supports multiple save locations per game, and persists source choices in SQLite.

**Architecture:** Add application-level discovery/validation models that distinguish install candidates from save-root candidates and expose proof strings for the UI. Persist selected source configuration through a SQLite settings store instead of JSON, while keeping the dynamic save source loader reading the latest validated selections. WPF owns wizard state and presentation; application/infrastructure owns validation, discovery, and persistence.

**Tech Stack:** .NET/WPF, CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, xUnit.

---

### Task 1: SQLite Source Settings Store

**Files:**
- Modify: `src/AtsEmployeeStats.Application/Saves/GameSourceManagement.cs`
- Create: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteGameSourceSettingsStore.cs`
- Test: `tests/AtsEmployeeStats.Tests/LocalGameSourceManagementTests.cs`

**Steps:**
1. Add failing tests proving source settings round-trip through SQLite and expose whether the wizard has been completed.
2. Run the focused tests and verify failure because the SQLite store does not exist.
3. Implement a SQLite table for source settings with game, enabled, install path, profile path, save path, and completion metadata.
4. Run focused tests and verify they pass.

### Task 2: Discovery and Validation Candidates

**Files:**
- Modify: `src/AtsEmployeeStats.Application/Saves/GameSourceManagement.cs`
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/LocalGameSourceDiscovery.cs`
- Test: `tests/AtsEmployeeStats.Tests/GameSourceManagementTests.cs`
- Test: `tests/AtsEmployeeStats.Tests/LocalGameSourceManagementTests.cs`

**Steps:**
1. Add failing tests for install candidate proof, multiple save roots, valid/invalid save-root validation, and blocking invalid enabled sources.
2. Run the focused tests and verify expected failures.
3. Add candidate records, validation records, and use-case methods for wizard discovery and validated save.
4. Update local discovery to return all plausible `profiles` and `steam_profiles` roots with save counts/proof.
5. Run focused tests and verify they pass.

### Task 3: WPF Wizard View Model

**Files:**
- Modify: `src/AtsEmployeeStats.Wpf/ViewModels/MainWindowViewModel.cs`
- Test: `tests/AtsEmployeeStats.Tests/WpfPresentationMigrationTests.cs`

**Steps:**
1. Add failing tests for first-run wizard visibility, per-game step flow, selected save roots, and validation status text.
2. Run the focused tests and verify expected failures.
3. Add wizard state view models and commands without changing persistence behavior beyond the application use case.
4. Run focused tests and verify they pass.

### Task 4: WPF Wizard UI

**Files:**
- Modify: `src/AtsEmployeeStats.Wpf/MainWindow.xaml`
- Modify: `src/AtsEmployeeStats.Wpf/App.xaml.cs`
- Test: `tests/AtsEmployeeStats.Tests/WpfPresentationMigrationTests.cs`

**Steps:**
1. Add failing presentation tests that the raw three-textbox editor is gone, the wizard overlay exists, and the main UI has a compact source summary.
2. Run focused tests and verify expected failures.
3. Replace inline source editor with a wizard panel and compact source summary.
4. Register the SQLite settings store.
5. Run focused tests and verify they pass.

### Task 5: Final Verification

**Files:**
- All touched source and test files.

**Steps:**
1. Run focused tests for source management, save catalog, WPF migration, and SQLite save source.
2. Run `dotnet build AtsEmployeeStats.sln`.
3. Run `dotnet test AtsEmployeeStats.sln`.
4. Report any existing warnings separately from new failures.
