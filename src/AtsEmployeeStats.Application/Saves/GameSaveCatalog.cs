namespace AtsEmployeeStats.Application.Saves;

public sealed record SaveGame(
    string SaveGameId,
    GameType Game,
    string ProfileName,
    string SaveName,
    string SaveDirectory,
    string SourceKey,
    string? SaveRootPath = null);

public interface IConfiguredGameSaveDiscovery
{
    Task<IReadOnlyList<SaveGame>> FindSaveGamesAsync(
        GameSourceConfiguration source,
        CancellationToken cancellationToken);
}

public sealed class GameSaveCatalogUseCase(IConfiguredGameSaveDiscovery discovery)
{
    public async Task<IReadOnlyList<SaveGame>> FindSaveGamesAsync(
        IReadOnlyList<GameSourceConfiguration> sources,
        CancellationToken cancellationToken)
    {
        var saves = new List<SaveGame>();
        foreach (var source in sources.Where(source => source.Enabled && source.EffectiveSavePaths.Count > 0))
        {
            cancellationToken.ThrowIfCancellationRequested();
            saves.AddRange(await discovery.FindSaveGamesAsync(source, cancellationToken));
        }

        return saves
            .GroupBy(save => save.SaveGameId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(save => save.Game)
            .ThenBy(save => save.ProfileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(save => save.SaveName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
