# Cities And Jobs Analytics Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement the cities, jobs, routes, trailers, trends, and drill-down architecture described in `docs/plans/2026-05-27-cities-and-jobs-architecture.md`.

**Architecture:** Build this in vertical slices. First make cities, directed routes, trailers, and compact trend read models real in domain/DTO/projection/SQLite. Then expose route-backed API/UI surfaces for jobs, cities, trailers, trucks, and routes. Finally add chart-ready trend aggregates, sparklines, assignment history, and anonymized export contracts.

**Tech Stack:** .NET 10, xUnit, Microsoft.Data.Sqlite, Blazor WebAssembly, existing ATS SII parser/projection.

---

### Task 1: City, Route, Trailer, And Trend Domain Read Models

**Files:**
- Modify: `src/AtsEmployeeStats.Domain/Statistics/StatisticsModels.cs`
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs`
- Modify: `tests/AtsEmployeeStats.Tests/StatisticsProjectionTests.cs`

**Step 1: Write failing projection tests**

Add tests proving:

- every mission origin/destination creates a city
- cities aggregate visit count, inbound profit, outbound profit, bidirectional profit, and owned-garage flag
- directed routes aggregate job count and profit
- trailers are modeled from trailer units and job references
- company/driver/garage/truck/city profit trend points are grouped by `TimestampDay`

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter StatisticsProjectionTests -p:BaseOutputPath=artifacts\testbin\cities-domain-red\`

Expected: FAIL because the new domain records and properties do not exist.

**Step 2: Implement minimal domain records and projection**

Add:

- `CityStatistic`
- `RouteStatistic`
- `TrailerStatistic`
- `TrendPointStatistic`

Extend `CompanyStatistics` with `Trailers`, `Cities`, `Routes`, and `ProfitTrends`.

**Step 3: Run targeted tests**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter StatisticsProjectionTests -p:BaseOutputPath=artifacts\testbin\cities-domain-green\`

Expected: PASS.

### Task 2: Contracts And Dashboard Mapper

**Files:**
- Modify: `src/AtsEmployeeStats.Contracts/StatisticsDtos.cs`
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs`
- Modify: `tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs`

**Step 1: Write failing mapper tests**

Assert `CompanyDto` includes `Trailers`, `Cities`, `Routes`, and compact `SparklineDto` trend data with the requested 7/14 day window.

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter StatisticsDashboardMapperTests -p:BaseOutputPath=artifacts\testbin\cities-dto-red\`

Expected: FAIL.

**Step 2: Implement DTOs and mapping**

Add DTOs:

- `EntityTrendPointDto`
- `SparklineDto`
- `TrailerDto`
- `CityDto`
- `RouteDto`

Map domain read models into company payloads without removing existing fields.

**Step 3: Run targeted tests**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter StatisticsDashboardMapperTests -p:BaseOutputPath=artifacts\testbin\cities-dto-green\`

Expected: PASS.

### Task 3: SQLite Silver And Gold Persistence

**Files:**
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`
- Modify: `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs`

**Step 1: Write failing persistence tests**

Assert the SQLite source persists and reads:

- `silver_cities`
- `silver_routes`
- `silver_trailers`
- `gold_city_profitability`
- `gold_route_profitability`
- `gold_*_profit_trend` rows for 7 and 14 day windows

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter SqliteMedallionSaveSnapshotSourceTests -p:BaseOutputPath=artifacts\testbin\cities-sqlite-red\`

Expected: FAIL.

**Step 2: Implement schema and persistence**

Create tables if missing, delete/rebuild derived silver/gold rows during `PersistSilverAndGoldAsync`, and read gold rows back into `AtsStatistics`.

**Step 3: Run targeted tests**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter SqliteMedallionSaveSnapshotSourceTests -p:BaseOutputPath=artifacts\testbin\cities-sqlite-green\`

Expected: PASS.

### Task 4: API Query Endpoints

**Files:**
- Modify: `src/AtsEmployeeStats.Api/Program.cs`
- Modify: `tests/AtsEmployeeStats.Tests/StatisticsApiTests.cs`

**Step 1: Write failing API tests**

Add tests for:

- `GET /api/companies`
- `GET /api/companies/{companyId}`
- `GET /api/companies/{companyId}/drivers/{driverId}`
- `GET /api/companies/{companyId}/garages/{garageId}`
- `GET /api/companies/{companyId}/trucks/{truckId}`
- `GET /api/companies/{companyId}/trailers/{trailerId}`
- `GET /api/companies/{companyId}/jobs/{jobId}`
- `GET /api/companies/{companyId}/cities/{cityId}`
- `GET /api/companies/{companyId}/routes/{originCityId}/{destinationCityId}`

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter StatisticsApiTests -p:BaseOutputPath=artifacts\testbin\cities-api-red\`

Expected: FAIL.

**Step 2: Implement endpoints**

Use the existing `StatisticsService` and mapper payload first. Return `404` for missing ids and honor `rangeDays`.

**Step 3: Run targeted tests**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter StatisticsApiTests -p:BaseOutputPath=artifacts\testbin\cities-api-green\`

Expected: PASS.

### Task 5: Blazor Drill-Down UI

**Files:**
- Modify: `src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs`
- Modify: `tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs`
- Modify: `src/AtsEmployeeStats.Web/Pages/CompanyDetail.razor`
- Create: `src/AtsEmployeeStats.Web/Pages/JobDetail.razor`
- Create: `src/AtsEmployeeStats.Web/Pages/CityDetail.razor`
- Create: `src/AtsEmployeeStats.Web/Pages/TruckDetail.razor`
- Create: `src/AtsEmployeeStats.Web/Pages/TrailerDetail.razor`
- Modify: `src/AtsEmployeeStats.Web/wwwroot/css/app.css`

**Step 1: Write failing view-model tests**

Assert helpers can find jobs/cities/trucks/trailers/routes and return related child rows.

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter DashboardViewModelTests -p:BaseOutputPath=artifacts\testbin\cities-ui-red\`

Expected: FAIL.

**Step 2: Implement helpers and pages**

Add Company tabs for Details, Drivers, Garages, Trucks, Trailers, Jobs, and Cities. Add detail pages with the tab structures from the architecture.

**Step 3: Build**

Run: `dotnet build AtsEmployeeStats.sln -p:BaseOutputPath=artifacts\build\cities-ui\`

Expected: PASS.

### Task 6: Visualization Framework

**Files:**
- Create or modify: `src/AtsEmployeeStats.Web/Shared/Sparkline.razor`
- Modify: list/detail pages under `src/AtsEmployeeStats.Web/Pages`
- Modify: `src/AtsEmployeeStats.Web/wwwroot/css/app.css`
- Add tests where helper behavior is non-trivial.

**Step 1: Add compact chart DTO tests**

Assert list DTOs have selected-window sparkline data and detail DTOs have chart-ready trend points.

**Step 2: Implement UI components**

Render inline sparklines in list rows and larger trend sections on detail pages. Keep list rows compact.

**Step 3: Verify build and browser smoke**

Run `dotnet build`, then run the API and smoke test `/`, company detail, job detail, and city detail.

### Task 7: Historical Assignment Tables

**Files:**
- Modify: `StatisticsProjection.cs`
- Modify: `StatisticsModels.cs`
- Modify: `SqliteMedallionSaveSnapshotSource.cs`
- Add/modify relevant tests.

**Steps:**

1. Write failing tests for driver-truck and driver-garage assignment ranges across multiple snapshots.
2. Add assignment domain records and projection logic.
3. Persist `silver_driver_truck_assignments` and `silver_driver_garage_assignments`.
4. Expose assignment timelines on driver/truck/garage detail DTOs and pages.

### Task 8: Cloud Aggregate Export Contract

**Files:**
- Create: `src/AtsEmployeeStats.Contracts/CloudAggregateDtos.cs`
- Create or modify tests under `tests/AtsEmployeeStats.Tests`

**Steps:**

1. Write failing tests proving export payloads contain no raw save-unit data.
2. Add versioned anonymized aggregate DTOs.
3. Add a local builder that emits gold-layer upload payloads from SQLite/read models.
4. Verify payloads include schema version, metric version, window boundaries, sample counts, and source snapshot counts.

### Task 9: Full Verification

Run:

```powershell
dotnet test AtsEmployeeStats.sln -p:BaseOutputPath=artifacts\testbin\cities-full\
dotnet build AtsEmployeeStats.sln -p:BaseOutputPath=artifacts\build\cities-full\
```

Then run the API and smoke test:

- `/`
- `/companies/{companyId}`
- `/companies/{companyId}/jobs/{jobId}`
- `/companies/{companyId}/cities/{cityId}`
- `/companies/{companyId}/trailers/{trailerId}`
- `/companies/{companyId}/trucks/{truckId}`

