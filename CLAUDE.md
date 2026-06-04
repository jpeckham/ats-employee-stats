# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```powershell
# Run the app
dotnet run --project src/AtsEmployeeStats.Wpf

# Build the solution
dotnet build

# Build release
dotnet build -c Release
```

There are no automated tests in this repository.

## Architecture

This is a WPF companion app for American Truck Simulator that reads historical `game.sii` save files and produces profit statistics per company, garage, driver, truck, trailer, city, and route.

### Layer dependencies

```
Wpf → Application + Contracts + Infrastructure
Infrastructure → Application + Domain
Application → Domain
Contracts (standalone DTOs)
```

### Data pipeline: Bronze → Silver → Gold

`SqliteMedallionSaveSnapshotSource` in `Infrastructure` is the main implementation. It implements three interfaces simultaneously:
- `ISaveSnapshotSource` — reads parsed save snapshots
- `IStatisticsIngestor` — runs the full ingestion pipeline
- `IStatisticsQuerySource` — reads pre-computed statistics from gold tables

The pipeline stages:
1. **Bronze** — raw parsed SII units stored verbatim in SQLite (`bronze_save_files`, `bronze_sii_units`). Save files are cached by path + size + mtime; content is SHA256-hashed and re-parsed only on change.
2. **Silver** — denormalized domain entities written by `PersistSilverAndGoldAsync` from `AtsStatistics` (garages, drivers, trucks, jobs, trailers, cities, routes, assignments).
3. **Gold** — further aggregated views for the UI (rankings, driver job types, city profitability, profit trends, recent jobs).

After silver tables are written, reference data patches are applied in-place: `ApplyReferenceDriverNamesAsync`, `ApplyReferenceCargoNamesAsync`, `ApplyReferenceCityNamesAsync`.

`StatisticsService` in `Application` wraps the source: it calls `IStatisticsIngestor.IngestAsync` when available, then reads from `IStatisticsQuerySource` if available, otherwise falls back to `StatisticsProjection.Build(snapshots)` in memory.

### Schema evolution

New columns are added via `EnsureColumnAsync` calls at the bottom of `EnsureSchemaAsync`. Structural changes (e.g., adding a surrogate PK) require a `Migrate*Async` helper that checks for the column before dropping and recreating the table.

**When adding a new field to a silver table**: you must wire it through in four places inside `SqliteMedallionSaveSnapshotSource`:
1. `EnsureSchemaAsync` — column in CREATE TABLE + `EnsureColumnAsync` call for existing DBs
2. `PersistSilverAndGoldAsync` — INSERT with the new parameter
3. The matching SELECT in `ReadGoldStatisticsAsync` / the gold query methods

See `memory/feedback-silver-layer-checklist.md` for the full checklist.

### SII parsing

`SiiSaveParser` in `Infrastructure` parses plain-text SII files into `SiiDocument` (list of `SiiUnit`s). Each unit has a type, an ID, a `Values` dictionary (scalar fields), and an `Arrays` dictionary (indexed arrays like `employees[0]`).

`SiiSaveTextDecoder` handles decryption/decompression of binary ATS save files before parsing.

`ScsReferenceData` / `ScsReferenceDataIngestor` extract locale data from the game's `.scs` archives (driver names, cargo names, city names) for display name enrichment.

### StatisticsProjection

`StatisticsProjection.Build` in `Application` converts a flat list of `SaveSnapshot`s into `AtsStatistics`. Key design points:
- Snapshots are grouped by company key (profile name + source key).
- The **latest** snapshot in each group drives current entity lookups (garages, drivers, trucks). All snapshots are merged for mission history.
- Mission deduplication: `delivery_log_entry` / `profit_log_entry` entries deduplicate on a composite key (type|source|dest|cargo|truck|trailerType|profit); `job` units deduplicate on unit ID.
- Player driver: the `player` unit is synthesized into a virtual `driver` with ID `"player"`. The `driver_player` unit bridges the player's profit log.
- Profit log rollover: when `profit_log` entries roll over in newer saves, older snapshots may carry attribution the newest lost — the merger picks the best available driver/truck/trailer/garage across snapshots.

### WPF UI

`MainWindowViewModel` drives the shell. Navigation is a tree (left pane, `ExplorerNodeViewModel`) with a detail panel on the right. Selecting a node sets the detail view to an `EntityDetailViewModel` subclass (Company, Garage, Driver, Truck, Trailer, City).

Each detail view has tabs (`DetailTabViewModel`): an Overview tab backed by `OverviewViewModel`, and data grid tabs. Columns are built programmatically in `MainWindow.xaml.cs` via `ConfigureDetailGridColumns`. Sparkline cells use `SparklineControl`.

`Rows.cs` and `DetailViewModels.cs` in `ViewModels` contain the row/column factories for each entity type. `DashboardStatisticsDtoMapper` / `StatisticsDashboardMapper` translate domain models to the `Contracts` DTOs consumed by view models.

### Contracts

`AtsEmployeeStats.Contracts` holds DTOs that cross the Application→Wpf boundary. `CloudAggregateDtos` are outbound aggregate payloads (not currently wired to any external service — placeholders for future cloud sync).
