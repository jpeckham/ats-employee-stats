using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;
using Microsoft.Data.Sqlite;

namespace AtsEmployeeStats.Infrastructure.Saves;

public sealed class SqliteMedallionSaveSnapshotSource(
    string rootPath,
    string databasePath,
    AtsReferenceDataOptions? referenceDataOptions = null,
    IScsExtractorDownloader? scsExtractorDownloader = null,
    IScsArchiveExtractor? scsArchiveExtractor = null) : ISaveSnapshotSource, IStatisticsQuerySource, IStatisticsIngestor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SiiSaveTextDecoder _decoder = new();

    public async Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        await using var connection = await OpenDatabaseAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        if (!Directory.Exists(rootPath))
        {
            progress?.Report(new SaveLoadProgress(
                SaveLoadStage.Completed,
                CompletedFiles: 0,
                TotalFiles: 0,
                CompletedUnits: 0,
                TotalUnits: 0,
                Message: "Save root was not found."));
            return [];
        }

        progress?.Report(new SaveLoadProgress(
            SaveLoadStage.DiscoveringFiles,
            CompletedFiles: 0,
            TotalFiles: 0,
            CompletedUnits: 0,
            TotalUnits: 0,
            Message: "Discovering game.sii files..."));

        var paths = DiscoverCandidatePaths(null, cancellationToken);
        progress?.Report(new SaveLoadProgress(
            SaveLoadStage.FilesDiscovered,
            CompletedFiles: 0,
            TotalFiles: paths.Count,
            CompletedUnits: 0,
            TotalUnits: 0,
            Message: $"Found {paths.Count:N0} save file{(paths.Count == 1 ? string.Empty : "s")}."));

        var snapshots = new List<SaveSnapshot>();
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
                snapshots.Add(cached);
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

            SaveSnapshot? snapshot;
            try
            {
                metadata = await ReadHashedFileMetadataAsync(metadata, cancellationToken);
                snapshot = await TryIngestSnapshotAsync(
                    connection,
                    metadata,
                    completedFiles,
                    paths.Count,
                    completedUnits,
                    estimatedTotalUnits,
                    progress,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                snapshot = null;
            }
            completedFiles++;

            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
                var unitCount = snapshot.Document.Units.Count;
                if (estimatedTotalUnits == 0 && unitCount > 0)
                {
                    estimatedTotalUnits = unitCount * paths.Count;
                }

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
            else
            {
                progress?.Report(new SaveLoadProgress(
                    SaveLoadStage.FileLoaded,
                    CompletedFiles: completedFiles,
                    TotalFiles: paths.Count,
                    CompletedUnits: completedUnits,
                    TotalUnits: estimatedTotalUnits,
                    Message: $"Skipped {completedFiles:N0} of {paths.Count:N0} save files.",
                    CurrentFile: path));
            }
        }

        progress?.Report(new SaveLoadProgress(
            SaveLoadStage.Completed,
            CompletedFiles: completedFiles,
            TotalFiles: paths.Count,
            CompletedUnits: completedUnits,
            TotalUnits: Math.Max(estimatedTotalUnits, completedUnits),
            Message: $"Loaded {completedFiles:N0} save file{(completedFiles == 1 ? string.Empty : "s")}."));

        return snapshots
            .OrderByDescending(snapshot => snapshot.LastWritten)
            .ToList();
    }

    public async Task<AtsStatistics> ReadStatisticsAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        await using var connection = await OpenDatabaseAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        return await ReadGoldStatisticsAsync(connection, cancellationToken);
    }

    public async Task IngestAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null,
        bool force = false)
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
        if (anyIngested || !goldHasData || force)
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

    private async Task<SqliteConnection> OpenDatabaseAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString());
        await connection.OpenAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        return connection;
    }

    private static async Task ConfigureConnectionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            pragma journal_mode = wal;
            pragma synchronous = normal;
            pragma temp_store = memory;
            pragma cache_size = -20000;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists bronze_save_files (
                save_id text primary key,
                full_path text not null,
                profile_id text not null,
                save_slot_name text not null,
                last_write_time_utc text not null,
                file_size integer not null,
                content_hash text not null,
                ingested_time_utc text not null,
                parse_status text not null,
                error_message text
            );

            create table if not exists bronze_sii_units (
                save_id text not null,
                unit_ordinal integer not null,
                unit_type text not null,
                unit_id text not null,
                scalar_values_json text not null,
                array_values_json text not null,
                primary key (save_id, unit_ordinal),
                foreign key (save_id) references bronze_save_files(save_id) on delete cascade
            );

            create table if not exists bronze_reference_archives (
                archive_id text primary key,
                full_path text not null,
                content_hash text not null,
                extracted_time_utc text not null,
                status text not null,
                error_message text
            );

            create table if not exists bronze_reference_sii_units (
                archive_id text not null,
                relative_path text not null,
                unit_ordinal integer not null,
                unit_type text not null,
                unit_id text not null,
                scalar_values_json text not null,
                array_values_json text not null,
                primary key (archive_id, relative_path, unit_ordinal),
                foreign key (archive_id) references bronze_reference_archives(archive_id) on delete cascade
            );

            create table if not exists silver_companies (
                company_id text primary key,
                display_name text not null,
                last_updated_utc text not null
            );

            create table if not exists silver_garages (
                company_id text not null,
                garage_id text not null,
                display_name text not null,
                profit integer not null,
                employee_count integer not null,
                truck_count integer not null,
                primary key (company_id, garage_id)
            );

            create table if not exists silver_drivers (
                company_id text not null,
                driver_id text not null,
                display_name text not null,
                profit integer not null,
                garage_id text,
                truck_id text,
                primary key (company_id, driver_id)
            );

            create table if not exists silver_trucks (
                company_id text not null,
                truck_id text not null,
                display_name text not null,
                profit integer not null,
                garage_id text,
                driver_id text,
                license_plate text,
                model_name text,
                definition_path text,
                primary key (company_id, truck_id)
            );

            create table if not exists silver_jobs (
                company_id text not null,
                job_id text not null,
                driver_id text,
                truck_id text,
                trailer_id text,
                trailer_type text,
                cargo text,
                origin_city text,
                destination_city text,
                profit integer not null,
                timestamp_day integer,
                garage_id text,
                primary key (company_id, job_id)
            );

            create table if not exists silver_driver_recent_jobs (
                company_id text not null,
                driver_id text not null,
                job_id text not null,
                truck_id text,
                cargo text,
                origin_city text,
                destination_city text,
                revenue integer not null,
                expenses integer not null,
                profit integer not null,
                distance integer,
                timestamp_day integer,
                primary key (company_id, driver_id, job_id)
            );

            create table if not exists silver_trailer_types (
                company_id text not null,
                trailer_type text not null,
                profit integer not null,
                mission_count integer not null,
                primary key (company_id, trailer_type)
            );

            create table if not exists silver_trailers (
                company_id text not null,
                trailer_id text not null,
                trailer_type text not null,
                profit integer not null,
                job_count integer not null,
                primary key (company_id, trailer_id)
            );

            create table if not exists silver_cities (
                company_id text not null,
                city_id text not null,
                display_name text not null,
                has_owned_garage integer not null,
                is_garage_eligible integer not null,
                visit_count integer not null,
                outbound_profit integer not null,
                inbound_profit integer not null,
                bidirectional_profit integer not null,
                expansion_score real not null,
                primary key (company_id, city_id)
            );

            create table if not exists silver_routes (
                company_id text not null,
                origin_city_id text not null,
                destination_city_id text not null,
                profit integer not null,
                job_count integer not null,
                profit_per_mile real not null,
                return_coverage_ratio real not null,
                primary key (company_id, origin_city_id, destination_city_id)
            );

            create table if not exists gold_company_summary (
                company_id text primary key,
                display_name text not null,
                last_updated_utc text not null,
                garage_profit integer not null,
                mission_profit integer not null,
                driver_count integer not null,
                garage_count integer not null
            );

            create table if not exists gold_garage_ranking (
                company_id text not null,
                garage_id text not null,
                display_name text not null,
                profit integer not null,
                employee_count integer not null,
                truck_count integer not null,
                rank integer not null,
                primary key (company_id, garage_id)
            );

            create table if not exists gold_garage_drivers (
                company_id text not null,
                garage_id text not null,
                driver_id text not null,
                display_name text not null,
                profit integer not null,
                truck_id text,
                primary key (company_id, garage_id, driver_id)
            );

            create table if not exists gold_driver_job_types (
                company_id text not null,
                driver_id text not null,
                job_type text not null,
                mission_count integer not null,
                profit integer not null,
                primary key (company_id, driver_id, job_type)
            );

            create table if not exists gold_job_type_jobs (
                company_id text not null,
                driver_id text not null,
                job_type text not null,
                job_id text not null,
                profit integer not null,
                primary key (company_id, driver_id, job_type, job_id)
            );

            create table if not exists gold_job_details (
                company_id text not null,
                job_id text not null,
                driver_id text,
                job_type text,
                origin_city text,
                destination_city text,
                cargo text,
                trailer_type text,
                truck_id text,
                profit integer not null,
                timestamp_day integer,
                garage_id text,
                primary key (company_id, job_id)
            );

            create table if not exists gold_driver_recent_jobs (
                company_id text not null,
                driver_id text not null,
                job_id text not null,
                truck_id text,
                cargo text,
                origin_city text,
                destination_city text,
                revenue integer not null,
                expenses integer not null,
                profit integer not null,
                distance integer,
                timestamp_day integer,
                rank integer not null,
                primary key (company_id, driver_id, job_id)
            );

            create table if not exists gold_driver_job_pairs (
                company_id text not null,
                driver_id text not null,
                endpoint_a text not null,
                endpoint_b text not null,
                route_pair text not null,
                mission_count integer not null,
                profit integer not null,
                primary key (company_id, driver_id, endpoint_a, endpoint_b)
            );

            create table if not exists gold_driver_deadhead_summary (
                company_id text not null,
                driver_id text not null,
                inferred_deadhead_count integer not null,
                primary key (company_id, driver_id)
            );

            create table if not exists gold_city_profitability (
                company_id text not null,
                city_id text not null,
                display_name text not null,
                has_owned_garage integer not null,
                is_garage_eligible integer not null,
                visit_count integer not null,
                outbound_profit integer not null,
                inbound_profit integer not null,
                bidirectional_profit integer not null,
                expansion_score real not null,
                primary key (company_id, city_id)
            );

            create table if not exists gold_route_profitability (
                company_id text not null,
                origin_city_id text not null,
                destination_city_id text not null,
                profit integer not null,
                job_count integer not null,
                profit_per_mile real not null,
                return_coverage_ratio real not null,
                primary key (company_id, origin_city_id, destination_city_id)
            );

            create table if not exists gold_profit_trends (
                company_id text not null,
                entity_kind text not null,
                entity_id text not null,
                game_day integer not null,
                profit integer not null,
                sample_count integer not null,
                primary key (company_id, entity_kind, entity_id, game_day)
            );

            create table if not exists silver_driver_truck_assignments (
                company_id text not null,
                driver_id text not null,
                truck_id text not null,
                effective_from_save_name text not null,
                effective_to_save_name text,
                is_current integer not null,
                primary key (company_id, driver_id, effective_from_save_name)
            );

            create table if not exists silver_driver_garage_assignments (
                company_id text not null,
                driver_id text not null,
                garage_id text not null,
                effective_from_save_name text not null,
                effective_to_save_name text,
                is_current integer not null,
                primary key (company_id, driver_id, effective_from_save_name)
            );

            create table if not exists app_metadata (
                key   text primary key,
                value text not null
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "silver_trucks", "license_plate", "text", cancellationToken);
        await EnsureColumnAsync(connection, "silver_trucks", "model_name", "text", cancellationToken);
        await EnsureColumnAsync(connection, "silver_trucks", "definition_path", "text", cancellationToken);
        await EnsureColumnAsync(connection, "silver_jobs", "timestamp_day", "integer", cancellationToken);
        await EnsureColumnAsync(connection, "gold_job_details", "timestamp_day", "integer", cancellationToken);
        await EnsureColumnAsync(connection, "gold_job_details", "trailer_id", "text", cancellationToken);
        await EnsureColumnAsync(connection, "silver_trailers", "body_type", "text", cancellationToken);
        await EnsureColumnAsync(connection, "silver_trailers", "is_articulated", "integer", cancellationToken);
        await EnsureColumnAsync(connection, "silver_jobs", "garage_id", "text", cancellationToken);
        await EnsureColumnAsync(connection, "gold_job_details", "garage_id", "text", cancellationToken);
        await EnsureColumnAsync(connection, "silver_trailers", "garage_id", "text", cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"pragma table_info({tableName})";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(reader.GetString(1), columnName))
                {
                    return;
                }
            }
        }

        await ExecuteAsync(
            connection,
            $"alter table {tableName} add column {columnName} {columnDefinition}",
            cancellationToken);
    }

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

    private static FileMetadata ReadFastFileMetadata(string path)
    {
        var info = new FileInfo(path);
        return new FileMetadata(
            BuildSaveId(path),
            path,
            GetProfileSegment(path),
            GetSaveSlot(path),
            info.LastWriteTimeUtc,
            info.Length,
            ContentHash: null);
    }

    private static async Task<FileMetadata> ReadHashedFileMetadataAsync(
        FileMetadata metadata,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(metadata.FullPath);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return metadata with { ContentHash = Convert.ToHexString(hashBytes).ToLowerInvariant() };
    }

    private async Task<SaveSnapshot?> TryReadCachedSnapshotAsync(
        SqliteConnection connection,
        FileMetadata metadata,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select count(*)
            from bronze_save_files
            where save_id = $save_id
              and full_path = $full_path
              and last_write_time_utc = $last_write_time_utc
              and file_size = $file_size
              and parse_status = 'parsed'
            """;
        Add(command, "$save_id", metadata.SaveId);
        Add(command, "$full_path", metadata.FullPath);
        Add(command, "$last_write_time_utc", FormatUtc(metadata.LastWriteTimeUtc));
        Add(command, "$file_size", metadata.FileSize);

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        if (count == 0)
        {
            return null;
        }

        return new SaveSnapshot(
            metadata.FullPath,
            metadata.LastWriteTimeUtc,
            await ReadBronzeDocumentAsync(connection, metadata.SaveId, cancellationToken));
    }

    private async Task<SaveSnapshot?> TryIngestSnapshotAsync(
        SqliteConnection connection,
        FileMetadata metadata,
        int completedFiles,
        int totalFiles,
        int completedUnits,
        int estimatedTotalUnits,
        IProgress<SaveLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await _decoder.DecodeAsync(metadata.FullPath, cancellationToken);
            if (!content.Contains("SiiNunit", StringComparison.OrdinalIgnoreCase))
            {
                await UpsertFileAsync(connection, metadata, "skipped", "File did not contain plain SII content.", cancellationToken);
                return null;
            }

            var currentFileTotalUnits = SiiSaveParser.CountUnits(content);
            var parserProgress = new InlineProgress<int>(currentFileCompletedUnits =>
            {
                progress?.Report(new SaveLoadProgress(
                    SaveLoadStage.LoadingFiles,
                    CompletedFiles: completedFiles,
                    TotalFiles: totalFiles,
                    CompletedUnits: completedUnits + currentFileCompletedUnits,
                    TotalUnits: Math.Max(estimatedTotalUnits, completedUnits + currentFileTotalUnits),
                    Message: $"Parsing save file {completedFiles + 1:N0} of {totalFiles:N0}.",
                    CurrentFile: metadata.FullPath,
                    CurrentFileCompletedUnits: currentFileCompletedUnits,
                    CurrentFileTotalUnits: currentFileTotalUnits));
            });
            var document = SiiSaveParser.Parse(content, parserProgress);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await UpsertFileAsync(connection, metadata, "parsed", null, cancellationToken);
            await DeleteUnitsAsync(connection, metadata.SaveId, cancellationToken);
            await InsertUnitsAsync(connection, metadata.SaveId, document, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new SaveSnapshot(metadata.FullPath, metadata.LastWriteTimeUtc, document);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await UpsertFileAsync(connection, metadata, "failed", ex.Message, cancellationToken);
            return null;
        }
    }

    private static async Task UpsertFileAsync(
        SqliteConnection connection,
        FileMetadata metadata,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into bronze_save_files (
                save_id,
                full_path,
                profile_id,
                save_slot_name,
                last_write_time_utc,
                file_size,
                content_hash,
                ingested_time_utc,
                parse_status,
                error_message
            )
            values (
                $save_id,
                $full_path,
                $profile_id,
                $save_slot_name,
                $last_write_time_utc,
                $file_size,
                $content_hash,
                $ingested_time_utc,
                $parse_status,
                $error_message
            )
            on conflict(save_id) do update set
                full_path = excluded.full_path,
                profile_id = excluded.profile_id,
                save_slot_name = excluded.save_slot_name,
                last_write_time_utc = excluded.last_write_time_utc,
                file_size = excluded.file_size,
                content_hash = excluded.content_hash,
                ingested_time_utc = excluded.ingested_time_utc,
                parse_status = excluded.parse_status,
                error_message = excluded.error_message
            """;
        Add(command, "$save_id", metadata.SaveId);
        Add(command, "$full_path", metadata.FullPath);
        Add(command, "$profile_id", metadata.ProfileSegment);
        Add(command, "$save_slot_name", metadata.SaveSlot);
        Add(command, "$last_write_time_utc", FormatUtc(metadata.LastWriteTimeUtc));
        Add(command, "$file_size", metadata.FileSize);
        Add(command, "$content_hash", metadata.ContentHash ?? throw new InvalidOperationException("Content hash is required before persisting save metadata."));
        Add(command, "$ingested_time_utc", FormatUtc(DateTime.UtcNow));
        Add(command, "$parse_status", status);
        Add(command, "$error_message", errorMessage);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteUnitsAsync(
        SqliteConnection connection,
        string saveId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "delete from bronze_sii_units where save_id = $save_id";
        Add(command, "$save_id", saveId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertUnitsAsync(
        SqliteConnection connection,
        string saveId,
        SiiDocument document,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into bronze_sii_units (
                save_id,
                unit_ordinal,
                unit_type,
                unit_id,
                scalar_values_json,
                array_values_json
            )
            values (
                $save_id,
                $unit_ordinal,
                $unit_type,
                $unit_id,
                $scalar_values_json,
                $array_values_json
            )
            """;
        var saveIdParameter = command.Parameters.Add("$save_id", SqliteType.Text);
        var ordinalParameter = command.Parameters.Add("$unit_ordinal", SqliteType.Integer);
        var typeParameter = command.Parameters.Add("$unit_type", SqliteType.Text);
        var idParameter = command.Parameters.Add("$unit_id", SqliteType.Text);
        var scalarJsonParameter = command.Parameters.Add("$scalar_values_json", SqliteType.Text);
        var arrayJsonParameter = command.Parameters.Add("$array_values_json", SqliteType.Text);
        await command.PrepareAsync(cancellationToken);

        for (var i = 0; i < document.Units.Count; i++)
        {
            var unit = document.Units[i];
            saveIdParameter.Value = saveId;
            ordinalParameter.Value = i;
            typeParameter.Value = unit.Type;
            idParameter.Value = unit.Id;
            scalarJsonParameter.Value = JsonSerializer.Serialize(unit.Values, JsonOptions);
            arrayJsonParameter.Value = JsonSerializer.Serialize(unit.Arrays, JsonOptions);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task IngestReferenceDataAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (referenceDataOptions is null || !referenceDataOptions.Enabled)
        {
            return;
        }

        var ingestor = new ScsReferenceDataIngestor(referenceDataOptions, scsExtractorDownloader, scsArchiveExtractor);
        ExtractedReferenceData? extracted;
        try
        {
            extracted = await ingestor.ExtractLocaleAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await RecordReferenceExtractionFailureAsync(connection, ex.Message, cancellationToken);
            return;
        }

        if (extracted is null)
        {
            return;
        }

        var existingCount = await CountReferenceUnitsAsync(connection, extracted.ArchiveHash, cancellationToken);
        if (existingCount > 0)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            insert or replace into bronze_reference_archives (
                archive_id, full_path, content_hash, extracted_time_utc, status, error_message
            )
            values ($archive_id, $full_path, $content_hash, $extracted_time_utc, $status, $error_message)
            """,
            cancellationToken,
            ("$archive_id", extracted.ArchiveHash),
            ("$full_path", extracted.ArchivePath),
            ("$content_hash", extracted.ArchiveHash),
            ("$extracted_time_utc", FormatUtc(DateTime.UtcNow)),
            ("$status", "parsed"),
            ("$error_message", null));
        await ExecuteAsync(
            connection,
            "delete from bronze_reference_sii_units where archive_id = $archive_id",
            cancellationToken,
            ("$archive_id", extracted.ArchiveHash));

        foreach (var fileName in new[] { "driver_names.sii", "local.sii" })
        {
            foreach (var path in Directory.EnumerateFiles(extracted.OutputDirectory, fileName, SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(extracted.OutputDirectory, path)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                var document = SiiSaveParser.Parse(await File.ReadAllTextAsync(path, cancellationToken));
                await InsertReferenceUnitsAsync(connection, extracted.ArchiveHash, relativePath, document, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RecordReferenceExtractionFailureAsync(
        SqliteConnection connection,
        string message,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            """
            insert or replace into bronze_reference_archives (
                archive_id, full_path, content_hash, extracted_time_utc, status, error_message
            )
            values ($archive_id, $full_path, $content_hash, $extracted_time_utc, $status, $error_message)
            """,
            cancellationToken,
            ("$archive_id", "locale-scs-extraction-failed"),
            ("$full_path", referenceDataOptions?.GameInstallRoot ?? string.Empty),
            ("$content_hash", string.Empty),
            ("$extracted_time_utc", FormatUtc(DateTime.UtcNow)),
            ("$status", "failed"),
            ("$error_message", message));
    }

    private static async Task<long> CountReferenceUnitsAsync(
        SqliteConnection connection,
        string archiveId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from bronze_reference_sii_units where archive_id = $archive_id";
        Add(command, "$archive_id", archiveId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    private static async Task InsertReferenceUnitsAsync(
        SqliteConnection connection,
        string archiveId,
        string relativePath,
        SiiDocument document,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into bronze_reference_sii_units (
                archive_id,
                relative_path,
                unit_ordinal,
                unit_type,
                unit_id,
                scalar_values_json,
                array_values_json
            )
            values (
                $archive_id,
                $relative_path,
                $unit_ordinal,
                $unit_type,
                $unit_id,
                $scalar_values_json,
                $array_values_json
            )
            """;
        var archiveIdParameter = command.Parameters.Add("$archive_id", SqliteType.Text);
        var relativePathParameter = command.Parameters.Add("$relative_path", SqliteType.Text);
        var ordinalParameter = command.Parameters.Add("$unit_ordinal", SqliteType.Integer);
        var typeParameter = command.Parameters.Add("$unit_type", SqliteType.Text);
        var idParameter = command.Parameters.Add("$unit_id", SqliteType.Text);
        var scalarJsonParameter = command.Parameters.Add("$scalar_values_json", SqliteType.Text);
        var arrayJsonParameter = command.Parameters.Add("$array_values_json", SqliteType.Text);
        await command.PrepareAsync(cancellationToken);

        for (var i = 0; i < document.Units.Count; i++)
        {
            var unit = document.Units[i];
            archiveIdParameter.Value = archiveId;
            relativePathParameter.Value = relativePath;
            ordinalParameter.Value = i;
            typeParameter.Value = unit.Type;
            idParameter.Value = unit.Id;
            scalarJsonParameter.Value = JsonSerializer.Serialize(unit.Values, JsonOptions);
            arrayJsonParameter.Value = JsonSerializer.Serialize(unit.Arrays, JsonOptions);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<SiiDocument> ReadBronzeDocumentAsync(
        SqliteConnection connection,
        string saveId,
        CancellationToken cancellationToken)
    {
        var units = new List<SiiUnit>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select unit_type, unit_id, scalar_values_json, array_values_json
            from bronze_sii_units
            where save_id = $save_id
            order by unit_ordinal
            """;
        Add(command, "$save_id", saveId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(2), JsonOptions)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var arrays = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(reader.GetString(3), JsonOptions)
                ?? [];

            units.Add(new SiiUnit(
                reader.GetString(0),
                reader.GetString(1),
                new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase),
                arrays.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<string>)pair.Value,
                    StringComparer.OrdinalIgnoreCase)));
        }

        return new SiiDocument(units);
    }

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

    private static async Task PersistSilverAndGoldAsync(
        SqliteConnection connection,
        AtsStatistics statistics,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var table in new[]
        {
            "silver_companies",
            "silver_garages",
            "silver_drivers",
            "silver_trucks",
            "silver_jobs",
            "silver_driver_recent_jobs",
            "silver_trailer_types",
            "silver_trailers",
            "silver_cities",
            "silver_routes",
            "gold_company_summary",
            "gold_garage_ranking",
            "gold_garage_drivers",
            "gold_driver_job_types",
            "gold_job_type_jobs",
            "gold_job_details",
            "gold_driver_recent_jobs",
            "gold_driver_job_pairs",
            "gold_driver_deadhead_summary",
            "gold_city_profitability",
            "gold_route_profitability",
            "gold_profit_trends",
            "silver_driver_truck_assignments",
            "silver_driver_garage_assignments"
        })
        {
            await ExecuteAsync(connection, $"delete from {table}", cancellationToken);
        }

        foreach (var company in statistics.Companies)
        {
            await ExecuteAsync(
                connection,
                """
                insert into silver_companies (company_id, display_name, last_updated_utc)
                values ($company_id, $display_name, $last_updated_utc)
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$display_name", company.DisplayName),
                ("$last_updated_utc", FormatUtc(company.LastUpdated.UtcDateTime)));

            foreach (var garage in company.Garages)
            {
                await ExecuteAsync(
                    connection,
                    """
                    insert into silver_garages (company_id, garage_id, display_name, profit, employee_count, truck_count)
                    values ($company_id, $garage_id, $display_name, $profit, $employee_count, $truck_count)
                    """,
                    cancellationToken,
                    ("$company_id", company.Id),
                    ("$garage_id", garage.Id),
                    ("$display_name", garage.DisplayName),
                    ("$profit", garage.Profit),
                    ("$employee_count", garage.EmployeeCount),
                    ("$truck_count", garage.TruckCount));
            }

            foreach (var driver in company.Drivers)
            {
                await ExecuteAsync(
                    connection,
                    """
                    insert into silver_drivers (company_id, driver_id, display_name, profit, garage_id, truck_id)
                    values ($company_id, $driver_id, $display_name, $profit, $garage_id, $truck_id)
                    """,
                    cancellationToken,
                    ("$company_id", company.Id),
                    ("$driver_id", driver.Id),
                    ("$display_name", driver.DisplayName),
                    ("$profit", driver.Profit),
                    ("$garage_id", driver.GarageId),
                    ("$truck_id", driver.TruckId));
            }

            foreach (var truck in company.Trucks)
            {
                await ExecuteAsync(
                    connection,
                    """
                    insert into silver_trucks (
                        company_id, truck_id, display_name, profit, garage_id, driver_id, license_plate, model_name, definition_path
                    )
                    values (
                        $company_id, $truck_id, $display_name, $profit, $garage_id, $driver_id, $license_plate, $model_name, $definition_path
                    )
                    """,
                    cancellationToken,
                    ("$company_id", company.Id),
                    ("$truck_id", truck.Id),
                    ("$display_name", truck.DisplayName),
                    ("$profit", truck.Profit),
                    ("$garage_id", truck.GarageId),
                    ("$driver_id", truck.DriverId),
                    ("$license_plate", truck.LicensePlate),
                    ("$model_name", truck.ModelName),
                    ("$definition_path", truck.DefinitionPath));
            }

            foreach (var mission in company.Missions)
            {
                await ExecuteAsync(
                    connection,
                    """
                    insert into silver_jobs (
                        company_id, job_id, driver_id, truck_id, trailer_id, trailer_type, cargo, origin_city, destination_city, profit, timestamp_day, garage_id
                    )
                    values (
                        $company_id, $job_id, $driver_id, $truck_id, $trailer_id, $trailer_type, $cargo, $origin_city, $destination_city, $profit, $timestamp_day, $garage_id
                    )
                    """,
                    cancellationToken,
                    ("$company_id", company.Id),
                    ("$job_id", mission.Id),
                    ("$driver_id", mission.DriverId),
                    ("$truck_id", mission.TruckId),
                    ("$trailer_id", mission.TrailerId),
                    ("$trailer_type", mission.TrailerType),
                    ("$cargo", mission.Cargo),
                    ("$origin_city", mission.SourceCity),
                    ("$destination_city", mission.TargetCity),
                    ("$profit", mission.Profit),
                    ("$timestamp_day", mission.TimestampDay),
                    ("$garage_id", mission.GarageId));
            }

            foreach (var job in company.RecentDriverJobs)
            {
                await ExecuteAsync(
                    connection,
                    """
                    insert into silver_driver_recent_jobs (
                        company_id, driver_id, job_id, truck_id, cargo, origin_city, destination_city,
                        revenue, expenses, profit, distance, timestamp_day
                    )
                    values (
                        $company_id, $driver_id, $job_id, $truck_id, $cargo, $origin_city, $destination_city,
                        $revenue, $expenses, $profit, $distance, $timestamp_day
                    )
                    """,
                    cancellationToken,
                    ("$company_id", company.Id),
                    ("$driver_id", job.DriverId),
                    ("$job_id", job.Id),
                    ("$truck_id", job.TruckId),
                    ("$cargo", job.Cargo),
                    ("$origin_city", job.SourceCity),
                    ("$destination_city", job.TargetCity),
                    ("$revenue", job.Revenue),
                    ("$expenses", job.Expenses),
                    ("$profit", job.Profit),
                    ("$distance", job.Distance),
                    ("$timestamp_day", job.TimestampDay));
            }

            foreach (var trailerType in company.TrailerTypes)
            {
                await ExecuteAsync(
                    connection,
                    """
                    insert into silver_trailer_types (company_id, trailer_type, profit, mission_count)
                    values ($company_id, $trailer_type, $profit, $mission_count)
                    """,
                    cancellationToken,
                    ("$company_id", company.Id),
                    ("$trailer_type", trailerType.Id),
                    ("$profit", trailerType.Profit),
                    ("$mission_count", trailerType.MissionCount));
            }

            foreach (var trailer in company.Trailers)
            {
                await ExecuteAsync(
                    connection,
                    """
                    insert into silver_trailers (company_id, trailer_id, trailer_type, profit, job_count, body_type, is_articulated, garage_id)
                    values ($company_id, $trailer_id, $trailer_type, $profit, $job_count, $body_type, $is_articulated, $garage_id)
                    """,
                    cancellationToken,
                    ("$company_id", company.Id),
                    ("$trailer_id", trailer.Id),
                    ("$trailer_type", trailer.TrailerType),
                    ("$profit", trailer.Profit),
                    ("$job_count", trailer.JobCount),
                    ("$body_type", trailer.BodyType),
                    ("$is_articulated", trailer.IsArticulated ? 1 : 0),
                    ("$garage_id", trailer.GarageId));
            }

            foreach (var city in company.Cities)
            {
                await ExecuteAsync(
                    connection,
                    """
                    insert into silver_cities (
                        company_id, city_id, display_name, has_owned_garage, is_garage_eligible,
                        visit_count, outbound_profit, inbound_profit, bidirectional_profit, expansion_score
                    )
                    values (
                        $company_id, $city_id, $display_name, $has_owned_garage, $is_garage_eligible,
                        $visit_count, $outbound_profit, $inbound_profit, $bidirectional_profit, $expansion_score
                    )
                    """,
                    cancellationToken,
                    ("$company_id", company.Id),
                    ("$city_id", city.Id),
                    ("$display_name", city.DisplayName),
                    ("$has_owned_garage", city.HasOwnedGarage ? 1 : 0),
                    ("$is_garage_eligible", city.IsGarageEligible ? 1 : 0),
                    ("$visit_count", city.VisitCount),
                    ("$outbound_profit", city.OutboundProfit),
                    ("$inbound_profit", city.InboundProfit),
                    ("$bidirectional_profit", city.BidirectionalProfit),
                    ("$expansion_score", city.ExpansionScore));
            }

            foreach (var route in company.Routes)
            {
                await ExecuteAsync(
                    connection,
                    """
                    insert into silver_routes (
                        company_id, origin_city_id, destination_city_id, profit, job_count, profit_per_mile, return_coverage_ratio
                    )
                    values (
                        $company_id, $origin_city_id, $destination_city_id, $profit, $job_count, $profit_per_mile, $return_coverage_ratio
                    )
                    """,
                    cancellationToken,
                    ("$company_id", company.Id),
                    ("$origin_city_id", route.OriginCityId),
                    ("$destination_city_id", route.DestinationCityId),
                    ("$profit", route.Profit),
                    ("$job_count", route.JobCount),
                    ("$profit_per_mile", route.ProfitPerMile),
                    ("$return_coverage_ratio", route.ReturnCoverageRatio));
            }

            await ApplyReferenceDriverNamesAsync(connection, cancellationToken);
            await PersistGoldAsync(connection, company, cancellationToken);
            await ApplyReferenceCargoNamesAsync(connection, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ApplyReferenceDriverNamesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var locale = await LoadLocaleAsync(connection, "%driver_names%", cancellationToken);
        if (locale.Count == 0) return;

        await using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = "update silver_drivers set display_name = $display_name where driver_id = $driver_id";
        var driverParam = updateCmd.Parameters.Add("$driver_id", Microsoft.Data.Sqlite.SqliteType.Text);
        var nameParam = updateCmd.Parameters.Add("$display_name", Microsoft.Data.Sqlite.SqliteType.Text);

        List<string> driverIds;
        await using (var selectCmd = connection.CreateCommand())
        {
            selectCmd.CommandText = "select distinct driver_id from silver_drivers";
            await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
            driverIds = [];
            while (await reader.ReadAsync(cancellationToken))
                driverIds.Add(reader.GetString(0));
        }

        foreach (var driverId in driverIds)
        {
            if (locale.TryGetValue(driverId, out var name) && !string.IsNullOrWhiteSpace(name))
            {
                driverParam.Value = driverId;
                nameParam.Value = name;
                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    private static async Task ApplyReferenceCargoNamesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var locale = await LoadLocaleAsync(connection, "%local.sii%", cancellationToken);
        if (locale.Count == 0) return;

        foreach (var tableName in new[] { "silver_jobs", "gold_job_details" })
        {
            List<string> cargoIds;
            await using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.CommandText = $"select distinct cargo from {tableName} where cargo is not null";
                await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
                cargoIds = [];
                while (await reader.ReadAsync(cancellationToken))
                    cargoIds.Add(reader.GetString(0));
            }

            await using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = $"update {tableName} set cargo = $display_name where cargo = $cargo_id";
            var cargoParam = updateCmd.Parameters.Add("$cargo_id", Microsoft.Data.Sqlite.SqliteType.Text);
            var nameParam = updateCmd.Parameters.Add("$display_name", Microsoft.Data.Sqlite.SqliteType.Text);

            foreach (var cargoId in cargoIds)
            {
                if (locale.TryGetValue("cn_" + cargoId, out var name) && !string.IsNullOrWhiteSpace(name))
                {
                    cargoParam.Value = cargoId;
                    nameParam.Value = name;
                    await updateCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }
    }

    private static async Task<Dictionary<string, string>> LoadLocaleAsync(
        SqliteConnection connection,
        string pathPattern,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select array_values_json
            from bronze_reference_sii_units
            where unit_type = 'localization_db'
              and relative_path like $path_pattern
            """;
        Add(command, "$path_pattern", pathPattern);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var json = reader.GetString(0);
            if (string.IsNullOrWhiteSpace(json)) continue;
            var arrays = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, JsonOptions);
            if (arrays is null) continue;
            if (!arrays.TryGetValue("key", out var keys) || !arrays.TryGetValue("val", out var vals)) continue;
            var count = Math.Min(keys.Count, vals.Count);
            for (var i = 0; i < count; i++)
            {
                if (!string.IsNullOrWhiteSpace(keys[i]) && !string.IsNullOrWhiteSpace(vals[i]))
                    result.TryAdd(keys[i], vals[i]);
            }
        }
        return result;
    }

    private static async Task PersistGoldAsync(
        SqliteConnection connection,
        CompanyStatistics company,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            """
            insert into gold_company_summary (
                company_id, display_name, last_updated_utc, garage_profit, mission_profit, driver_count, garage_count
            )
            values (
                $company_id, $display_name, $last_updated_utc, $garage_profit, $mission_profit, $driver_count, $garage_count
            )
            """,
            cancellationToken,
            ("$company_id", company.Id),
            ("$display_name", company.DisplayName),
            ("$last_updated_utc", FormatUtc(company.LastUpdated.UtcDateTime)),
            ("$garage_profit", company.Garages.Sum(garage => garage.Profit)),
            ("$mission_profit", company.Missions.Sum(mission => mission.Profit)),
            ("$driver_count", company.Drivers.Count),
            ("$garage_count", company.Garages.Count));

        var rank = 1;
        foreach (var garage in company.Garages.OrderByDescending(garage => garage.Profit))
        {
            await ExecuteAsync(
                connection,
                """
                insert into gold_garage_ranking (company_id, garage_id, display_name, profit, employee_count, truck_count, rank)
                values ($company_id, $garage_id, $display_name, $profit, $employee_count, $truck_count, $rank)
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$garage_id", garage.Id),
                ("$display_name", garage.DisplayName),
                ("$profit", garage.Profit),
                ("$employee_count", garage.EmployeeCount),
                ("$truck_count", garage.TruckCount),
                ("$rank", rank++));
        }

        foreach (var driver in company.Drivers.Where(driver => !string.IsNullOrWhiteSpace(driver.GarageId)))
        {
            var displayName = await ReadSilverDriverDisplayNameAsync(
                connection,
                company.Id,
                driver.Id,
                cancellationToken) ?? driver.DisplayName;

            await ExecuteAsync(
                connection,
                """
                insert into gold_garage_drivers (company_id, garage_id, driver_id, display_name, profit, truck_id)
                values ($company_id, $garage_id, $driver_id, $display_name, $profit, $truck_id)
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$garage_id", driver.GarageId),
                ("$driver_id", driver.Id),
                ("$display_name", displayName),
                ("$profit", driver.Profit),
                ("$truck_id", driver.TruckId));
        }

        foreach (var group in company.Missions
            .Where(mission => !string.IsNullOrWhiteSpace(mission.DriverId))
            .GroupBy(mission => new { DriverId = mission.DriverId!, JobType = mission.TrailerType ?? "unknown" }))
        {
            await ExecuteAsync(
                connection,
                """
                insert into gold_driver_job_types (company_id, driver_id, job_type, mission_count, profit)
                values ($company_id, $driver_id, $job_type, $mission_count, $profit)
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$driver_id", group.Key.DriverId),
                ("$job_type", group.Key.JobType),
                ("$mission_count", group.Count()),
                ("$profit", group.Sum(mission => mission.Profit)));

            foreach (var mission in group)
            {
                await ExecuteAsync(
                    connection,
                    """
                    insert into gold_job_type_jobs (company_id, driver_id, job_type, job_id, profit)
                    values ($company_id, $driver_id, $job_type, $job_id, $profit)
                    """,
                    cancellationToken,
                    ("$company_id", company.Id),
                    ("$driver_id", group.Key.DriverId),
                    ("$job_type", group.Key.JobType),
                    ("$job_id", mission.Id),
                    ("$profit", mission.Profit));
            }
        }

        foreach (var mission in company.Missions)
        {
            await ExecuteAsync(
                connection,
                """
                insert into gold_job_details (
                    company_id, job_id, driver_id, job_type, origin_city, destination_city, cargo, trailer_type, truck_id, profit, timestamp_day, trailer_id, garage_id
                )
                values (
                    $company_id, $job_id, $driver_id, $job_type, $origin_city, $destination_city, $cargo, $trailer_type, $truck_id, $profit, $timestamp_day, $trailer_id, $garage_id
                )
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$job_id", mission.Id),
                ("$driver_id", mission.DriverId),
                ("$job_type", mission.TrailerType ?? "unknown"),
                ("$origin_city", mission.SourceCity),
                ("$destination_city", mission.TargetCity),
                ("$cargo", mission.Cargo),
                ("$trailer_type", mission.TrailerType),
                ("$truck_id", mission.TruckId),
                ("$profit", mission.Profit),
                ("$timestamp_day", mission.TimestampDay),
                ("$trailer_id", mission.TrailerId),
                ("$garage_id", mission.GarageId));
        }

        foreach (var group in company.RecentDriverJobs.GroupBy(job => job.DriverId, StringComparer.OrdinalIgnoreCase))
        {
            var recentJobRank = 1;
            foreach (var job in group
                .OrderByDescending(job => job.TimestampDay ?? int.MinValue)
                .ThenByDescending(job => job.Profit)
                .ThenBy(job => job.Id, StringComparer.OrdinalIgnoreCase)
                .Take(4))
            {
                await ExecuteAsync(
                    connection,
                    """
                    insert into gold_driver_recent_jobs (
                        company_id, driver_id, job_id, truck_id, cargo, origin_city, destination_city,
                        revenue, expenses, profit, distance, timestamp_day, rank
                    )
                    values (
                        $company_id, $driver_id, $job_id, $truck_id, $cargo, $origin_city, $destination_city,
                        $revenue, $expenses, $profit, $distance, $timestamp_day, $rank
                    )
                    """,
                    cancellationToken,
                    ("$company_id", company.Id),
                    ("$driver_id", job.DriverId),
                    ("$job_id", job.Id),
                    ("$truck_id", job.TruckId),
                    ("$cargo", job.Cargo),
                    ("$origin_city", job.SourceCity),
                    ("$destination_city", job.TargetCity),
                    ("$revenue", job.Revenue),
                    ("$expenses", job.Expenses),
                    ("$profit", job.Profit),
                    ("$distance", job.Distance),
                    ("$timestamp_day", job.TimestampDay),
                    ("$rank", recentJobRank++));
            }
        }

        foreach (var group in company.Missions
            .Where(mission =>
                !string.IsNullOrWhiteSpace(mission.DriverId) &&
                !string.IsNullOrWhiteSpace(mission.SourceCity) &&
                !string.IsNullOrWhiteSpace(mission.TargetCity))
            .GroupBy(mission => BuildRoutePairKey(mission.DriverId!, mission.SourceCity!, mission.TargetCity!)))
        {
            await ExecuteAsync(
                connection,
                """
                insert into gold_driver_job_pairs (
                    company_id, driver_id, endpoint_a, endpoint_b, route_pair, mission_count, profit
                )
                values (
                    $company_id, $driver_id, $endpoint_a, $endpoint_b, $route_pair, $mission_count, $profit
                )
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$driver_id", group.Key.DriverId),
                ("$endpoint_a", group.Key.EndpointA),
                ("$endpoint_b", group.Key.EndpointB),
                ("$route_pair", $"{group.Key.EndpointADisplay} <-> {group.Key.EndpointBDisplay}"),
                ("$mission_count", group.Count()),
                ("$profit", group.Sum(mission => mission.Profit)));
        }

        foreach (var driver in company.Drivers)
        {
            await ExecuteAsync(
                connection,
                """
                insert into gold_driver_deadhead_summary (company_id, driver_id, inferred_deadhead_count)
                values ($company_id, $driver_id, $inferred_deadhead_count)
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$driver_id", driver.Id),
                ("$inferred_deadhead_count", CountInferredDeadheads(company, driver)));
        }

        foreach (var city in company.Cities)
        {
            await ExecuteAsync(
                connection,
                """
                insert into gold_city_profitability (
                    company_id, city_id, display_name, has_owned_garage, is_garage_eligible,
                    visit_count, outbound_profit, inbound_profit, bidirectional_profit, expansion_score
                )
                values (
                    $company_id, $city_id, $display_name, $has_owned_garage, $is_garage_eligible,
                    $visit_count, $outbound_profit, $inbound_profit, $bidirectional_profit, $expansion_score
                )
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$city_id", city.Id),
                ("$display_name", city.DisplayName),
                ("$has_owned_garage", city.HasOwnedGarage ? 1 : 0),
                ("$is_garage_eligible", city.IsGarageEligible ? 1 : 0),
                ("$visit_count", city.VisitCount),
                ("$outbound_profit", city.OutboundProfit),
                ("$inbound_profit", city.InboundProfit),
                ("$bidirectional_profit", city.BidirectionalProfit),
                ("$expansion_score", city.ExpansionScore));
        }

        foreach (var route in company.Routes)
        {
            await ExecuteAsync(
                connection,
                """
                insert into gold_route_profitability (
                    company_id, origin_city_id, destination_city_id, profit, job_count, profit_per_mile, return_coverage_ratio
                )
                values (
                    $company_id, $origin_city_id, $destination_city_id, $profit, $job_count, $profit_per_mile, $return_coverage_ratio
                )
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$origin_city_id", route.OriginCityId),
                ("$destination_city_id", route.DestinationCityId),
                ("$profit", route.Profit),
                ("$job_count", route.JobCount),
                ("$profit_per_mile", route.ProfitPerMile),
                ("$return_coverage_ratio", route.ReturnCoverageRatio));
        }

        foreach (var trend in company.ProfitTrends)
        {
            await ExecuteAsync(
                connection,
                """
                insert into gold_profit_trends (company_id, entity_kind, entity_id, game_day, profit, sample_count)
                values ($company_id, $entity_kind, $entity_id, $game_day, $profit, $sample_count)
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$entity_kind", trend.EntityKind),
                ("$entity_id", trend.EntityId),
                ("$game_day", trend.GameDay),
                ("$profit", trend.Profit),
                ("$sample_count", trend.SampleCount));
        }

        foreach (var assignment in company.DriverTruckAssignments)
        {
            await ExecuteAsync(
                connection,
                """
                insert into silver_driver_truck_assignments (company_id, driver_id, truck_id, effective_from_save_name, effective_to_save_name, is_current)
                values ($company_id, $driver_id, $truck_id, $effective_from_save_name, $effective_to_save_name, $is_current)
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$driver_id", assignment.DriverId),
                ("$truck_id", assignment.TruckId),
                ("$effective_from_save_name", assignment.EffectiveFromSaveName),
                ("$effective_to_save_name", assignment.EffectiveToSaveName),
                ("$is_current", assignment.IsCurrent ? 1 : 0));
        }

        foreach (var assignment in company.DriverGarageAssignments)
        {
            await ExecuteAsync(
                connection,
                """
                insert into silver_driver_garage_assignments (company_id, driver_id, garage_id, effective_from_save_name, effective_to_save_name, is_current)
                values ($company_id, $driver_id, $garage_id, $effective_from_save_name, $effective_to_save_name, $is_current)
                """,
                cancellationToken,
                ("$company_id", company.Id),
                ("$driver_id", assignment.DriverId),
                ("$garage_id", assignment.GarageId),
                ("$effective_from_save_name", assignment.EffectiveFromSaveName),
                ("$effective_to_save_name", assignment.EffectiveToSaveName),
                ("$is_current", assignment.IsCurrent ? 1 : 0));
        }
    }

    private static RoutePairKey BuildRoutePairKey(string driverId, string sourceCity, string targetCity)
    {
        var endpoints = new[] { sourceCity, targetCity }
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new RoutePairKey(
            driverId,
            endpoints[0],
            endpoints[1],
            FormatRouteEndpoint(endpoints[0]),
            FormatRouteEndpoint(endpoints[1]));
    }

    private static string FormatRouteEndpoint(string value) =>
        string.Join(' ', value
            .Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    private static int CountInferredDeadheads(CompanyStatistics company, DriverStatistic driver)
    {
        if (string.IsNullOrWhiteSpace(driver.GarageId))
        {
            return 0;
        }

        var homeGarage = company.Garages.FirstOrDefault(garage =>
            StringComparer.OrdinalIgnoreCase.Equals(garage.Id, driver.GarageId));
        if (homeGarage is null)
        {
            return 0;
        }

        var homeArea = homeGarage.DisplayName;
        var driverMissions = company.Missions
            .Where(mission => StringComparer.OrdinalIgnoreCase.Equals(mission.DriverId, driver.Id))
            .Where(mission =>
                !string.IsNullOrWhiteSpace(mission.SourceCity) &&
                !string.IsNullOrWhiteSpace(mission.TargetCity) &&
                mission.Profit > 0)
            .ToList();

        if (driverMissions.Count < 2)
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < driverMissions.Count; i++)
        {
            var mission = driverMissions[i];
            if (SameArea(mission.TargetCity, homeArea))
            {
                continue;
            }

            var laterMissions = driverMissions.Skip(i + 1).ToList();
            if (!laterMissions.Any(candidate => SameArea(candidate.SourceCity, homeArea)))
            {
                continue;
            }

            if (!laterMissions.Any(candidate => SameArea(candidate.SourceCity, mission.TargetCity)))
            {
                count++;
            }
        }

        return count;
    }

    private static bool SameArea(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        StringComparer.OrdinalIgnoreCase.Equals(left, right);

    private static async Task<AtsStatistics> ReadGoldStatisticsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var companyRows = new List<(string Id, string DisplayName, DateTimeOffset LastUpdated)>();
        var companies = new List<CompanyStatistics>();
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select company_id, display_name, last_updated_utc
                from gold_company_summary
                order by garage_profit desc, display_name
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                companyRows.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    DateTimeOffset.Parse(reader.GetString(2))));
            }
        }

        foreach (var companyRow in companyRows)
        {
            companies.Add(new CompanyStatistics(
                companyRow.Id,
                companyRow.DisplayName,
                companyRow.LastUpdated,
                await ReadGaragesAsync(connection, companyRow.Id, cancellationToken),
                await ReadDriversAsync(connection, companyRow.Id, cancellationToken),
                await ReadTrucksAsync(connection, companyRow.Id, cancellationToken),
                await ReadMissionsAsync(connection, companyRow.Id, cancellationToken),
                await ReadTrailerTypesAsync(connection, companyRow.Id, cancellationToken),
                await ReadRecentDriverJobsAsync(connection, companyRow.Id, cancellationToken),
                await ReadTrailersAsync(connection, companyRow.Id, cancellationToken),
                await ReadCitiesAsync(connection, companyRow.Id, cancellationToken),
                await ReadRoutesAsync(connection, companyRow.Id, cancellationToken),
                await ReadProfitTrendsAsync(connection, companyRow.Id, cancellationToken),
                await ReadDriverTruckAssignmentsAsync(connection, companyRow.Id, cancellationToken),
                await ReadDriverGarageAssignmentsAsync(connection, companyRow.Id, cancellationToken)));
        }

        return new AtsStatistics(
            companies.Count == 0 ? null : companies.Max(company => company.LastUpdated),
            companies);
    }

    private static async Task<IReadOnlyList<GarageStatistic>> ReadGaragesAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<GarageStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select garage_id, display_name, profit, employee_count, truck_count
            from gold_garage_ranking
            where company_id = $company_id
            order by rank
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new GarageStatistic(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt32(3), reader.GetInt32(4)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<DriverStatistic>> ReadDriversAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<DriverStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select driver_id, display_name, profit, garage_id, truck_id
            from silver_drivers
            where company_id = $company_id
            order by profit desc, display_name
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new DriverStatistic(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                GetNullableString(reader, 3),
                GetNullableString(reader, 4)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<TruckStatistic>> ReadTrucksAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<TruckStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select truck_id, display_name, profit, garage_id, driver_id, license_plate, model_name, definition_path
            from silver_trucks
            where company_id = $company_id
            order by profit desc, display_name
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new TruckStatistic(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                GetNullableString(reader, 3),
                GetNullableString(reader, 4),
                GetNullableString(reader, 5),
                GetNullableString(reader, 6),
                GetNullableString(reader, 7)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<MissionStatistic>> ReadMissionsAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<MissionStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select job_id, driver_id, truck_id, trailer_type, cargo, origin_city, destination_city, profit, timestamp_day, trailer_id, garage_id
            from gold_job_details
            where company_id = $company_id
            order by profit desc, job_id
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new MissionStatistic(
                reader.GetString(0),
                GetNullableString(reader, 1),
                GetNullableString(reader, 2),
                TrailerId: GetNullableString(reader, 9),
                GetNullableString(reader, 3),
                GetNullableString(reader, 4),
                GetNullableString(reader, 5),
                GetNullableString(reader, 6),
                reader.GetInt64(7),
                GetNullableInt(reader, 8),
                GarageId: GetNullableString(reader, 10)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<DriverRecentJobStatistic>> ReadRecentDriverJobsAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<DriverRecentJobStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select job_id, driver_id, truck_id, cargo, origin_city, destination_city,
                   revenue, expenses, profit, distance, timestamp_day
            from gold_driver_recent_jobs
            where company_id = $company_id
            order by driver_id, rank
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new DriverRecentJobStatistic(
                reader.GetString(0),
                reader.GetString(1),
                GetNullableString(reader, 2),
                GetNullableString(reader, 3),
                GetNullableString(reader, 4),
                GetNullableString(reader, 5),
                reader.GetInt64(6),
                reader.GetInt64(7),
                reader.GetInt64(8),
                GetNullableInt(reader, 9),
                GetNullableInt(reader, 10)));
        }

        return values;
    }

    private static async Task<string?> ReadSilverDriverDisplayNameAsync(
        SqliteConnection connection,
        string companyId,
        string driverId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select display_name
            from silver_drivers
            where company_id = $company_id
              and driver_id = $driver_id
            """;
        Add(command, "$company_id", companyId);
        Add(command, "$driver_id", driverId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static async Task<IReadOnlyList<TrailerTypeStatistic>> ReadTrailerTypesAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<TrailerTypeStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select trailer_type, profit, mission_count
            from silver_trailer_types
            where company_id = $company_id
            order by profit desc, trailer_type
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new TrailerTypeStatistic(reader.GetString(0), reader.GetInt64(1), reader.GetInt32(2)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<TrailerStatistic>> ReadTrailersAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<TrailerStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select trailer_id, trailer_type, profit, job_count, body_type, is_articulated, garage_id
            from silver_trailers
            where company_id = $company_id
            order by profit desc, trailer_id
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new TrailerStatistic(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt32(3),
                IsArticulated: reader.IsDBNull(5) ? false : reader.GetInt32(5) != 0,
                BodyType: GetNullableString(reader, 4),
                GarageId: GetNullableString(reader, 6)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<CityStatistic>> ReadCitiesAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<CityStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select city_id, display_name, has_owned_garage, is_garage_eligible,
                   visit_count, outbound_profit, inbound_profit, bidirectional_profit, expansion_score
            from gold_city_profitability
            where company_id = $company_id
            order by has_owned_garage desc, city_id
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new CityStatistic(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2) != 0,
                reader.GetInt32(3) != 0,
                reader.GetInt32(4),
                reader.GetInt64(5),
                reader.GetInt64(6),
                reader.GetInt64(7),
                reader.GetDecimal(8)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<RouteStatistic>> ReadRoutesAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<RouteStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select origin_city_id, destination_city_id, profit, job_count, profit_per_mile, return_coverage_ratio
            from gold_route_profitability
            where company_id = $company_id
            order by origin_city_id, destination_city_id
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new RouteStatistic(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt32(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<TrendPointStatistic>> ReadProfitTrendsAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<TrendPointStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select entity_kind, entity_id, game_day, profit, sample_count
            from gold_profit_trends
            where company_id = $company_id
            order by entity_kind, entity_id, game_day
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new TrendPointStatistic(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt64(3),
                reader.GetInt32(4)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<DriverTruckAssignmentStatistic>> ReadDriverTruckAssignmentsAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<DriverTruckAssignmentStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select driver_id, truck_id, effective_from_save_name, effective_to_save_name, is_current
            from silver_driver_truck_assignments
            where company_id = $company_id
            order by driver_id, is_current, effective_from_save_name
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new DriverTruckAssignmentStatistic(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                GetNullableString(reader, 3),
                reader.GetInt32(4) != 0));
        }

        return values;
    }

    private static async Task<IReadOnlyList<DriverGarageAssignmentStatistic>> ReadDriverGarageAssignmentsAsync(
        SqliteConnection connection,
        string companyId,
        CancellationToken cancellationToken)
    {
        var values = new List<DriverGarageAssignmentStatistic>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select driver_id, garage_id, effective_from_save_name, effective_to_save_name, is_current
            from silver_driver_garage_assignments
            where company_id = $company_id
            order by driver_id, is_current, effective_from_save_name
            """;
        Add(command, "$company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new DriverGarageAssignmentStatistic(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                GetNullableString(reader, 3),
                reader.GetInt32(4) != 0));
        }

        return values;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        foreach (var parameter in parameters)
        {
            Add(command, parameter.Name, parameter.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? GetNullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static int? GetNullableInt(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static void Add(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static string BuildSaveId(string path)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetProfileSegment(string path)
    {
        var parts = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var saveIndex = Array.FindIndex(parts, part => StringComparer.OrdinalIgnoreCase.Equals(part, "save"));
        return saveIndex > 0 ? parts[saveIndex - 1] : string.Empty;
    }

    private static string GetSaveSlot(string path)
    {
        var parts = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var saveIndex = Array.FindIndex(parts, part => StringComparer.OrdinalIgnoreCase.Equals(part, "save"));
        return saveIndex >= 0 && saveIndex + 1 < parts.Length ? parts[saveIndex + 1] : string.Empty;
    }

    private static bool IsBackupPath(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));

    private static string FormatUtc(DateTime value) =>
        value.ToUniversalTime().ToString("O");

    private sealed class InlineProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }

    private sealed record SavePath(string Path, DateTime LastWriteTimeUtc, string ProfileSegment, string SaveSlot);

    private sealed record RoutePairKey(
        string DriverId,
        string EndpointA,
        string EndpointB,
        string EndpointADisplay,
        string EndpointBDisplay);

    private sealed record FileMetadata(
        string SaveId,
        string FullPath,
        string ProfileSegment,
        string SaveSlot,
        DateTime LastWriteTimeUtc,
        long FileSize,
        string? ContentHash);
}
