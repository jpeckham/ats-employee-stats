using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Wpf.Controllers;

namespace AtsEmployeeStats.Tests;

public sealed class GameSourcePresenterTests
{
    [Fact]
    public async Task LoadGameSourcesAsync_maps_configured_sources_and_discovered_saves_to_wpf_rows()
    {
        var presenter = CreatePresenter(
            installations:
            [
                new GameInstallation(GameType.Ats, @"C:\ATS", @"C:\ATS\profiles", @"C:\ATS\profiles", true)
            ],
            settings: new GameSourceSettings(
            [
                new GameSourceConfiguration(GameType.Ats, true, @"D:\ATS", null, null, [@"D:\ATS\profiles", @"D:\ATS\steam_profiles"])
            ]),
            saves:
            [
                new SaveGame(
                    "ats:profile:autosave",
                    GameType.Ats,
                    "Profile",
                    "autosave",
                    @"D:\ATS\profiles\Profile\save\autosave",
                    "Ats:D:\\ATS\\profiles",
                    @"D:\ATS\profiles")
            ]);

        await presenter.LoadGameSourcesAsync();

        var source = Assert.Single(presenter.GameSources);
        Assert.Equal("Ats", source.GameKey);
        Assert.Equal("ATS", source.GameName);
        Assert.Equal("ats-", source.SourcePrefix);
        Assert.True(source.Enabled);
        Assert.Equal(@"D:\ATS", source.InstallPath);
        Assert.Equal(@"C:\ATS\profiles", source.ProfilePath);
        Assert.Equal(@"C:\ATS\profiles", source.SavePath);
        Assert.Equal([@"D:\ATS\profiles", @"D:\ATS\steam_profiles"], source.SavePaths);

        var save = Assert.Single(presenter.GameSaves);
        Assert.Equal("Ats", save.GameKey);
        Assert.Equal("Profile", save.ProfileName);
        Assert.Equal("autosave", save.SaveName);
        Assert.Equal(@"D:\ATS\profiles", save.SaveRootPath);
    }

    [Fact]
    public async Task StartSourceWizardAsync_maps_candidates_and_navigates_wizard_steps()
    {
        var presenter = CreatePresenter(
            installations:
            [
                new GameInstallation(GameType.Ats, @"C:\ATS", @"C:\ATS\profiles", @"C:\ATS\profiles", true)
            ],
            settings: new GameSourceSettings(
            [
                new GameSourceConfiguration(GameType.Ats, true, @"D:\ConfiguredATS", null, @"D:\ConfiguredSaves")
            ]),
            candidates: new Dictionary<GameType, GameSourceCandidates>
            {
                [GameType.Ats] = new(
                    GameType.Ats,
                    [
                        new GameInstallCandidate(GameType.Ats, @"C:\ATS", true, ["steam app"]),
                        new GameInstallCandidate(GameType.Ats, @"D:\ConfiguredATS", false, ["configured"])
                    ],
                    [
                        new GameSaveRootCandidate(GameType.Ats, @"C:\ATS\profiles", true, 2, ["game.sii"]),
                        new GameSaveRootCandidate(GameType.Ats, @"D:\ConfiguredSaves", false, 0, ["configured"])
                    ]),
                [GameType.Ets2] = new(GameType.Ets2, [], [])
            });
        await presenter.LoadGameSourcesAsync();

        var result = await presenter.StartSourceWizardAsync();

        Assert.True(result.Succeeded);
        Assert.True(presenter.IsSourceWizardVisible);
        Assert.Equal("Step 1 of 2", presenter.SourceWizardStepText);
        Assert.Equal("Ats", presenter.CurrentWizardGame?.GameKey);
        Assert.Equal(@"D:\ConfiguredATS", presenter.CurrentWizardGame?.InstallCandidates.Single(candidate => candidate.IsSelected).Path);
        Assert.Contains(presenter.CurrentWizardGame!.SaveRootCandidates, candidate => candidate.Path == @"D:\ConfiguredSaves" && candidate.IsSelected);
        Assert.Contains(presenter.CurrentWizardGame.SaveRootCandidates, candidate => candidate.Path == @"C:\ATS\profiles" && candidate.IsSelected);

        presenter.NextSourceWizardStep();

        Assert.Equal("Step 2 of 2", presenter.SourceWizardStepText);
        Assert.Equal("Ets2", presenter.CurrentWizardGame?.GameKey);

        presenter.PreviousSourceWizardStep();

        Assert.Equal("Step 1 of 2", presenter.SourceWizardStepText);
        Assert.Equal("Ats", presenter.CurrentWizardGame?.GameKey);
    }

    [Fact]
    public async Task FinishSourceWizardAsync_persists_selected_sources_and_reloads_rows()
    {
        var settings = new InMemoryGameSourceSettingsStore(new GameSourceSettings([]));
        var presenter = CreatePresenter(
            installations:
            [
                new GameInstallation(GameType.Ats, null, null, null, false),
                new GameInstallation(GameType.Ets2, null, null, null, false)
            ],
            settingsStore: settings,
            candidates: new Dictionary<GameType, GameSourceCandidates>
            {
                [GameType.Ats] = new(
                    GameType.Ats,
                    [new GameInstallCandidate(GameType.Ats, @"C:\ATS", true, ["steam app"])],
                    [
                        new GameSaveRootCandidate(GameType.Ats, @"C:\ATS\profiles", true, 2, ["profiles"]),
                        new GameSaveRootCandidate(GameType.Ats, @"C:\ATS\steam_profiles", true, 1, ["steam profiles"])
                    ]),
                [GameType.Ets2] = new(GameType.Ets2, [], [])
            },
            validationResults: new Dictionary<string, GamePathValidation>
            {
                [$"{GameType.Ats}|C:\\ATS\\profiles"] = new(@"C:\ATS\profiles", true, ["profiles"]),
                [$"{GameType.Ats}|C:\\ATS\\steam_profiles"] = new(@"C:\ATS\steam_profiles", true, ["steam profiles"])
            });
        await presenter.LoadGameSourcesAsync();
        await presenter.StartSourceWizardAsync();
        presenter.CurrentWizardGame!.HasGame = true;
        presenter.SourceWizardGames.Single(game => game.GameKey == "Ets2").HasGame = false;

        var result = await presenter.FinishSourceWizardAsync();

        Assert.True(result.Succeeded);
        Assert.False(presenter.IsSourceWizardVisible);
        var saved = Assert.Single(settings.SavedSettings.Sources, source => source.Game == GameType.Ats);
        Assert.True(saved.Enabled);
        Assert.Equal(@"C:\ATS", saved.InstallPath);
        Assert.Equal(@"C:\ATS", saved.ProfilePath);
        Assert.Equal([@"C:\ATS\profiles", @"C:\ATS\steam_profiles"], saved.EffectiveSavePaths);
        Assert.Single(presenter.GameSources, source => source.GameKey == "Ats" && source.Enabled);
    }

    private static GameSourcePresenter CreatePresenter(
        IReadOnlyList<GameInstallation> installations,
        GameSourceSettings? settings = null,
        InMemoryGameSourceSettingsStore? settingsStore = null,
        IReadOnlyList<SaveGame>? saves = null,
        IReadOnlyDictionary<GameType, GameSourceCandidates>? candidates = null,
        IReadOnlyDictionary<string, GamePathValidation>? validationResults = null)
    {
        var discovery = new StubGameSourceDiscovery(installations, candidates, validationResults);
        var store = settingsStore ?? new InMemoryGameSourceSettingsStore(settings ?? new GameSourceSettings([]));
        var sourceUseCase = new GameSourceManagementUseCase(discovery, store);
        var catalogUseCase = new GameSaveCatalogUseCase(new StubConfiguredGameSaveDiscovery(saves ?? []));
        return new GameSourcePresenter(sourceUseCase, catalogUseCase);
    }

    private sealed class StubGameSourceDiscovery(
        IReadOnlyList<GameInstallation> installations,
        IReadOnlyDictionary<GameType, GameSourceCandidates>? candidates,
        IReadOnlyDictionary<string, GamePathValidation>? validationResults) : IGameSourceDiscovery
    {
        public Task<IReadOnlyList<GameInstallation>> DiscoverInstallationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(installations);

        public Task<GameSourceCandidates> DiscoverCandidatesAsync(GameType game, CancellationToken cancellationToken) =>
            Task.FromResult(candidates?.GetValueOrDefault(game) ?? new GameSourceCandidates(game, [], []));

        public Task<GamePathValidation> ValidateSaveRootAsync(GameType game, string path, CancellationToken cancellationToken) =>
            Task.FromResult(validationResults?.GetValueOrDefault($"{game}|{path}") ?? new GamePathValidation(path, true, []));
    }

    private sealed class InMemoryGameSourceSettingsStore(GameSourceSettings settings) : IGameSourceSettingsStore
    {
        private GameSourceSettings _settings = settings;

        public GameSourceSettings SavedSettings { get; private set; } = settings;

        public Task<GameSourceSettings> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_settings);

        public Task SaveAsync(GameSourceSettings settings, CancellationToken cancellationToken)
        {
            _settings = settings;
            SavedSettings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class StubConfiguredGameSaveDiscovery(IReadOnlyList<SaveGame> saves) : IConfiguredGameSaveDiscovery
    {
        public Task<IReadOnlyList<SaveGame>> FindSaveGamesAsync(
            GameSourceConfiguration source,
            CancellationToken cancellationToken) =>
            Task.FromResult(saves);
    }
}
