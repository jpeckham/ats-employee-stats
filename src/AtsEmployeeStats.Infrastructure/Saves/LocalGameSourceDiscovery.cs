using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Infrastructure.Saves;

public sealed class LocalGameSourceDiscovery : IGameSourceDiscovery
{
    private readonly string? _documentsPath;
    private readonly IReadOnlyList<string> _steamLibraryPaths;
    private readonly Func<string, bool> _directoryExists;
    private readonly Func<string, string, IEnumerable<string>> _enumerateFiles;
    private readonly Func<string, IEnumerable<string>> _enumerateDirectories;

    public LocalGameSourceDiscovery()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            LocalSteamPath.FindLibraries(),
            Directory.Exists,
            (path, pattern) => Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories),
            Directory.EnumerateDirectories)
    {
    }

    public LocalGameSourceDiscovery(
        string? documentsPath,
        string? steamPath,
        Func<string, bool> directoryExists)
        : this(
            documentsPath,
            string.IsNullOrWhiteSpace(steamPath) ? [] : [steamPath],
            directoryExists,
            (path, pattern) => Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories),
            Directory.EnumerateDirectories)
    {
    }

    public LocalGameSourceDiscovery(
        string? documentsPath,
        string? steamPath,
        Func<string, bool> directoryExists,
        Func<string, string, IEnumerable<string>> enumerateFiles)
        : this(
            documentsPath,
            string.IsNullOrWhiteSpace(steamPath) ? [] : [steamPath],
            directoryExists,
            enumerateFiles,
            Directory.EnumerateDirectories)
    {
    }

    public LocalGameSourceDiscovery(
        string? documentsPath,
        IReadOnlyList<string> steamLibraryPaths,
        Func<string, bool> directoryExists)
        : this(
            documentsPath,
            steamLibraryPaths,
            directoryExists,
            (path, pattern) => Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories),
            Directory.EnumerateDirectories)
    {
    }

    public LocalGameSourceDiscovery(
        string? documentsPath,
        IReadOnlyList<string> steamLibraryPaths,
        Func<string, bool> directoryExists,
        Func<string, string, IEnumerable<string>> enumerateFiles,
        Func<string, IEnumerable<string>> enumerateDirectories)
    {
        _documentsPath = LocalPath.NormalizeOrNull(documentsPath);
        _steamLibraryPaths = steamLibraryPaths
            .Select(LocalPath.Normalize)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()!;
        _directoryExists = directoryExists;
        _enumerateFiles = enumerateFiles;
        _enumerateDirectories = enumerateDirectories;
    }

    public Task<IReadOnlyList<GameInstallation>> DiscoverInstallationsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<GameInstallation> installations =
        [
            Build(GameType.Ats, "American Truck Simulator"),
            Build(GameType.Ets2, "Euro Truck Simulator 2")
        ];
        return Task.FromResult(installations);
    }

    public Task<GameSourceCandidates> DiscoverCandidatesAsync(GameType game, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var folderName = FolderName(game);

        var installCandidates = _steamLibraryPaths
            .Select(path => Path.Combine(path, "steamapps", "common", folderName))
            .Select(LocalPath.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new GameInstallCandidate(game, path, Exists(path), BuildInstallProof(path)))
            .ToList();

        var profilePath = string.IsNullOrWhiteSpace(_documentsPath)
            ? null
            : Path.Combine(_documentsPath, folderName);
        var saveCandidates = BuildSaveRootCandidates(game, profilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => LocalPath.Normalize(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => BuildSaveRootCandidate(game, path!))
            .ToList();

        return Task.FromResult(new GameSourceCandidates(game, installCandidates, saveCandidates));
    }

    public Task<GamePathValidation> ValidateSaveRootAsync(GameType game, string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidate = BuildSaveRootCandidate(game, path);
        return Task.FromResult(new GamePathValidation(path, candidate.IsValid, candidate.Proofs));
    }

    private GameInstallation Build(GameType game, string folderName)
    {
        var installPath = SelectFirstExisting(_steamLibraryPaths
            .Select(path => Path.Combine(path, "steamapps", "common", folderName))
            .Select(LocalPath.Normalize)
            .Cast<string?>()
            .ToArray());
        var profilePath = string.IsNullOrWhiteSpace(_documentsPath)
            ? null
            : Path.Combine(_documentsPath, folderName);
        var savePath = SelectFirstExisting(BuildSaveRootCandidates(game, profilePath).ToArray());

        var exists =
            Exists(installPath) ||
            Exists(profilePath) ||
            Exists(savePath);

        return new GameInstallation(game, installPath, profilePath, savePath, exists);
    }

    private IEnumerable<string?> BuildSaveRootCandidates(GameType game, string? profilePath)
    {
        if (profilePath is not null)
        {
            yield return Path.Combine(profilePath, "profiles");
            yield return Path.Combine(profilePath, "steam_profiles");
        }

        var steamAppId = SteamAppId(game);
        foreach (var steamPath in _steamLibraryPaths)
        {
            var userdataRoot = Path.Combine(steamPath, "userdata");
            if (!Exists(userdataRoot))
                continue;

            foreach (var userDirectory in SafeEnumerateDirectories(userdataRoot))
            {
                var remote = Path.Combine(LocalPath.Normalize(userDirectory), steamAppId, "remote");
                yield return Path.Combine(remote, "profiles");
                yield return Path.Combine(remote, "steam_profiles");
            }
        }
    }

    private GameSaveRootCandidate BuildSaveRootCandidate(GameType game, string path)
    {
        var proofs = new List<string>();
        if (Exists(path))
            proofs.Add("Save root exists");
        else
            proofs.Add("Save root does not exist");

        var saveFileCount = 0;
        if (Exists(path))
        {
            saveFileCount = SafeEnumerateFiles(path, "game.sii")
                .Count(file => !IsBackupPath(file));
        }

        proofs.Add($"game.sii files found: {saveFileCount}");
        var normalizedPath = LocalPath.Normalize(path);
        return new GameSaveRootCandidate(game, normalizedPath, Exists(normalizedPath) && saveFileCount > 0, saveFileCount, proofs);
    }

    private IReadOnlyList<string> BuildInstallProof(string path)
    {
        var proofs = new List<string>();
        proofs.Add(Exists(path) ? "Install folder exists" : "Install folder not found");
        var binPath = Path.Combine(path, "bin");
        if (Exists(binPath))
            proofs.Add("bin folder exists");
        return proofs;
    }

    private string? SelectFirstExisting(params string?[] paths)
    {
        foreach (var path in paths)
        {
            if (Exists(path))
                return path;
        }

        return paths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
    }

    private bool Exists(string? path) =>
        !string.IsNullOrWhiteSpace(path) && _directoryExists(path);

    private IEnumerable<string> SafeEnumerateFiles(string path, string pattern)
    {
        try
        {
            return _enumerateFiles(LocalPath.Normalize(path), pattern)
                .Select(LocalPath.Normalize)
                .ToList();
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

    private static string FolderName(GameType game) =>
        game switch
        {
            GameType.Ats => "American Truck Simulator",
            GameType.Ets2 => "Euro Truck Simulator 2",
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
        };

    private static string SteamAppId(GameType game) =>
        game switch
        {
            GameType.Ats => "270880",
            GameType.Ets2 => "227300",
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
        };

    private IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return _enumerateDirectories(LocalPath.Normalize(path))
                .Select(LocalPath.Normalize)
                .ToList();
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

    private static bool IsBackupPath(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));
}
