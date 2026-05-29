# Company Screens Fix — Design Spec
_2026-05-28_

## Summary

Five root-cause bugs make every numeric column on the Companies screens show zero and every ID column show raw save-file identifiers. This spec fixes all of them, wires up the SCS locale pipeline for human-readable names, enriches the data model with sparklines and trailer metadata, and delivers all the UI improvements listed in the prompt.

---

## Root Causes (data layer)

### RC-1 Driver-to-job linkage broken

All 6,960 jobs are `profit_log_entry` units, which carry no `driver` field. The link is:

```
driver_ai.profit_log → profit_log.stats_data[] → profit_log_entry
```

`BuildSnapshotMissions` in `StatisticsProjection` harvests `profit_log_entry` units globally and calls `BuildMission`, which tries `FirstKnownValue(job, "driver", "employee")` — both fields absent. Every `DriverId` comes out null. Downstream: driver profit = 0, job count = 0, truck profit = 0, company profit = 0.

**Fix:** Before building missions, walk each driver's profit_log chain in `BuildCompany` and build a reverse map `Dictionary<string, string>(entryId → driverId)`. Pass this map into `BuildMission` as an extra lookup; fall back to it when `driver` and `employee` fields are absent.

### RC-2 Garage display names are unit IDs

Garage units have no `city` or `name` scalar field — those fields do not exist in ATS save files. The city is encoded in the unit ID itself: `garage.las_vegas`, `garage.cheyenne`, etc. `FirstKnownValue(garage, "city", "name")` always returns null, so `DisplayName` falls back to the full unit ID string.

**Fix:** Extract the city slug from the unit ID (everything after `"garage."`) and run it through `FormatRouteEndpoint`. `garage.las_vegas` → "las_vegas" → "Las Vegas".

Also used to build `ownedGarageCities` and `garageEligibleCityIds` in `BuildCityStats` — both currently produce wrong keys ("garage-las-vegas" ≠ "las_vegas"), so `HasOwnedGarage` and `IsGarageEligible` are always false on every city.

**Fix:** Use the extracted city slug (not the display name) when building these sets.

### RC-3 Trailer type is a nameless pointer

`BuildTrailerStats` calls `FirstKnownValue(trailer, "trailer_definition", "trailer_def", "definition")` which returns a nameless pointer like `_nameless.26b.e1c1.8190`. The `trailer_def` unit that pointer resolves to (present in the same save document) has two useful fields:
- `body_type`: "lowboy", "dropdeck", "dry_van", "reefer", etc.
- `chain_type`: "single" or "double" (double = articulated)
- `source_name`: e.g. "trailer_def.scs.lowboy.single_45.wood" (human-readable fallback)

**Fix:** In `BuildTrailerStats`, resolve the nameless trailer_definition pointer through `unitsById` to get the `trailer_def` unit, then read `body_type` and `chain_type`. Add `BodyType` and `IsArticulated` to `TrailerStatistic`.

### RC-4 Company profit totals only garage-attributed missions

`ToCompanyDto` sets `company.Profit` to `garageProfit.Values.DefaultIfEmpty(0).Sum()`. `garageProfit` is built only from missions where `DriverId != null && driverToGarage.ContainsKey(DriverId)`. With RC-1 unfixed, both conditions fail for every mission → profit = 0.

**Fix:** Change company `Profit` to `filteredMissions.Sum(m => m.Profit)` — the total of all missions in the date range regardless of driver/garage attribution. Garage-level profit stays attributed (used in garage rows).

### RC-5 trailer_id missing from gold_job_details

`silver_jobs` stores `trailer_id` but `gold_job_details` does not include it. `ReadMissionsAsync` hardcodes `TrailerId: null`. Needed for the Jobs tab to distinguish player-owned trailers from job-provided ones, and for linking trailer sparklines.

**Fix:** Add `trailer_id` column to `gold_job_details` via `EnsureColumn` migration. Populate on insert from `PersistGoldAsync`. Read in `ReadMissionsAsync`.

---

## SCS Locale Pipeline

### SCS-1 Fix extractor decrypt flag

`ProcessScsArchiveExtractor` calls:
```
scs_extractor.exe <archivePath> <outputDir>
```
locale.scs uses no-directory-listing format and is encrypted; it requires:
```
scs_extractor.exe -271309 <archivePath> <outputDir>
```
**Fix:** Prepend `-271309` to the argument list in `ProcessScsArchiveExtractor.ExtractAsync`.

The previous failed extraction recorded `archive_id = "locale-scs-extraction-failed"` in `bronze_reference_archives`. The re-attempt guard (`CountReferenceUnitsAsync`) checks unit count for the *current archive hash* — which is 0 — so re-extraction will be attempted automatically on next ingestion without any cleanup needed.

### SCS-2 Parse local.sii for cargo names

`IngestReferenceDataAsync` currently only enumerates `driver_names.sii`. Extend it to also enumerate `local.sii` from the same `locale/en_us/` directory. Both files use the same `localization_db` / `key[]` / `val[]` structure and are stored in `bronze_reference_sii_units`.

### SCS-3 Fix ApplyReferenceDriverNamesAsync

Current query filters `unit_type IN ('driver_name', 'driver')` — wrong. Both locale files use unit type `localization_db`. The arrays contain parallel `key[]` and `val[]` lists; the key is the driver ID (e.g. `"driver.208"`), the value is the full name (e.g. `"Jeff Coleman"`).

**Fix:** Build the locale lookup in C# (safer than SQL `json_each` parallel unnesting across two arrays). After deserializing `bronze_reference_sii_units` rows for `driver_names.sii`, zip `key[]` and `val[]` arrays into a `Dictionary<string, string>`. Then run a single parameterized UPDATE per matched driver. Replace the existing method entirely.

```
LoadLocaleDb(rows filtered to driver_names.sii) → Dictionary<driverId, fullName>
foreach match: UPDATE silver_drivers SET display_name = $name WHERE driver_id = $id AND company_id = $company
```

### SCS-4 Add ApplyReferenceCargoNamesAsync

Cargo IDs in save files (e.g. `yard_truck`) map to locale keys with `cn_` prefix (e.g. `cn_yard_truck`). Same pattern as SCS-3 but using the `local.sii` localization_db rows.

Build `Dictionary<string, string>` from local.sii (keys include the `cn_` prefix). For each `silver_jobs` row, look up `"cn_" + cargo` in the dictionary and UPDATE if found. Same for `silver_driver_recent_jobs.cargo`.

This is application-layer lookup, not SQL — avoids fragile parallel `json_each` positional joins.

---

## Data Model Additions

### StatisticsModels.cs

```csharp
// Add to TrailerStatistic
bool IsArticulated
string? BodyType

// Add to GarageStatistic  (no change — sparklines come from ProfitTrends, mapped per entity)
```

### StatisticsDtos.cs

```csharp
// GarageDto — add
SparklineDto? Trend

// DriverDto — add
SparklineDto? Trend

// TruckDto — add
long ProfitPerDay
SparklineDto? Trend

// TrailerDto — add
bool IsArticulated
string? BodyType
long ProfitPerDay
SparklineDto? Trend

// CompanyDto — add
string? OwnerName   // populated by splitting DisplayName on '|'; DisplayName keeps the part before '|'
```

**Company name split:** In `GetCompanyDisplayName`, if the raw name contains `|`, the part before `|` is the trucking company name and the part after is the owner/profile name. `CompanyDto.DisplayName` = trucking company name; `CompanyDto.OwnerName` = owner name.

### StatisticsDashboardMapper.cs additions

- `ToCompanyDto` sets `Profit = filteredMissions.Sum(m => m.Profit)` (RC-4)
- `ToGarageDto` adds `Trend = ToSparkline(company.ProfitTrends, "garage", garage.Id, fromDay, toDay)`
- `ToDriverDto` adds `Trend = ToSparkline(company.ProfitTrends, "driver", driver.Id, fromDay, toDay)`
- `ToTruckDto` adds `ProfitPerDay = MoneyPerDay(truckProfit.GetValueOrDefault(truck.Id), rangeDays)` and `Trend = ToSparkline(..., "truck", truck.Id, ...)`
- `ToTrailerDto` adds `ProfitPerDay`, `Trend`, `IsArticulated`, `BodyType` from the domain model

### DashboardViewModel.cs additions

```csharp
public static string GetDriverDisplayName(CompanyDto company, string? driverId)
// mirrors GetTruckDisplayName: returns driver.DisplayName or driverId or "-"
```

---

## UI Changes (CompanyDetail.razor)

### Routing

Add a second `@page` directive:
```razor
@page "/companies/{CompanyId}"
@page "/companies/{CompanyId}/{Tab}"
```

Add `[Parameter] public string? Tab { get; set; }` and initialize `_activeTab = Tab ?? "details"` in `OnParametersSet`. Tab NavLinks update the URL (use `<NavLink href="@($"/companies/{...}/garages")" ...>`) — this pushes history so the browser back button works.

### Header / topbar

- Display: `@SelectedCompany.DisplayName` followed by `@(SelectedCompany.OwnerName is not null ? $" · {SelectedCompany.OwnerName}" : "")` as a subtitle or sub-heading
- Profit stat shows `filteredMissions.Sum` (non-zero after RC-4 fix)

### Details tab — Profit Trend table

- Add `title="Number of save snapshots contributing to this day's data"` to the `Samples` `<th>`

### Garages tab

| Before | After |
|---|---|
| `$/Day` header | `Avg $/day` |
| Garage column shows raw ID | City name ("Las Vegas") after RC-2 |
| Profit = 0 | Non-zero after RC-1 |
| No sparkline | Inline SVG sparkline column |

Row NavLink href stays `/companies/{companyId}/garages/{garageId}`.

### Drivers tab

| Before | After |
|---|---|
| Driver column shows `driver.208` | Locale name after SCS-3 |
| Profit = 0 | Non-zero after RC-1 |
| Jobs = 0 | Non-zero after RC-1 |
| `$/Day` | `Avg $/day` |
| No sparkline | Inline SVG sparkline column |

### Trucks tab

| Before | After |
|---|---|
| Garage shows `garage.las_vegas` | "Las Vegas" via `GetGarageDisplayName` |
| Driver shows `driver.208` | Locale name via `GetDriverDisplayName` |
| Profit = 0 | Non-zero after RC-1 |
| No Avg $/day | Added `truck.ProfitPerDay` column |
| No sparkline | Inline SVG sparkline column |

### Trailers tab

| Before | After |
|---|---|
| Trailer column shows `_nameless.xxx` | BodyType formatted: "Lowboy", "Dry Van", etc. |
| No articulated indicator | "· Double" badge when `IsArticulated` |
| Type column shows nameless pointer | BodyType (same as above; consolidate into Trailer column, rename Type → Body) |
| Profit = 0 | Non-zero after RC-1 + RC-5 |
| Jobs = 0 | Non-zero after RC-5 |
| No Avg $/day | Added `trailer.ProfitPerDay` |
| No sparkline | Inline SVG sparkline column |

### Jobs tab

| Before | After |
|---|---|
| Cargo shows `yard_truck` | "Yard Truck" after SCS-4 |
| Driver is blank | Driver name after RC-1 + SCS-3 |
| Truck often blank or identifier | `GetTruckDisplayName`; wrapped in `<NavLink href=".../trucks/{truckId}">` when non-null |
| Trailer shows "unknown" | Player-owned trailer: `<NavLink href=".../trailers/{trailerId}">` with body type label; job-provided (TrailerId null): blank |

### Cities tab

| Before | After |
|---|---|
| Garage always blank | "Owned" / "-" after RC-2 |
| Eligible always blank | "Yes" / "No" after RC-2 |
| No Total column | Added: `OutboundProfit + InboundProfit` |
| No sorting | Sorted descending by OutboundProfit + InboundProfit |
| No column tooltips | `title` on Visits, Expansion, Inbound, Outbound, Total headers |

Tooltip text:
- **Visits** — "Total trips originating from or arriving at this city"
- **Expansion** — "Buy-garage priority score (0 = already owned; higher = stronger candidate)"
- **Inbound** — "Total profit from jobs delivered TO this city"
- **Outbound** — "Total profit from jobs originating FROM this city"
- **Total** — "Combined inbound + outbound profit"

---

## Sparkline rendering

Add a `SparklineSvg` helper on `DashboardBase` (or a static helper) that renders an inline SVG polyline from `SparklineDto.Points`. Dimensions: 80×24px. Points are normalized to the min/max value in the series. Returns `MarkupString.Empty` when `Trend` is null or has fewer than 2 points.

```
width=80, height=24, viewBox="0 0 80 24"
<polyline points="..." fill="none" stroke="currentColor" stroke-width="1.5"/>
```

X positions are evenly spaced across the window; Y positions are `(1 - normalizedValue) * 22 + 1` to keep 1px padding.

---

## Files changed

| File | Change |
|---|---|
| `ScsReferenceData.cs` | Add `-271309` flag to extractor args |
| `SqliteMedallionSaveSnapshotSource.cs` | Parse `local.sii`; fix failure record cleanup; fix `ApplyReferenceDriverNamesAsync`; add `ApplyReferenceCargoNamesAsync`; add `trailer_id` to gold schema; fix `ReadMissionsAsync` |
| `StatisticsProjection.cs` | RC-1 reverse lookup; RC-2 garage city extraction; RC-3 trailer def resolution |
| `StatisticsModels.cs` | `TrailerStatistic`: add `IsArticulated`, `BodyType` |
| `StatisticsDtos.cs` | Add `Trend` to Garage/Driver/Truck/Trailer DTOs; `ProfitPerDay` to Truck/Trailer; `IsArticulated`/`BodyType` to Trailer; `OwnerName` to Company |
| `StatisticsDashboardMapper.cs` | RC-4 company profit; populate new DTO fields; company name split |
| `DashboardViewModel.cs` | Add `GetDriverDisplayName` |
| `DashboardBase.cs` | Add `SparklineSvg` helper |
| `CompanyDetail.razor` | All UI changes above |
| `StatisticsApiTests.cs` | Update snapshots/assertions for new DTO shape |
| `StatisticsDashboardMapperTests.cs` | Add tests for company profit fix, sparkline population |
