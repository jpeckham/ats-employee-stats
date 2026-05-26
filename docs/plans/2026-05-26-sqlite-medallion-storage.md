# SQLite Medallion Storage Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add SQLite-backed storage for ATS analytic save data so unchanged saves are cached instead of decoded and parsed on every startup.

**Architecture:** Introduce an infrastructure snapshot source that discovers candidate `game.sii` files, records bronze file metadata and raw units in SQLite, and returns the same `SaveSnapshot` data used by the existing statistics projection. Keep the visible dashboard behavior stable while later slices move silver and gold reads behind the service.

**Tech Stack:** .NET 10, xUnit, `Microsoft.Data.Sqlite`, existing SII decoder/parser, existing `ISaveSnapshotSource`.

---

### Task 1: Bronze SQLite Source

**Files:**
- Create: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`
- Create: `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs`
- Modify: `src/AtsEmployeeStats.Infrastructure/AtsEmployeeStats.Infrastructure.csproj`

**Step 1: Write failing tests**

Cover:
- unchanged `game.sii` files are reused from bronze cache when metadata/hash still match
- changed `game.sii` files are re-ingested and replace old bronze units
- default discovery includes autosave/autosave_job current-history saves first and excludes `.bak` profile folders and `multiplayer_backup*` slots
- persisted bronze units store scalar and array values as JSON and can replay snapshots

**Step 2: Run test to verify failure**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter SqliteMedallionSaveSnapshotSourceTests`

Expected: FAIL because the SQLite source does not exist yet.

**Step 3: Implement minimal source**

Add `Microsoft.Data.Sqlite`, create bronze tables, compute stable save id from normalized full path, compute content hash, upsert metadata, replace units for changed files, and rebuild `SaveSnapshot` from SQLite rows for unchanged files.

**Step 4: Run targeted tests**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter SqliteMedallionSaveSnapshotSourceTests`

Expected: PASS.

### Task 2: CLI Wiring

**Files:**
- Modify: `src/AtsEmployeeStats.Console/Program.cs`
- Modify: `tests/AtsEmployeeStats.Tests/CommandLineOptionsTests.cs`

**Step 1: Write failing tests**

Cover:
- `--db-path <path>` is parsed
- default database path is `%LOCALAPPDATA%\AtsEmployeeStats\ats-employee-stats.db`

**Step 2: Run test to verify failure**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter CommandLineOptionsTests`

Expected: FAIL because `DbPath` is not yet parsed.

**Step 3: Wire source**

Use `SqliteMedallionSaveSnapshotSource` in `Program.cs` with default history days and parsed database path.

**Step 4: Run targeted tests**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter CommandLineOptionsTests`

Expected: PASS.

### Task 3: Full Verification and Gap Audit

Run: `dotnet test AtsEmployeeStats.sln`

Then re-read `docs/prompts/sqlite-medallion-goal.md` and record remaining gaps for silver canonical tables, gold query models, deadhead inference, and Terminal.Gui drilldown if they are not implemented in this slice.
