# Drilldown Dashboard Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a company-first drilldown dashboard with garage, driver, job type, and route-pair profitability while fixing missing truck assignment and defaulting save history to 14 days.

**Architecture:** Extend the existing statistics projection and SQLite medallion warehouse, then refactor the Terminal.Gui dashboard into a small state-driven drilldown builder. Keep bronze data reusable and rebuild silver/gold derived tables from bronze when projections or gold aggregates change.

**Tech Stack:** .NET 10, C#, xUnit, Terminal.Gui, Microsoft.Data.Sqlite.

---

### Task 1: Default History Window

**Files:**
- Modify: `src/AtsEmployeeStats.Console/Program.cs`
- Test: `tests/AtsEmployeeStats.Tests/CommandLineOptionsTests.cs`

**Step 1: Write the failing test**

Add a test proving `CommandLineOptions.Parse([]).HistoryDays` equals `14`.

**Step 2: Run test to verify it fails**

Run: `dotnet test AtsEmployeeStats.sln --filter CommandLineOptionsTests`

Expected: fail because the current default is `5`.

**Step 3: Write minimal implementation**

Change `CommandLineOptions.DefaultHistoryDays` from `5` to `14`.

**Step 4: Run test to verify it passes**

Run: `dotnet test AtsEmployeeStats.sln --filter CommandLineOptionsTests`

Expected: pass.

### Task 2: Truck Assignment Inference

**Files:**
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsProjectionTests.cs`

**Step 1: Write the failing test**

Create a save sample where:

- one garage has `drivers[0]: driver.alice`
- the same garage has `vehicles[0]: truck.alice`
- `driver.alice` has no `assigned_truck`

Assert the projected driver has `TruckId == "truck.alice"` and the projected truck has `DriverId == "driver.alice"`.

**Step 2: Run test to verify it fails**

Run: `dotnet test AtsEmployeeStats.sln --filter StatisticsProjectionTests`

Expected: fail because only driver scalar fields are currently used.

**Step 3: Write minimal implementation**

Build driver/truck maps from:

- driver scalar truck fields
- truck scalar driver fields
- garage driver/vehicle array index pairs

Merge in priority order without overwriting explicit driver assignments.

**Step 4: Run test to verify it passes**

Run: `dotnet test AtsEmployeeStats.sln --filter StatisticsProjectionTests`

Expected: pass.

### Task 3: Route-Pair Aggregation Model

**Files:**
- Modify: `src/AtsEmployeeStats.Domain/Statistics/StatisticsModels.cs`
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`
- Test: `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs`

**Step 1: Write the failing test**

Add two missions for the same driver:

- `phoenix -> denver` profit `3000`
- `denver -> phoenix` profit `2500`

Assert the warehouse produces one pair row for `Phoenix <-> Denver`, mission count `2`, and profit `5500`.

**Step 2: Run test to verify it fails**

Run: `dotnet test AtsEmployeeStats.sln --filter SqliteMedallionSaveSnapshotSourceTests`

Expected: fail because no route-pair gold table exists.

**Step 3: Write minimal implementation**

Add a route-pair statistic record or internal query row. Create and populate `gold_driver_job_pairs` during gold rebuild. Canonicalize city pairs case-insensitively by sorting the two endpoint labels for grouping, while keeping a display route like `Phoenix <-> Denver`.

**Step 4: Run test to verify it passes**

Run: `dotnet test AtsEmployeeStats.sln --filter SqliteMedallionSaveSnapshotSourceTests`

Expected: pass.

### Task 4: Drilldown Dashboard State

**Files:**
- Modify: `src/AtsEmployeeStats.Console/Program.cs`
- Test: `tests/AtsEmployeeStats.Tests/TerminalDashboardAppTests.cs`

**Step 1: Write the failing tests**

Add tests for:

- initial dashboard screen title is `Trucking Companies`
- garage screen for selected company has `Garage`, `Profit`, `$/Day`, `Drivers`, `Trucks`
- driver screen for selected garage excludes drivers from other garages
- driver job screen shows job type and route-pair summaries

**Step 2: Run tests to verify they fail**

Run: `dotnet test AtsEmployeeStats.sln --filter TerminalDashboardAppTests`

Expected: fail because the current dashboard is flat.

**Step 3: Write minimal implementation**

Introduce a small dashboard state object with:

- selected screen
- selected range days
- selected company ID
- selected garage ID
- selected driver ID

Update `TerminalGuiDashboard.BuildWindow` to render one screen at a time from that state. Keep compatibility overloads if existing tests need them.

**Step 4: Run tests to verify they pass**

Run: `dotnet test AtsEmployeeStats.sln --filter TerminalDashboardAppTests`

Expected: pass.

### Task 5: Terminal.Gui Navigation

**Files:**
- Modify: `src/AtsEmployeeStats.Console/Program.cs`
- Test: `tests/AtsEmployeeStats.Tests/TerminalDashboardAppTests.cs`

**Step 1: Write the failing test**

Exercise the dashboard navigation helpers or state transition methods:

- company selection transitions to garages
- garage selection transitions to drivers
- driver selection transitions to jobs
- range toggle updates dollars per day

**Step 2: Run test to verify it fails**

Run: `dotnet test AtsEmployeeStats.sln --filter TerminalDashboardAppTests`

Expected: fail because no transition helpers exist.

**Step 3: Write minimal implementation**

Wire `ListView` or `TableView` open/selection events to update dashboard state and rebuild the window. Add a compact range selector for `Last 14 days` and `Last 7 days`, defaulting to 14.

**Step 4: Run test to verify it passes**

Run: `dotnet test AtsEmployeeStats.sln --filter TerminalDashboardAppTests`

Expected: pass.

### Task 6: Final Verification

**Files:**
- Verify all changed files

**Step 1: Run focused tests**

Run:

```powershell
dotnet test AtsEmployeeStats.sln --filter "CommandLineOptionsTests|StatisticsProjectionTests|SqliteMedallionSaveSnapshotSourceTests|TerminalDashboardAppTests"
```

Expected: pass.

**Step 2: Run full suite**

Run:

```powershell
dotnet test AtsEmployeeStats.sln
```

Expected: pass.

**Step 3: Inspect worktree**

Run:

```powershell
git status --short
```

Expected: only intentional changes for the warehouse speed work, drilldown dashboard, tests, and docs.

