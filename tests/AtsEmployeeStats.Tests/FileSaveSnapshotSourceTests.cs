using AtsEmployeeStats.Infrastructure.Saves;
using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Tests;

public sealed class FileSaveSnapshotSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ats-stats-{Guid.NewGuid():N}");

    [Fact]
    public async Task ReadAllAsync_reads_game_sii_files_recursively_as_snapshots()
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
            }
            """);

        var source = new FileSaveSnapshotSource(_root);

        var snapshots = await source.ReadAllAsync(CancellationToken.None);

        var snapshot = Assert.Single(snapshots);
        Assert.EndsWith("game.sii", snapshot.Name, StringComparison.OrdinalIgnoreCase);
        Assert.Single(snapshot.Document.Units, unit => unit.Type == "player");
    }

    [Fact]
    public async Task ReadAllAsync_skips_saves_older_than_the_history_window()
    {
        var recentPath = await WriteSaveAsync("recent", "Recent Line");
        var oldPath = await WriteSaveAsync("old", "Old Line");
        File.SetLastWriteTimeUtc(recentPath, DateTime.UtcNow.AddDays(-1));
        File.SetLastWriteTimeUtc(oldPath, DateTime.UtcNow.AddDays(-10));

        var source = new FileSaveSnapshotSource(_root, TimeSpan.FromDays(5));

        var snapshots = await source.ReadAllAsync(CancellationToken.None);

        var snapshot = Assert.Single(snapshots);
        Assert.Contains("recent", snapshot.Name);
    }

    [Fact]
    public async Task ReadAllAsync_returns_saves_newest_to_oldest()
    {
        var oldestPath = await WriteSaveAsync("oldest", "Oldest Line");
        var newestPath = await WriteSaveAsync("newest", "Newest Line");
        var middlePath = await WriteSaveAsync("middle", "Middle Line");
        File.SetLastWriteTimeUtc(oldestPath, DateTime.UtcNow.AddDays(-3));
        File.SetLastWriteTimeUtc(middlePath, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(newestPath, DateTime.UtcNow.AddDays(-1));

        var source = new FileSaveSnapshotSource(_root);

        var snapshots = await source.ReadAllAsync(CancellationToken.None);

        Assert.Collection(
            snapshots,
            snapshot => Assert.Contains("newest", snapshot.Name),
            snapshot => Assert.Contains("middle", snapshot.Name),
            snapshot => Assert.Contains("oldest", snapshot.Name));
    }

    [Fact]
    public async Task ReadAllAsync_reports_discovery_and_predictive_unit_progress()
    {
        await WriteSaveAsync("first", "First Line", extraUnits: 3);
        await WriteSaveAsync("second", "Second Line", extraUnits: 3);
        var progressEvents = new List<SaveLoadProgress>();
        var progress = new CapturingProgress(progressEvents);

        var source = new FileSaveSnapshotSource(_root);

        await source.ReadAllAsync(CancellationToken.None, progress);

        Assert.Contains(
            progressEvents,
            update => update.Stage == SaveLoadStage.FilesDiscovered
                && update.CompletedFiles == 0
                && update.TotalFiles == 2);
        Assert.Contains(
            progressEvents,
            update => update.Stage == SaveLoadStage.FileLoaded
                && update.TotalUnits == 8
                && update.CompletedUnits >= 4);
        Assert.Contains(
            progressEvents,
            update => update.Stage == SaveLoadStage.Completed
                && update.CompletedFiles == 2
                && update.TotalFiles == 2);
    }

    [Fact]
    public async Task ReadAllAsync_reports_current_file_unit_progress()
    {
        await WriteSaveAsync("first", "First Line", extraUnits: 3);
        var progressEvents = new List<SaveLoadProgress>();
        var progress = new CapturingProgress(progressEvents);

        var source = new FileSaveSnapshotSource(_root);

        await source.ReadAllAsync(CancellationToken.None, progress);

        Assert.Contains(
            progressEvents,
            update => update.Stage == SaveLoadStage.LoadingFiles
                && update.CurrentFileTotalUnits == 4
                && update.CurrentFileCompletedUnits > 0
                && update.CurrentFileCompletedUnits <= update.CurrentFileTotalUnits);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<string> WriteSaveAsync(string slotName, string companyName, int extraUnits = 0)
    {
        var saveDirectory = Path.Combine(_root, "profiles", "506C61796572", "save", slotName);
        Directory.CreateDirectory(saveDirectory);
        var savePath = Path.Combine(saveDirectory, "game.sii");
        var extraContent = string.Concat(Enumerable.Range(0, extraUnits).Select(index => $$"""

            dummy : dummy.{{index}} {
              value: {{index}}
            }
            """));
        await File.WriteAllTextAsync(savePath, $$"""
            SiiNunit
            {
            player : player {
              company_name: "{{companyName}}"
            }
            {{extraContent}}
            }
            """);

        return savePath;
    }

    private sealed class CapturingProgress(List<SaveLoadProgress> events) : IProgress<SaveLoadProgress>
    {
        public void Report(SaveLoadProgress value) => events.Add(value);
    }
}
