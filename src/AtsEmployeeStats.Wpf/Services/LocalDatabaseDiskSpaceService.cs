using System.IO;

namespace AtsEmployeeStats.Wpf.Services;

public sealed class LocalDatabaseDiskSpaceService(string databasePath) : IDatabaseDiskSpaceService
{
    private const double DatabaseToSaveRatio = 19.2;

    public static LocalDatabaseDiskSpaceService CreateDefault()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtsEmployeeStats");
        return new LocalDatabaseDiskSpaceService(Path.Combine(dataDirectory, "ats-employee-stats.db"));
    }

    public DatabaseDiskSpaceEstimate Estimate(IReadOnlyList<string> saveRoots)
    {
        var selectedSaveBytes = saveRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .SelectMany(EnumerateGameSiiFiles)
            .Where(path => !IsBackupPath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Sum(GetLength);
        var projectedDatabaseBytes = (long)Math.Ceiling(selectedSaveBytes * DatabaseToSaveRatio);
        var existingDatabaseBytes = GetExistingDatabaseBytes();
        var requiredAdditionalBytes = Math.Max(0, projectedDatabaseBytes - existingDatabaseBytes);
        var freeBytes = GetDatabaseDriveFreeBytes();
        return new DatabaseDiskSpaceEstimate(
            selectedSaveBytes,
            projectedDatabaseBytes,
            existingDatabaseBytes,
            requiredAdditionalBytes,
            freeBytes,
            freeBytes >= requiredAdditionalBytes);
    }

    private static IEnumerable<string> EnumerateGameSiiFiles(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "game.sii", SearchOption.AllDirectories).ToList();
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static long GetLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private long GetDatabaseDriveFreeBytes()
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var fullPath = Path.GetFullPath(string.IsNullOrWhiteSpace(directory) ? databasePath : directory);
        var root = Path.GetPathRoot(fullPath);
        return string.IsNullOrWhiteSpace(root) ? 0 : new DriveInfo(root).AvailableFreeSpace;
    }

    private long GetExistingDatabaseBytes()
    {
        try
        {
            return File.Exists(databasePath) ? new FileInfo(databasePath).Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static bool IsBackupPath(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));
}
