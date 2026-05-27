using System.IO.Compression;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Infrastructure.Saves;
using Microsoft.Data.Sqlite;

namespace AtsEmployeeStats.Tests;

public sealed class ScsReferenceDataIngestionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ats-stats-reference-{Guid.NewGuid():N}");
    private readonly string _dbPath;

    public ScsReferenceDataIngestionTests()
    {
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "warehouse.db");
    }

    [Fact]
    public async Task ScsExtractorBootstrapper_downloads_and_unzips_extractor_when_missing()
    {
        var cacheRoot = Path.Combine(_root, "cache");
        var downloader = new CapturingDownloader();
        await using (var archive = ZipFile.Open(downloader.ZipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("scs_extractor.exe");
            await using var stream = entry.Open();
            await stream.WriteAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);
        }

        var bootstrapper = new ScsExtractorBootstrapper(downloader);

        var extractorPath = await bootstrapper.EnsureExtractorAsync(cacheRoot, CancellationToken.None);

        Assert.Equal(ScsExtractorBootstrapper.DefaultDownloadUri, downloader.DownloadUri);
        Assert.Equal(Path.Combine(cacheRoot, "tools", "scs_extractor.exe"), extractorPath);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(extractorPath));
    }

    [Fact]
    public async Task StatisticsService_extracts_locale_scs_and_persists_driver_names_to_reference_bronze()
    {
        var saveRoot = await WriteSaveAsync();
        var gameRoot = Path.Combine(_root, "game");
        Directory.CreateDirectory(gameRoot);
        await File.WriteAllTextAsync(Path.Combine(gameRoot, "locale.scs"), "fake archive");
        var options = new AtsReferenceDataOptions(
            Enabled: true,
            GameInstallRoot: gameRoot,
            CacheRoot: Path.Combine(_root, "reference-cache"));
        var downloader = new ExistingExtractorDownloader();
        var extractor = new FakeArchiveExtractor();
        var source = new SqliteMedallionSaveSnapshotSource(saveRoot, _dbPath, referenceDataOptions: options, scsExtractorDownloader: downloader, scsArchiveExtractor: extractor);
        var service = new StatisticsService(source);

        await service.LoadAsync(CancellationToken.None);

        Assert.Equal(Path.Combine(gameRoot, "locale.scs"), extractor.ArchivePath);

        await using var connection = OpenTestConnection();
        await connection.OpenAsync();
        Assert.Equal(
            ("locale/en_us/driver_names.sii", "driver_name", "driver.23", "Alice"),
            await QuerySingleAsync<(string, string, string, string)>(
                connection,
                """
                select relative_path, unit_type, unit_id, json_extract(scalar_values_json, '$.name')
                from bronze_reference_sii_units
                """,
                reader => (reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3))));
    }

    [Fact]
    public async Task StatisticsService_uses_reference_driver_names_when_save_only_has_driver_id()
    {
        var saveRoot = await WriteDriverOnlySaveAsync();
        var gameRoot = Path.Combine(_root, "game");
        Directory.CreateDirectory(gameRoot);
        await File.WriteAllTextAsync(Path.Combine(gameRoot, "locale.scs"), "fake archive");
        var options = new AtsReferenceDataOptions(
            Enabled: true,
            GameInstallRoot: gameRoot,
            CacheRoot: Path.Combine(_root, "reference-cache"));
        var source = new SqliteMedallionSaveSnapshotSource(
            saveRoot,
            _dbPath,
            referenceDataOptions: options,
            scsExtractorDownloader: new ExistingExtractorDownloader(),
            scsArchiveExtractor: new FakeArchiveExtractor());
        var service = new StatisticsService(source);

        var statistics = await service.LoadAsync(CancellationToken.None);

        var driver = Assert.Single(Assert.Single(statistics.Companies).Drivers);
        Assert.Equal("Alice Ramirez", driver.DisplayName);
    }

    [Fact]
    public async Task StatisticsService_continues_when_locale_extractor_cannot_extract_archive()
    {
        var saveRoot = await WriteSaveAsync();
        var gameRoot = Path.Combine(_root, "game");
        Directory.CreateDirectory(gameRoot);
        await File.WriteAllTextAsync(Path.Combine(gameRoot, "locale.scs"), "fake archive");
        var options = new AtsReferenceDataOptions(
            Enabled: true,
            GameInstallRoot: gameRoot,
            CacheRoot: Path.Combine(_root, "reference-cache"));
        var source = new SqliteMedallionSaveSnapshotSource(
            saveRoot,
            _dbPath,
            referenceDataOptions: options,
            scsExtractorDownloader: new ExistingExtractorDownloader(),
            scsArchiveExtractor: new FailingArchiveExtractor());
        var service = new StatisticsService(source);

        var statistics = await service.LoadAsync(CancellationToken.None);

        Assert.Single(statistics.Companies);

        await using var connection = OpenTestConnection();
        await connection.OpenAsync();
        Assert.Equal(0L, await CountAsync(connection, "select count(*) from bronze_reference_sii_units"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<string> WriteSaveAsync()
    {
        var saveDirectory = Path.Combine(_root, "profiles", "506C61796572", "save", "autosave");
        Directory.CreateDirectory(saveDirectory);
        await File.WriteAllTextAsync(Path.Combine(saveDirectory, "game.sii"), """
            SiiNunit
            {
            player : player {
              company_name: "Desert Line"
            }
            }
            """);
        return _root;
    }

    private async Task<string> WriteDriverOnlySaveAsync()
    {
        var saveDirectory = Path.Combine(_root, "profiles", "506C61796572", "save", "autosave");
        Directory.CreateDirectory(saveDirectory);
        await File.WriteAllTextAsync(Path.Combine(saveDirectory, "game.sii"), """
            SiiNunit
            {
            player : player {
              company_name: "Desert Line"
            }

            garage : garage.phoenix {
              drivers[0]: driver.23
            }

            driver_ai : driver.23 {
              profit_log[0]: 1000
            }
            }
            """);
        return _root;
    }

    private SqliteConnection OpenTestConnection() =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Pooling = false
        }.ToString());

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

    private static async Task<long> CountAsync(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private sealed class CapturingDownloader : IScsExtractorDownloader
    {
        public string ZipPath { get; } = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");
        public Uri? DownloadUri { get; private set; }

        public Task DownloadAsync(Uri downloadUri, string destinationPath, CancellationToken cancellationToken)
        {
            DownloadUri = downloadUri;
            File.Copy(ZipPath, destinationPath, overwrite: true);
            return Task.CompletedTask;
        }
    }

    private sealed class ExistingExtractorDownloader : IScsExtractorDownloader
    {
        public async Task DownloadAsync(Uri downloadUri, string destinationPath, CancellationToken cancellationToken)
        {
            await using var archiveStream = File.Create(destinationPath);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create);
            var entry = archive.CreateEntry("scs_extractor.exe");
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(new byte[] { 1 }, cancellationToken);
        }
    }

    private sealed class FakeArchiveExtractor : IScsArchiveExtractor
    {
        public string? ArchivePath { get; private set; }

        public async Task ExtractAsync(string extractorPath, string archivePath, string outputDirectory, CancellationToken cancellationToken)
        {
            ArchivePath = archivePath;
            var localeDirectory = Path.Combine(outputDirectory, "locale", "en_us");
            Directory.CreateDirectory(localeDirectory);
            await File.WriteAllTextAsync(Path.Combine(localeDirectory, "driver_names.sii"), """
                SiiNunit
                {
                driver_name : driver.23 {
                  name: "Alice"
                  surname: "Ramirez"
                }
                }
                """, cancellationToken);
        }
    }

    private sealed class FailingArchiveExtractor : IScsArchiveExtractor
    {
        public Task ExtractAsync(string extractorPath, string archivePath, string outputDirectory, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("*** ERROR *** : Root directory not found, can not extract this archive!");
    }
}
