using System.Text;
using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Infrastructure.Saves;

public sealed class LocalConfiguredGameSaveDiscovery : IConfiguredGameSaveDiscovery
{
    private readonly Func<string, string, IEnumerable<string>> _enumerateFiles;

    public LocalConfiguredGameSaveDiscovery()
        : this((path, pattern) => Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories))
    {
    }

    public LocalConfiguredGameSaveDiscovery(Func<string, string, IEnumerable<string>> enumerateFiles)
    {
        _enumerateFiles = enumerateFiles;
    }

    public Task<IReadOnlyList<SaveGame>> FindSaveGamesAsync(
        GameSourceConfiguration source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!source.Enabled || source.EffectiveSavePaths.Count == 0)
            return Task.FromResult<IReadOnlyList<SaveGame>>([]);

        var saves = source.EffectiveSavePaths
            .SelectMany(path => SafeEnumerateFiles(path, "game.sii")
                .Select(file => new { SaveRoot = path, File = file }))
            .Where(path => !IsBackupPath(path.File))
            .Select(path => BuildSaveGame(source.Game, path.SaveRoot, path.File))
            .GroupBy(save => save.SaveGameId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        return Task.FromResult<IReadOnlyList<SaveGame>>(saves);
    }

    private static SaveGame BuildSaveGame(GameType game, string saveRoot, string path)
    {
        var profile = GetProfileSegment(path);
        var slot = GetSaveSlot(path);
        var sourceKey = $"{game}:{saveRoot}";
        return new SaveGame(
            $"{game}:{profile}:{slot}",
            game,
            DecodeHexProfileName(profile) ?? profile,
            slot,
            Path.GetDirectoryName(path) ?? path,
            sourceKey,
            saveRoot);
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

    private static string? DecodeHexProfileName(string profileSegment)
    {
        if (profileSegment.Length == 0 ||
            profileSegment.Length % 2 != 0 ||
            profileSegment.Any(character => !Uri.IsHexDigit(character)))
        {
            return null;
        }

        var bytes = new byte[profileSegment.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(profileSegment.Substring(i * 2, 2), 16);
        }

        var decoded = Encoding.UTF8.GetString(bytes).Trim('\0', ' ');
        return string.IsNullOrWhiteSpace(decoded) ? null : decoded;
    }

    private static bool IsBackupPath(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));

    private IEnumerable<string> SafeEnumerateFiles(string path, string pattern)
    {
        try
        {
            return _enumerateFiles(path, pattern).ToList();
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
}
