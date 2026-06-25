# Driver Profit Distance Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a sortable driver-table profit-per-distance column with ATS miles and ETS2 kilometers.

**Architecture:** Keep the calculation in WPF row projection. `Rows.Driver(company, driver)` already has `CompanyDto`, `DriverDto`, and recent job distances, so no contract or database change is needed.

**Tech Stack:** C# 13, .NET 10, WPF, xUnit.

---

### Task 1: Driver Row Profit Distance

**Files:**
- Modify: `src/AtsEmployeeStats.Wpf/ViewModels/Rows.cs`
- Modify: `src/AtsEmployeeStats.Wpf/ViewModels/DetailViewModels.cs`
- Test: `tests/AtsEmployeeStats.Tests/WpfPresentationMigrationTests.cs`

**Step 1: Write the failing test**

Add a test asserting:
- Drivers columns include `Profit/Dist`.
- `Rows.Driver` returns `$5.00/mi` for ATS-style company IDs.
- `Rows.Driver` returns `€5.00/km` for `ets2-*` company IDs.
- Missing distance returns `-`.
- Sort values are numeric.

**Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter Wpf_driver_rows_show_profit_per_distance
```

Expected: FAIL because column and row fields do not exist yet.

**Step 3: Write minimal implementation**

Add `ProfitPerDistance` and `ProfitPerDistanceSort` to `GridRowViewModel`, add a `Profit/Dist` column to `TableColumns.Drivers`, and compute formatted value in `Rows.Driver`.

**Step 4: Run focused test**

Run same command. Expected: PASS.

**Step 5: Run wider verification**

Run:

```powershell
dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj
```

Expected: PASS.
