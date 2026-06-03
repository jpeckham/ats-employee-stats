namespace AtsEmployeeStats.Application.Saves;

public enum GameType
{
    Ats,
    Ets2
}

public sealed record GameInstallation(
    GameType Game,
    string? InstallPath,
    string? ProfilePath,
    string? SavePath,
    bool Exists);

public sealed record GameSourceConfiguration(
    GameType Game,
    bool Enabled,
    string? InstallPath,
    string? ProfilePath,
    string? SavePath,
    IReadOnlyList<string>? SavePaths = null)
{
    public IReadOnlyList<string> EffectiveSavePaths
    {
        get
        {
            var paths = (SavePaths ?? [])
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (paths.Count > 0)
                return paths;

            return string.IsNullOrWhiteSpace(SavePath) ? [] : [SavePath.Trim()];
        }
    }
}

public sealed record GameSourceSettings(
    IReadOnlyList<GameSourceConfiguration> Sources,
    bool WizardCompleted = false);

public sealed record GameInstallCandidate(
    GameType Game,
    string Path,
    bool IsValid,
    IReadOnlyList<string> Proofs);

public sealed record GameSaveRootCandidate(
    GameType Game,
    string Path,
    bool IsValid,
    int SaveFileCount,
    IReadOnlyList<string> Proofs);

public sealed record GameSourceCandidates(
    GameType Game,
    IReadOnlyList<GameInstallCandidate> InstallCandidates,
    IReadOnlyList<GameSaveRootCandidate> SaveRootCandidates);

public sealed record GamePathValidation(
    string Path,
    bool IsValid,
    IReadOnlyList<string> Proofs);

public sealed record GameSourceSaveResult(
    bool Saved,
    IReadOnlyList<string> Errors);

public interface IGameSourceDiscovery
{
    Task<IReadOnlyList<GameInstallation>> DiscoverInstallationsAsync(CancellationToken cancellationToken);

    Task<GameSourceCandidates> DiscoverCandidatesAsync(GameType game, CancellationToken cancellationToken);

    Task<GamePathValidation> ValidateSaveRootAsync(GameType game, string path, CancellationToken cancellationToken);
}

public interface IGameSourceSettingsStore
{
    Task<GameSourceSettings> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(GameSourceSettings settings, CancellationToken cancellationToken);
}

public sealed class GameSourceManagementUseCase(
    IGameSourceDiscovery discovery,
    IGameSourceSettingsStore settingsStore)
{
    public async Task<IReadOnlyList<GameSourceConfiguration>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var discovered = await discovery.DiscoverInstallationsAsync(cancellationToken);
        var settings = await settingsStore.LoadAsync(cancellationToken);
        var overrides = settings.Sources.ToDictionary(source => source.Game);

        return discovered
            .GroupBy(source => source.Game)
            .Select(group => Merge(group.Key, group.First(), overrides))
            .OrderBy(source => source.Game)
            .ToList();
    }

    public async Task<bool> RequiresWizardAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        return !settings.WizardCompleted || settings.Sources.Count == 0;
    }

    public Task<GameSourceCandidates> DiscoverCandidatesAsync(GameType game, CancellationToken cancellationToken) =>
        discovery.DiscoverCandidatesAsync(game, cancellationToken);

    public Task SaveAsync(
        IReadOnlyList<GameSourceConfiguration> sources,
        CancellationToken cancellationToken) =>
        settingsStore.SaveAsync(new GameSourceSettings(sources), cancellationToken);

    public async Task<GameSourceSaveResult> SaveValidatedAsync(
        IReadOnlyList<GameSourceConfiguration> sources,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        foreach (var source in sources.Where(source => source.Enabled))
        {
            var paths = source.EffectiveSavePaths;
            if (paths.Count == 0)
            {
                errors.Add($"{DisplayName(source.Game)} needs at least one save location.");
                continue;
            }

            foreach (var path in paths)
            {
                var validation = await discovery.ValidateSaveRootAsync(source.Game, path, cancellationToken);
                if (!validation.IsValid)
                    errors.Add($"{DisplayName(source.Game)} save location is not valid: {path}");
            }
        }

        if (errors.Count > 0)
            return new GameSourceSaveResult(false, errors);

        await settingsStore.SaveAsync(
            new GameSourceSettings(NormalizeSources(sources), WizardCompleted: true),
            cancellationToken);
        return new GameSourceSaveResult(true, []);
    }

    private static GameSourceConfiguration Merge(
        GameType game,
        GameInstallation installation,
        IReadOnlyDictionary<GameType, GameSourceConfiguration> overrides)
    {
        if (!overrides.TryGetValue(game, out var configured))
        {
            return new GameSourceConfiguration(
                game,
                Enabled: installation.Exists,
                installation.InstallPath,
                installation.ProfilePath,
                installation.SavePath,
                string.IsNullOrWhiteSpace(installation.SavePath) ? [] : [installation.SavePath]);
        }

        return new GameSourceConfiguration(
            game,
            configured.Enabled,
            Coalesce(configured.InstallPath, installation.InstallPath),
            Coalesce(configured.ProfilePath, installation.ProfilePath),
            Coalesce(configured.SavePath, installation.SavePath),
            configured.EffectiveSavePaths.Count > 0
                ? configured.EffectiveSavePaths
                : string.IsNullOrWhiteSpace(installation.SavePath) ? [] : [installation.SavePath]);
    }

    private static string? Coalesce(string? configured, string? discovered) =>
        string.IsNullOrWhiteSpace(configured) ? discovered : configured;

    private static IReadOnlyList<GameSourceConfiguration> NormalizeSources(IReadOnlyList<GameSourceConfiguration> sources) =>
        sources
            .Select(source => source with
            {
                SavePath = source.EffectiveSavePaths.FirstOrDefault(),
                SavePaths = source.EffectiveSavePaths
            })
            .ToList();

    private static string DisplayName(GameType game) =>
        game == GameType.Ats ? "ATS" : "ETS2";
}
