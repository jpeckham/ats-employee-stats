# Garage Trailers — Design Spec
_2026-05-30_

## Summary

Two additions: (1) a **Trailers** count column on the garages list in `CompanyDetail`, and (2) a **Trailers** tab on the garage detail page showing which player-owned trailers have been used by trucks assigned to that garage.

---

## Ownership distinction

Player-owned trailers exist as persistent `trailer` units in the save file and appear in `company.Trailers`. Missions that used them have a non-null `TrailerId`. Job-provided trailers have no individual unit — only `TrailerType` is captured. The Trailers tab and count therefore show **player-owned trailers only**. This is consistent with the existing Jobs tab design (which links player trailers and blanks job-provided ones).

Trailer-type profitability aggregations (`company.TrailerTypes`) already include all jobs regardless of ownership and are unaffected by this change.

---

## Garage-to-trailer association

Trailers have no direct `GarageId`. The association is derived from missions: a trailer is considered associated with a garage if at least one job was completed using that trailer by a truck currently assigned to that garage.

**Derivation path:** `garage → trucks (via truck.GarageId) → missions (via mission.TruckId) → distinct TrailerIds → TrailerDtos`

This is computable entirely from existing DTO data with no new API surface.

---

## Data layer

### `StatisticsDtos.cs`

Add `TrailerCount` to `GarageDto`:

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

### `StatisticsDashboardMapper.cs`

Before building `garageDtos`, compute per-garage distinct trailer counts from `filteredMissions`:

```csharp
var truckGarage = company.Trucks
    .Where(t => t.GarageId != null)
    .ToDictionary(t => t.Id, t => t.GarageId!, StringComparer.OrdinalIgnoreCase);

var garageTrailerCount = filteredMissions
    .Where(m => m.TruckId != null && m.TrailerId != null && truckGarage.ContainsKey(m.TruckId!))
    .GroupBy(m => truckGarage[m.TruckId!], StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key, g => g.Select(m => m.TrailerId!).Distinct(StringComparer.OrdinalIgnoreCase).Count(), StringComparer.OrdinalIgnoreCase);
```

Populate in `GarageDto`:
```csharp
TrailerCount = garageTrailerCount.GetValueOrDefault(garage.Id)
```

Add `trailerCount` sort key to the garages `SortedList` call:
```csharp
("trailerCount", g => (IComparable?)g.TrailerCount)
```

### `DashboardViewModel.cs`

Add `GetGarageTrailers`:

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
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    return (company.Trailers ?? [])
        .Where(t => trailerIds.Contains(t.Id))
        .ToList();
}
```

---

## UI layer

### `CompanyDetail.razor` — Garages table

Add a sortable **Trailers** column after Trucks:

```razor
<SortableColumnHeader Column="trailerCount" ActiveColumn="@_garagesSortBy" Descending="@_garagesSortDesc" OnSort="SortGaragesAsync">Trailers</SortableColumnHeader>
```

Add `@garage.TrailerCount` in the corresponding `<td>`.

### `GarageDetail.razor` — Summary + Trailers tab

**Summary row:** Add a Trailers stat card:
```razor
<div>
    <span>Trailers</span>
    <strong>@DashboardViewModel.GetGarageTrailers(SelectedCompany, SelectedGarage.Id).Count</strong>
</div>
```

**Tab button:** Add alongside Drivers / Trucks:
```razor
<button class="@TabClass("trailers")" @onclick="@(() => _activeTab = "trailers")">Trailers</button>
```

**Trailers tab content:** Table matching the company-level trailers table style (Type, Profit, Avg $/day, Jobs, sparkline, View link). Sort state variables `_trailersSortBy` / `_trailersSortDesc` added; sorting is applied client-side via LINQ on the result of `GetGarageTrailers`.

Columns: Body Type (with "· Double" badge if articulated), Profit, Avg $/day, Jobs, sparkline, View button.

---

## Files changed

| File | Change |
|---|---|
| `StatisticsDtos.cs` | Add `TrailerCount = 0` to `GarageDto` |
| `StatisticsDashboardMapper.cs` | Compute `garageTrailerCount`; populate in `GarageDto`; add `trailerCount` sort key |
| `DashboardViewModel.cs` | Add `GetGarageTrailers` |
| `CompanyDetail.razor` | Add sortable Trailers column to garages table |
| `GarageDetail.razor` | Add Trailers summary card, tab button, and tab content |
