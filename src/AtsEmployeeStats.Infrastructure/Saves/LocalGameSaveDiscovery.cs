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
            LocalSteamPath.Find(),
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
        _documentsPath = LocalPath.NormalizeOrNull(documentsPath);
        _steamPath = LocalPath.NormalizeOrNull(steamPath);
        _directoryExists = directoryExists;
        _enumerateDirectories = enumerateDirectories;
    }

    public Task<IReadOnlyList<GameSaveRoot>> FindCandidateRootsAsync(
        GameSaveKind game,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = BuildCandidates(game)
            .Select(LocalPath.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
                foreach (var userDir in _enumerateDirectories(userdataRoot).Select(LocalPath.Normalize))
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
}
