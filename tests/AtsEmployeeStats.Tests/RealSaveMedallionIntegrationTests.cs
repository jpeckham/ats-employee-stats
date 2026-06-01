using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Infrastructure.Saves;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace AtsEmployeeStats.Tests;

public sealed class RealSaveMedallionIntegrationTests(ITestOutputHelper output)
{
    private const string EnableVariable = "ATS_EMPLOYEE_STATS_REAL_SAVE_TESTS";

    [SkippableTheory]
    [InlineData(GameSaveKind.AmericanTruckSimulator)]
    [InlineData(GameSaveKind.EuroTruckSimulator2)]
    public async Task Real_save_files_ingest_into_medallion_statistics(GameSaveKind game)
    {
        SkipUnlessEnabled();

        var rootDiscovery = new LocalGameSaveDiscovery();
        var rootUseCase = new GameSaveDiscoveryUseCase(rootDiscovery);
        var roots = await rootUseCase.FindSaveRootsAsync(game, CancellationToken.None);
        if (roots.Count == 0)
            Skip.If(true, $"{game} is not installed or has no discoverable save roots.");

        var fileUseCase = new GameSaveFileDiscoveryUseCase(new LocalGameSaveFileDiscovery(rootDiscovery));
        var saveFiles = await fileUseCase.FindSaveFilesAsync(game, CancellationToken.None);
        if (saveFiles.Count == 0)
            Skip.If(true, $"{game} has no discoverable game.sii save files.");

        var root = roots.FirstOrDefault(candidate =>
            saveFiles.Any(file => IsUnderRoot(file.Path, candidate.Path))) ?? roots[0];

        using var temp = new TemporaryDirectory($"ats-real-save-{game}-");
        var databasePath = Path.Combine(temp.Path, "medallion.db");
        var source = new SqliteMedallionSaveSnapshotSource(
            root.Path,
            databasePath,
            new AtsReferenceDataOptions(
                Enabled: false,
                GameInstallRoot: null,
                CacheRoot: Path.Combine(temp.Path, "reference-cache")));
        var service = new StatisticsService(source);

        await service.IngestAsync(CancellationToken.None);
        var statistics = await service.LoadAsync(CancellationToken.None);

        Assert.NotNull(statistics.LastUpdated);
        Assert.NotEmpty(statistics.Companies);
        Assert.Contains(statistics.Companies, company => !string.IsNullOrWhiteSpace(company.DisplayName));

        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString());
        await connection.OpenAsync();

        Assert.True(await CountRowsAsync(connection, "bronze_save_files") > 0);
        Assert.True(await CountRowsAsync(connection, "silver_companies") > 0);
        Assert.True(await CountRowsAsync(connection, "gold_company_summary") > 0);

        output.WriteLine($"Ingested {saveFiles.Count:N0} {game} save files from {root.Path} into {databasePath}.");
    }

    private static void SkipUnlessEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "1", StringComparison.Ordinal))
        {
            Skip.If(true, $"Set {EnableVariable}=1 to run opt-in real save integration tests.");
        }
    }

    private static bool IsUnderRoot(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != "." &&
            !relative.StartsWith("..", StringComparison.Ordinal) &&
            !Path.IsPathRooted(relative);
    }

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from {tableName}";
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory(string prefix)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
