using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Infrastructure.Saves;
using Microsoft.Data.Sqlite;

namespace AtsEmployeeStats.Tests;

public sealed class SqliteMedallionSaveSnapshotSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ats-stats-sqlite-{Guid.NewGuid():N}");
    private readonly string _dbPath;

    public SqliteMedallionSaveSnapshotSourceTests()
    {
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "warehouse.db");
    }

    [Fact]
    public async Task ReadAllAsync_reuses_unchanged_bronze_units_without_reparsing()
    {
        await WriteSaveAsync("autosave", "Desert Line", extraUnits: 2);
        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);

        await source.ReadAllAsync(CancellationToken.None);

        var progressEvents = new List<SaveLoadProgress>();
        var cached = await source.ReadAllAsync(CancellationToken.None, new CapturingProgress(progressEvents));

        Assert.Single(cached);
        Assert.Equal(3, cached[0].Document.Units.Count);
        Assert.DoesNotContain(progressEvents, update => update.Stage == SaveLoadStage.LoadingFiles);
    }

    [Fact]
    public async Task ReadAllAsync_replays_unchanged_bronze_units_without_reopening_save_content()
    {
        var savePath = await WriteSaveAsync("autosave", "Desert Line", extraUnits: 2);
        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
        await source.ReadAllAsync(CancellationToken.None);

        using var exclusiveLock = new FileStream(
            savePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        var cached = await source.ReadAllAsync(CancellationToken.None);

        var snapshot = Assert.Single(cached);
        Assert.Equal(3, snapshot.Document.Units.Count);
    }

    [Fact]
    public async Task ReadAllAsync_reingests_changed_files_and_replaces_old_bronze_units()
    {
        var savePath = await WriteSaveAsync("autosave", "Desert Line", extraUnits: 2);
        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
        await source.ReadAllAsync(CancellationToken.None);

        await File.WriteAllTextAsync(savePath, """
            SiiNunit
            {
            player : player {
              company_name: "Copper Line"
            }
            }
            """);
        File.SetLastWriteTimeUtc(savePath, DateTime.UtcNow.AddMinutes(1));

        var snapshots = await source.ReadAllAsync(CancellationToken.None);

        var snapshot = Assert.Single(snapshots);
        Assert.Single(snapshot.Document.Units);
        Assert.Equal("Copper Line", snapshot.Document.Units[0].GetValue("company_name"));

        using var connection = OpenTestConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from bronze_sii_units";
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync() ?? 0L));
    }

    [Fact]
    public async Task ReadAllAsync_includes_all_save_slots_and_excludes_backup_locations()
    {
        await WriteSaveAsync("manual_save", "Manual Line");
        await WriteSaveAsync("autosave", "Auto Line");
        await WriteSaveAsync("autosave_job_1", "Job Line");
        await WriteSaveAsync("multiplayer_backup_1", "Backup Line");
        await WriteSaveAsync("autosave", "Bak Profile Line", profileSegment: "506C61796572.bak");

        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);

        var snapshots = await source.ReadAllAsync(CancellationToken.None);

        Assert.Equal(3, snapshots.Count);
        Assert.Contains(snapshots, s => s.Name.Contains("manual_save", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(snapshots, s => s.Name.Contains($"{Path.DirectorySeparatorChar}autosave{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(snapshots, s => s.Name.Contains("autosave_job_1", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(snapshots, s => s.Name.Contains("multiplayer_backup", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(snapshots, s => s.Name.Contains(".bak", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAllAsync_persists_bronze_units_with_scalar_and_array_json()
    {
        await WriteSaveAsync("autosave", "Desert Line", includeGarage: true);
        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);

        await source.ReadAllAsync(CancellationToken.None);

        using var connection = OpenTestConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = """
            select unit_type, unit_id, scalar_values_json, array_values_json
            from bronze_sii_units
            where unit_type = 'garage'
            """;

        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("garage", reader.GetString(0));
        Assert.Equal("garage.phoenix", reader.GetString(1));
        Assert.Contains("\"city\":\"phoenix\"", reader.GetString(2));
        Assert.Contains("\"employees\":[\"driver.alice\"]", reader.GetString(3));
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task ReadAllAsync_records_bronze_status_and_error_message_for_unparseable_saves()
    {
        await WriteRawSaveAsync("autosave", "not a plain SII save");
        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);

        var snapshots = await source.ReadAllAsync(CancellationToken.None);

        Assert.Empty(snapshots);

        using var connection = OpenTestConnection();
        await connection.OpenAsync();

        Assert.Equal(
            ("skipped", "File did not contain plain SII content."),
            await QuerySingleAsync<(string, string)>(
                connection,
                "select parse_status, error_message from bronze_save_files",
                reader => (reader.GetString(0), reader.GetString(1))));
    }

    [Fact]
    public async Task ReadAllAsync_persists_all_bronze_save_file_metadata()
    {
        var savePath = await WriteSaveAsync("autosave", "Desert Line");
        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);

        await source.ReadAllAsync(CancellationToken.None);

        using var connection = OpenTestConnection();
        await connection.OpenAsync();

        var metadata = await QuerySingleAsync<(string SaveId, string FullPath, string ProfileId, string Slot, long Size, string Hash, string Status)>(
            connection,
            """
            select save_id, full_path, profile_id, save_slot_name, file_size, content_hash, parse_status
            from bronze_save_files
            """,
            reader => (
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetString(5),
                reader.GetString(6)));

        Assert.False(string.IsNullOrWhiteSpace(metadata.SaveId));
        Assert.Equal(savePath, metadata.FullPath);
        Assert.Equal("506C61796572", metadata.ProfileId);
        Assert.Equal("autosave", metadata.Slot);
        Assert.Equal(new FileInfo(savePath).Length, metadata.Size);
        Assert.Matches("^[a-f0-9]{64}$", metadata.Hash);
        Assert.Equal("parsed", metadata.Status);
    }

    [Fact]
    public async Task StatisticsService_persists_silver_driver_names_truck_assignments_and_gold_drilldown_models()
    {
        await WriteAnalyticSaveAsync();
        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
        var service = new StatisticsService(source);

        await service.IngestAsync(CancellationToken.None);
        var statistics = await service.LoadAsync(CancellationToken.None);

        var company = Assert.Single(statistics.Companies);
        Assert.Equal("Desert Line", company.DisplayName);

        using var connection = OpenTestConnection();
        await connection.OpenAsync();

        Assert.Equal(
            ("Alice Ramirez", "garage.phoenix", "truck.alice"),
            await QuerySingleAsync<(string, string, string)>(
                connection,
                "select display_name, garage_id, truck_id from silver_drivers where driver_id = 'driver.alice'",
                reader => (reader.GetString(0), reader.GetString(1), reader.GetString(2))));
        Assert.Equal(
            ("garage.phoenix", "phoenix", 2400L),
            await QuerySingleAsync<(string, string, long)>(
                connection,
                "select garage_id, display_name, profit from gold_garage_ranking",
                reader => (reader.GetString(0), reader.GetString(1), reader.GetInt64(2))));
        Assert.Equal(
            ("driver.alice", "trailer_def.scs.box.reefer", 1),
            await QuerySingleAsync<(string, string, int)>(
                connection,
                "select driver_id, job_type, mission_count from gold_driver_job_types",
                reader => (reader.GetString(0), reader.GetString(1), reader.GetInt32(2))));
        Assert.Equal(
            ("phoenix", "denver", "cargo.medicine", "truck.alice", 2400L),
            await QuerySingleAsync<(string, string, string, string, long)>(
                connection,
                "select origin_city, destination_city, cargo, truck_id, profit from gold_job_details",
                reader => (reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4))));
    }

    [Fact]
    public async Task StatisticsService_persists_enriched_truck_metadata_and_recent_driver_jobs()
    {
        await WriteEnrichedAnalyticSaveAsync();
        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
        var service = new StatisticsService(source);

        await service.IngestAsync(CancellationToken.None);
        var statistics = await service.LoadAsync(CancellationToken.None);

        var company = Assert.Single(statistics.Companies);
        var truck = Assert.Single(company.Trucks);
        Assert.Equal("Kenworth T680 - PA76356 Montana", truck.DisplayName);
        Assert.Equal("PA76356 Montana", truck.LicensePlate);
        Assert.Equal("Kenworth T680", truck.ModelName);

        Assert.Collection(
            company.RecentDriverJobs,
            job =>
            {
                Assert.Equal("entry.new", job.Id);
                Assert.Equal("driver.alice", job.DriverId);
                Assert.Equal(178, job.TimestampDay);
            },
            job =>
            {
                Assert.Equal("entry.old", job.Id);
                Assert.Equal(177, job.TimestampDay);
            });

        using var connection = OpenTestConnection();
        await connection.OpenAsync();

        Assert.Equal(
            ("Kenworth T680 - PA76356 Montana", "PA76356 Montana", "Kenworth T680", "/def/vehicle/truck/kenworth.t680/data.sii"),
            await QuerySingleAsync<(string, string, string, string)>(
                connection,
                "select display_name, license_plate, model_name, definition_path from silver_trucks where truck_id = 'truck.alice'",
                reader => (reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3))));

        Assert.Equal(
            ("entry.new", "driver.alice", 2000L, 178),
            await QuerySingleAsync<(string, string, long, int)>(
                connection,
                """
                select job_id, driver_id, profit, timestamp_day
                from gold_driver_recent_jobs
                where driver_id = 'driver.alice'
                order by timestamp_day desc
                limit 1
                """,
                reader => (reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt32(3))));

        using var nullCommand = connection.CreateCommand();
        nullCommand.CommandText = """
            select count(*)
            from silver_drivers
            where truck_id = 'null'
            """;
        Assert.Equal(0L, (long)(await nullCommand.ExecuteScalarAsync() ?? 0L));
    }

    [Fact]
    public async Task StatisticsService_persists_inferred_deadhead_count_when_driver_returns_to_home_without_paid_job_from_destination()
    {
        await WriteDeadheadRouteHistoryAsync();
        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
        var service = new StatisticsService(source);

        await service.IngestAsync(CancellationToken.None);
        await service.LoadAsync(CancellationToken.None);

        using var connection = OpenTestConnection();
        await connection.OpenAsync();

        Assert.Equal(
            ("driver.alice", 1),
            await QuerySingleAsync<(string, int)>(
                connection,
                "select driver_id, inferred_deadhead_count from gold_driver_deadhead_summary where driver_id = 'driver.alice'",
                reader => (reader.GetString(0), reader.GetInt32(1))));
    }

    [Fact]
    public async Task StatisticsService_persists_driver_job_pairs_by_combining_both_route_directions()
    {
        await WriteRoutePairHistoryAsync();
        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
        var service = new StatisticsService(source);

        await service.IngestAsync(CancellationToken.None);
        await service.LoadAsync(CancellationToken.None);

        using var connection = OpenTestConnection();
        await connection.OpenAsync();

        Assert.Equal(
            ("driver.alice", "denver", "phoenix", "Denver <-> Phoenix", 2, 5500L),
            await QuerySingleAsync<(string, string, string, string, int, long)>(
                connection,
                """
                select driver_id, endpoint_a, endpoint_b, route_pair, mission_count, profit
                from gold_driver_job_pairs
                where driver_id = 'driver.alice'
                """,
                reader => (
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetInt64(5))));
    }

    [Fact]
    public async Task StatisticsService_persists_city_route_trailer_and_trend_read_models()
    {
        await WriteCityRouteTrailerAnalyticsAsync();
        var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
        var service = new StatisticsService(source);

        await service.IngestAsync(CancellationToken.None);
        var statistics = await service.LoadAsync(CancellationToken.None);

        var company = Assert.Single(statistics.Companies);
        Assert.Contains(company.Cities, city => city.Id == "phoenix" && city.HasOwnedGarage && city.BidirectionalProfit == 5500);
        Assert.Contains(company.Cities, city => city.Id == "denver" && city.IsGarageEligible && city.ExpansionScore > 0);
        Assert.Contains(company.Routes, route => route.OriginCityId == "phoenix" && route.DestinationCityId == "denver" && route.Profit == 3000);
        Assert.Contains(company.Trailers, trailer => trailer.Id == "trailer.reefer.1" && trailer.Profit == 5500 && trailer.JobCount == 2);
        Assert.Contains(company.ProfitTrends, point => point.EntityKind == "company" && point.GameDay == 200 && point.Profit == 3000);

        using var connection = OpenTestConnection();
        await connection.OpenAsync();

        Assert.Equal(
            ("phoenix", 1, 1, 2, 3000L, 2500L, 5500L),
            await QuerySingleAsync<(string, int, int, int, long, long, long)>(
                connection,
                """
                select city_id, has_owned_garage, is_garage_eligible, visit_count, outbound_profit, inbound_profit, bidirectional_profit
                from silver_cities
                where city_id = 'phoenix'
                """,
                reader => (
                    reader.GetString(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3),
                    reader.GetInt64(4),
                    reader.GetInt64(5),
                    reader.GetInt64(6))));

        Assert.Equal(
            ("phoenix", "denver", 3000L, 1),
            await QuerySingleAsync<(string, string, long, int)>(
                connection,
                """
                select origin_city_id, destination_city_id, profit, job_count
                from gold_route_profitability
                where origin_city_id = 'phoenix'
                """,
                reader => (reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt32(3))));

        Assert.Equal(
            ("trailer.reefer.1", "trailer_def.scs.box.reefer", 5500L, 2),
            await QuerySingleAsync<(string, string, long, int)>(
                connection,
                """
                select trailer_id, trailer_type, profit, job_count
                from silver_trailers
                """,
                reader => (reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt32(3))));

        Assert.Equal(
            ("company", "desert-line", 200, 3000L, 1),
            await QuerySingleAsync<(string, string, int, long, int)>(
                connection,
                """
                select entity_kind, entity_id, game_day, profit, sample_count
                from gold_profit_trends
                where entity_kind = 'company' and game_day = 200
                """,
                reader => (reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt64(3), reader.GetInt32(4))));
    }

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

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<string> WriteSaveAsync(
        string slotName,
        string companyName,
        int extraUnits = 0,
        bool includeGarage = false,
        string profileSegment = "506C61796572")
    {
        var saveDirectory = Path.Combine(_root, "profiles", profileSegment, "save", slotName);
        Directory.CreateDirectory(saveDirectory);
        var savePath = Path.Combine(saveDirectory, "game.sii");
        var extraContent = string.Concat(Enumerable.Range(0, extraUnits).Select(index => $$"""

            dummy : dummy.{{index}} {
              value: {{index}}
            }
            """));
        var garageContent = includeGarage ? """

            garage : garage.phoenix {
              city: phoenix
              employees[0]: driver.alice
            }
            """ : string.Empty;

        await File.WriteAllTextAsync(savePath, $$"""
            SiiNunit
            {
            player : player {
              company_name: "{{companyName}}"
            }
            {{garageContent}}
            {{extraContent}}
            }
            """);

        await Task.Delay(5);
        File.SetLastWriteTimeUtc(savePath, DateTime.UtcNow);
        return savePath;
    }

    private async Task<string> WriteRawSaveAsync(string slotName, string content)
    {
        var saveDirectory = Path.Combine(_root, "profiles", "506C61796572", "save", slotName);
        Directory.CreateDirectory(saveDirectory);
        var savePath = Path.Combine(saveDirectory, "game.sii");
        await File.WriteAllTextAsync(savePath, content);
        File.SetLastWriteTimeUtc(savePath, DateTime.UtcNow);
        return savePath;
    }

    private async Task WriteAnalyticSaveAsync()
    {
        var saveDirectory = Path.Combine(_root, "profiles", "506C61796572", "save", "autosave");
        Directory.CreateDirectory(saveDirectory);
        var savePath = Path.Combine(saveDirectory, "game.sii");
        await File.WriteAllTextAsync(savePath, """
            SiiNunit
            {
            player : player {
              company_name: "Desert Line"
            }

            garage : garage.phoenix {
              city: phoenix
              employees[0]: driver.alice
              vehicles[0]: truck.alice
              profit_log[0]: 2400
            }

            driver : driver.alice {
              name: "Alice Ramirez"
              assigned_truck: truck.alice
              profit_log[0]: 2400
            }

            vehicle : truck.alice {
              license_plate: "ATS-100"
              profit_log[0]: 2400
            }

            job : _nameless.job.1 {
              driver: driver.alice
              truck: truck.alice
              trailer: trailer.reefer.1
              cargo: cargo.medicine
              income: 2400
              source_city: phoenix
              target_city: denver
            }

            trailer : trailer.reefer.1 {
              trailer_definition: trailer_def.scs.box.reefer
            }
            }
            """);
        File.SetLastWriteTimeUtc(savePath, DateTime.UtcNow);
    }

    private async Task WriteEnrichedAnalyticSaveAsync()
    {
        var saveDirectory = Path.Combine(_root, "profiles", "506C61796572", "save", "autosave");
        Directory.CreateDirectory(saveDirectory);
        var savePath = Path.Combine(saveDirectory, "game.sii");
        await File.WriteAllTextAsync(savePath, """
            SiiNunit
            {
            player : player {
              company_name: "Desert Line"
            }

            garage : garage.phoenix {
              city: phoenix
              drivers: 1
              drivers[0]: driver.alice
              vehicles: 1
              vehicles[0]: truck.alice
            }

            driver_ai : driver.alice {
              assigned_truck: null
              profit_log: log.driver
            }

            profit_log : log.driver {
              stats_data: 2
              stats_data[0]: entry.old
              stats_data[1]: entry.new
            }

            profit_log_entry : entry.old {
              revenue: 1200
              wage: 200
              maintenance: 50
              fuel: 25
              distance: 300
              cargo: cargo.apples
              source_city: phoenix
              destination_city: tucson
              timestamp_day: 177
            }

            profit_log_entry : entry.new {
              revenue: 2400
              wage: 300
              maintenance: 75
              fuel: 25
              distance: 450
              cargo: cargo.medicine
              source_city: tucson
              destination_city: denver
              timestamp_day: 178
            }

            vehicle : truck.alice {
              license_plate: "<color value=FF000000> PA76356|montana"
              accessories: 1
              accessories[0]: accessory.base
            }

            vehicle_accessory : accessory.base {
              data_path: "/def/vehicle/truck/kenworth.t680/data.sii"
            }
            }
            """);
        File.SetLastWriteTimeUtc(savePath, DateTime.UtcNow);
    }

    private async Task WriteDeadheadRouteHistoryAsync()
    {
        var saveDirectory = Path.Combine(_root, "profiles", "506C61796572", "save", "autosave");
        Directory.CreateDirectory(saveDirectory);
        var savePath = Path.Combine(saveDirectory, "game.sii");
        await File.WriteAllTextAsync(savePath, """
            SiiNunit
            {
            player : player {
              company_name: "Desert Line"
            }

            garage : garage.phoenix {
              city: phoenix
              employees[0]: driver.alice
              vehicles[0]: truck.alice
            }

            driver : driver.alice {
              name: "Alice Ramirez"
              assigned_truck: truck.alice
            }

            vehicle : truck.alice {
              license_plate: "ATS-100"
            }

            job : _nameless.job.outbound {
              driver: driver.alice
              truck: truck.alice
              cargo: cargo.medicine
              income: 2400
              source_city: phoenix
              target_city: denver
            }

            job : _nameless.job.after-return {
              driver: driver.alice
              truck: truck.alice
              cargo: cargo.apples
              income: 1400
              source_city: phoenix
              target_city: tucson
            }
            }
            """);
        File.SetLastWriteTimeUtc(savePath, DateTime.UtcNow);
    }

    private async Task WriteRoutePairHistoryAsync()
    {
        var saveDirectory = Path.Combine(_root, "profiles", "506C61796572", "save", "autosave");
        Directory.CreateDirectory(saveDirectory);
        var savePath = Path.Combine(saveDirectory, "game.sii");
        await File.WriteAllTextAsync(savePath, """
            SiiNunit
            {
            player : player {
              company_name: "Desert Line"
            }

            garage : garage.phoenix {
              city: phoenix
              employees[0]: driver.alice
              vehicles[0]: truck.alice
            }

            driver : driver.alice {
              name: "Alice Ramirez"
              assigned_truck: truck.alice
            }

            vehicle : truck.alice {
              license_plate: "ATS-100"
            }

            job : _nameless.job.outbound {
              driver: driver.alice
              truck: truck.alice
              cargo: cargo.medicine
              income: 3000
              source_city: phoenix
              target_city: denver
            }

            job : _nameless.job.inbound {
              driver: driver.alice
              truck: truck.alice
              cargo: cargo.medicine
              income: 2500
              source_city: denver
              target_city: phoenix
            }
            }
            """);
        File.SetLastWriteTimeUtc(savePath, DateTime.UtcNow);
    }

    private async Task WriteCityRouteTrailerAnalyticsAsync()
    {
        var saveDirectory = Path.Combine(_root, "profiles", "506C61796572", "save", "autosave");
        Directory.CreateDirectory(saveDirectory);
        var savePath = Path.Combine(saveDirectory, "game.sii");
        await File.WriteAllTextAsync(savePath, """
            SiiNunit
            {
            player : player {
              company_name: "Desert Line"
            }

            garage : garage.phoenix {
              city: phoenix
              employees[0]: driver.alice
              vehicles[0]: truck.alice
            }

            garage : garage.denver {
              city: denver
              status: 0
            }

            driver : driver.alice {
              name: "Alice Ramirez"
              assigned_truck: truck.alice
            }

            vehicle : truck.alice {
              license_plate: "ATS-100"
            }

            trailer : trailer.reefer.1 {
              trailer_definition: trailer_def.scs.box.reefer
            }

            job : job.outbound {
              driver: driver.alice
              truck: truck.alice
              trailer: trailer.reefer.1
              cargo: cargo.medicine
              income: 3000
              source_city: phoenix
              target_city: denver
              timestamp_day: 200
            }

            job : job.return {
              driver: driver.alice
              truck: truck.alice
              trailer: trailer.reefer.1
              cargo: cargo.paper
              income: 2500
              source_city: denver
              target_city: phoenix
              timestamp_day: 201
            }
            }
            """);
        File.SetLastWriteTimeUtc(savePath, DateTime.UtcNow);
    }

    private static async Task<T> QuerySingleAsync<T>(
        SqliteConnection connection,
        string commandText,
        Func<SqliteDataReader, T> read)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var value = read(reader);
        Assert.False(await reader.ReadAsync());
        return value;
    }

    private sealed class CapturingProgress(List<SaveLoadProgress> events) : IProgress<SaveLoadProgress>
    {
        public void Report(SaveLoadProgress value) => events.Add(value);
    }

    private SqliteConnection OpenTestConnection() =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Pooling = false
        }.ToString());
}
