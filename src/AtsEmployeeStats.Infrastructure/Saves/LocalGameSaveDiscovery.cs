using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Infrastructure.Saves;

public sealed class LocalGameSaveDiscovery : IGameSaveDiscovery
{
    private readonly string? _documentsPath;
    private readonly string? _steamPath;
    private readonly Func<string, bool> _directoryExists;
    private readonly Func<string, IEnumerable<string>> _enumerateDirectories;

    public LocalGameSaveDiscovery()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            FindSteamPath(),
            Directory.Exists,
            Directory.EnumerateDirectories)
    {
    }

    public LocalGameSaveDiscovery(
        string? documentsPath,
        string? steamPath,
        Func<string, bool> directoryExists,
        Func<string, IEnumerable<string>> enumerateDirectories)
    {
        _documentsPath = documentsPath;
        _steamPath = steamPath;
        _directoryExists = directoryExists;
        _enumerateDirectories = enumerateDirectories;
    }

    public Task<IReadOnlyList<GameSaveRoot>> FindCandidateRootsAsync(
        GameSaveKind game,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = BuildCandidates(game)
            .Select(path => new GameSaveRoot(game, path, _directoryExists(path)))
            .ToList();
        return Task.FromResult<IReadOnlyList<GameSaveRoot>>(candidates);
    }

    private IEnumerable<string> BuildCandidates(GameSaveKind game)
    {
        var documentFolderName = game switch
        {
            GameSaveKind.AmericanTruckSimulator => "American Truck Simulator",
            GameSaveKind.EuroTruckSimulator2 => "Euro Truck Simulator 2",
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
        };
        var steamAppId = game switch
        {
            GameSaveKind.AmericanTruckSimulator => "270880",
            GameSaveKind.EuroTruckSimulator2 => "227300",
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
        };

        if (!string.IsNullOrWhiteSpace(_steamPath))
        {
            var userdataRoot = Path.Combine(_steamPath, "userdata");
            if (_directoryExists(userdataRoot))
            {
                foreach (var userDir in _enumerateDirectories(userdataRoot))
                {
                    var remote = Path.Combine(userDir, steamAppId, "remote");
                    yield return Path.Combine(remote, "profiles");
                    yield return Path.Combine(remote, "steam_profiles");
                    yield return remote;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_documentsPath))
        {
            var documentsRoot = Path.Combine(_documentsPath, documentFolderName);
            yield return Path.Combine(documentsRoot, "profiles");
            yield return Path.Combine(documentsRoot, "steam_profiles");
            yield return documentsRoot;
        }
    }

    private static string? FindSteamPath()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
                if (key?.GetValue("SteamPath") is string regPath &&
                    !string.IsNullOrWhiteSpace(regPath) &&
                    Directory.Exists(regPath))
                {
                    return regPath;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
            }
        }

        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam");
        return Directory.Exists(defaultPath) ? defaultPath : null;
    }
}
