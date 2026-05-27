# Save Data Silver Enrichment Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enrich silver/gold statistics with validated driver, truck, and recent-job data and show the latest four driver jobs in the Blazor UI.

**Architecture:** Keep bronze SII units as the raw source. Extend statistics projection to normalize pseudo-null values, derive truck display metadata from vehicle/accessory data, extract driver recent jobs from profit logs, persist the enriched data into silver/gold, expose it through DTOs, and render it in driver detail.

**Tech Stack:** C#/.NET 10, SQLite via Microsoft.Data.Sqlite, Blazor WebAssembly, xUnit.

---

### Task 1: Projection Tests For Normalization And Enrichment

**Files:**
- Modify: `tests/AtsEmployeeStats.Tests/StatisticsProjectionTests.cs`
- Modify: `src/AtsEmployeeStats.Domain/Statistics/StatisticsModels.cs`
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs`

**Step 1: Write failing tests**

Add tests asserting:

- literal `null`, `nil`, and empty SII values do not become truck ids or display labels.
- a driver profit log with `stats_data` entries creates recent driver jobs ordered by `timestamp_day` descending.
- a vehicle with a base accessory `/def/vehicle/truck/kenworth.t680/data.sii` has model name `Kenworth T680`.
- a marked-up license plate such as `<color value=FF000000> PA76356|montana` becomes `PA76356 Montana`.

**Step 2: Run failing tests**

Run: `dotnet test --filter StatisticsProjectionTests -p:BaseOutputPath=artifacts\testbin\silver-red\`

Expected: FAIL because domain/projection fields do not exist or values are not enriched.

**Step 3: Implement minimal projection changes**

Add `DriverRecentJobStatistic`, extend `CompanyStatistics`, `TruckStatistic`, and `MissionStatistic`, then implement helper methods:

- `CleanSiiValue`
- `CleanLicensePlate`
- `ExtractTruckDefinitionPath`
- `FormatTruckModelName`
- `BuildDriverRecentJobs`

**Step 4: Run tests**

Run: `dotnet test --filter StatisticsProjectionTests -p:BaseOutputPath=artifacts\testbin\silver-green1\`

Expected: PASS.

### Task 2: SQLite Silver/Gold Persistence

**Files:**
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`
- Modify: `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs`

**Step 1: Write failing tests**

Add a test save with driver profit log entries and vehicle accessory data. Assert:

- `silver_trucks` contains cleaned license plate, model name, and definition path.
- `silver_driver_recent_jobs` contains recent rows for the driver.
- `gold_driver_recent_jobs` returns the latest four rows by timestamp day.
- no `truck_id` field contains literal `null`.

**Step 2: Run failing tests**

Run: `dotnet test --filter SqliteMedallionSaveSnapshotSourceTests -p:BaseOutputPath=artifacts\testbin\sqlite-red\`

Expected: FAIL because schema/persistence do not include the new data.

**Step 3: Implement schema and persistence**

Extend schema creation and migration with new columns/tables. Add delete/rebuild handling for the new tables. Persist and read recent jobs and truck metadata.

**Step 4: Run tests**

Run: `dotnet test --filter SqliteMedallionSaveSnapshotSourceTests -p:BaseOutputPath=artifacts\testbin\sqlite-green\`

Expected: PASS.

### Task 3: Contracts And Dashboard Mapper

**Files:**
- Modify: `src/AtsEmployeeStats.Contracts/StatisticsDtos.cs`
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs`
- Modify: `tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs`

**Step 1: Write failing tests**

Assert DTOs expose truck metadata, mission timestamp day, and recent driver jobs with per-driver latest-four ordering preserved from statistics.

**Step 2: Run failing tests**

Run: `dotnet test --filter StatisticsDashboardMapperTests -p:BaseOutputPath=artifacts\testbin\dto-red\`

Expected: FAIL because DTO fields do not exist.

**Step 3: Implement mapper changes**

Extend DTO records and map the new domain fields.

**Step 4: Run tests**

Run: `dotnet test --filter StatisticsDashboardMapperTests -p:BaseOutputPath=artifacts\testbin\dto-green\`

Expected: PASS.

### Task 4: Blazor UI For Recent Jobs And Truck Names

**Files:**
- Modify: `src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs`
- Modify: `tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs`
- Modify: `src/AtsEmployeeStats.Web/Pages/CompanyDetail.razor`
- Modify: `src/AtsEmployeeStats.Web/Pages/GarageDetail.razor`
- Modify: `src/AtsEmployeeStats.Web/Pages/DriverDetail.razor`

**Step 1: Write failing helper tests**

Add tests for:

- `DashboardViewModel.GetRecentDriverJobs(company, driverId)` returns the latest four rows by timestamp day.
- truck display labels prefer model and plate over id.

**Step 2: Run failing tests**

Run: `dotnet test --filter DashboardViewModelTests -p:BaseOutputPath=artifacts\testbin\ui-red\`

Expected: FAIL because helper methods do not exist.

**Step 3: Implement helper and UI changes**

Render:

- recent jobs table in driver detail.
- truck display names in company, garage, and driver truck tables.
- current truck labels using truck metadata when available.

**Step 4: Build**

Run: `dotnet build -p:BaseOutputPath=artifacts\build\silver-ui\`

Expected: PASS.

### Task 5: Verification And Real Warehouse Spot Check

**Files:**
- No required file changes.

**Step 1: Run full tests**

Run: `dotnet test -p:BaseOutputPath=artifacts\testbin\silver-full\`

Expected: PASS.

**Step 2: Run real data load or query check**

Run the app or statistics load against the default warehouse/save root. Verify with SQLite queries that:

- `silver_driver_recent_jobs` has rows.
- `silver_trucks.model_name` is populated for owned trucks with base vehicle accessory paths.
- `silver_drivers.truck_id` and `silver_trucks.driver_id` do not contain literal `null`.

**Step 3: Manual UI smoke**

Run the API on a spare port and verify `/companies/.../drivers/...` shows recent jobs and enriched truck labels.
