# SQLite Medallion Warehouse Goal Prompt

Implement a SQLite-backed medallion data warehouse for the ATS Employee Stats app so startup does not repeatedly decode and parse every save file.

## Intent

Replace the current direct save-file scan pipeline with a cached, layered SQLite pipeline:

1. Bronze: cache discovered and parsed save-file contents.
2. Silver: normalize raw SII units into canonical ATS business entities.
3. Gold: build dashboard/use-case-specific query models for Terminal.Gui drilldown workflows.

The app should still support live updates, newest-to-oldest save handling, last-N-days filtering, and startup progress feedback.

## Key Requirements

- Store the SQLite database in `%LOCALAPPDATA%\AtsEmployeeStats\ats-employee-stats.db` by default.
- Add a `--db-path <path>` CLI option for debugging and tests.
- On startup, discover save files, prefer autosave slots, and avoid unnecessarily reprocessing unchanged files.
- Prefer `save\autosave*` and `save\autosave_job*` for current gameplay history.
- Exclude `.bak` profile folders and `multiplayer_backup*` save slots by default unless an explicit option is later added.
- Keep the practical last-5-days default history window.
- Keep save files ordered newest-to-oldest.
- Keep the two startup progress bars:
  - file progress: files processed out of total candidate files
  - current save progress: parsed units out of total units in the current save

## Bronze Layer

Create tables for raw ingestion and replay:

- `bronze_save_files`
  - stable save id
  - full path
  - profile id/path segment
  - save slot name
  - last write time UTC
  - file size
  - content hash
  - ingested time UTC
  - decode/parse status
  - error message if parse failed

- `bronze_sii_units`
  - save id
  - unit ordinal
  - unit type
  - unit id
  - raw scalar values as JSON
  - raw array values as JSON

The bronze layer should be append/update safe: changed files are re-ingested; unchanged files are reused.

## Silver Layer

Normalize bronze SII units into canonical ATS entities and relationships:

- companies/player characters/trucking companies
- garages
- drivers
- trucks
- jobs/completed deliveries
- trailer types
- driver-garage relationships
- truck-garage relationships
- driver-truck relationships

Silver is where the app should resolve:

- player character/company partitioning
- driver display names instead of raw ids
- truck assignment so driver truck is not always null
- route/cargo/trailer/job normalization
- garage ownership and driver placement

## Gold Layer

Build query models/views for the dashboard workflow:

- company summary
- profitable garage ranking
- selected garage -> drivers in that garage
- selected driver -> job types
- selected job type -> specific jobs
- job detail with origin city, destination city, cargo, trailer type, truck, profit
- driver deadhead summary

The Terminal.Gui dashboard should read gold models rather than scanning files directly.

## Deadhead Definition

A deadhead is a non-paying empty return after a paid delivery when the driver cannot pick up a paying job on the way back toward their garage.

First implementation can infer this approximately from route history:

- identify a driver's paid delivery ending away from the driver's home garage area
- check whether the next known paid job starts from that destination/route area
- if not, and the driver later appears back at or working from the garage area, count that gap as a likely deadhead return

Keep this metric labeled as inferred unless explicit ATS save records for empty returns are found.

## Implementation Strategy

Use a staged migration:

1. Add SQLite infrastructure and bronze caching while preserving the existing visible dashboard behavior.
2. Build silver canonical tables from bronze and move existing projection logic to silver-backed reads.
3. Build gold models for the drilldown UI.
4. Replace the flat console dashboard with a Terminal.Gui workflow using menus/tables/lists.

Use TDD for each step. Preserve existing tests and add focused tests for:

- unchanged save files are reused from bronze
- changed save files are re-ingested
- autosave filtering/default selection
- bronze unit persistence
- silver driver names and truck assignment
- gold drilldown models
- inferred deadhead counts

