# Save Data Silver Enrichment Design

## Goal

Profile ATS save data more deeply and enrich the medallion silver layer so trucks, drivers, jobs, and recent driver history are represented with validated values and useful display names in the UI.

## Evidence From Current Warehouse

The existing warehouse at `%LOCALAPPDATA%\AtsEmployeeStats\ats-employee-stats.db` contains bronze units for 27 parsed saves. Profiling showed:

- `driver_ai` units contain skills, current city, `driver_job`, `assigned_truck`, and `profit_log`, but not direct human-readable names.
- `profit_log.stats_data[]` points to `profit_log_entry` rows. These rows include revenue, wage, maintenance, fuel, distance, cargo, source/destination city/company, and `timestamp_day`; this is the source for the in-game recent job history.
- Literal SII pseudo-null values such as `null` and `nil` are currently leaking into silver fields.
- `vehicle` units include license plates and accessory IDs. Accessory rows expose `/def/vehicle/truck/<model>/data.sii`, which can be converted into a useful truck model label even without fully localized reference data.
- Reference extraction currently records `locale-scs-extraction-failed`, so driver-name localization is not available from the installed reference archive yet.

## Recommended Approach

Keep bronze as a faithful source-of-record and enrich silver from parsed save units. The statistics projection should carry recent job entries and richer truck metadata forward into silver, gold, DTOs, and the Blazor UI.

Silver should normalize pseudo-null values at the projection boundary. Derived presentation fields should prefer validated values and fall back to stable IDs only when no better data exists.

## Data Model Changes

Add domain records:

- `DriverRecentJobStatistic`: recent job entry for a driver, sourced from `profit_log_entry`.

Extend existing records:

- `CompanyStatistics` gains `RecentDriverJobs`.
- `TruckStatistic` gains `LicensePlate`, `ModelName`, and `DefinitionPath`.
- `MissionStatistic` gains optional `TimestampDay` when it comes from a profit log or delivery log entry.

Extend contracts:

- `CompanyDto` gains `RecentDriverJobs`.
- `TruckDto` gains `LicensePlate`, `ModelName`, and `DefinitionPath`.
- `MissionDto` gains `TimestampDay`.
- New `DriverRecentJobDto` carries driver id, truck id, cargo, source/destination, revenue, expenses, profit, distance, timestamp day.

## Silver And Gold Tables

Add or migrate:

- `silver_trucks.license_plate`
- `silver_trucks.model_name`
- `silver_trucks.definition_path`
- `silver_driver_recent_jobs`
- `gold_driver_recent_jobs`

Because existing code already rebuilds silver/gold from bronze each load, schema changes can be handled by `alter table add column` for new columns and `create table if not exists` for new tables.

## Display Rules

Driver display:

- Use reference/in-game name when available.
- Otherwise use driver id.
- Never display literal `null`.

Truck display:

- Use `Model Name - Plate` when either is available.
- Use cleaned license plate only if no model is found.
- Fall back to truck id.

Driver detail UI:

- Show a compact `Recent Jobs` table using the latest four recent-job entries for that driver, ordered by `TimestampDay` descending.
- Keep the existing all-jobs table below as historical detail.

## Validation And Profiling

Add unit tests for:

- SII pseudo-null normalization.
- recent jobs extracted from driver `profit_log.stats_data`.
- truck model extraction from vehicle accessory `data_path`.
- license plate markup cleanup.
- DTO and UI helper behavior for latest four driver jobs.

Add a small profiling query/script or test helper later if deeper diagnostics need to be surfaced in the app. For this pass, tests plus real warehouse spot checks are enough to prevent known invalid values from leaking.
