# Trailer Natural Key Phase 3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stabilise job↔trailer attribution across save-to-save unit_id churn so that trailer profit aggregation and the trailer detail job list both use the stable license plate identity instead of the volatile unit_id.

**Architecture:** `MissionStatistic` (and `MissionDto`) gain a `TrailerLicensePlate` field populated from a cross-snapshot `unit_id → license_plate` map built in `BuildCompany`. `BuildTrailerStats` groups missions by license plate (not unit_id), fixing profit/job_count aggregation across reloads. `BuildProfitTrends` uses `mission.TrailerLicensePlate` directly (removing the now-redundant `trailerIdToLicensePlate` dict parameter added in Phase 2). `GetTrailerJobs` in `DashboardViewModel` switches from unit_id matching to license plate matching. The DB layer adds `trailer_pk INTEGER` to `silver_jobs`, and `trailer_pk INTEGER` + `trailer_license_plate TEXT` to `gold_job_details`. `ReadMissionsAsync` reads `trailer_license_plate` from `gold_job_details` so the DB read path also populates `MissionStatistic.TrailerLicensePlate`.

**Tech Stack:** C# (.NET 10), xUnit, Blazor, SQLite (Microsoft.Data.Sqlite)

---

## File Map

| File | Change |
|------|--------|
| `src/AtsEmployeeStats.Domain/Statistics/StatisticsModels.cs` | Add `TrailerLicensePlate` to `MissionStatistic` |
| `src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs` | Build cross-snapshot plate map; set `TrailerLicensePlate` on missions; fix `BuildTrailerStats` grouping; simplify `BuildProfitTrends` (remove dict param) |
| `src/AtsEmployeeStats.Contracts/StatisticsDtos.cs` | Add `TrailerLicensePlate` to `MissionDto` |
| `src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs` | Pass `mission.TrailerLicensePlate` to `MissionDto` |
| `src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs` | `GetTrailerJobs`/`GetTrailerTrucks` accept `TrailerDto`; match by license plate |
| `src/AtsEmployeeStats.Web/Pages/TrailerDetail.razor` | Pass `SelectedTrailer` to `GetTrailerJobs`/`GetTrailerTrucks` |
| `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs` | Add `trailer_pk` to `silver_jobs`; add `trailer_pk` + `trailer_license_plate` to `gold_job_details`; reorder inserts; build `trailerIdToPk` map; populate new columns; `ReadMissionsAsync` reads `trailer_license_plate` |
| `tests/AtsEmployeeStats.Tests/StatisticsProjectionTests.cs` | Cross-snapshot attribution test |
| `tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs` | `TrailerLicensePlate` on `MissionDto` |
| `tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs` | `GetTrailerJobs(company, trailer)` test |
| `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs` | `trailer_pk` + `trailer_license_plate` persistence test |

---

## Task 1: Add `TrailerLicensePlate` to `MissionStatistic` and fix cross-snapshot attribution in `StatisticsProjection`

**Files:**
- Modify: `src/AtsEmployeeStats.Domain/Statistics/StatisticsModels.cs`
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsProjectionTests.cs`

### Background

`BuildCompany` currently builds `trailers` from only the **latest** snapshot. Missions come from **all** snapshots via `BuildSnapshotMissions`. If a trailer had unit_id `A` in an older snapshot and unit_id `B` in the latest, jobs from the older snapshot (with `TrailerId = A`) are not attributed to the current trailer (with `Id = B`). Phase 3 fixes this by resolving all snapshot unit_ids to license plates and grouping by plate.

Also, `BuildProfitTrends` currently accepts a `trailerIdToLicensePlate` dictionary (added in Phase 2) to resolve trailer trend entity IDs. After this task, each `MissionStatistic` carries `TrailerLicensePlate` directly — so the dictionary parameter is no longer needed.

- [ ] **Step 1: Write the failing test**

Add this test to `StatisticsProjectionTests.cs` after `Build_extracts_license_plate_from_trailer_unit`:

```csharp
[Fact]
public void Build_attributes_missions_from_all_snapshots_to_trailer_by_license_plate()
{
    // Snapshot 1: trailer has unit_id "trailer.A" — a historical save after a game reload
    var snapshot1 = new SaveSnapshot(
        "save-1",
        new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero),
        SiiSaveParser.Parse("""
            SiiNunit
            {
            garage : garage.phoenix {
              employees[0]: driver.alice
              trailers[0]: trailer.A
            }

            driver : driver.alice {
            }

            trailer : trailer.A {
              trailer_definition: trailer_def.scs.box.reefer
              license_plate: "200B-420|texas"
            }

            trailer_def : trailer_def.scs.box.reefer {
              body_type: "box"
              chain_type: "double"
            }

            trailer_utilization_log : trailer_log.A {
              total_transported_cargoes: 1
            }

            job : job.old {
              trailer: trailer.A
              income: 2000
              source_city: phoenix
              target_city: denver
              timestamp_day: 150
            }
            }
            """));

    // Snapshot 2: same physical trailer, unit_id reassigned to "trailer.B" after game reload
    var snapshot2 = new SaveSnapshot(
        "save-2",
        new DateTimeOffset(2026, 5, 30, 10, 0, 0, TimeSpan.Zero),
        SiiSaveParser.Parse("""
            SiiNunit
            {
            garage : garage.phoenix {
              employees[0]: driver.alice
              trailers[0]: trailer.B
            }

            driver : driver.alice {
            }

            trailer : trailer.B {
              trailer_definition: trailer_def.scs.box.reefer
              license_plate: "200B-420|texas"
            }

            trailer_def : trailer_def.scs.box.reefer {
              body_type: "box"
              chain_type: "double"
            }

            trailer_utilization_log : trailer_log.B {
              total_transported_cargoes: 1
            }

            job : job.new {
              trailer: trailer.B
              income: 3000
              source_city: denver
              target_city: phoenix
              timestamp_day: 200
            }
            }
            """));

    var company = Assert.Single(StatisticsProjection.Build([snapshot1, snapshot2]).Companies);
    var trailer = Assert.Single(company.Trailers);

    // Both jobs (2000 + 3000) should be attributed to the license plate "200B-420 Texas"
    Assert.Equal("200B-420 Texas", trailer.LicensePlate);
    Assert.Equal(5000, trailer.Profit);

    // Both missions should carry the trailer's license plate
    Assert.All(company.Missions, m => Assert.Equal("200B-420 Texas", m.TrailerLicensePlate));

    // Trend points should use "200B-420 Texas" as EntityId
    var trailerTrends = company.ProfitTrends.Where(p => p.EntityKind == "trailer").ToList();
    Assert.Equal(2, trailerTrends.Count);
    Assert.All(trailerTrends, p => Assert.Equal("200B-420 Texas", p.EntityId));
}
```

- [ ] **Step 2: Run to verify it fails**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "Build_attributes_missions_from_all_snapshots_to_trailer_by_license_plate" -v minimal
```

Expected: FAIL — `MissionStatistic` has no `TrailerLicensePlate`; trailer profit is split across two unit_ids.

- [ ] **Step 3: Add `TrailerLicensePlate` to `MissionStatistic`**

In `StatisticsModels.cs`, change `MissionStatistic` from:
```csharp
public sealed record MissionStatistic(
    string Id,
    string? DriverId,
    string? TruckId,
    string? TrailerId,
    string? TrailerType,
    string? Cargo,
    string? SourceCity,
    string? TargetCity,
    long Profit,
    int? TimestampDay = null,
    string? GarageId = null);
```
to:
```csharp
public sealed record MissionStatistic(
    string Id,
    string? DriverId,
    string? TruckId,
    string? TrailerId,
    string? TrailerType,
    string? Cargo,
    string? SourceCity,
    string? TargetCity,
    long Profit,
    int? TimestampDay = null,
    string? GarageId = null,
    string? TrailerLicensePlate = null);
```

- [ ] **Step 4: Build cross-snapshot `allUnitIdToLicensePlate` in `BuildCompany`**

Read `StatisticsProjection.cs`. In `BuildCompany`, before the `var missionStats = snapshots.SelectMany(...)` block (around line 123), add:

```csharp
var allUnitIdToLicensePlate = snapshots
    .SelectMany(s => s.Document.Units.Where(u => u.TypeEquals("trailer")))
    .Select(u => (UnitId: u.Id, LicensePlate: CleanLicensePlate(FirstKnownValue(u, "license_plate"))))
    .Where(pair => !string.IsNullOrWhiteSpace(pair.LicensePlate))
    .GroupBy(pair => pair.UnitId, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key, g => g.First().LicensePlate!, StringComparer.OrdinalIgnoreCase);
```

- [ ] **Step 5: Populate `TrailerLicensePlate` during mission de-duplication**

In `BuildCompany`, the mission de-dup block (around line 123-148) resolves `trailerId` across snapshots. After resolving `trailerId`, add `trailerLicensePlate` and include it in the `with` expression:

Change the `.Select(group => { ... })` block. The current structure resolves `driverId`, `truckId`, `trailerId`, `garageId` then returns either `winner` or `winner with { ... }`. Change it to:

```csharp
.Select(group =>
{
    var winner = group.OrderByDescending(m => m.LastWritten).First().Statistic;
    var driverId = winner.DriverId
        ?? group.Select(m => m.Statistic.DriverId).FirstOrDefault(id => id != null);
    var truckId = winner.TruckId
        ?? group.Select(m => m.Statistic.TruckId).FirstOrDefault(id => id != null);
    var trailerId = winner.TrailerId
        ?? group.Select(m => m.Statistic.TrailerId).FirstOrDefault(id => id != null);
    // Garage: use earliest attribution — that's where the driver was when the job was done
    var garageId = group
        .OrderBy(m => m.LastWritten)
        .Select(m => m.Statistic.GarageId)
        .FirstOrDefault(id => id != null);
    var trailerLicensePlate = trailerId is not null
        ? allUnitIdToLicensePlate.GetValueOrDefault(trailerId)
        : null;
    return (driverId == winner.DriverId && truckId == winner.TruckId && trailerId == winner.TrailerId && garageId == winner.GarageId && trailerLicensePlate == winner.TrailerLicensePlate)
        ? winner
        : winner with { DriverId = driverId, TruckId = truckId, TrailerId = trailerId, GarageId = garageId, TrailerLicensePlate = trailerLicensePlate };
})
```

- [ ] **Step 6: Fix `BuildTrailerStats` to group by license plate**

In `BuildTrailerStats` (around line 469), change `missionProfitByTrailer` from grouping by `mission.TrailerId!` to grouping by `(mission.TrailerLicensePlate ?? mission.TrailerId!)`:

```csharp
var missionProfitByTrailer = missions
    .Where(mission => !string.IsNullOrWhiteSpace(mission.TrailerId))
    .GroupBy(
        mission => mission.TrailerLicensePlate ?? mission.TrailerId!,
        StringComparer.OrdinalIgnoreCase)
    .ToDictionary(
        group => group.Key,
        group => group.Sum(mission => mission.Profit),
        StringComparer.OrdinalIgnoreCase);
```

Change the profit lookup (around line 490) from `trailer.Id` to `trailer.LicensePlate ?? trailer.Id`:

```csharp
var profit = missionProfitByTrailer.GetValueOrDefault(trailer.LicensePlate ?? trailer.Id);
```

- [ ] **Step 7: Simplify `BuildProfitTrends` — remove dictionary parameter**

In `BuildProfitTrends` (around line 626), change the signature from:
```csharp
private static IReadOnlyList<TrendPointStatistic> BuildProfitTrends(
    string companyId,
    IReadOnlyCollection<MissionStatistic> missions,
    IReadOnlyDictionary<string, string> trailerIdToLicensePlate)
```
to:
```csharp
private static IReadOnlyList<TrendPointStatistic> BuildProfitTrends(
    string companyId,
    IReadOnlyCollection<MissionStatistic> missions)
```

Change the trailer grouping from:
```csharp
trends.AddRange(timedMissions
    .Where(mission => !string.IsNullOrWhiteSpace(mission.TrailerId))
    .GroupBy(
        mission => trailerIdToLicensePlate.GetValueOrDefault(mission.TrailerId!) ?? mission.TrailerId!,
        StringComparer.OrdinalIgnoreCase)
    .SelectMany(group => BuildTrend("trailer", group.Key, group)));
```
to:
```csharp
trends.AddRange(timedMissions
    .Where(mission => !string.IsNullOrWhiteSpace(mission.TrailerId))
    .GroupBy(
        mission => mission.TrailerLicensePlate ?? mission.TrailerId!,
        StringComparer.OrdinalIgnoreCase)
    .SelectMany(group => BuildTrend("trailer", group.Key, group)));
```

- [ ] **Step 8: Remove `trailerIdToLicensePlate` from `BuildCompany`**

In `BuildCompany`, remove the two lines that build `trailerIdToLicensePlate` from `individualTrailerStats` (around lines 165-167):
```csharp
var trailerIdToLicensePlate = individualTrailerStats
    .Where(t => !string.IsNullOrWhiteSpace(t.LicensePlate))
    .ToDictionary(t => t.Id, t => t.LicensePlate!, StringComparer.OrdinalIgnoreCase);
```

And update the `BuildProfitTrends` call (around line 182) from:
```csharp
BuildProfitTrends(companyId, missionStats, trailerIdToLicensePlate),
```
to:
```csharp
BuildProfitTrends(companyId, missionStats),
```

- [ ] **Step 9: Run the full test suite**

```
dotnet test tests/AtsEmployeeStats.Tests -v minimal
```

Expected: the new cross-snapshot test passes; all other tests pass (1 pre-existing failure `StatisticsService_persists_city_route_trailer_and_trend_read_models` is OK).

- [ ] **Step 10: Commit**

```bash
git add src/AtsEmployeeStats.Domain/Statistics/StatisticsModels.cs src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs tests/AtsEmployeeStats.Tests/StatisticsProjectionTests.cs
git commit -m "feat: fix cross-snapshot trailer attribution by license plate"
```

---

## Task 2: Add `TrailerLicensePlate` to `MissionDto`; update `GetTrailerJobs` to match by plate

**Files:**
- Modify: `src/AtsEmployeeStats.Contracts/StatisticsDtos.cs`
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs`
- Modify: `src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs`
- Modify: `src/AtsEmployeeStats.Web/Pages/TrailerDetail.razor`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs`
- Test: `tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs`

- [ ] **Step 1: Update mapper test**

Read `tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs`.

In the mapper test that constructs a `MissionStatistic`, add `TrailerLicensePlate: "200B-420 Texas"` to it. Then after the existing `MissionDto` assertions, add:
```csharp
var job = Assert.Single(company.Missions);
Assert.Equal("200B-420 Texas", job.TrailerLicensePlate);
```

- [ ] **Step 2: Run to verify it fails**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "StatisticsDashboardMapperTests" -v minimal
```

Expected: FAIL — `MissionDto` has no `TrailerLicensePlate`.

- [ ] **Step 3: Add `TrailerLicensePlate` to `MissionDto`**

Read `src/AtsEmployeeStats.Contracts/StatisticsDtos.cs`.

In `MissionDto`, add `string? TrailerLicensePlate = null` as the last optional parameter:
```csharp
public sealed record MissionDto(
    string Id,
    string? DriverId,
    string? TruckId,
    string? TrailerType,
    string? Cargo,
    string? SourceCity,
    string? TargetCity,
    long Profit,
    int? TimestampDay = null,
    string? TrailerId = null,
    string? GarageId = null,
    string? TrailerLicensePlate = null);
```

- [ ] **Step 4: Wire `TrailerLicensePlate` through `StatisticsDashboardMapper`**

Read `src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs`.

Find where `MissionDto` is constructed (around line 107). Add `mission.TrailerLicensePlate` as the last argument:
```csharp
new MissionDto(
    mission.Id,
    mission.DriverId,
    mission.TruckId,
    mission.TrailerType,
    mission.Cargo,
    mission.SourceCity,
    mission.TargetCity,
    mission.Profit,
    mission.TimestampDay,
    mission.TrailerId,
    mission.GarageId,
    mission.TrailerLicensePlate)
```

- [ ] **Step 5: Update `GetTrailerJobs` and `GetTrailerTrucks` in `DashboardViewModel`**

Read `src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs`.

Change `GetTrailerJobs` from matching by `string trailerId` (unit_id) to accepting a `TrailerDto` and matching by license plate:

```csharp
public static IReadOnlyList<MissionDto> GetTrailerJobs(CompanyDto company, TrailerDto trailer) =>
    company.Missions
        .Where(mission => trailer.LicensePlate is not null
            ? IdEquals(mission.TrailerLicensePlate, trailer.LicensePlate)
            : IdEquals(mission.TrailerId, trailer.Id))
        .ToList();
```

Change `GetTrailerTrucks` to accept `TrailerDto` and call the updated `GetTrailerJobs`:

```csharp
public static IReadOnlyList<TruckDto> GetTrailerTrucks(CompanyDto company, TrailerDto trailer)
{
    var truckIds = GetTrailerJobs(company, trailer)
        .Where(job => !string.IsNullOrWhiteSpace(job.TruckId))
        .Select(job => job.TruckId!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    return company.Trucks
        .Where(truck => truckIds.Contains(truck.Id))
        .ToList();
}
```

- [ ] **Step 6: Update `DashboardViewModelTests`**

Read `tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs`.

Find the test that calls `GetTrailerJobs(company, "trailer.reefer.1")`. Update it to call `GetTrailerJobs(company, trailer)` where `trailer` is the `TrailerDto` from the test data. The `TrailerDto` already has `LicensePlate: "200B-420 Texas"` from the Phase 2 test update.

Change the call from:
```csharp
DashboardViewModel.GetTrailerJobs(company, "trailer.reefer.1")
```
to (using the `TrailerDto` object from the test data):
```csharp
var trailerDto = Assert.Single(company.Trailers!);
DashboardViewModel.GetTrailerJobs(company, trailerDto)
```

Similarly update any `GetTrailerTrucks` calls that use the old string signature.

- [ ] **Step 7: Update `TrailerDetail.razor`**

Read `src/AtsEmployeeStats.Web/Pages/TrailerDetail.razor`.

Change the two call sites that pass `SelectedTrailer.Id`:

```razor
@DashboardViewModel.GetTrailerTrucks(SelectedCompany, SelectedTrailer.Id)
```
to:
```razor
@DashboardViewModel.GetTrailerTrucks(SelectedCompany, SelectedTrailer)
```

```razor
@foreach (var job in DashboardViewModel.GetTrailerJobs(SelectedCompany, SelectedTrailer.Id))
```
to:
```razor
@foreach (var job in DashboardViewModel.GetTrailerJobs(SelectedCompany, SelectedTrailer))
```

Also update `GetTrailerTrucks` call in the detail section:
```razor
@DashboardViewModel.GetTrailerTrucks(SelectedCompany, SelectedTrailer.Id).Count
```
to:
```razor
@DashboardViewModel.GetTrailerTrucks(SelectedCompany, SelectedTrailer).Count
```

- [ ] **Step 8: Run the full test suite**

```
dotnet test tests/AtsEmployeeStats.Tests -v minimal
```

Expected: all pass.

- [ ] **Step 9: Commit**

```bash
git add src/AtsEmployeeStats.Contracts/StatisticsDtos.cs src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs src/AtsEmployeeStats.Web/Pages/TrailerDetail.razor tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs
git commit -m "feat: add TrailerLicensePlate to MissionDto; GetTrailerJobs matches by plate"
```

---

## Task 3: Persist `trailer_pk` and `trailer_license_plate` in `silver_jobs` / `gold_job_details`

**Files:**
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`
- Test: `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs`

### Background

- `silver_jobs` gets `trailer_pk INTEGER` (FK to `silver_trailers.id`) — populated by looking up `mission.TrailerId` in the just-inserted trailers.
- `gold_job_details` gets `trailer_pk INTEGER` + `trailer_license_plate TEXT` — `trailer_license_plate` comes from `mission.TrailerLicensePlate` (set by the projection in Task 1); `trailer_pk` comes from the same lookup.
- The **insert order** in `PersistSilverAndGoldAsync` currently inserts silver_jobs (missions) BEFORE silver_trailers. This must be reversed: **insert silver_trailers first**, then build a `trailer_id → id` lookup map, then insert silver_jobs using that map.
- `gold_job_details` is populated in `PersistGoldAsync` (a separate static method called at the end of the company loop). It queries `silver_trailers` directly to build its own map.
- `ReadMissionsAsync` must also SELECT `trailer_license_plate` from `gold_job_details` and pass it to `MissionStatistic.TrailerLicensePlate`.

- [ ] **Step 1: Add failing test**

Read `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs` to understand the test helpers (`WriteAnalyticSaveAsync`, `OpenTestConnection`, etc.).

Add this test after `StatisticsService_persists_trailer_license_plate_to_silver_trailers`:

```csharp
[Fact]
public async Task StatisticsService_persists_trailer_pk_to_silver_jobs_and_gold_job_details()
{
    await WriteAnalyticSaveAsync();
    var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
    var service = new StatisticsService(source);

    await service.IngestAsync(CancellationToken.None);

    using var connection = OpenTestConnection();
    await connection.OpenAsync();

    // silver_jobs should have trailer_pk set
    await using var silverCmd = connection.CreateCommand();
    silverCmd.CommandText = """
        select sj.trailer_pk, st.id
        from silver_jobs sj
        join silver_trailers st on st.company_id = sj.company_id and st.trailer_id = sj.trailer_id
        where sj.company_id = 'desert-line' and sj.job_id = 'job.outbound'
        """;
    await using var silverReader = await silverCmd.ExecuteReaderAsync();
    Assert.True(await silverReader.ReadAsync());
    Assert.Equal(silverReader.GetInt64(1), silverReader.GetInt64(0)); // trailer_pk matches silver_trailers.id

    // gold_job_details should have trailer_pk and trailer_license_plate set
    await using var goldCmd = connection.CreateCommand();
    goldCmd.CommandText = """
        select trailer_pk, trailer_license_plate
        from gold_job_details
        where company_id = 'desert-line' and job_id = 'job.outbound'
        """;
    await using var goldReader = await goldCmd.ExecuteReaderAsync();
    Assert.True(await goldReader.ReadAsync());
    Assert.True(goldReader.GetInt64(0) > 0);
    Assert.Equal("200B-420 Texas", goldReader.GetString(1));
}
```

- [ ] **Step 2: Run to verify it fails**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "StatisticsService_persists_trailer_pk_to_silver_jobs_and_gold_job_details" -v minimal
```

Expected: FAIL — columns don't exist yet.

- [ ] **Step 3: Update DDL for `silver_jobs`**

Read `SqliteMedallionSaveSnapshotSource.cs`. Find the `create table if not exists silver_jobs` DDL (around line 409). Add `trailer_pk INTEGER` after `trailer_id`:

```sql
create table if not exists silver_jobs (
    company_id text not null,
    job_id text not null,
    driver_id text,
    truck_id text,
    trailer_id text,
    trailer_pk integer,
    trailer_type text,
    cargo text,
    origin_city text,
    destination_city text,
    profit integer not null,
    timestamp_day integer,
    garage_id text,
    primary key (company_id, job_id)
);
```

- [ ] **Step 4: Update DDL for `gold_job_details`**

Find the `create table if not exists gold_job_details` DDL (around line 534). Add `trailer_pk INTEGER` and `trailer_license_plate TEXT`:

```sql
create table if not exists gold_job_details (
    company_id text not null,
    job_id text not null,
    driver_id text,
    job_type text,
    origin_city text,
    destination_city text,
    cargo text,
    trailer_type text,
    truck_id text,
    profit integer not null,
    timestamp_day integer,
    garage_id text,
    trailer_pk integer,
    trailer_license_plate text,
    primary key (company_id, job_id)
);
```

- [ ] **Step 5: Add `EnsureColumnAsync` calls**

In `EnsureSchemaAsync`, add three new `EnsureColumnAsync` calls (after the existing ones):

```csharp
await EnsureColumnAsync(connection, "silver_jobs", "trailer_pk", "integer", cancellationToken);
await EnsureColumnAsync(connection, "gold_job_details", "trailer_pk", "integer", cancellationToken);
await EnsureColumnAsync(connection, "gold_job_details", "trailer_license_plate", "text", cancellationToken);
```

- [ ] **Step 6: Reorder inserts — move silver_trailers BEFORE silver_jobs**

In `PersistSilverAndGoldAsync`, the current insert order for the company loop is:
1. silver_companies INSERT
2. silver_garages INSERT
3. silver_drivers INSERT
4. silver_trucks INSERT
5. **silver_jobs INSERT** (missions) ← currently before trailers
6. silver_driver_recent_jobs INSERT
7. silver_trailer_types INSERT
8. **silver_trailers INSERT** ← currently after jobs

Move the `foreach (var trailer in company.Trailers)` block (silver_trailers INSERT) to be immediately after silver_trucks and before silver_jobs. After the trailer inserts, add a query to build `trailerIdToPk`:

```csharp
// (trailers moved here — before jobs)
foreach (var trailer in company.Trailers)
{
    await ExecuteAsync(
        connection,
        """
        insert into silver_trailers (company_id, company_pk, trailer_id, trailer_type, profit, job_count, body_type, is_articulated, garage_id, license_plate)
        values ($company_id, $company_pk, $trailer_id, $trailer_type, $profit, $job_count, $body_type, $is_articulated, $garage_id, $license_plate)
        """,
        cancellationToken,
        ("$company_id", company.Id),
        ("$company_pk", companyPk),
        ("$trailer_id", trailer.Id),
        ("$trailer_type", trailer.TrailerType),
        ("$profit", trailer.Profit),
        ("$job_count", trailer.JobCount),
        ("$body_type", trailer.BodyType),
        ("$is_articulated", trailer.IsArticulated ? 1 : 0),
        ("$garage_id", trailer.GarageId),
        ("$license_plate", trailer.LicensePlate));
}

// Build trailer_id → silver_trailers.id map for use in job inserts
var trailerIdToPk = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
await using (var trailerPkCmd = connection.CreateCommand())
{
    trailerPkCmd.CommandText = "select trailer_id, id from silver_trailers where company_id = $company_id";
    Add(trailerPkCmd, "$company_id", company.Id);
    await using var trailerPkReader = await trailerPkCmd.ExecuteReaderAsync(cancellationToken);
    while (await trailerPkReader.ReadAsync(cancellationToken))
        trailerIdToPk[trailerPkReader.GetString(0)] = trailerPkReader.GetInt64(1);
}

// (then silver_jobs INSERT follows — now uses trailerIdToPk)
```

Then remove the old `foreach (var trailer in company.Trailers)` block from its original location (after silver_trailer_types).

- [ ] **Step 7: Add `trailer_pk` to silver_jobs INSERT**

Update the silver_jobs INSERT (which now follows the trailers block) to include `trailer_pk`:

```csharp
foreach (var mission in company.Missions)
{
    trailerIdToPk.TryGetValue(mission.TrailerId ?? "", out var trailerPk);
    await ExecuteAsync(
        connection,
        """
        insert into silver_jobs (
            company_id, job_id, driver_id, truck_id, trailer_id, trailer_pk, trailer_type, cargo, origin_city, destination_city, profit, timestamp_day, garage_id
        )
        values (
            $company_id, $job_id, $driver_id, $truck_id, $trailer_id, $trailer_pk, $trailer_type, $cargo, $origin_city, $destination_city, $profit, $timestamp_day, $garage_id
        )
        """,
        cancellationToken,
        ("$company_id", company.Id),
        ("$job_id", mission.Id),
        ("$driver_id", mission.DriverId),
        ("$truck_id", mission.TruckId),
        ("$trailer_id", mission.TrailerId),
        ("$trailer_pk", trailerPk == 0 ? (object)DBNull.Value : trailerPk),
        ("$trailer_type", mission.TrailerType),
        ("$cargo", mission.Cargo),
        ("$origin_city", mission.SourceCity),
        ("$destination_city", mission.TargetCity),
        ("$profit", mission.Profit),
        ("$timestamp_day", mission.TimestampDay),
        ("$garage_id", mission.GarageId));
}
```

Note: `TryGetValue` returns `false` and leaves `trailerPk = 0` when the key isn't found. Use `DBNull.Value` in that case so the column stores NULL rather than 0.

- [ ] **Step 8: Add `trailer_pk` and `trailer_license_plate` to gold_job_details INSERT in `PersistGoldAsync`**

Read `PersistGoldAsync`. It's a separate static method called for each company. At the start of `PersistGoldAsync`, before the mission loop, query `silver_trailers` to build `trailerIdToPk`:

```csharp
var trailerIdToPk = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
await using (var trailerPkCmd = connection.CreateCommand())
{
    trailerPkCmd.CommandText = "select trailer_id, id from silver_trailers where company_id = $company_id";
    Add(trailerPkCmd, "$company_id", company.Id);
    await using var reader = await trailerPkCmd.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
        trailerIdToPk[reader.GetString(0)] = reader.GetInt64(1);
}
```

Then update the gold_job_details INSERT to include `trailer_pk` and `trailer_license_plate`:

```csharp
foreach (var mission in company.Missions)
{
    trailerIdToPk.TryGetValue(mission.TrailerId ?? "", out var trailerPk);
    await ExecuteAsync(
        connection,
        """
        insert into gold_job_details (
            company_id, job_id, driver_id, job_type, origin_city, destination_city, cargo, trailer_type, truck_id, profit, timestamp_day, trailer_id, garage_id, trailer_pk, trailer_license_plate
        )
        values (
            $company_id, $job_id, $driver_id, $job_type, $origin_city, $destination_city, $cargo, $trailer_type, $truck_id, $profit, $timestamp_day, $trailer_id, $garage_id, $trailer_pk, $trailer_license_plate
        )
        """,
        cancellationToken,
        ("$company_id", company.Id),
        ("$job_id", mission.Id),
        ("$driver_id", mission.DriverId),
        ("$job_type", mission.TrailerType ?? "unknown"),
        ("$origin_city", mission.SourceCity),
        ("$destination_city", mission.TargetCity),
        ("$cargo", mission.Cargo),
        ("$trailer_type", mission.TrailerType),
        ("$truck_id", mission.TruckId),
        ("$profit", mission.Profit),
        ("$timestamp_day", mission.TimestampDay),
        ("$trailer_id", mission.TrailerId),
        ("$garage_id", mission.GarageId),
        ("$trailer_pk", trailerPk == 0 ? (object)DBNull.Value : trailerPk),
        ("$trailer_license_plate", mission.TrailerLicensePlate));
}
```

- [ ] **Step 9: Update `ReadMissionsAsync` to read `trailer_license_plate`**

Find `ReadMissionsAsync` (around line 2140). Change the SQL from:
```sql
select job_id, driver_id, truck_id, trailer_type, cargo, origin_city, destination_city, profit, timestamp_day, trailer_id, garage_id
from gold_job_details
where company_id = $company_id
order by profit desc, job_id
```
to:
```sql
select job_id, driver_id, truck_id, trailer_type, cargo, origin_city, destination_city, profit, timestamp_day, trailer_id, garage_id, trailer_license_plate
from gold_job_details
where company_id = $company_id
order by profit desc, job_id
```

Update the reader to also read column 11:
```csharp
values.Add(new MissionStatistic(
    reader.GetString(0),
    GetNullableString(reader, 1),
    GetNullableString(reader, 2),
    TrailerId: GetNullableString(reader, 9),
    GetNullableString(reader, 3),
    GetNullableString(reader, 4),
    GetNullableString(reader, 5),
    GetNullableString(reader, 6),
    reader.GetInt64(7),
    GetNullableInt(reader, 8),
    GarageId: GetNullableString(reader, 10),
    TrailerLicensePlate: GetNullableString(reader, 11)));
```

- [ ] **Step 10: Run the full test suite**

```
dotnet test tests/AtsEmployeeStats.Tests -v minimal
```

Expected: all pass (1 pre-existing failure `StatisticsService_persists_city_route_trailer_and_trend_read_models` is OK).

- [ ] **Step 11: Commit**

```bash
git add src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs
git commit -m "feat: persist trailer_pk and trailer_license_plate in silver_jobs / gold_job_details"
```

---

## Self-Review

### Spec Coverage Check

| Spec requirement | Covered by |
|-----------------|-----------|
| `silver_jobs` / `gold_job_details` add `trailer_pk INTEGER` | Task 3 |
| At ingestion, resolve unit_id → surrogate via just-inserted silver_trailers rows | Task 3 steps 6-8 |
| Trailer job-history aggregation uses surrogate/plate, not text unit_id | Task 1 |
| Trailer detail page job list works correctly across save files | Task 1 (projection) + Task 2 (GetTrailerJobs by plate) |

### Placeholder Scan

No TBDs. All code snippets are complete.

### Type Consistency

- `MissionStatistic.TrailerLicensePlate: string?` — referenced in Tasks 1, 3
- `MissionDto.TrailerLicensePlate: string?` — referenced in Tasks 2, 3
- `GetTrailerJobs(CompanyDto company, TrailerDto trailer)` — referenced in Tasks 2 (definition) and `TrailerDetail.razor` (call site)
- `GetTrailerTrucks(CompanyDto company, TrailerDto trailer)` — referenced in Tasks 2 (definition) and `TrailerDetail.razor` (call sites ×2)
