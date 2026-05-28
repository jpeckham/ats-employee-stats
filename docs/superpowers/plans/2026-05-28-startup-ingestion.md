# Startup Ingestion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Load save files once at startup using a high-water mark, then serve all API requests directly from SQLite gold tables with no per-request disk I/O.

**Architecture:** A new `IStatisticsIngestor` interface separates "ingest new saves" from "read cached statistics." `SqliteMedallionSaveSnapshotSource` implements it by tracking a `last_loaded_save_utc` high-water mark in a new `app_metadata` SQLite table — first run scans everything, subsequent runs scan only files newer than the mark. A new `SaveIngestionService : IHostedService` runs ingestion once at app startup; all API endpoints then call `ReadStatisticsAsync`, which becomes a pure gold table read.

**Tech Stack:** C# 13 / .NET 10, Microsoft.Data.Sqlite, ASP.NET Core hosted services, SignalR

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Create | `src/AtsEmployeeStats.Application/Statistics/IStatisticsIngestor.cs` | New interface: `IngestAsync` |
| Create | `src/AtsEmployeeStats.Api/SaveIngestionService.cs` | Hosted service: runs ingestion at startup |
| Modify | `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs` | Add `app_metadata` schema, HWM helpers, `IngestAsync`, pure-gold `ReadStatisticsAsync`, remove `historyWindow` |
| Modify | `src/AtsEmployeeStats.Application/Statistics/StatisticsService.cs` | Add `IngestAsync` delegating to `IStatisticsIngestor` |
| Modify | `src/AtsEmployeeStats.Api/Program.cs` | Register `IStatisticsIngestor`, register `SaveIngestionService`, update reload endpoint |
| Modify | `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs` | Update `StatisticsService` tests to call `IngestAsync` first; add HWM tests |

---

### Task 1: Add `IStatisticsIngestor` and stubs

**Files:**
- Create: `src/AtsEmployeeStats.Application/Statistics/IStatisticsIngestor.cs`
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs` (class declaration + stub)
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsService.cs`

- [ ] **Step 1: Create `IStatisticsIngestor.cs`**

```csharp
// src/AtsEmployeeStats.Application/Statistics/IStatisticsIngestor.cs
using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Application.Statistics;

public interface IStatisticsIngestor
{
    Task IngestAsync(CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null);
}
```

- [ ] **Step 2: Add `IStatisticsIngestor` to the class declaration and add a stub**

In `SqliteMedallionSaveSnapshotSource.cs`, change the class declaration from:
```csharp
public sealed class SqliteMedallionSaveSnapshotSource(
    string rootPath,
    string databasePath,
    TimeSpan? historyWindow = null,
    AtsReferenceDataOptions? referenceDataOptions = null,
    IScsExtractorDownloader? scsExtractorDownloader = null,
    IScsArchiveExtractor? scsArchiveExtractor = null) : ISaveSnapshotSource, IStatisticsQuerySource
```
to:
```csharp
public sealed class SqliteMedallionSaveSnapshotSource(
    string rootPath,
    string databasePath,
    AtsReferenceDataOptions? referenceDataOptions = null,
    IScsExtractorDownloader? scsExtractorDownloader = null,
    IScsArchiveExtractor? scsArchiveExtractor = null) : ISaveSnapshotSource, IStatisticsQuerySource, IStatisticsIngestor
```

Add this stub method immediately after `ReadStatisticsAsync`:
```csharp
public Task IngestAsync(CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null)
    => throw new NotImplementedException();
```

- [ ] **Step 3: Add `IngestAsync` to `StatisticsService`**

Replace the entire content of `src/AtsEmployeeStats.Application/Statistics/StatisticsService.cs` with:
```csharp
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Statistics;

public sealed class StatisticsService(ISaveSnapshotSource saveSnapshotSource)
{
    public async Task IngestAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        if (saveSnapshotSource is IStatisticsIngestor ingestor)
        {
            await ingestor.IngestAsync(cancellationToken, progress);
        }
    }

    public async Task<AtsStatistics> LoadAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        if (saveSnapshotSource is IStatisticsQuerySource querySource)
        {
            return await querySource.ReadStatisticsAsync(cancellationToken, progress);
        }

        var snapshots = await saveSnapshotSource.ReadAllAsync(cancellationToken, progress);
        return StatisticsProjection.Build(snapshots);
    }
}
```

- [ ] **Step 4: Build to verify it compiles (ignore the `Program.cs` registration error for `historyWindow` — fix in Task 7)**

```
dotnet build src/AtsEmployeeStats.Infrastructure/AtsEmployeeStats.Infrastructure.csproj
dotnet build src/AtsEmployeeStats.Application/AtsEmployeeStats.Application.csproj
```

Expected: builds successfully (the `Program.cs` in Api may fail on the `historyWindow` arg — that's fine, fix in Task 7).

- [ ] **Step 5: Commit**

```
git add src/AtsEmployeeStats.Application/Statistics/IStatisticsIngestor.cs
git add src/AtsEmployeeStats.Application/Statistics/StatisticsService.cs
git add src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs
git commit -m "feat: add IStatisticsIngestor interface and stubs"
```

---

### Task 2: Write failing tests for `IngestAsync`

**Files:**
- Modify: `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs`

- [ ] **Step 1: Add three new test methods**

Add these three tests to the test class (before the `Dispose` method):

```csharp
[Fact]
public async Task IngestAsync_on_first_run_ingests_all_saves_regardless_of_age()
{
    var oldSavePath = await WriteSaveAsync("manual_save", "Old Line");
    File.SetLastWriteTimeUtc(oldSavePath, DateTime.UtcNow.AddDays(-30));
    await WriteSaveAsync("autosave", "New Line");

    var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
    var service = new StatisticsService(source);

    await service.IngestAsync(CancellationToken.None);

    using var connection = OpenTestConnection();
    await connection.OpenAsync();
    using var command = connection.CreateCommand();
    command.CommandText = "select count(*) from bronze_save_files where parse_status = 'parsed'";
    Assert.Equal(2L, (long)(await command.ExecuteScalarAsync() ?? 0L));
}

[Fact]
public async Task IngestAsync_skips_saves_older_than_high_water_mark_on_subsequent_runs()
{
    await WriteSaveAsync("autosave", "Desert Line");
    var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
    var service = new StatisticsService(source);

    await service.IngestAsync(CancellationToken.None);

    var oldSavePath = await WriteRawSaveAsync("manual_save", """
        SiiNunit
        {
        player : player {
          company_name: "Old Line"
        }
        }
        """);
    File.SetLastWriteTimeUtc(oldSavePath, DateTime.UtcNow.AddHours(-1));

    await service.IngestAsync(CancellationToken.None);

    using var connection = OpenTestConnection();
    await connection.OpenAsync();
    using var command = connection.CreateCommand();
    command.CommandText = "select count(*) from bronze_save_files";
    Assert.Equal(1L, (long)(await command.ExecuteScalarAsync() ?? 0L));
}

[Fact]
public async Task IngestAsync_reports_no_file_loading_on_second_run_when_no_new_saves()
{
    await WriteSaveAsync("autosave", "Desert Line");
    var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
    var service = new StatisticsService(source);

    await service.IngestAsync(CancellationToken.None);

    var progress = new List<SaveLoadProgress>();
    await service.IngestAsync(CancellationToken.None, new CapturingProgress(progress));

    Assert.DoesNotContain(progress, p => p.Stage == SaveLoadStage.LoadingFiles);
}
```

- [ ] **Step 2: Run the new tests to confirm they fail**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "IngestAsync" --no-build
```

Expected: all three fail with `NotImplementedException` (or compile error if not built yet — run `dotnet build` first).

---

### Task 3: Implement `app_metadata` schema + HWM helpers

**Files:**
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`

- [ ] **Step 1: Add `app_metadata` to `EnsureSchemaAsync`**

In `EnsureSchemaAsync`, append to the end of the `command.CommandText` SQL string (before the closing `"""`):

```sql

            create table if not exists app_metadata (
                key   text primary key,
                value text not null
            );
```

- [ ] **Step 2: Add `GetHighWaterMarkAsync`, `SetHighWaterMarkAsync`, `GoldHasDataAsync`, and `ReadAllBronzeSnapshotsAsync`**

Add these four private methods anywhere in the class (e.g., after `ReadBronzeDocumentAsync`):

```csharp
private static async Task<DateTime?> GetHighWaterMarkAsync(
    SqliteConnection connection,
    CancellationToken cancellationToken)
{
    await using var command = connection.CreateCommand();
    command.CommandText = "select value from app_metadata where key = 'last_loaded_save_utc'";
    var value = await command.ExecuteScalarAsync(cancellationToken) as string;
    return value is null
        ? null
        : DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
}

private static async Task SetHighWaterMarkAsync(
    SqliteConnection connection,
    CancellationToken cancellationToken)
{
    await using var command = connection.CreateCommand();
    command.CommandText = """
        insert into app_metadata (key, value)
        select 'last_loaded_save_utc', max(last_write_time_utc)
        from bronze_save_files
        where parse_status = 'parsed'
        on conflict(key) do update set value = excluded.value
        """;
    await command.ExecuteNonQueryAsync(cancellationToken);
}

private static async Task<bool> GoldHasDataAsync(
    SqliteConnection connection,
    CancellationToken cancellationToken)
{
    await using var command = connection.CreateCommand();
    command.CommandText = "select count(*) from gold_company_summary";
    return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L) > 0;
}

private static async Task<IReadOnlyList<SaveSnapshot>> ReadAllBronzeSnapshotsAsync(
    SqliteConnection connection,
    CancellationToken cancellationToken)
{
    var rows = new List<(string SaveId, string FullPath, DateTime LastWriteTimeUtc)>();
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select save_id, full_path, last_write_time_utc
            from bronze_save_files
            where parse_status = 'parsed'
            order by last_write_time_utc desc
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((
                reader.GetString(0),
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind)));
        }
    }

    var snapshots = new List<SaveSnapshot>(rows.Count);
    foreach (var (saveId, fullPath, lastWriteTimeUtc) in rows)
    {
        var document = await ReadBronzeDocumentAsync(connection, saveId, cancellationToken);
        snapshots.Add(new SaveSnapshot(fullPath, lastWriteTimeUtc, document));
    }
    return snapshots;
}
```

- [ ] **Step 3: Build to verify no compile errors**

```
dotnet build src/AtsEmployeeStats.Infrastructure/AtsEmployeeStats.Infrastructure.csproj
```

Expected: builds successfully.

- [ ] **Step 4: Commit**

```
git add src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs
git commit -m "feat: add app_metadata schema and HWM helpers"
```

---

### Task 4: Implement `IngestAsync` full logic

**Files:**
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`

- [ ] **Step 1: Update `DiscoverCandidatePaths` to accept a `sinceUtc` parameter instead of using `historyWindow`**

Replace:
```csharp
private List<string> DiscoverCandidatePaths(CancellationToken cancellationToken)
{
    var cutoffUtc = historyWindow is null ? (DateTime?)null : DateTime.UtcNow.Subtract(historyWindow.Value);
    return Directory
        .EnumerateFiles(rootPath, "game.sii", SearchOption.AllDirectories)
        .Select(path => new SavePath(path, File.GetLastWriteTimeUtc(path), GetProfileSegment(path), GetSaveSlot(path)))
        .Where(file => cutoffUtc is null || file.LastWriteTimeUtc >= cutoffUtc)
        .Where(file => !IsBackupPath(file.Path))
        .Where(file => !file.SaveSlot.StartsWith("multiplayer_backup", StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(file => file.LastWriteTimeUtc)
        .Select(file => file.Path)
        .ToList();
}
```

With:
```csharp
private List<string> DiscoverCandidatePaths(DateTime? sinceUtc, CancellationToken cancellationToken)
{
    return Directory
        .EnumerateFiles(rootPath, "game.sii", SearchOption.AllDirectories)
        .Select(path => new SavePath(path, File.GetLastWriteTimeUtc(path), GetProfileSegment(path), GetSaveSlot(path)))
        .Where(file => sinceUtc is null || file.LastWriteTimeUtc >= sinceUtc)
        .Where(file => !IsBackupPath(file.Path))
        .Where(file => !file.SaveSlot.StartsWith("multiplayer_backup", StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(file => file.LastWriteTimeUtc)
        .Select(file => file.Path)
        .ToList();
}
```

- [ ] **Step 2: Update the existing `DiscoverCandidatePaths` call inside `ReadAllAsync`**

In `ReadAllAsync`, find:
```csharp
var paths = DiscoverCandidatePaths(cancellationToken);
```
Replace with:
```csharp
var paths = DiscoverCandidatePaths(null, cancellationToken);
```

- [ ] **Step 3: Replace the `IngestAsync` stub with the full implementation**

Replace:
```csharp
public Task IngestAsync(CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null)
    => throw new NotImplementedException();
```

With:
```csharp
public async Task IngestAsync(
    CancellationToken cancellationToken,
    IProgress<SaveLoadProgress>? progress = null)
{
    await using var connection = await OpenDatabaseAsync(cancellationToken);
    await EnsureSchemaAsync(connection, cancellationToken);
    await IngestReferenceDataAsync(connection, cancellationToken);

    if (!Directory.Exists(rootPath))
    {
        progress?.Report(new SaveLoadProgress(
            SaveLoadStage.Completed,
            CompletedFiles: 0,
            TotalFiles: 0,
            CompletedUnits: 0,
            TotalUnits: 0,
            Message: "Save root was not found."));
        return;
    }

    var highWaterMark = await GetHighWaterMarkAsync(connection, cancellationToken);

    progress?.Report(new SaveLoadProgress(
        SaveLoadStage.DiscoveringFiles,
        CompletedFiles: 0,
        TotalFiles: 0,
        CompletedUnits: 0,
        TotalUnits: 0,
        Message: "Discovering game.sii files..."));

    var paths = DiscoverCandidatePaths(highWaterMark, cancellationToken);

    progress?.Report(new SaveLoadProgress(
        SaveLoadStage.FilesDiscovered,
        CompletedFiles: 0,
        TotalFiles: paths.Count,
        CompletedUnits: 0,
        TotalUnits: 0,
        Message: $"Found {paths.Count:N0} save file{(paths.Count == 1 ? string.Empty : "s")}."));

    var anyIngested = false;
    var completedFiles = 0;
    var completedUnits = 0;
    var estimatedTotalUnits = 0;

    foreach (var path in paths)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var metadata = ReadFastFileMetadata(path);
        var cached = await TryReadCachedSnapshotAsync(connection, metadata, cancellationToken);
        if (cached is not null)
        {
            completedFiles++;
            completedUnits += cached.Document.Units.Count;
            progress?.Report(new SaveLoadProgress(
                SaveLoadStage.FileLoaded,
                CompletedFiles: completedFiles,
                TotalFiles: paths.Count,
                CompletedUnits: completedUnits,
                TotalUnits: Math.Max(estimatedTotalUnits, completedUnits),
                Message: $"Loaded {completedFiles:N0} of {paths.Count:N0} save files.",
                CurrentFile: path,
                CurrentFileCompletedUnits: cached.Document.Units.Count,
                CurrentFileTotalUnits: cached.Document.Units.Count));
            continue;
        }

        try
        {
            metadata = await ReadHashedFileMetadataAsync(metadata, cancellationToken);
            var snapshot = await TryIngestSnapshotAsync(
                connection, metadata, completedFiles, paths.Count,
                completedUnits, estimatedTotalUnits, progress, cancellationToken);
            completedFiles++;
            if (snapshot is not null)
            {
                anyIngested = true;
                var unitCount = snapshot.Document.Units.Count;
                if (estimatedTotalUnits == 0 && unitCount > 0)
                    estimatedTotalUnits = unitCount * paths.Count;
                completedUnits += unitCount;
                progress?.Report(new SaveLoadProgress(
                    SaveLoadStage.FileLoaded,
                    CompletedFiles: completedFiles,
                    TotalFiles: paths.Count,
                    CompletedUnits: completedUnits,
                    TotalUnits: Math.Max(estimatedTotalUnits, completedUnits),
                    Message: $"Loaded {completedFiles:N0} of {paths.Count:N0} save files.",
                    CurrentFile: path,
                    CurrentFileCompletedUnits: unitCount,
                    CurrentFileTotalUnits: unitCount));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            completedFiles++;
        }
    }

    var goldHasData = await GoldHasDataAsync(connection, cancellationToken);
    if (anyIngested || !goldHasData)
    {
        var allSnapshots = await ReadAllBronzeSnapshotsAsync(connection, cancellationToken);
        var statistics = StatisticsProjection.Build(allSnapshots);
        await PersistSilverAndGoldAsync(connection, statistics, cancellationToken);
        await SetHighWaterMarkAsync(connection, cancellationToken);
    }

    progress?.Report(new SaveLoadProgress(
        SaveLoadStage.Completed,
        CompletedFiles: completedFiles,
        TotalFiles: paths.Count,
        CompletedUnits: completedUnits,
        TotalUnits: Math.Max(estimatedTotalUnits, completedUnits),
        Message: $"Loaded {completedFiles:N0} save file{(completedFiles == 1 ? string.Empty : "s")}."));
}
```

- [ ] **Step 4: Run the three new tests and verify they pass**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "IngestAsync"
```

Expected: all three pass.

- [ ] **Step 5: Commit**

```
git add src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs
git commit -m "feat: implement IngestAsync with high-water mark discovery"
```

---

### Task 5: Change `ReadStatisticsAsync` to a pure gold read

**Files:**
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`

- [ ] **Step 1: Replace `ReadStatisticsAsync` with a pure gold read**

Replace the entire `ReadStatisticsAsync` method:
```csharp
public async Task<AtsStatistics> ReadStatisticsAsync(
    CancellationToken cancellationToken,
    IProgress<SaveLoadProgress>? progress = null)
{
    var snapshots = await ReadAllAsync(cancellationToken, progress);
    var statistics = StatisticsProjection.Build(snapshots);

    await using var connection = await OpenDatabaseAsync(cancellationToken);
    await EnsureSchemaAsync(connection, cancellationToken);
    await IngestReferenceDataAsync(connection, cancellationToken);
    await PersistSilverAndGoldAsync(connection, statistics, cancellationToken);
    return await ReadGoldStatisticsAsync(connection, cancellationToken);
}
```

With:
```csharp
public async Task<AtsStatistics> ReadStatisticsAsync(
    CancellationToken cancellationToken,
    IProgress<SaveLoadProgress>? progress = null)
{
    await using var connection = await OpenDatabaseAsync(cancellationToken);
    await EnsureSchemaAsync(connection, cancellationToken);
    return await ReadGoldStatisticsAsync(connection, cancellationToken);
}
```

- [ ] **Step 2: Build to confirm no compile errors**

```
dotnet build src/AtsEmployeeStats.Infrastructure/AtsEmployeeStats.Infrastructure.csproj
```

Expected: builds successfully.

- [ ] **Step 3: Run ALL tests to see which `StatisticsService` tests now fail**

```
dotnet test tests/AtsEmployeeStats.Tests
```

Expected: the five tests that call `service.LoadAsync(...)` will fail because gold is empty (ingestion hasn't been called). The `ReadAllAsync` tests should still pass. Make note of which fail.

- [ ] **Step 4: Commit the pure-gold change before fixing tests**

```
git add src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs
git commit -m "feat: ReadStatisticsAsync is now a pure gold read"
```

---

### Task 6: Update existing `StatisticsService` tests

**Files:**
- Modify: `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs`

Each of the five failing tests calls `service.LoadAsync(...)` to trigger ingestion + read. With the new design they must call `service.IngestAsync(...)` first, then `service.LoadAsync(...)`.

- [ ] **Step 1: Update `StatisticsService_persists_silver_driver_names_truck_assignments_and_gold_drilldown_models`**

Find:
```csharp
var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
var service = new StatisticsService(source);

var statistics = await service.LoadAsync(CancellationToken.None);
```

Replace with:
```csharp
var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
var service = new StatisticsService(source);

await service.IngestAsync(CancellationToken.None);
var statistics = await service.LoadAsync(CancellationToken.None);
```

- [ ] **Step 2: Update `StatisticsService_persists_enriched_truck_metadata_and_recent_driver_jobs`**

Find:
```csharp
var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
var service = new StatisticsService(source);

var statistics = await service.LoadAsync(CancellationToken.None);
```

Replace with:
```csharp
var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
var service = new StatisticsService(source);

await service.IngestAsync(CancellationToken.None);
var statistics = await service.LoadAsync(CancellationToken.None);
```

- [ ] **Step 3: Update `StatisticsService_persists_inferred_deadhead_count_when_driver_returns_to_home_without_paid_job_from_destination`**

Find:
```csharp
var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
var service = new StatisticsService(source);

await service.LoadAsync(CancellationToken.None);
```

Replace with:
```csharp
var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
var service = new StatisticsService(source);

await service.IngestAsync(CancellationToken.None);
await service.LoadAsync(CancellationToken.None);
```

- [ ] **Step 4: Update `StatisticsService_persists_driver_job_pairs_by_combining_both_route_directions`**

Find:
```csharp
var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
var service = new StatisticsService(source);

await service.LoadAsync(CancellationToken.None);
```

Replace with:
```csharp
var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
var service = new StatisticsService(source);

await service.IngestAsync(CancellationToken.None);
await service.LoadAsync(CancellationToken.None);
```

- [ ] **Step 5: Update `StatisticsService_persists_city_route_trailer_and_trend_read_models`**

Find:
```csharp
var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
var service = new StatisticsService(source);

var statistics = await service.LoadAsync(CancellationToken.None);
```

Replace with:
```csharp
var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
var service = new StatisticsService(source);

await service.IngestAsync(CancellationToken.None);
var statistics = await service.LoadAsync(CancellationToken.None);
```

- [ ] **Step 6: Run all tests and verify they all pass**

```
dotnet test tests/AtsEmployeeStats.Tests
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```
git add tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs
git commit -m "test: update StatisticsService tests to use IngestAsync before LoadAsync"
```

---

### Task 7: Create `SaveIngestionService`

**Files:**
- Create: `src/AtsEmployeeStats.Api/SaveIngestionService.cs`

- [ ] **Step 1: Create the file**

```csharp
// src/AtsEmployeeStats.Api/SaveIngestionService.cs
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AtsEmployeeStats.Api;

public sealed class SaveIngestionService(
    StatisticsService statisticsService,
    IHubContext<StatisticsHub> hub) : IHostedService
{
    private Task _ingestionTask = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ingestionTask = RunIngestionAsync();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => _ingestionTask;

    private async Task RunIngestionAsync()
    {
        var progress = new Progress<SaveLoadProgress>(update =>
        {
            _ = hub.Clients.All.SendAsync(
                "LoadingProgress",
                DashboardProgressMapper.ToDashboardProgressDto(update));
        });

        try
        {
            await statisticsService.IngestAsync(CancellationToken.None, progress);
        }
        catch
        {
            // errors during startup ingestion are non-fatal; UI will show stale/empty data
        }
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

```
dotnet build src/AtsEmployeeStats.Api/AtsEmployeeStats.Api.csproj
```

Expected: build error on `historyWindow` argument — that's fixed in Task 8.

- [ ] **Step 3: Commit**

```
git add src/AtsEmployeeStats.Api/SaveIngestionService.cs
git commit -m "feat: add SaveIngestionService hosted service"
```

---

### Task 8: Wire everything in `Program.cs`

**Files:**
- Modify: `src/AtsEmployeeStats.Api/Program.cs`

- [ ] **Step 1: Replace the `ISaveSnapshotSource` registration to also expose `IStatisticsIngestor` and fix the removed `historyWindow` arg**

Replace:
```csharp
builder.Services.TryAddSingleton<ISaveSnapshotSource>(services =>
{
    var options = services.GetRequiredService<IOptions<StatisticsApiOptions>>().Value;
    var referenceDataOptions = new AtsReferenceDataOptions(
        options.ReferenceDataEnabled,
        options.AtsInstallRoot,
        Path.Combine(Path.GetDirectoryName(options.DatabasePath) ?? CommandLineDefaults.DefaultDataDirectory(), "reference-cache"));
    return new SqliteMedallionSaveSnapshotSource(
        options.SaveRoot,
        options.DatabasePath,
        TimeSpan.FromDays(options.HistoryDays),
        referenceDataOptions);
});
```

With:
```csharp
builder.Services.TryAddSingleton<SqliteMedallionSaveSnapshotSource>(services =>
{
    var options = services.GetRequiredService<IOptions<StatisticsApiOptions>>().Value;
    var referenceDataOptions = new AtsReferenceDataOptions(
        options.ReferenceDataEnabled,
        options.AtsInstallRoot,
        Path.Combine(Path.GetDirectoryName(options.DatabasePath) ?? CommandLineDefaults.DefaultDataDirectory(), "reference-cache"));
    return new SqliteMedallionSaveSnapshotSource(
        options.SaveRoot,
        options.DatabasePath,
        referenceDataOptions);
});
builder.Services.TryAddSingleton<ISaveSnapshotSource>(sp =>
    sp.GetRequiredService<SqliteMedallionSaveSnapshotSource>());
builder.Services.TryAddSingleton<IStatisticsIngestor>(sp =>
    sp.GetRequiredService<SqliteMedallionSaveSnapshotSource>());
```

Also add the required using at the top of `Program.cs`:
```csharp
using AtsEmployeeStats.Application.Statistics;
```

- [ ] **Step 2: Register `SaveIngestionService` as a hosted service**

After `builder.Services.AddSingleton<StatisticsService>();`, add:
```csharp
builder.Services.AddHostedService<SaveIngestionService>();
```

- [ ] **Step 3: Update the reload endpoint to call `IngestAsync` then `LoadAsync`**

Replace:
```csharp
app.MapPost("/api/statistics/reload", async (
    int? rangeDays,
    StatisticsService service,
    IOptions<StatisticsApiOptions> options,
    IHubContext<StatisticsHub> hub,
    CancellationToken cancellationToken) =>
{
    await hub.Clients.All.SendAsync(
        "StatusChanged",
        new DashboardStatusDto("Reloading saves...", IsError: false),
        cancellationToken);
    var progress = BuildSignalRProgress(hub, cancellationToken);
    var statistics = await service.LoadAsync(cancellationToken, progress);
    var dto = StatisticsDashboardMapper.ToDashboardDto(statistics, rangeDays ?? options.Value.HistoryDays);
    await hub.Clients.All.SendAsync("StatisticsUpdated", dto, cancellationToken);
    return Results.Ok(dto);
});
```

With:
```csharp
app.MapPost("/api/statistics/reload", async (
    int? rangeDays,
    StatisticsService service,
    IOptions<StatisticsApiOptions> options,
    IHubContext<StatisticsHub> hub,
    CancellationToken cancellationToken) =>
{
    await hub.Clients.All.SendAsync(
        "StatusChanged",
        new DashboardStatusDto("Reloading saves...", IsError: false),
        cancellationToken);
    var progress = BuildSignalRProgress(hub, cancellationToken);
    await service.IngestAsync(cancellationToken, progress);
    var statistics = await service.LoadAsync(cancellationToken, progress);
    var dto = StatisticsDashboardMapper.ToDashboardDto(statistics, rangeDays ?? options.Value.HistoryDays);
    await hub.Clients.All.SendAsync("StatisticsUpdated", dto, cancellationToken);
    return Results.Ok(dto);
});
```

- [ ] **Step 4: Build the full solution**

```
dotnet build
```

Expected: builds successfully with no errors.

- [ ] **Step 5: Run the full test suite**

```
dotnet test tests/AtsEmployeeStats.Tests
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```
git add src/AtsEmployeeStats.Api/Program.cs
git commit -m "feat: wire startup ingestion service and pure-gold API reads"
```

---

## Self-Review Checklist

- **First-run scans all saves:** `IngestAsync` calls `DiscoverCandidatePaths(null, ct)` when `highWaterMark` is null (no `app_metadata` row). `DiscoverCandidatePaths` with null sinceUtc has no lower-bound filter → all files scanned. ✓
- **Subsequent runs use HWM:** `GetHighWaterMarkAsync` reads `last_loaded_save_utc`; `DiscoverCandidatePaths(hwm, ct)` filters `file.LastWriteTimeUtc >= hwm`. ✓
- **HWM is updated after ingestion:** `SetHighWaterMarkAsync` runs `MAX(last_write_time_utc) FROM bronze_save_files WHERE parse_status = 'parsed'` and upserts into `app_metadata`. ✓
- **`historyDays` no longer limits ingestion:** `historyWindow` removed from constructor; `HistoryDays` is only used in `StatisticsDashboardMapper.ToDashboardDto(statistics, rangeDays ?? options.Value.HistoryDays)`. ✓
- **Gold rebuild only on change:** `anyIngested || !goldHasData` guard in `IngestAsync`. ✓
- **`ReadStatisticsAsync` is a pure gold read:** No disk I/O, no bronze scan, no silver/gold rebuild. ✓
- **Startup ingestion via `IHostedService`:** `SaveIngestionService.StartAsync` fires `RunIngestionAsync` as a background task; API serves immediately. ✓
- **Reload endpoint re-ingests:** calls `IngestAsync` then `LoadAsync`. ✓
- **All existing tests updated:** five `StatisticsService_*` tests get `IngestAsync` before `LoadAsync`. ✓
- **Three new HWM tests:** first-run, HWM filter, no-loading-stage-on-second-run. ✓
- **Type consistency:** `IStatisticsIngestor` defined in Task 1, referenced in Task 4 (`SqliteMedallionSaveSnapshotSource`), Task 8 (`Program.cs`). `StatisticsService.IngestAsync` defined in Task 1, called in tests (Task 6), reload endpoint (Task 8), `SaveIngestionService` (Task 7). ✓
