using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Wpf.Controllers;
using AtsEmployeeStats.Wpf.Services;
using AtsEmployeeStats.Wpf.Threading;

namespace AtsEmployeeStats.Tests;

public sealed class GameSourcePresenterTests
{
    [Fact]
    public async Task LoadGameSourcesAsync_maps_configured_sources_and_discovered_saves_to_wpf_rows()
    {
        var backgroundRunner = new RecordingBackgroundRunner();
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
            ],
            backgroundRunner: backgroundRunner);

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
        Assert.Equal(2, backgroundRunner.RunCount);
    }

    [Fact]
    public async Task StartSourceWizardAsync_maps_candidates_and_navigates_wizard_steps()
    {
        var backgroundRunner = new RecordingBackgroundRunner();
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
            },
            backgroundRunner: backgroundRunner);
        await presenter.LoadGameSourcesAsync();

        var result = await presenter.StartSourceWizardAsync();

        Assert.True(result.Succeeded);
        Assert.True(presenter.IsSourceWizardVisible);
        Assert.Equal("Step 1 of 2", presenter.SourceWizardStepText);
        Assert.Equal("Ats", presenter.CurrentWizardGame?.GameKey);
        Assert.Equal(3, backgroundRunner.RunCount);
        Assert.Equal(@"D:\ConfiguredATS", presenter.CurrentWizardGame?.InstallCandidates.Single(candidate => candidate.IsSelected).Path);
        Assert.Contains(presenter.CurrentWizardGame!.SaveRootCandidates, candidate => candidate.Path == @"D:\ConfiguredSaves" && !candidate.CanSelect && !candidate.IsSelected);
        Assert.Contains(presenter.CurrentWizardGame.SaveRootCandidates, candidate => candidate.Path == @"C:\ATS\profiles" && candidate.IsSelected);

        presenter.NextSourceWizardStep();

        Assert.Equal("Step 2 of 2", presenter.SourceWizardStepText);
        Assert.Equal("Ets2", presenter.CurrentWizardGame?.GameKey);

        presenter.PreviousSourceWizardStep();

        Assert.Equal("Step 1 of 2", presenter.SourceWizardStepText);
        Assert.Equal("Ats", presenter.CurrentWizardGame?.GameKey);
    }

    [Fact]
    public async Task StartSourceWizardAsync_leaves_zero_save_candidates_visible_but_unselected_and_unselectable()
    {
        var presenter = CreatePresenter(
            installations:
            [
                new GameInstallation(GameType.Ats, null, null, null, false),
                new GameInstallation(GameType.Ets2, null, null, null, false)
            ],
            settings: new GameSourceSettings(
            [
                new GameSourceConfiguration(GameType.Ats, true, null, null, @"D:\ConfiguredEmpty", [@"D:\ConfiguredEmpty"])
            ]),
            candidates: new Dictionary<GameType, GameSourceCandidates>
            {
                [GameType.Ats] = new(
                    GameType.Ats,
                    [],
                    [
                        new GameSaveRootCandidate(GameType.Ats, @"C:\ATS\profiles", true, 2, ["profiles"]),
                        new GameSaveRootCandidate(GameType.Ats, @"D:\ConfiguredEmpty", true, 0, ["configured"])
                    ]),
                [GameType.Ets2] = new(GameType.Ets2, [], [])
            },
            validationResults: new Dictionary<string, GamePathValidation>
            {
                [$"{GameType.Ats}|C:\\ATS\\profiles"] = new(@"C:\ATS\profiles", true, ["profiles"]),
                [$"{GameType.Ats}|D:\\ConfiguredEmpty"] = new(@"D:\ConfiguredEmpty", false, ["no saves"])
            });
        await presenter.LoadGameSourcesAsync();

        await presenter.StartSourceWizardAsync();

        var emptyCandidate = presenter.CurrentWizardGame!.SaveRootCandidates.Single(candidate => candidate.Path == @"D:\ConfiguredEmpty");
        Assert.False(emptyCandidate.CanSelect);
        Assert.False(emptyCandidate.IsSelected);

        emptyCandidate.IsSelected = true;

        Assert.False(emptyCandidate.IsSelected);

        var result = await presenter.FinishSourceWizardAsync();

        Assert.True(result.Succeeded);
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

    [Fact]
    public async Task FinishSourceWizardAsync_blocks_save_when_selected_saves_need_more_space_than_available()
    {
        var settings = new InMemoryGameSourceSettingsStore(new GameSourceSettings([]));
        var diskSpace = new StubDatabaseDiskSpaceService(
            new DatabaseDiskSpaceEstimate(
                SelectedSaveBytes: 1_000,
                ProjectedDatabaseBytes: 2_100,
                ExistingDatabaseBytes: 0,
                RequiredAdditionalBytes: 2_100,
                FreeBytes: 2_000,
                HasEnoughSpace: false));
        var confirmation = new StubSourceWizardConfirmation(true);
        var presenter = CreatePresenter(
            installations:
            [
                new GameInstallation(GameType.Ats, null, null, null, false)
            ],
            settingsStore: settings,
            candidates: new Dictionary<GameType, GameSourceCandidates>
            {
                [GameType.Ats] = new(
                    GameType.Ats,
                    [],
                    [new GameSaveRootCandidate(GameType.Ats, @"C:\ATS\profiles", true, 2, ["profiles"])]),
                [GameType.Ets2] = new(GameType.Ets2, [], [])
            },
            diskSpaceService: diskSpace,
            sourceWizardConfirmation: confirmation);
        await presenter.LoadGameSourcesAsync();
        await presenter.StartSourceWizardAsync();
        presenter.CurrentWizardGame!.HasGame = true;

        var result = await presenter.FinishSourceWizardAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("Building the Employee Database takes at least 2.1 KB", result.StatusText);
        Assert.Contains("needs 2.1 KB more free space", result.StatusText);
        Assert.Contains("You have 2.0 KB free", result.StatusText);
        Assert.Contains("free up space or remove old save games", result.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal([@"C:\ATS\profiles"], diskSpace.SaveRoots);
        Assert.Equal(0, confirmation.CallCount);
        Assert.Empty(settings.SavedSettings.Sources);
    }

    [Fact]
    public async Task FinishSourceWizardAsync_keeps_wizard_open_when_database_build_confirmation_is_cancelled()
    {
        var settings = new InMemoryGameSourceSettingsStore(new GameSourceSettings([]));
        var diskSpace = new StubDatabaseDiskSpaceService(
            new DatabaseDiskSpaceEstimate(
                SelectedSaveBytes: 1_000,
                ProjectedDatabaseBytes: 2_100,
                ExistingDatabaseBytes: 0,
                RequiredAdditionalBytes: 2_100,
                FreeBytes: 4_096,
                HasEnoughSpace: true));
        var confirmation = new StubSourceWizardConfirmation(false);
        var presenter = CreatePresenter(
            installations:
            [
                new GameInstallation(GameType.Ats, null, null, null, false)
            ],
            settingsStore: settings,
            candidates: new Dictionary<GameType, GameSourceCandidates>
            {
                [GameType.Ats] = new(
                    GameType.Ats,
                    [],
                    [new GameSaveRootCandidate(GameType.Ats, @"C:\ATS\profiles", true, 2, ["profiles"])]),
                [GameType.Ets2] = new(GameType.Ets2, [], [])
            },
            diskSpaceService: diskSpace,
            sourceWizardConfirmation: confirmation);
        await presenter.LoadGameSourcesAsync();
        await presenter.StartSourceWizardAsync();
        presenter.CurrentWizardGame!.HasGame = true;

        var result = await presenter.FinishSourceWizardAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Source setup was not saved.", result.StatusText);
        Assert.True(presenter.IsSourceWizardVisible);
        Assert.Equal(1, confirmation.CallCount);
        Assert.Same(diskSpace.EstimateResult, confirmation.Estimates.Single());
        Assert.Empty(settings.SavedSettings.Sources);
    }

    [Fact]
    public async Task FinishSourceWizardAsync_confirms_database_space_before_saving_selected_sources()
    {
        var settings = new InMemoryGameSourceSettingsStore(new GameSourceSettings([]));
        var diskSpace = new StubDatabaseDiskSpaceService(
            new DatabaseDiskSpaceEstimate(
                SelectedSaveBytes: 1_000,
                ProjectedDatabaseBytes: 2_100,
                ExistingDatabaseBytes: 0,
                RequiredAdditionalBytes: 2_100,
                FreeBytes: 4_096,
                HasEnoughSpace: true));
        var confirmation = new StubSourceWizardConfirmation(true);
        var presenter = CreatePresenter(
            installations:
            [
                new GameInstallation(GameType.Ats, null, null, null, false)
            ],
            settingsStore: settings,
            candidates: new Dictionary<GameType, GameSourceCandidates>
            {
                [GameType.Ats] = new(
                    GameType.Ats,
                    [],
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
            },
            diskSpaceService: diskSpace,
            sourceWizardConfirmation: confirmation);
        await presenter.LoadGameSourcesAsync();
        await presenter.StartSourceWizardAsync();
        presenter.CurrentWizardGame!.HasGame = true;

        var result = await presenter.FinishSourceWizardAsync();

        Assert.True(result.Succeeded);
        Assert.Equal([@"C:\ATS\profiles", @"C:\ATS\steam_profiles"], diskSpace.SaveRoots);
        Assert.Equal(1, confirmation.CallCount);
        Assert.Same(diskSpace.EstimateResult, confirmation.Estimates.Single());
        Assert.False(presenter.IsSourceWizardVisible);
        Assert.True(settings.SavedSettings.WizardCompleted);
    }

    private static GameSourcePresenter CreatePresenter(
        IReadOnlyList<GameInstallation> installations,
        GameSourceSettings? settings = null,
        InMemoryGameSourceSettingsStore? settingsStore = null,
        IReadOnlyList<SaveGame>? saves = null,
        IReadOnlyDictionary<GameType, GameSourceCandidates>? candidates = null,
        IReadOnlyDictionary<string, GamePathValidation>? validationResults = null,
        IBackgroundRunner? backgroundRunner = null,
        IDatabaseDiskSpaceService? diskSpaceService = null,
        ISourceWizardConfirmation? sourceWizardConfirmation = null)
    {
        var discovery = new StubGameSourceDiscovery(installations, candidates, validationResults);
        var store = settingsStore ?? new InMemoryGameSourceSettingsStore(settings ?? new GameSourceSettings([]));
        var sourceUseCase = new GameSourceManagementUseCase(discovery, store);
        var catalogUseCase = new GameSaveCatalogUseCase(new StubConfiguredGameSaveDiscovery(saves ?? []));
        return new GameSourcePresenter(
            sourceUseCase,
            catalogUseCase,
            backgroundRunner ?? new ImmediateBackgroundRunner(),
            diskSpaceService ?? new StubDatabaseDiskSpaceService(new DatabaseDiskSpaceEstimate(0, 0, 0, 0, long.MaxValue, true)),
            sourceWizardConfirmation ?? new StubSourceWizardConfirmation(true));
    }

    private sealed class RecordingBackgroundRunner : IBackgroundRunner
    {
        public int RunCount { get; private set; }

        public Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
        {
            RunCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(work());
        }

        public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default)
        {
            RunCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return await work();
        }
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

    private sealed class StubDatabaseDiskSpaceService(DatabaseDiskSpaceEstimate estimate) : IDatabaseDiskSpaceService
    {
        public DatabaseDiskSpaceEstimate EstimateResult { get; } = estimate;

        public IReadOnlyList<string> SaveRoots { get; private set; } = [];

        public DatabaseDiskSpaceEstimate Estimate(IReadOnlyList<string> saveRoots)
        {
            SaveRoots = saveRoots;
            return EstimateResult;
        }
    }

    private sealed class StubSourceWizardConfirmation(bool result) : ISourceWizardConfirmation
    {
        public int CallCount { get; private set; }

        public List<DatabaseDiskSpaceEstimate> Estimates { get; } = [];

        public bool ConfirmDatabaseBuild(DatabaseDiskSpaceEstimate estimate)
        {
            CallCount++;
            Estimates.Add(estimate);
            return result;
        }
    }
}
