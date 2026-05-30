# Trailer Natural Key Phase 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stabilise trailer identity by extracting `license_plate` from the SII unit, propagating it through all layers (domain → contract → DB), and switching trailer URLs from the volatile `unit_id` to the stable license plate.

**Architecture:** `TrailerStatistic` and `TrailerDto` gain a `LicensePlate` field. The `silver_trailers` table gains a surrogate `id INTEGER PRIMARY KEY`, a `company_pk` FK, and a `license_plate TEXT` column (structural migration like Phase 1). The API endpoint and Blazor route for trailer detail switch from `{trailerId}` to `{licensePlate}`. Sparkline trend points for trailers use the license plate as entity_id when available, falling back to the volatile unit_id otherwise.

**Tech Stack:** C# (.NET 10), xUnit, Blazor, SQLite (Microsoft.Data.Sqlite)

---

## File Map

| File | Change |
|------|--------|
| `src/AtsEmployeeStats.Domain/Statistics/StatisticsModels.cs` | Add `LicensePlate` to `TrailerStatistic` |
| `src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs` | Extract `license_plate` in `BuildTrailerStats`; build `trailerIdToLicensePlate`; thread into `BuildProfitTrends` |
| `src/AtsEmployeeStats.Contracts/StatisticsDtos.cs` | Add `LicensePlate` to `TrailerDto` |
| `src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs` | Pass `LicensePlate`; use as sparkline entity_id |
| `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs` | Migrate `silver_trailers` schema; update INSERT/SELECT; populate `company_pk` |
| `src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs` | `FindTrailer` matches on `LicensePlate` |
| `src/AtsEmployeeStats.Api/Program.cs` | Trailer API endpoint matches on `trailer.LicensePlate` |
| `src/AtsEmployeeStats.Web/Pages/TrailerDetail.razor` | Route param `{LicensePlate}`, lookup by plate |
| `src/AtsEmployeeStats.Web/Pages/CompanyDetail.razor` | Trailer links use plate; inline job→trailer lookup; remove `GetTrailerBodyLabel` |
| `src/AtsEmployeeStats.Web/Pages/GarageDetail.razor` | Trailer links use plate |
| `tests/AtsEmployeeStats.Tests/StatisticsProjectionTests.cs` | Add license plate to test data; assert plate + trend entity_id |
| `tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs` | Assert `LicensePlate` on `TrailerDto` |
| `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs` | New test for plate persistence; update `WriteAnalyticSaveAsync` |
| `tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs` | Update `FindTrailer` test to use plate |
| `tests/AtsEmployeeStats.Tests/StatisticsApiTests.cs` | Add plate to test save; update trailer URL in tests |

---

## Task 1: Add `LicensePlate` to `TrailerStatistic` and extract it in `BuildTrailerStats`

**Files:**
- Modify: `src/AtsEmployeeStats.Domain/Statistics/StatisticsModels.cs:160-167`
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs:458-504`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsProjectionTests.cs`

- [ ] **Step 1: Add `license_plate` to the trailer unit in `Build_creates_city_route_trailer_and_trend_read_models_from_jobs` and assert it**

In `StatisticsProjectionTests.cs` around line 760, change the trailer unit from:
```
trailer : trailer.reefer.1 {
  trailer_definition: trailer_def.scs.box.reefer
}
```
to:
```
trailer : trailer.reefer.1 {
  trailer_definition: trailer_def.scs.box.reefer
  license_plate: "200B-420|texas"
}
```

After the existing `Assert.Equal(5500, trailer.Profit);` assertion at line 837, add:
```csharp
Assert.Equal("200B-420 Texas", trailer.LicensePlate);
```

Also add a new focused test after the `Build_sets_trailer_garage_id_from_garage_trailers_array_and_job_count_from_utilization_log` test (after line 1003):
```csharp
[Fact]
public void Build_extracts_license_plate_from_trailer_unit()
{
    var snapshot = new SaveSnapshot(
        "trailer-plate",
        new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero),
        SiiSaveParser.Parse("""
            SiiNunit
            {
            garage : garage.phoenix {
              employees[0]: driver.alice
              trailers[0]: trailer.reefer.1
            }

            driver : driver.alice {
            }

            trailer : trailer.reefer.1 {
              trailer_definition: trailer_def.scs.box.reefer
              license_plate: "TRL-001|california"
            }

            job : job.1 {
              trailer: trailer.reefer.1
              income: 1000
              source_city: phoenix
              target_city: los_angeles
            }
            }
            """));

    var trailer = Assert.Single(Assert.Single(StatisticsProjection.Build([snapshot]).Companies).Trailers);

    Assert.Equal("TRL-001 California", trailer.LicensePlate);
}
```

- [ ] **Step 2: Run the new test to verify it fails**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "Build_extracts_license_plate_from_trailer_unit" -v minimal
```

Expected: FAIL — `TrailerStatistic` has no `LicensePlate` property.

- [ ] **Step 3: Add `LicensePlate` to `TrailerStatistic`**

In `StatisticsModels.cs` lines 160-167, change:
```csharp
public sealed record TrailerStatistic(
    string Id,
    string TrailerType,
    long Profit,
    int JobCount,
    bool IsArticulated = false,
    string? BodyType = null,
    string? GarageId = null);
```
to:
```csharp
public sealed record TrailerStatistic(
    string Id,
    string TrailerType,
    long Profit,
    int JobCount,
    bool IsArticulated = false,
    string? BodyType = null,
    string? GarageId = null,
    string? LicensePlate = null);
```

- [ ] **Step 4: Extract `license_plate` in `BuildTrailerStats`**

In `StatisticsProjection.cs` inside `BuildTrailerStats` (around line 487), change the lambda's return block. The current code reads:
```csharp
var profit = missionProfitByTrailer.GetValueOrDefault(trailer.Id);
var jobCount = trailerToJobCount.GetValueOrDefault(trailer.Id);
var garageId = trailerToGarage.GetValueOrDefault(trailer.Id);

return new TrailerStatistic(
    trailer.Id,
    defId ?? "unknown",
    profit,
    jobCount,
    isArticulated,
    bodyType,
    garageId);
```

Change it to:
```csharp
var profit = missionProfitByTrailer.GetValueOrDefault(trailer.Id);
var jobCount = trailerToJobCount.GetValueOrDefault(trailer.Id);
var garageId = trailerToGarage.GetValueOrDefault(trailer.Id);
var licensePlate = CleanLicensePlate(FirstKnownValue(trailer, "license_plate"));

return new TrailerStatistic(
    trailer.Id,
    defId ?? "unknown",
    profit,
    jobCount,
    isArticulated,
    bodyType,
    garageId,
    licensePlate);
```

- [ ] **Step 5: Run both tests to verify they pass**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "Build_extracts_license_plate_from_trailer_unit|Build_creates_city_route_trailer_and_trend_read_models_from_jobs" -v minimal
```

Expected: PASS.

- [ ] **Step 6: Run the full suite to catch regressions**

```
dotnet test tests/AtsEmployeeStats.Tests -v minimal
```

Expected: all pass.

- [ ] **Step 7: Commit**

```bash
git add src/AtsEmployeeStats.Domain/Statistics/StatisticsModels.cs src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs tests/AtsEmployeeStats.Tests/StatisticsProjectionTests.cs
git commit -m "feat: extract license_plate into TrailerStatistic"
```

---

## Task 2: Use license plate as entity_id for trailer trend points

**Files:**
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs:621-677`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsProjectionTests.cs`

- [ ] **Step 1: Add trailer trend assertions to `Build_creates_city_route_trailer_and_trend_read_models_from_jobs`**

The test already has the trailer with `license_plate: "200B-420|texas"` (added in Task 1) and two jobs: `job.outbound` at day 200 for $3000 and `job.return` at day 201 for $2500. After the existing company trend assertions (after line 855), add:

```csharp
Assert.Collection(
    company.ProfitTrends.Where(point => point.EntityKind == "trailer").OrderBy(p => p.GameDay),
    point =>
    {
        Assert.Equal("200B-420 Texas", point.EntityId);
        Assert.Equal(200, point.GameDay);
        Assert.Equal(3000, point.Profit);
    },
    point =>
    {
        Assert.Equal("200B-420 Texas", point.EntityId);
        Assert.Equal(201, point.GameDay);
        Assert.Equal(2500, point.Profit);
    });
```

- [ ] **Step 2: Run to verify it fails**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "Build_creates_city_route_trailer_and_trend_read_models_from_jobs" -v minimal
```

Expected: FAIL — trailer trend entity_id is `"trailer.reefer.1"` not `"200B-420 Texas"`.

- [ ] **Step 3: Build `trailerIdToLicensePlate` in `BuildCompany` and update the `BuildProfitTrends` call**

In `StatisticsProjection.cs` `BuildCompany`, before the `return new CompanyStatistics(...)` call (around line 165), add:
```csharp
var trailerIdToLicensePlate = trailers
    .Select(t => (TrailerId: t.Id, LicensePlate: CleanLicensePlate(FirstKnownValue(t, "license_plate"))))
    .Where(pair => !string.IsNullOrWhiteSpace(pair.LicensePlate))
    .ToDictionary(pair => pair.TrailerId, pair => pair.LicensePlate!, StringComparer.OrdinalIgnoreCase);
```

Change the `BuildProfitTrends(companyId, missionStats)` call to:
```csharp
BuildProfitTrends(companyId, missionStats, trailerIdToLicensePlate)
```

- [ ] **Step 4: Update `BuildProfitTrends` signature and trailer grouping**

Change the method signature from:
```csharp
private static IReadOnlyList<TrendPointStatistic> BuildProfitTrends(
    string companyId,
    IReadOnlyCollection<MissionStatistic> missions)
```
to:
```csharp
private static IReadOnlyList<TrendPointStatistic> BuildProfitTrends(
    string companyId,
    IReadOnlyCollection<MissionStatistic> missions,
    IReadOnlyDictionary<string, string> trailerIdToLicensePlate)
```

Change the trailer trend section from:
```csharp
trends.AddRange(timedMissions
    .Where(mission => !string.IsNullOrWhiteSpace(mission.TrailerId))
    .GroupBy(mission => mission.TrailerId!, StringComparer.OrdinalIgnoreCase)
    .SelectMany(group => BuildTrend("trailer", group.Key, group)));
```
to:
```csharp
trends.AddRange(timedMissions
    .Where(mission => !string.IsNullOrWhiteSpace(mission.TrailerId))
    .GroupBy(
        mission => trailerIdToLicensePlate.GetValueOrDefault(mission.TrailerId!) ?? mission.TrailerId!,
        StringComparer.OrdinalIgnoreCase)
    .SelectMany(group => BuildTrend("trailer", group.Key, group)));
```

- [ ] **Step 5: Run the full test suite**

```
dotnet test tests/AtsEmployeeStats.Tests -v minimal
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs tests/AtsEmployeeStats.Tests/StatisticsProjectionTests.cs
git commit -m "feat: use license plate as entity_id for trailer trend points"
```

---

## Task 3: Add `LicensePlate` to `TrailerDto` and wire through mapper

**Files:**
- Modify: `src/AtsEmployeeStats.Contracts/StatisticsDtos.cs:89-98`
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs:94-103`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs`

- [ ] **Step 1: Update the mapper test to carry `LicensePlate` through**

In `StatisticsDashboardMapperTests.cs` around line 171, change:
```csharp
new TrailerStatistic("trailer.reefer.1", "trailer_def.scs.box.reefer", 5500, 2)
```
to:
```csharp
new TrailerStatistic("trailer.reefer.1", "trailer_def.scs.box.reefer", 5500, 2, LicensePlate: "200B-420 Texas")
```

After the existing `Assert.Equal(2, trailer.JobCount);` assertion (line 199), add:
```csharp
Assert.Equal("200B-420 Texas", trailer.LicensePlate);
```

- [ ] **Step 2: Run to verify it fails**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "ToDashboardDto_maps_city_route_trailer_and_sparkline_read_models" -v minimal
```

Expected: FAIL — `TrailerDto` has no `LicensePlate` property.

- [ ] **Step 3: Add `LicensePlate` to `TrailerDto`**

In `StatisticsDtos.cs` lines 89-98, change:
```csharp
public sealed record TrailerDto(
    string Id,
    string TrailerType,
    long Profit,
    int JobCount,
    bool IsArticulated = false,
    string? BodyType = null,
    long ProfitPerDay = 0,
    SparklineDto? Trend = null,
    string? GarageId = null);
```
to:
```csharp
public sealed record TrailerDto(
    string Id,
    string TrailerType,
    long Profit,
    int JobCount,
    bool IsArticulated = false,
    string? BodyType = null,
    long ProfitPerDay = 0,
    SparklineDto? Trend = null,
    string? GarageId = null,
    string? LicensePlate = null);
```

- [ ] **Step 4: Wire `LicensePlate` through the mapper and update sparkline entity_id**

In `StatisticsDashboardMapper.cs` around line 94, change the `trailerDtos` construction from:
```csharp
var trailerDtos = company.Trailers.Select(trailer => new TrailerDto(
    trailer.Id,
    trailer.TrailerType,
    trailerRangeProfit.GetValueOrDefault(trailer.Id),
    trailer.JobCount,
    trailer.IsArticulated,
    trailer.BodyType,
    MoneyPerDay(trailerRangeProfit.GetValueOrDefault(trailer.Id), rangeDays),
    ToSparkline(company.ProfitTrends, "trailer", trailer.Id, fromDay, toDay),
    trailer.GarageId));
```
to:
```csharp
var trailerDtos = company.Trailers.Select(trailer => new TrailerDto(
    trailer.Id,
    trailer.TrailerType,
    trailerRangeProfit.GetValueOrDefault(trailer.Id),
    trailer.JobCount,
    trailer.IsArticulated,
    trailer.BodyType,
    MoneyPerDay(trailerRangeProfit.GetValueOrDefault(trailer.Id), rangeDays),
    ToSparkline(company.ProfitTrends, "trailer", trailer.LicensePlate ?? trailer.Id, fromDay, toDay),
    trailer.GarageId,
    trailer.LicensePlate));
```

- [ ] **Step 5: Run the full suite**

```
dotnet test tests/AtsEmployeeStats.Tests -v minimal
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add src/AtsEmployeeStats.Contracts/StatisticsDtos.cs src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs
git commit -m "feat: add LicensePlate to TrailerDto and wire through mapper"
```

---

## Task 4: Migrate `silver_trailers` schema and persist `license_plate`

**Files:**
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`
- Test: `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs`

Background: Like Phase 1's `MigrateCompanySurrogateKeyAsync`, we need a one-time structural migration: add `id INTEGER PRIMARY KEY`, `company_pk INTEGER` (FK to silver_companies.id), and `license_plate TEXT`. Silver/gold tables are deleted-and-refilled on every ingest — they are NOT dropped — so a structural migration is required for existing databases.

- [ ] **Step 1: Add `license_plate` to `WriteAnalyticSaveAsync` in the test file**

In `SqliteMedallionSaveSnapshotSourceTests.cs` around line 595, change:
```
trailer : trailer.reefer.1 {
  trailer_definition: trailer_def.scs.box.reefer
}
```
to:
```
trailer : trailer.reefer.1 {
  trailer_definition: trailer_def.scs.box.reefer
  license_plate: "200B-420|texas"
}
```

(`CleanLicensePlate("200B-420|texas")` → `"200B-420 Texas"`)

- [ ] **Step 2: Write the failing test**

Add a new test after `StatisticsService_assigns_integer_surrogate_key_to_silver_companies` (after line 495):

```csharp
[Fact]
public async Task StatisticsService_persists_trailer_license_plate_to_silver_trailers()
{
    await WriteAnalyticSaveAsync();
    var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
    var service = new StatisticsService(source);

    await service.IngestAsync(CancellationToken.None);

    using var connection = OpenTestConnection();
    await connection.OpenAsync();
    await using var cmd = connection.CreateCommand();
    cmd.CommandText = "select license_plate, company_pk from silver_trailers where company_id = 'desert-line' and trailer_id = 'trailer.reefer.1'";
    await using var reader = await cmd.ExecuteReaderAsync();
    Assert.True(await reader.ReadAsync());
    Assert.Equal("200B-420 Texas", reader.GetString(0));
    Assert.True(reader.GetInt64(1) > 0);
}
```

- [ ] **Step 3: Run to verify it fails**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "StatisticsService_persists_trailer_license_plate_to_silver_trailers" -v minimal
```

Expected: FAIL — `license_plate` column missing.

- [ ] **Step 4: Update the initial DDL for `silver_trailers`**

In `SqliteMedallionSaveSnapshotSource.cs` around line 449, change:
```sql
create table if not exists silver_trailers (
    company_id text not null,
    trailer_id text not null,
    trailer_type text not null,
    profit integer not null,
    job_count integer not null,
    primary key (company_id, trailer_id)
);
```
to:
```sql
create table if not exists silver_trailers (
    id integer primary key,
    company_id text not null,
    company_pk integer,
    trailer_id text not null,
    trailer_type text not null,
    profit integer not null,
    job_count integer not null,
    license_plate text
);
```

(The columns `body_type`, `is_articulated`, `garage_id` are added via `EnsureColumnAsync` calls that follow — keep those.)

- [ ] **Step 5: Add a `MigrateTrailerSchemaAsync` method and call it**

After the `MigrateCompanySurrogateKeyAsync` method (around line 692), add:

```csharp
private static async Task MigrateTrailerSchemaAsync(
    SqliteConnection connection,
    CancellationToken cancellationToken)
{
    await using var check = connection.CreateCommand();
    check.CommandText = "pragma table_info(silver_trailers)";
    await using var reader = await check.ExecuteReaderAsync(cancellationToken);
    var hasIdColumn = false;
    while (await reader.ReadAsync(cancellationToken))
    {
        if (string.Equals(reader.GetString(1), "id", StringComparison.OrdinalIgnoreCase))
        {
            hasIdColumn = true;
            break;
        }
    }

    if (hasIdColumn)
    {
        return;
    }

    await ExecuteAsync(connection, "drop table if exists silver_trailers", cancellationToken);
    await ExecuteAsync(
        connection,
        """
        create table silver_trailers (
            id integer primary key,
            company_id text not null,
            company_pk integer,
            trailer_id text not null,
            trailer_type text not null,
            profit integer not null,
            job_count integer not null,
            body_type text,
            is_articulated integer,
            garage_id text,
            license_plate text
        )
        """,
        cancellationToken);
}
```

At the end of the `EnsureSchemaAsync` method (around line 655, after the existing `EnsureColumnAsync` calls), add:
```csharp
await MigrateTrailerSchemaAsync(connection, cancellationToken);
```

- [ ] **Step 6: Add `EnsureColumnAsync` for `license_plate` on `silver_trailers`**

In the `EnsureColumnAsync` block (around line 654), add:
```csharp
await EnsureColumnAsync(connection, "silver_trailers", "license_plate", "text", cancellationToken);
```
(Needed for databases that have the `id` column already but were created before `license_plate` was added.)

- [ ] **Step 7: Read company_pk after company INSERT and pass to trailer INSERT**

In `PersistSilverAndGoldAsync` (around line 1247), change the company insert block from:

```csharp
await ExecuteAsync(
    connection,
    """
    insert into silver_companies (company_id, display_name, last_updated_utc)
    values ($company_id, $display_name, $last_updated_utc)
    """,
    cancellationToken,
    ("$company_id", company.Id),
    ("$display_name", company.DisplayName),
    ("$last_updated_utc", FormatUtc(company.LastUpdated.UtcDateTime)));
```

to:

```csharp
await ExecuteAsync(
    connection,
    """
    insert into silver_companies (company_id, display_name, last_updated_utc)
    values ($company_id, $display_name, $last_updated_utc)
    """,
    cancellationToken,
    ("$company_id", company.Id),
    ("$display_name", company.DisplayName),
    ("$last_updated_utc", FormatUtc(company.LastUpdated.UtcDateTime)));

long companyPk;
await using (var pkCmd = connection.CreateCommand())
{
    pkCmd.CommandText = "select id from silver_companies where company_id = $company_id";
    Add(pkCmd, "$company_id", company.Id);
    companyPk = (long)(await pkCmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
}
```

- [ ] **Step 8: Update the `silver_trailers` INSERT to include `company_pk` and `license_plate`**

Change the trailer INSERT (around line 1394) from:
```csharp
await ExecuteAsync(
    connection,
    """
    insert into silver_trailers (company_id, trailer_id, trailer_type, profit, job_count, body_type, is_articulated, garage_id)
    values ($company_id, $trailer_id, $trailer_type, $profit, $job_count, $body_type, $is_articulated, $garage_id)
    """,
    cancellationToken,
    ("$company_id", company.Id),
    ("$trailer_id", trailer.Id),
    ("$trailer_type", trailer.TrailerType),
    ("$profit", trailer.Profit),
    ("$job_count", trailer.JobCount),
    ("$body_type", trailer.BodyType),
    ("$is_articulated", trailer.IsArticulated ? 1 : 0),
    ("$garage_id", trailer.GarageId));
```
to:
```csharp
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
```

Note: `companyPk` must be declared before the `foreach (var garage in company.Garages)` loop, not inside it. Move the declaration to be in scope for all inserts within the `foreach (var company in statistics.Companies)` loop.

- [ ] **Step 9: Update the SELECT in `ReadTrailersAsync` to include `license_plate`**

In `ReadTrailersAsync` (around line 2198), change the SQL from:
```sql
select trailer_id, trailer_type, profit, job_count, body_type, is_articulated, garage_id
from silver_trailers
where company_id = $company_id
order by profit desc, trailer_id
```
to:
```sql
select trailer_id, trailer_type, profit, job_count, body_type, is_articulated, garage_id, license_plate
from silver_trailers
where company_id = $company_id
order by profit desc, trailer_id
```

Change the reader call (around line 2208) from:
```csharp
values.Add(new TrailerStatistic(
    reader.GetString(0),
    reader.GetString(1),
    reader.GetInt64(2),
    reader.GetInt32(3),
    IsArticulated: reader.IsDBNull(5) ? false : reader.GetInt32(5) != 0,
    BodyType: GetNullableString(reader, 4),
    GarageId: GetNullableString(reader, 6)));
```
to:
```csharp
values.Add(new TrailerStatistic(
    reader.GetString(0),
    reader.GetString(1),
    reader.GetInt64(2),
    reader.GetInt32(3),
    IsArticulated: reader.IsDBNull(5) ? false : reader.GetInt32(5) != 0,
    BodyType: GetNullableString(reader, 4),
    GarageId: GetNullableString(reader, 6),
    LicensePlate: GetNullableString(reader, 7)));
```

- [ ] **Step 10: Run the full suite**

```
dotnet test tests/AtsEmployeeStats.Tests -v minimal
```

Expected: all pass.

- [ ] **Step 11: Commit**

```bash
git add src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs
git commit -m "feat: migrate silver_trailers schema; persist license_plate and company_pk"
```

---

## Task 5: Change `FindTrailer` to match on `LicensePlate`

**Files:**
- Modify: `src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs:19-20`
- Test: `tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs`

- [ ] **Step 1: Update the `FindTrailer` test**

In `DashboardViewModelTests.cs`, find the `TrailerDto` constructed around line 133:
```csharp
new TrailerDto("trailer.reefer.1", "reefer", 4_500, 2),
```
Change it to:
```csharp
new TrailerDto("trailer.reefer.1", "reefer", 4_500, 2, LicensePlate: "200B-420 Texas"),
```

Find the `FindTrailer` assertion around line 68:
```csharp
Assert.Equal("trailer.reefer.1", DashboardViewModel.FindTrailer(company, "trailer.reefer.1")?.Id);
```
Change it to:
```csharp
Assert.Equal("trailer.reefer.1", DashboardViewModel.FindTrailer(company, "200B-420 Texas")?.Id);
```

- [ ] **Step 2: Run to verify it fails**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "DashboardViewModelTests" -v minimal
```

Expected: FAIL — `FindTrailer` still matches on `Id`.

- [ ] **Step 3: Change `FindTrailer` to match on `LicensePlate`**

In `DashboardViewModel.cs` lines 19-20, change:
```csharp
public static TrailerDto? FindTrailer(CompanyDto company, string trailerId) =>
    (company.Trailers ?? []).FirstOrDefault(trailer => IdEquals(trailer.Id, trailerId));
```
to:
```csharp
public static TrailerDto? FindTrailer(CompanyDto company, string licensePlate) =>
    (company.Trailers ?? []).FirstOrDefault(trailer => IdEquals(trailer.LicensePlate, licensePlate));
```

- [ ] **Step 4: Run the full suite**

```
dotnet test tests/AtsEmployeeStats.Tests -v minimal
```

Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs
git commit -m "feat: FindTrailer matches on LicensePlate instead of Id"
```

---

## Task 6: Update API endpoint and tests to use license plate URL

**Files:**
- Modify: `src/AtsEmployeeStats.Api/Program.cs:137-149`
- Modify: `tests/AtsEmployeeStats.Tests/StatisticsApiTests.cs`

- [ ] **Step 1: Add `license_plate` to the test save data in `TestSaveSnapshotSource`**

In `StatisticsApiTests.cs` around line 357, change:
```
trailer : trailer.reefer.1 {
  trailer_definition: trailer_def.scs.box.reefer
}
```
to:
```
trailer : trailer.reefer.1 {
  trailer_definition: trailer_def.scs.box.reefer
  license_plate: "200B-420|texas"
}
```

`CleanLicensePlate("200B-420|texas")` → `"200B-420 Texas"`. URL-encoded: `200B-420%20Texas`.

- [ ] **Step 2: Update all three trailer URL assertions in the API tests**

Change all occurrences of:
```csharp
await client.GetFromJsonAsync<TrailerDto>("/api/companies/desert-line/trailers/trailer.reefer.1")
```
to:
```csharp
await client.GetFromJsonAsync<TrailerDto>("/api/companies/desert-line/trailers/200B-420%20Texas")
```

These appear at lines 88, 140, and 285.

- [ ] **Step 3: Run to verify the tests fail**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "StatisticsApiTests" -v minimal
```

Expected: FAIL — API still matches on `trailer.Id` so the new URL returns 404/null.

- [ ] **Step 4: Update the API endpoint in `Program.cs`**

Change the endpoint (around lines 137-149) from:
```csharp
app.MapGet("/api/companies/{companyId}/trailers/{trailerId}", async (
    string companyId,
    string trailerId,
    int? fromDay,
    int? toDay,
    [FromServices] StatisticsService service,
    [FromServices] StatisticsHub hub,
    CancellationToken cancellationToken) =>
{
    var company = FindCompany(await LoadDashboardAsync(fromDay, toDay, service, hub, cancellationToken), companyId);
    var trailer = company?.Trailers?.FirstOrDefault(trailer => IdEquals(trailer.Id, trailerId));
    return trailer is null ? Results.NotFound() : Results.Ok(trailer);
});
```
to:
```csharp
app.MapGet("/api/companies/{companyId}/trailers/{licensePlate}", async (
    string companyId,
    string licensePlate,
    int? fromDay,
    int? toDay,
    [FromServices] StatisticsService service,
    [FromServices] StatisticsHub hub,
    CancellationToken cancellationToken) =>
{
    var company = FindCompany(await LoadDashboardAsync(fromDay, toDay, service, hub, cancellationToken), companyId);
    var trailer = company?.Trailers?.FirstOrDefault(trailer => IdEquals(trailer.LicensePlate, licensePlate));
    return trailer is null ? Results.NotFound() : Results.Ok(trailer);
});
```

Note: read `Program.cs` before editing to confirm the exact parameter annotations used in the surrounding endpoints (the `[FromServices]` annotations may differ from what's shown above — match the existing style).

- [ ] **Step 5: Run the full suite**

```
dotnet test tests/AtsEmployeeStats.Tests -v minimal
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add src/AtsEmployeeStats.Api/Program.cs tests/AtsEmployeeStats.Tests/StatisticsApiTests.cs
git commit -m "feat: trailer API endpoint matches on license plate"
```

---

## Task 7: Update Razor pages — trailer links and detail route

**Files:**
- Modify: `src/AtsEmployeeStats.Web/Pages/TrailerDetail.razor`
- Modify: `src/AtsEmployeeStats.Web/Pages/CompanyDetail.razor`
- Modify: `src/AtsEmployeeStats.Web/Pages/GarageDetail.razor`

(No unit tests — verify by running the app and navigating to a trailer detail page)

- [ ] **Step 1: Update `TrailerDetail.razor` — route directive**

Change line 1 from:
```razor
@page "/companies/{CompanyId}/trailers/{TrailerId}"
```
to:
```razor
@page "/companies/{CompanyId}/trailers/{LicensePlate}"
```

- [ ] **Step 2: Update `TrailerDetail.razor` — header and breadcrumb**

Change the `<h1>` from:
```razor
<h1>@(SelectedTrailer?.Id ?? "Trailer")</h1>
```
to:
```razor
<h1>@(SelectedTrailer?.LicensePlate ?? LicensePlate)</h1>
```

Change the breadcrumb `<span>` from:
```razor
<span>@SelectedTrailer.Id</span>
```
to:
```razor
<span>@(SelectedTrailer.LicensePlate ?? LicensePlate)</span>
```

- [ ] **Step 3: Update `TrailerDetail.razor` — detail table row**

Change the "Trailer id" row from:
```razor
<tr><td>Trailer id</td><td>@SelectedTrailer.Id</td></tr>
```
to:
```razor
<tr><td>License plate</td><td>@(SelectedTrailer.LicensePlate ?? "-")</td></tr>
```

- [ ] **Step 4: Update `TrailerDetail.razor` — `@code` block**

Change the entire `@code` block from:
```csharp
@code {
    [Parameter] public string CompanyId { get; set; } = string.Empty;
    [Parameter] public string TrailerId { get; set; } = string.Empty;

    private string _activeTab = "details";

    private CompanyDto? SelectedCompany => DashboardViewModel.FindCompany(Statistics, CompanyId);
    private TrailerDto? SelectedTrailer => SelectedCompany is null ? null : DashboardViewModel.FindTrailer(SelectedCompany, TrailerId);
    private string TabClass(string tab) => _activeTab == tab ? "active" : string.Empty;
}
```
to:
```csharp
@code {
    [Parameter] public string CompanyId { get; set; } = string.Empty;
    [Parameter] public string LicensePlate { get; set; } = string.Empty;

    private string _activeTab = "details";

    private CompanyDto? SelectedCompany => DashboardViewModel.FindCompany(Statistics, CompanyId);
    private TrailerDto? SelectedTrailer => SelectedCompany is null ? null : DashboardViewModel.FindTrailer(SelectedCompany, LicensePlate);
    private string TabClass(string tab) => _activeTab == tab ? "active" : string.Empty;
}
```

- [ ] **Step 5: Update `CompanyDetail.razor` — trailer list "View" link**

Change the trailer "View" link (around line 243) from:
```razor
<NavLink class="button-link" href="@($"/companies/{Segment(SelectedCompany.Id)}/trailers/{Segment(trailer.Id)}")">View</NavLink>
```
to:
```razor
<NavLink class="button-link" href="@($"/companies/{Segment(SelectedCompany.Id)}/trailers/{Segment(trailer.LicensePlate ?? trailer.Id)}")">View</NavLink>
```

- [ ] **Step 6: Update `CompanyDetail.razor` — job→trailer link (inline lookup)**

Replace the job→trailer link block (around lines 298-302):
```razor
@if (!string.IsNullOrWhiteSpace(job.TrailerId))
{
    var trailerBodyType = GetTrailerBodyLabel(SelectedCompany, job.TrailerId);
    <NavLink href="@($"/companies/{Segment(SelectedCompany.Id)}/trailers/{Segment(job.TrailerId)}")">@trailerBodyType</NavLink>
}
```
with:
```razor
@if (!string.IsNullOrWhiteSpace(job.TrailerId))
{
    var t = (SelectedCompany.Trailers ?? []).FirstOrDefault(x => string.Equals(x.Id, job.TrailerId, StringComparison.OrdinalIgnoreCase));
    var trailerSegment = t?.LicensePlate ?? job.TrailerId;
    var trailerLabel = t is not null ? FormatBodyType(t.BodyType ?? t.TrailerType) : job.TrailerId;
    <NavLink href="@($"/companies/{Segment(SelectedCompany.Id)}/trailers/{Segment(trailerSegment)}")">@trailerLabel</NavLink>
}
```

- [ ] **Step 7: Remove `GetTrailerBodyLabel` from `CompanyDetail.razor`**

Delete the private method from the `@code` block (around lines 421-426):
```csharp
private static string GetTrailerBodyLabel(CompanyDto company, string trailerId)
{
    var trailer = DashboardViewModel.FindTrailer(company, trailerId);
    if (trailer is null) return trailerId;
    return FormatBodyType(trailer.BodyType ?? trailer.TrailerType);
}
```

- [ ] **Step 8: Update `GarageDetail.razor` — trailer "View" link**

Change the trailer "View" link (around line 177) from:
```razor
<NavLink class="button-link" href="@($"/companies/{Segment(SelectedCompany.Id)}/trailers/{Segment(trailer.Id)}")">View</NavLink>
```
to:
```razor
<NavLink class="button-link" href="@($"/companies/{Segment(SelectedCompany.Id)}/trailers/{Segment(trailer.LicensePlate ?? trailer.Id)}")">View</NavLink>
```

- [ ] **Step 9: Run the full test suite**

```
dotnet test tests/AtsEmployeeStats.Tests -v minimal
```

Expected: all pass.

- [ ] **Step 10: Commit**

```bash
git add src/AtsEmployeeStats.Web/Pages/TrailerDetail.razor src/AtsEmployeeStats.Web/Pages/CompanyDetail.razor src/AtsEmployeeStats.Web/Pages/GarageDetail.razor
git commit -m "feat: trailer detail route uses license plate; update all trailer links"
```

---

## Self-Review

### Spec Coverage Check

| Spec item | Covered |
|-----------|---------|
| `BuildTrailerStats`: read `license_plate` from trailer SiiUnit | ✅ Task 1 |
| `TrailerStatistic`: add `LicensePlate` | ✅ Task 1 |
| `TrailerDto`: add `LicensePlate` | ✅ Task 3 |
| `StatisticsDashboardMapper`: pass `LicensePlate`; use as sparkline entity_id | ✅ Task 2 + 3 |
| `silver_trailers`: add `id`, `company_pk`, `license_plate` | ✅ Task 4 |
| Insert: populate `company_pk`; populate `license_plate` | ✅ Task 4 |
| `TrailerDetail` route: `{LicensePlate}` | ✅ Task 7 |
| `DashboardViewModel.FindTrailer`: match on `LicensePlate` | ✅ Task 5 |
| `StatisticsClient`: URL builder update | N/A — client has no trailer URL builder; URLs are built in Razor components |

### Placeholder Check

No TBDs, TODOs, or "similar to Task N" references. All code blocks are complete.

### Type Consistency Check

- `TrailerStatistic.LicensePlate` (string?) — defined Task 1, used Tasks 2, 3, 4
- `TrailerDto.LicensePlate` (string?) — defined Task 3, used Tasks 5, 6, 7
- `companyPk` (long) — declared immediately after company INSERT in Task 4, used in trailer INSERT in the same company loop body
- `FindTrailer(company, licensePlate)` — matches on `trailer.LicensePlate` in Task 5; called with `LicensePlate` route param in TrailerDetail in Task 7
- `GetTrailerBodyLabel` — removed in Task 7 Step 7; no remaining callers
