# Garage Trailers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Trailers count column to the garages list and a Trailers tab to the garage detail page showing which player-owned trailers have been used by trucks at that garage.

**Architecture:** `TrailerCount` is computed in the mapper from missions attributed to each garage (via `mission.GarageId`), stored on `GarageDto`, and used for the summary card and sortable column. `GetGarageTrailers` in `DashboardViewModel` derives the per-garage trailer list client-side by joining through trucks (since `MissionDto` has no `GarageId`). Only player-owned trailers appear — job-provided trailers have null `TrailerId` and are excluded at both layers.

**Tech Stack:** C# / ASP.NET / Blazor, xUnit tests

---

## Files

| File | Change |
|---|---|
| `src/AtsEmployeeStats.Contracts/StatisticsDtos.cs` | Add `TrailerCount = 0` to `GarageDto` |
| `src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs` | Compute per-garage trailer count; add `trailerCount` sort key |
| `src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs` | Add `GetGarageTrailers` |
| `src/AtsEmployeeStats.Web/Pages/DashboardBase.cs` | Move `FormatBodyType` here (needed by both CompanyDetail and GarageDetail) |
| `src/AtsEmployeeStats.Web/Pages/CompanyDetail.razor` | Add sortable Trailers column; remove `FormatBodyType` (now in DashboardBase) |
| `src/AtsEmployeeStats.Web/Pages/GarageDetail.razor` | Add Trailers summary card, tab button, and tab content |
| `tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs` | Add test for garage trailer counts |
| `tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs` | Add tests for `GetGarageTrailers` |

---

### Task 1: Garage trailer count in the data layer

**Files:**
- Modify: `src/AtsEmployeeStats.Contracts/StatisticsDtos.cs`
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs`
- Modify: `tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `StatisticsDashboardMapperTests`:

```csharp
[Fact]
public void ToDashboardDto_shows_correct_player_owned_trailer_count_per_garage()
{
    var statistics = new AtsStatistics(
        new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero),
        [
            new CompanyStatistics(
                "desert-line", "Desert Line",
                new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero),
                [
                    new GarageStatistic("garage.phoenix", "Phoenix", 0, 1, 1),
                    new GarageStatistic("garage.denver", "Denver", 0, 1, 1)
                ],
                [],
                [],
                [
                    // Phoenix garage: two distinct player-owned trailers
                    new MissionStatistic("job.1", null, null, "trailer.reefer.1", "reefer", null, "phoenix", "denver", 1000, GarageId: "garage.phoenix"),
                    new MissionStatistic("job.2", null, null, "trailer.flatbed.1", "flatbed", null, "denver", "phoenix", 800, GarageId: "garage.phoenix"),
                    // Same trailer again at phoenix — still counts as one
                    new MissionStatistic("job.3", null, null, "trailer.reefer.1", "reefer", null, "phoenix", "tucson", 600, GarageId: "garage.phoenix"),
                    // Job-provided trailer (TrailerId null) — must not be counted
                    new MissionStatistic("job.4", null, null, null, "reefer", null, "phoenix", "vegas", 500, GarageId: "garage.phoenix"),
                    // Denver garage: only one trailer
                    new MissionStatistic("job.5", null, null, "trailer.reefer.1", "reefer", null, "denver", "phoenix", 700, GarageId: "garage.denver"),
                ],
                [],
                [],
                [
                    new TrailerStatistic("trailer.reefer.1", "reefer", 0, 0),
                    new TrailerStatistic("trailer.flatbed.1", "flatbed", 0, 0)
                ],
                [], [], [], [], [])
        ]);

    var dto = StatisticsDashboardMapper.ToDashboardDto(statistics);

    var company = Assert.Single(dto.Companies);
    var phoenix = Assert.Single(company.Garages, g => g.Id == "garage.phoenix");
    var denver = Assert.Single(company.Garages, g => g.Id == "garage.denver");
    Assert.Equal(2, phoenix.TrailerCount);
    Assert.Equal(1, denver.TrailerCount);
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "ToDashboardDto_shows_correct_player_owned_trailer_count_per_garage"
```

Expected: compile error — `GarageDto` has no `TrailerCount` property.

- [ ] **Step 3: Add `TrailerCount` to `GarageDto`**

In `src/AtsEmployeeStats.Contracts/StatisticsDtos.cs`, change `GarageDto` to:

```csharp
public sealed record GarageDto(
    string Id,
    string DisplayName,
    long Profit,
    long ProfitPerDay,
    int EmployeeCount,
    int TruckCount,
    SparklineDto? Trend = null,
    int TrailerCount = 0);
```

- [ ] **Step 4: Compute per-garage trailer count in the mapper**

In `src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs`, add the `garageTrailerCount` dictionary directly before the `garageDtos` variable (around line 62). Use `mission.GarageId` — this is already set during projection and correctly represents which garage the job was attributed to:

```csharp
var garageTrailerCount = filteredMissions
    .Where(m => m.GarageId != null && m.TrailerId != null)
    .GroupBy(m => m.GarageId!, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(
        g => g.Key,
        g => g.Select(m => m.TrailerId!).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
        StringComparer.OrdinalIgnoreCase);
```

Then add `TrailerCount` to the `GarageDto` constructor call in `garageDtos`:

```csharp
var garageDtos = company.Garages.Select(garage => new GarageDto(
    garage.Id,
    garage.DisplayName,
    garageProfit.GetValueOrDefault(garage.Id),
    MoneyPerDay(garageProfit.GetValueOrDefault(garage.Id), rangeDays),
    garage.EmployeeCount,
    garage.TruckCount,
    ToSparkline(company.ProfitTrends, "garage", garage.Id, fromDay, toDay),
    garageTrailerCount.GetValueOrDefault(garage.Id)));
```

Then add `trailerCount` to the `SortedList` call for garages (around line 138):

```csharp
SortedList(garageDtos, sort?.GaragesSortBy, sort?.GaragesSortDir, "profit",
    ("name", g => (IComparable?)g.DisplayName),
    ("profit", g => g.Profit),
    ("profitPerDay", g => g.ProfitPerDay),
    ("driverCount", g => (IComparable?)g.EmployeeCount),
    ("truckCount", g => (IComparable?)g.TruckCount),
    ("trailerCount", g => (IComparable?)g.TrailerCount)),
```

- [ ] **Step 5: Run tests to verify they pass**

```
dotnet test tests/AtsEmployeeStats.Tests
```

Expected: all tests pass including the new one.

- [ ] **Step 6: Commit**

```
git add src/AtsEmployeeStats.Contracts/StatisticsDtos.cs src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs
git commit -m "feat: add TrailerCount to GarageDto, computed from mission attribution"
```

---

### Task 2: `GetGarageTrailers` in `DashboardViewModel`

**Files:**
- Modify: `src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs`
- Modify: `tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Add both tests to `DashboardViewModelTests`. The existing `CreateCompany()` fixture already has the right structure:
- `truck.current` and `truck.assigned` are at `garage.phoenix`
- `truck.historical` and `truck.other` are at `garage.denver`
- `job.1`: `truck.current` (phoenix) → `trailer.reefer.1`
- `job.2`: `truck.historical` (denver) → `trailer.reefer.1`
- `job.3`: `truck.other` (denver) → `trailer.dryvan.1`

```csharp
[Fact]
public void GetGarageTrailers_returns_only_player_owned_trailers_used_by_trucks_at_that_garage()
{
    var company = CreateCompany();

    var phoenixTrailers = DashboardViewModel.GetGarageTrailers(company, "garage.phoenix");
    var denverTrailers = DashboardViewModel.GetGarageTrailers(company, "garage.denver");

    // Only truck.current (phoenix) used a trailer — trailer.reefer.1 via job.1
    Assert.Equal(["trailer.reefer.1"], phoenixTrailers.Select(t => t.Id));
    // truck.historical used trailer.reefer.1 (job.2), truck.other used trailer.dryvan.1 (job.3)
    Assert.Equal(
        new[] { "trailer.dryvan.1", "trailer.reefer.1" },
        denverTrailers.Select(t => t.Id).OrderBy(x => x));
}

[Fact]
public void GetGarageTrailers_excludes_job_provided_trailers_that_have_no_trailer_id()
{
    var company = CreateCompany() with
    {
        Missions = [
            new MissionDto("job.no-trailer", "driver.alice", "truck.current", "reefer", "food", "phoenix", "denver", 1_000)
            // TrailerId defaults to null — this is a job-provided trailer
        ]
    };

    var trailers = DashboardViewModel.GetGarageTrailers(company, "garage.phoenix");

    Assert.Empty(trailers);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "GetGarageTrailers"
```

Expected: compile error — `GetGarageTrailers` does not exist.

- [ ] **Step 3: Add `GetGarageTrailers` to `DashboardViewModel`**

Add after `GetGarageTrucks` in `src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs`:

```csharp
public static IReadOnlyList<TrailerDto> GetGarageTrailers(CompanyDto company, string garageId)
{
    var truckIds = GetGarageTrucks(company, garageId)
        .Select(t => t.Id)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var trailerIds = company.Missions
        .Where(m => !string.IsNullOrWhiteSpace(m.TruckId) && truckIds.Contains(m.TruckId!)
                 && !string.IsNullOrWhiteSpace(m.TrailerId))
        .Select(m => m.TrailerId!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    return (company.Trailers ?? [])
        .Where(t => trailerIds.Contains(t.Id))
        .ToList();
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/AtsEmployeeStats.Tests
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```
git add src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs
git commit -m "feat: add GetGarageTrailers to DashboardViewModel"
```

---

### Task 3: Trailers column on the garages list

**Files:**
- Modify: `src/AtsEmployeeStats.Web/Pages/DashboardBase.cs`
- Modify: `src/AtsEmployeeStats.Web/Pages/CompanyDetail.razor`

- [ ] **Step 1: Move `FormatBodyType` to `DashboardBase`**

In `src/AtsEmployeeStats.Web/Pages/DashboardBase.cs`, add this protected static helper (both CompanyDetail and GarageDetail will need it):

```csharp
protected static string FormatBodyType(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return "-";
    var parts = value.Split(['.', '_'], StringSplitOptions.RemoveEmptyEntries);
    return string.Join(' ', parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
}
```

- [ ] **Step 2: Remove `FormatBodyType` from `CompanyDetail.razor`**

In `src/AtsEmployeeStats.Web/Pages/CompanyDetail.razor`, delete the private static `FormatBodyType` method from the `@code` block (it is now inherited from `DashboardBase`). The method signature to remove:

```csharp
private static string FormatBodyType(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return "-";
    var parts = value.Split(['.', '_'], StringSplitOptions.RemoveEmptyEntries);
    return string.Join(' ', parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
}
```

- [ ] **Step 3: Add the Trailers column header to the garages table**

In `CompanyDetail.razor`, inside the garages table `<thead>`, add a `SortableColumnHeader` for Trailers after the Trucks header (around line 133):

```razor
<SortableColumnHeader Column="trailerCount" ActiveColumn="@_garagesSortBy" Descending="@_garagesSortDesc" OnSort="SortGaragesAsync">Trailers</SortableColumnHeader>
```

- [ ] **Step 4: Add the Trailers count cell to each garage row**

In `CompanyDetail.razor`, inside the garages `<tbody>` `<tr>`, add the Trailers cell after `@garage.TruckCount` (around line 146):

```razor
<td>@garage.TrailerCount</td>
```

- [ ] **Step 5: Build to verify no compile errors**

```
dotnet build src/AtsEmployeeStats.Web
```

Expected: build succeeds with no errors.

- [ ] **Step 6: Commit**

```
git add src/AtsEmployeeStats.Web/Pages/DashboardBase.cs src/AtsEmployeeStats.Web/Pages/CompanyDetail.razor
git commit -m "feat: add Trailers column to garages list"
```

---

### Task 4: Trailers tab on the garage detail page

**Files:**
- Modify: `src/AtsEmployeeStats.Web/Pages/GarageDetail.razor`

- [ ] **Step 1: Add sort state and `SortTrailersAsync` to the `@code` block**

In `GarageDetail.razor`, add sort state variables alongside the existing ones:

```csharp
private string _trailersSortBy = "profit";
private bool _trailersSortDesc = true;
```

Add the sort handler method alongside `SortDriversAsync` and `SortTrucksAsync`:

```csharp
private async Task SortTrailersAsync((string Column, bool Descending) sort)
{
    (_trailersSortBy, _trailersSortDesc) = sort;
    await SetCollectionSortAsync(CollectionSort with { TrailersSortBy = _trailersSortBy, TrailersSortDir = _trailersSortDesc ? null : "asc" });
}
```

- [ ] **Step 2: Add `Trailers` to the detail-summary section**

In `GarageDetail.razor`, add a Trailers stat card to `<section class="detail-summary">` after the Trucks card (around line 79). Use `SelectedGarage.TrailerCount` directly — no need to call `GetGarageTrailers` just for a number:

```razor
<div>
    <span>Trailers</span>
    <strong>@SelectedGarage.TrailerCount</strong>
</div>
```

- [ ] **Step 3: Add the Trailers tab button**

In `GarageDetail.razor`, add the Trailers tab button to `<nav class="tabs">` after the Trucks button (around line 86):

```razor
<button class="@TabClass("trailers")" @onclick="@(() => _activeTab = "trailers")">Trailers</button>
```

- [ ] **Step 4: Add the trailers tab content**

In `GarageDetail.razor`, add the trailers section inside `<section class="content-stack">`, after the trucks `else` block (before the closing `}` of the outer `if`/`else`). `GetGarageTrailers` returns trailers already ordered by whatever `TrailersSortBy` the server applied to `company.Trailers`:

```razor
else if (_activeTab == "trailers")
{
    <table>
        <caption>Trailers - @SelectedGarage.DisplayName</caption>
        <thead>
            <tr>
                <SortableColumnHeader Column="name" ActiveColumn="@_trailersSortBy" Descending="@_trailersSortDesc" OnSort="SortTrailersAsync">Trailer</SortableColumnHeader>
                <SortableColumnHeader Column="profit" ActiveColumn="@_trailersSortBy" Descending="@_trailersSortDesc" OnSort="SortTrailersAsync">Profit</SortableColumnHeader>
                <SortableColumnHeader Column="profitPerDay" ActiveColumn="@_trailersSortBy" Descending="@_trailersSortDesc" OnSort="SortTrailersAsync">Avg $/day</SortableColumnHeader>
                <SortableColumnHeader Column="jobCount" ActiveColumn="@_trailersSortBy" Descending="@_trailersSortDesc" OnSort="SortTrailersAsync">Jobs</SortableColumnHeader>
                <th></th>
                <th></th>
            </tr>
        </thead>
        <tbody>
            @foreach (var trailer in DashboardViewModel.GetGarageTrailers(SelectedCompany, SelectedGarage.Id))
            {
                <tr>
                    <td>
                        @FormatBodyType(trailer.BodyType ?? trailer.TrailerType)
                        @if (trailer.IsArticulated)
                        {
                            <span class="badge">· Double</span>
                        }
                    </td>
                    <td>@Money(trailer.Profit)</td>
                    <td>@Money(trailer.ProfitPerDay)</td>
                    <td>@trailer.JobCount</td>
                    <td>@SparklineSvg(trailer.Trend)</td>
                    <td class="action-cell">
                        <NavLink class="button-link" href="@($"/companies/{Segment(SelectedCompany.Id)}/trailers/{Segment(trailer.Id)}")">View</NavLink>
                    </td>
                </tr>
            }
        </tbody>
    </table>
}
```

- [ ] **Step 5: Build to verify no compile errors**

```
dotnet build src/AtsEmployeeStats.Web
```

Expected: build succeeds.

- [ ] **Step 6: Run all tests**

```
dotnet test tests/AtsEmployeeStats.Tests
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```
git add src/AtsEmployeeStats.Web/Pages/GarageDetail.razor
git commit -m "feat: add Trailers tab and summary count to garage detail page"
```
