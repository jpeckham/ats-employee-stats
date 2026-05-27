# Dashboard Navigation Clarity Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make the Terminal.Gui drilldown dashboard obvious to navigate by showing location, back paths, and explicit open actions.

**Architecture:** Keep the existing `DrilldownDashboardState` state machine and `TerminalGuiDashboard.BuildWindow` factory. Add small UI helpers in `Program.cs` for breadcrumbs, selected entity labels, back transitions, and action buttons; test by inspecting the built Terminal.Gui view tree.

**Tech Stack:** C#/.NET 10, Terminal.Gui, xUnit.

---

### Task 1: Location And Back State

**Files:**
- Modify: `tests/AtsEmployeeStats.Tests/TerminalDashboardAppTests.cs`
- Modify: `src/AtsEmployeeStats.Console/Program.cs`

**Step 1: Write failing tests**

Add tests that assert:
- the garage screen shows `Location: Desert Line`
- the driver screen shows `Location: Desert Line > phoenix`
- the driver jobs screen shows `Location: Desert Line > phoenix > Alice Ramirez`
- the garage, driver, and driver jobs screens include a `Back` button
- `DrilldownDashboardState.Back()` moves jobs to drivers, drivers to garages, garages to companies

**Step 2: Run targeted tests to verify failure**

Run: `dotnet test --filter TerminalDashboardAppTests`

Expected: FAIL because location labels, `Back`, and `Back()` do not exist.

**Step 3: Implement minimal production code**

Add:
- `DrilldownDashboardState.Back()`
- `BuildLocationText(...)`
- back button on non-company screens
- context-aware table titles, including selected garage on drivers

**Step 4: Run targeted tests**

Run: `dotnet test --filter TerminalDashboardAppTests`

Expected: PASS.

### Task 2: Explicit Open Actions

**Files:**
- Modify: `tests/AtsEmployeeStats.Tests/TerminalDashboardAppTests.cs`
- Modify: `src/AtsEmployeeStats.Console/Program.cs`

**Step 1: Write failing tests**

Add tests that assert:
- company screen includes `Open Garages`
- garage screen includes `Open Drivers`
- driver screen includes `Open Jobs`

**Step 2: Run targeted tests to verify failure**

Run: `dotnet test --filter TerminalDashboardAppTests`

Expected: FAIL because action buttons do not exist.

**Step 3: Implement minimal production code**

Add action buttons near the active table. Buttons should open the currently selected row when possible, and table selection-change navigation remains as a secondary shortcut.

**Step 4: Verify**

Run:
- `dotnet test --filter TerminalDashboardAppTests`
- `dotnet test`

Expected: all tests pass.
