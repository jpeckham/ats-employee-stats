namespace AtsEmployeeStats.Application.Saves;

public enum GameSaveKind
{
    AmericanTruckSimulator,
    EuroTruckSimulator2
}

public sealed record GameSaveRoot(
    GameSaveKind Game,
    string Path,
    bool Exists);

public sealed record GameSaveFile(
    GameSaveKind Game,
    string Path,
    bool Exists);

public interface IGameSaveDiscovery
{
    Task<IReadOnlyList<GameSaveRoot>> FindCandidateRootsAsync(
        GameSaveKind game,
        CancellationToken cancellationToken);
}

public interface IGameSaveFileDiscovery
{
    Task<IReadOnlyList<GameSaveFile>> FindCandidateSaveFilesAsync(
        GameSaveKind game,
        CancellationToken cancellationToken);
}

public sealed class GameSaveDiscoveryUseCase(IGameSaveDiscovery discovery)
{
    public async Task<IReadOnlyList<GameSaveRoot>> FindSaveRootsAsync(
        GameSaveKind game,
        CancellationToken cancellationToken)
    {
        var candidates = await discovery.FindCandidateRootsAsync(game, cancellationToken);
        return candidates
            .Where(candidate => candidate.Exists)
            .GroupBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<string?> FindFirstSaveRootAsync(
        GameSaveKind game,
        CancellationToken cancellationToken) =>
        (await FindSaveRootsAsync(game, cancellationToken)).FirstOrDefault()?.Path;
}

public sealed class GameSaveFileDiscoveryUseCase(IGameSaveFileDiscovery discovery)
{
    public async Task<IReadOnlyList<GameSaveFile>> FindSaveFilesAsync(
        GameSaveKind game,
        CancellationToken cancellationToken)
    {
        var candidates = await discovery.FindCandidateSaveFilesAsync(game, cancellationToken);
        return candidates
            .Where(candidate => candidate.Exists)
            .GroupBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<string?> FindFirstSaveFileAsync(
        GameSaveKind game,
        CancellationToken cancellationToken) =>
        (await FindSaveFilesAsync(game, cancellationToken)).FirstOrDefault()?.Path;
}
