# Cities And Jobs Analytics Architecture

## Goal

Evolve ATS Employee Stats into a drill-down analytics platform that treats trucking companies, drivers, garages, trucks, trailers, jobs, routes, and cities as first-class analytical surfaces. The app remains a local SQLite-backed desktop/self-hosted experience, but the data model should support later anonymized community aggregation without replacing the core architecture.

## Acceptance Criteria

This architecture is complete when it provides:

- a coherent list -> detail -> child-tab navigation model for companies, drivers, garages, trucks, trailers, jobs, routes, and cities
- bronze, silver, and gold layer responsibilities with clear lineage from source save/reference evidence to UI-ready aggregates
- canonical entity and relationship recommendations for historical assignments, time-series analytics, and city/route modeling
- gold aggregate definitions that can power inline sparklines, detail charts, route heatmaps, expansion recommendations, and future community benchmarks
- DTO/read-model and API route recommendations that keep screen payloads focused
- an incremental implementation path that can be executed without redesigning the current SQLite/local app architecture

This design extends the current direction in:

- `2026-05-26-sqlite-medallion-storage.md`
- `2026-05-27-route-backed-navigation.md`
- `2026-05-27-save-data-silver-enrichment.md`

## Information Architecture

The primary navigation pattern stays list view -> detail view -> child tabs -> nested list/detail navigation. Top-level navigation starts with trucking companies because the save profile/player company is the natural boundary for all child data.

```text
Companies
+-- Company Detail
    +-- Details
    +-- Drivers
    |   +-- Driver Detail
    |       +-- Details
    |       +-- Trucks
    |       +-- Garages
    |       +-- Jobs
    |       +-- Skill Progression
    +-- Garages
    |   +-- Garage Detail
    |       +-- Details
    |       +-- Drivers
    |       +-- Trucks
    |       +-- Trailers
    |       +-- Jobs
    |       +-- Routes
    +-- Trucks
    |   +-- Truck Detail
    |       +-- Details
    |       +-- Drivers
    |       +-- Garages
    |       +-- Jobs
    |       +-- Trailers
    +-- Trailers
    |   +-- Trailer Detail
    |       +-- Details
    |       +-- Trucks
    |       +-- Jobs
    |       +-- Route Analytics
    +-- Jobs
    |   +-- Job Detail
    |       +-- Details
    |       +-- Route
    |       +-- Truck
    |       +-- Trailer
    |       +-- Driver
    |       +-- Garage
    |       +-- Profitability
    +-- Cities
        +-- City Detail
            +-- Details
            +-- Garages
            +-- Routes
            +-- Profitability
            +-- Expansion Potential
```

Recommended route shape:

```text
/
/companies/{companyId}
/companies/{companyId}/drivers/{driverId}
/companies/{companyId}/garages/{garageId}
/companies/{companyId}/garages/{garageId}/drivers/{driverId}
/companies/{companyId}/trucks/{truckId}
/companies/{companyId}/trailers/{trailerId}
/companies/{companyId}/jobs/{jobId}
/companies/{companyId}/cities/{cityId}
/companies/{companyId}/routes/{originCityId}/{destinationCityId}
```

Scoped routes are useful when the user reaches an entity through a parent context. A driver opened from a garage should keep the garage breadcrumb, while the same driver opened from company-level drivers should use the company breadcrumb.

## Medallion Architecture

### Bronze Layer

Bronze remains the append-friendly source of record for extracted save and localization data. It should preserve enough raw evidence to replay silver and gold transformations without returning to ATS files that have already been loaded.

Required bronze tables and concepts:

| Area | Recommendation |
| --- | --- |
| Save files | Keep `bronze_save_files` with stable save id, full path, profile id, slot name, file size, last write UTC, content hash, parse status, and ingest time. |
| Raw SII units | Keep `bronze_sii_units` with unit type, unit id, ordinal, scalar JSON, and array JSON. |
| Localization/reference data | Keep `bronze_reference_archives` and `bronze_reference_sii_units`; add localization key/value tables when extracted `.sii` or locale files are available. |
| Time | Store both real-world save timestamps and parsed ATS in-game day/timestamp values when present. |
| Snapshot lineage | Every silver row derived from a save should retain `source_save_id`, `source_unit_type`, `source_unit_id`, and effective in-game day where possible. |
| Non-destructive ingestion | Unchanged files are read from SQLite cache. Changed files replace only the affected bronze rows for that save id. Historical save snapshots remain separate rows by save id/path/hash evidence. |

Bronze should capture:

- raw save-game structures
- localization mappings
- driver/player/company data with stable game ids and friendly display names when localization or save data provides them
- trucks, trailers, garages, jobs, routes, financial data, skills
- truck and garage assignment evidence
- ATS in-game timestamps/days
- real-world save timestamps

Candidate save discovery should include manual saves, `autosave`, and `autosave_job` slots. It should exclude backup folders and multiplayer backup slots from analytics, but it should not silently drop ordinary save slots. SCS reference archives should use the same metadata/hash cache rule as save files so unchanged reference data is not re-extracted.

Source inclusion rules:

| Source | Include? | Notes |
| --- | --- | --- |
| Manual save slots | Yes | Treat each distinct `game.sii` path/hash as independent snapshot evidence. |
| `autosave` | Yes | Include current autosave history when it is not a backup copy. |
| `autosave_job` | Yes | Useful for job-level chronology and route reconstruction. |
| Backup profile folders | No | Exclude `.bak`/backup folders from analytics to avoid duplicated historical evidence. |
| `multiplayer_backup*` slots | No | Exclude generated backup slots unless explicitly imported later as a separate source type. |
| SCS reference archives | Yes | Cache by path/hash and reuse unchanged localization/reference extraction. |

Bronze lineage should be queryable in both directions:

```text
bronze_save_files
  -> bronze_sii_units
  -> silver_* row with source_save_id/source_unit_type/source_unit_id
  -> gold_* aggregate with source_snapshot_count/window boundaries
  -> DTO/read model with source aggregate identity where diagnostics need it
```

This trace makes a UI value explainable without reloading raw ATS files and keeps historical snapshots non-destructive.

### Silver Layer

Silver stores canonical normalized entities and relationships. It should be stable enough for UI queries but still close to game concepts.

Core entities:

| Entity | Primary purpose |
| --- | --- |
| `silver_companies` | Player/trucking company boundary. |
| `silver_drivers` | Canonical driver id, display name, current garage/truck, active status. |
| `silver_trucks` | Truck identity, model, plate, current garage/driver, profitability fields. |
| `silver_trailers` | Owned or job-used trailers, trailer definition, body/type, current assignment if known. |
| `silver_garages` | Garage id, display/city, size, ownership status, current capacity. |
| `silver_jobs` | Completed or known jobs with cargo, origin/destination, driver, truck, trailer, profit, distance, in-game timestamp. |
| `silver_cities` | Cities encountered through jobs/reference data, normalized id/name/state/region. |
| `silver_routes` | Directed route edge from origin city to destination city. |
| `silver_skill_progression` | Driver skill snapshots by in-game day/save id. |
| `silver_profit_snapshots` | Company/garage/driver/truck/trailer profit at save snapshot time. |
| `silver_driver_truck_assignments` | Effective assignment intervals for driver/truck relationships. |
| `silver_driver_garage_assignments` | Effective assignment intervals for driver/garage relationships. |

Historical relationships should use effective ranges:

```text
entity_id
related_entity_id
effective_from_save_id
effective_from_game_day
effective_to_save_id
effective_to_game_day
observed_last_save_id
is_current
```

If exact end time is unknown, close the prior assignment when a later snapshot shows a different assignment. This supports drivers assigned to many garages, drivers assigned to many trucks, trucks associated with many garages/drivers/jobs, and jobs linked to origin city, destination city, truck, trailer, garage, and driver.

Recommended silver keys and identity rules:

| Entity | Stable key rule |
| --- | --- |
| Company | Prefer save/profile company id; keep player/profile id separate for anonymization. |
| Driver | Use game driver id; never use localized display name as identity. |
| Truck | Use save vehicle id when owned; retain model/definition id as attributes for aggregation. |
| Trailer | Use owned trailer id when present; otherwise use job trailer definition plus job id for job-scoped trailers. |
| Garage | Use game garage id/city id; attach localized city display separately. |
| Job | Use source save id plus mission/profit-log ordinal/id when no durable job id exists. |
| City | Use normalized game city id, not display name. |
| Route | Use directed `origin_city_id + destination_city_id`; unordered route-pair id is a separate aggregate key. |

Silver rows should keep friendly display labels where available, but labels are not identity. This is required for localization, cloud aggregation, and comparing equivalent cities/routes across users.

### City Modeling

Cities should be created from every job origin and destination, then enriched from garage/reference data.

City annotations:

| Field | Meaning |
| --- | --- |
| `has_owned_garage` | The company owns a garage in the city. |
| `is_garage_eligible` | Reference data or discovered garage definitions indicate the city can host a garage. |
| `visit_count` | Number of jobs that start or end in the city. |
| `outbound_profit` | Profit from jobs originating in the city. |
| `inbound_profit` | Profit from jobs ending in the city. |
| `bidirectional_profit` | Profit on routes where both directions have observed jobs. |
| `deadhead_risk_score` | Heuristic score for cities that often strand drivers away from profitable outbound work. |
| `expansion_score` | Weighted score for garage expansion recommendations. |

City ids should be normalized from game ids, not display names, so localization and cloud aggregation can compare equivalent cities reliably.

## Entity Relationship Model

```text
TruckingCompany 1 -> many Driver
TruckingCompany 1 -> many Garage
TruckingCompany 1 -> many Truck
TruckingCompany 1 -> many Trailer
TruckingCompany 1 -> many Job
TruckingCompany 1 -> many CityMetric

Garage many -> 1 City
Driver many -> many Garage through DriverGarageAssignment
Driver many -> many Truck through DriverTruckAssignment
Truck many -> many Garage through TruckGarageAssignment

Job many -> 1 Driver
Job many -> 1 Truck
Job many -> 0..1 Trailer
Job many -> 0..1 Garage
Job many -> 1 OriginCity
Job many -> 1 DestinationCity
Job many -> 1 DirectedRoute

Route 1 -> many Job
RoutePair 1 -> many Route
Driver 1 -> many SkillProgression
Company/Garage/Driver/Truck/Trailer 1 -> many ProfitSnapshot
```

Jobs should be represented twice:

- Directed one-way ATS job: `origin_city_id -> destination_city_id`.
- Logical round-trip chain: a paired or sequenced relationship where an outbound job is matched with a return job for the same driver/truck/trailer when chronology and endpoints support it.

## Gold Layer Aggregates

Gold should expose chart-ready read models rather than forcing the UI to aggregate raw silver rows.

| Aggregate | Grain | Supports |
| --- | --- | --- |
| `gold_company_profit_trend` | company, game day, time window | company line chart, benchmark comparison |
| `gold_driver_profit_trend` | driver, game day, time window | driver list sparkline, detail trend |
| `gold_garage_profit_trend` | garage, game day, time window | garage productivity trend |
| `gold_truck_profit_trend` | truck, game day, time window | truck utilization/profit trend |
| `gold_trailer_profit_trend` | trailer, game day, time window | trailer utilization/profit trend |
| `gold_skill_profit_correlation` | driver, skill, game day/window | skill progression vs profitability |
| `gold_route_profitability` | directed route, window | route tables, route heatmaps |
| `gold_route_pair_efficiency` | unordered city pair, window | bidirectional efficiency, round-trip quality |
| `gold_deadhead_frequency` | driver/truck/city/window | deadhead reduction analysis |
| `gold_trailer_utilization` | trailer/type/window | trailer utilization charts |
| `gold_truck_utilization` | truck/window | truck use, idle risk |
| `gold_garage_productivity` | garage/window | profit per driver/truck/slot |
| `gold_city_profitability` | city/window | inbound/outbound city profitability |
| `gold_expansion_opportunity` | city/company/window | garage expansion recommendations |

Time windows should support at least 7 and 14 in-game days now. Store window metadata as data, not columns, so future windows such as 30, 90, or lifetime do not require schema redesign.

Recommended common columns:

```text
company_id
entity_id
window_days
game_day_start
game_day_end
real_time_start_utc
real_time_end_utc
metric_name / typed metric columns
sample_count
source_snapshot_count
```

## Visualization Framework

List views should stay compact. They should show current value, rank/sort controls, and one lightweight inline sparkline for the selected time window.

| Screen | List visualization | Detail visualization |
| --- | --- | --- |
| Companies | profit sparkline | company profit line, garage contribution bars |
| Drivers | daily profit sparkline | profit line, job mix bars, skill/profit overlay |
| Garages | productivity sparkline | profit per driver/truck, roster trend |
| Trucks | utilization sparkline | profit line, driver assignment timeline |
| Trailers | utilization sparkline | route/cargo usage, revenue by route |
| Jobs | none or compact profit marker | job profitability breakdown |
| Cities | outbound profit sparkline | inbound/outbound bars, route heatmap, expansion score |
| Routes | route profit sparkline | directed and bidirectional profitability charts |

Detail views should allow larger charts:

- line charts for trends
- bar charts for entity comparisons
- overlays for skill progression vs profit
- heatmaps for city/route profitability
- timeline bands for driver/truck/garage assignment history
- time filtering with 7-day and 14-day controls now, extensible later

## UI Layout Recommendations

Company list:

- Columns: company, profit, garages, drivers, trucks, selected-window sparkline, view.
- Avoid nested data on the home screen.

Company detail:

- Summary strip: profit, drivers, garages, trucks, trailers, jobs, cities.
- Tabs: Details, Drivers, Garages, Trucks, Trailers, Jobs, Cities.
- Each tab lists child entities with inline sparklines and `View` navigation.

Entity detail:

- Top summary strip with key metrics and current assignment.
- Child tabs match the information architecture.
- Charts appear above detailed tables only on detail screens.
- Related entity rows always link to their own detail route.

City detail:

- Details: visit count, owned garage status, eligibility, inbound/outbound totals.
- Garages: owned/current and eligible garage data.
- Routes: top inbound/outbound routes.
- Profitability: city trend and route heatmap.
- Expansion Potential: score, reasons, recommended next actions.

## DTO And Read Model Structures

Keep DTOs separated by read use case rather than making one large dashboard object carry every future chart.

```csharp
public sealed record EntityTrendPointDto(
    int GameDay,
    DateTimeOffset? SaveTimeUtc,
    long Value,
    int SampleCount);

public sealed record SparklineDto(
    int WindowDays,
    IReadOnlyList<EntityTrendPointDto> Points);

public sealed record CompanyListItemDto(
    string Id,
    string DisplayName,
    long Profit,
    int GarageCount,
    int DriverCount,
    int TruckCount,
    SparklineDto ProfitTrend);

public sealed record CityListItemDto(
    string Id,
    string DisplayName,
    bool HasOwnedGarage,
    bool IsGarageEligible,
    int VisitCount,
    long OutboundProfit,
    long BidirectionalProfit,
    decimal ExpansionScore,
    SparklineDto ProfitTrend);

public sealed record RouteProfitabilityDto(
    string OriginCityId,
    string DestinationCityId,
    long Profit,
    int JobCount,
    decimal ProfitPerMile,
    decimal ReturnCoverageRatio,
    SparklineDto ProfitTrend);
```

Detail read models should include related rows and chart models:

```csharp
public sealed record DriverDetailDto(
    DriverDto Driver,
    IReadOnlyList<DriverTruckAssignmentDto> TruckAssignments,
    IReadOnlyList<DriverGarageAssignmentDto> GarageAssignments,
    IReadOnlyList<MissionDto> Jobs,
    IReadOnlyList<SkillProgressionDto> SkillProgression,
    IReadOnlyList<EntityTrendPointDto> ProfitTrend);
```

## API And Query Patterns

Use route-backed API endpoints that mirror the UI:

```text
GET /api/statistics?rangeDays=14
GET /api/companies?rangeDays=14
GET /api/companies/{companyId}?rangeDays=14
GET /api/companies/{companyId}/drivers?rangeDays=14
GET /api/companies/{companyId}/drivers/{driverId}?rangeDays=14
GET /api/companies/{companyId}/garages/{garageId}?rangeDays=14
GET /api/companies/{companyId}/trucks/{truckId}?rangeDays=14
GET /api/companies/{companyId}/trailers/{trailerId}?rangeDays=14
GET /api/companies/{companyId}/jobs/{jobId}?rangeDays=14
GET /api/companies/{companyId}/cities/{cityId}?rangeDays=14
GET /api/companies/{companyId}/routes/{originCityId}/{destinationCityId}?rangeDays=14
POST /api/statistics/reload?rangeDays=14
```

Query rules:

- `rangeDays` uses ATS in-game day windows.
- API defaults to 14 days and supports 7 days.
- Long-lived extraction state stays server-side in SQLite.
- UI list endpoints return list-ready rows plus compact sparkline data.
- Detail endpoints return charts and child lists for that screen only.
- Reload should discover and ingest only changed or new save/reference files.

## Cloud Aggregation Preparation

The local extractor should be able to become:

1. standalone executable
2. local SQLite analytics generator
3. optional anonymized upload client

Future cloud workflow:

1. Local extraction.
2. Local SQLite generation.
3. Local analytics and visualizations.
4. Optional anonymized upload.
5. Cloud aggregation and benchmarking.

Cloud-ready rules:

- Keep stable local ids separate from upload ids.
- Hash or bucket company/profile/user identifiers before upload.
- Upload gold aggregates by normalized city, route, truck model, trailer type, and window, not raw save units.
- Do not upload raw SII rows by default.
- Preserve `source_snapshot_count`, window boundaries, and sample counts so cloud benchmarks can reject weak samples.
- Version every upload payload with schema version, game version when known, app version, and metric definition version.

Community benchmarks can then compare:

- driver profitability
- garage profitability
- route efficiency
- trailer utilization
- expansion strategies

## Incremental Roadmap

Detailed execution steps live in `docs/plans/2026-05-27-cities-and-jobs-implementation.md`. The roadmap below is the architectural sequence that implementation plan should continue to follow.

1. Formalize silver schema gaps: add `silver_cities`, `silver_routes`, `silver_trailers`, skill progression, profit snapshots, and historical assignment tables.
2. Persist city and route facts from job data: every job origin/destination creates or updates cities and directed routes.
3. Add route-backed UI surfaces for jobs and cities: company tabs for Jobs and Cities, then job/city detail routes.
4. Add gold trend tables: company, driver, garage, truck, trailer, and city profit trends for 7/14 day windows.
5. Add inline sparklines to list rows using compact `SparklineDto` data.
6. Add detail charts for drivers, garages, routes, and cities.
7. Add bidirectional route and deadhead heuristics: route pair efficiency, return coverage, and inferred deadhead counts.
8. Add expansion opportunity scoring: combine eligibility, no owned garage, visits, outbound profit, bidirectional strength, and deadhead reduction potential.
9. Split large dashboard payloads into screen-specific read endpoints after UI surfaces need more chart data.
10. Add anonymized aggregate export: versioned gold-layer upload payloads with no raw save units.

## Verification Matrix

| Requirement | Evidence this design provides |
| --- | --- |
| Navigation hierarchy is internally consistent | The route map and hierarchy give every top-level tab a detail route and child tabs. |
| All entities can drill into related entities | The relationship model and UI rules link jobs, cities, routes, drivers, garages, trucks, and trailers bidirectionally through detail routes. |
| Historical assignment tracking works | Silver assignment tables use effective ranges and save/game-day lineage. |
| Gold aggregates support intended charts | Each visualization maps to a named gold aggregate with entity/window grain. |
| Visualization strategy fits IA | List views use sparklines; detail views own larger charts and reports. |
| Future cloud aggregation remains possible | Upload rules use anonymized gold aggregates with schema/metric versioning and no raw SII data. |
| Existing no-reload constraint is preserved | Bronze ingestion keeps save metadata/hash caching and reloads only new or changed files. |
| All required save sources are included | Source inclusion rules explicitly include manual saves, `autosave`, and `autosave_job` while excluding duplicate backup sources. |
| DTO/API design avoids monolithic payload growth | Read-model guidance and route-specific endpoints keep chart data scoped to the current screen. |
