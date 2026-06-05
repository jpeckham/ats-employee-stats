using AtsEmployeeStats.Wpf.Services;

namespace AtsEmployeeStats.Tests;

public sealed class LocalDatabaseDiskSpaceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void Estimate_sums_selected_game_sii_files_and_excludes_backup_paths()
    {
        var saveRoot = Path.Combine(_root, "profiles");
        WriteBytes(Path.Combine(saveRoot, "profile1", "save", "autosave", "game.sii"), 100);
        WriteBytes(Path.Combine(saveRoot, "profile1", "save", "manual", "game.sii"), 200);
        WriteBytes(Path.Combine(saveRoot, "profile1", "save", "autosave.bak", "game.sii"), 400);
        WriteBytes(Path.Combine(saveRoot, "profile1", "save", "manual", "notes.txt"), 800);
        var service = new LocalDatabaseDiskSpaceService(Path.Combine(_root, "data", "ats-employee-stats.db"));

        var estimate = service.Estimate([saveRoot]);

        Assert.Equal(300, estimate.SelectedSaveBytes);
        Assert.Equal(5_760, estimate.ProjectedDatabaseBytes);
        Assert.Equal(5_760, estimate.RequiredAdditionalBytes);
        Assert.True(estimate.FreeBytes > 0);
        Assert.Equal(estimate.FreeBytes >= estimate.RequiredAdditionalBytes, estimate.HasEnoughSpace);
    }

    [Fact]
    public void Estimate_subtracts_existing_database_size_from_required_additional_space()
    {
        var saveRoot = Path.Combine(_root, "profiles");
        WriteBytes(Path.Combine(saveRoot, "profile1", "save", "autosave", "game.sii"), 100);
        var databasePath = Path.Combine(_root, "data", "ats-employee-stats.db");
        WriteBytes(databasePath, 1_000);
        var service = new LocalDatabaseDiskSpaceService(databasePath);

        var estimate = service.Estimate([saveRoot]);

        Assert.Equal(100, estimate.SelectedSaveBytes);
        Assert.Equal(1_920, estimate.ProjectedDatabaseBytes);
        Assert.Equal(1_000, estimate.ExistingDatabaseBytes);
        Assert.Equal(920, estimate.RequiredAdditionalBytes);
    }

    [Fact]
    public void Estimate_requires_no_additional_space_when_existing_database_is_larger_than_projection()
    {
        var saveRoot = Path.Combine(_root, "profiles");
        WriteBytes(Path.Combine(saveRoot, "profile1", "save", "autosave", "game.sii"), 100);
        var databasePath = Path.Combine(_root, "data", "ats-employee-stats.db");
        WriteBytes(databasePath, 2_900);
        var service = new LocalDatabaseDiskSpaceService(databasePath);

        var estimate = service.Estimate([saveRoot]);

        Assert.Equal(1_920, estimate.ProjectedDatabaseBytes);
        Assert.Equal(2_900, estimate.ExistingDatabaseBytes);
        Assert.Equal(0, estimate.RequiredAdditionalBytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static void WriteBytes(string path, int length)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[length]);
    }
}
