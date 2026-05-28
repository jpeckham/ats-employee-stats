# Startup Ingestion & SQLite-First Serving

**Date:** 2026-05-28
**Status:** Approved

## Problem

Every page navigation triggers a full `ReadStatisticsAsync` call which:
1. Scans the filesystem for save files
2. Ingests any not yet in bronze
3. **Always** wipes and rebuilds all silver/gold tables regardless of whether anything changed

This causes long load times on every page, even when no saves have changed.

## Requirements

- Load all available save files once and only once
- Detect save files that have not yet been loaded and load them
- Otherwise serve data directly from SQLite (no disk I/O per API request)
- On first run, scan all save files with no date cutoff (preserve full history)
- On subsequent startups, only scan files newer than the last ingested save date
- `historyDays` config continues to drive the dashboard display range — it no longer limits ingestion

## Solution: Startup Ingestion + High-Water Mark

### High-Water Mark

A new `app_metadata` table stores `last_loaded_save_utc`: the max `last_write_time_utc` across all successfully parsed saves in `bronze_save_files`. It is written after every successful ingestion cycle.

**First run** (no `last_loaded_save_utc` in DB): discover all `game.sii` files — no date cutoff.
**Subsequent runs**: discover only files where `mtime >= last_loaded_save_utc`.

This prevents re-scanning the full save directory on every startup while ensuring no new saves are missed.

### Ingestion Cycle

`IngestAsync` on `SqliteMedallionSaveSnapshotSource`:

1. Read `last_loaded_save_utc` from `app_metadata` (null on first run)
2. Discover candidate paths using the high-water mark (or no cutoff if first run)
3. For each path: check bronze cache (save_id + path + mtime + size + parse_status = 'parsed')
   - Cached: skip disk read
   - Not cached: decode, parse, write to `bronze_save_files` + `bronze_sii_units`
4. If any new files were ingested OR gold tables are empty:
   - Read all snapshots from bronze
   - Build statistics via `StatisticsProjection.Build`
   - Clear and repopulate silver/gold tables
   - Update `last_loaded_save_utc` = `MAX(last_write_time_utc)` from `bronze_save_files WHERE parse_status = 'parsed'`
5. If nothing new and gold has data: exit without touching silver/gold

### API Read Path

`ReadStatisticsAsync` (implementing `IStatisticsQuerySource`) becomes a pure gold read — no disk access, no bronze scan, no silver/gold rebuild. All API endpoints that call `StatisticsService.LoadAsync` now get an instant SQLite read.

### Startup Service

A new `SaveIngestionService : IHostedService` runs `StatisticsService.IngestAsync` once at app startup, passing SignalR progress so the UI receives loading updates. The API begins serving immediately (returning empty/stale gold) while ingestion completes in the background.

The existing `/api/statistics/reload` endpoint calls `IngestAsync` instead of `LoadAsync` to manually trigger a new ingestion cycle.

## Component Changes

| Component | Change |
|---|---|
| `app_metadata` SQLite table | New — stores `last_loaded_save_utc` |
| `IStatisticsIngestor` | New interface: `IngestAsync(CancellationToken, IProgress<SaveLoadProgress>?)` |
| `SqliteMedallionSaveSnapshotSource` | Implements `IStatisticsIngestor`. `IngestAsync` implements the ingestion cycle above. `ReadStatisticsAsync` becomes a pure gold read. `DiscoverCandidatePaths` drops `historyWindow` as an ingestion filter; uses high-water mark instead |
| `StatisticsService` | Gains `IngestAsync` — delegates to `IStatisticsIngestor` if source implements it, otherwise falls back to full scan for non-SQLite sources |
| `SaveIngestionService` | New `IHostedService` in `AtsEmployeeStats.Api` — calls `StatisticsService.IngestAsync` at startup |
| `Program.cs` | Registers `SqliteMedallionSaveSnapshotSource` as singleton for `ISaveSnapshotSource`, `IStatisticsQuerySource`, and `IStatisticsIngestor`. Registers `SaveIngestionService`. Reload endpoint calls `IngestAsync` |

## What Does Not Change

- Bronze cache logic (save_id + path + mtime + size) — unchanged
- Silver/gold rebuild logic — unchanged, just no longer runs on every request
- `historyDays` as a dashboard display range default — unchanged
- SignalR progress reporting — unchanged, works for both startup and reload
- `FileSaveSnapshotSource` — unaffected (no SQLite, fallback path in `StatisticsService`)
