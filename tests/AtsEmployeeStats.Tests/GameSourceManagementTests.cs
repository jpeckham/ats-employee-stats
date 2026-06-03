using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class GameSourceManagementTests
{
    [Fact]
    public async Task DiscoverAsync_returns_enabled_ats_and_ets2_sources_with_profile_and_save_paths()
    {
        var discovery = new StubGameSourceDiscovery([
            new GameInstallation(GameType.Ats, "C:\\Steam\\steamapps\\common\\American Truck Simulator", "C:\\Docs\\American Truck Simulator", "C:\\Docs\\American Truck Simulator\\profiles", true),
            new GameInstallation(GameType.Ets2, "C:\\Steam\\steamapps\\common\\Euro Truck Simulator 2", "C:\\Docs\\Euro Truck Simulator 2", "C:\\Docs\\Euro Truck Simulator 2\\profiles", true)
        ]);
        var settings = new InMemoryGameSourceSettingsStore(new GameSourceSettings([]));
        var useCase = new GameSourceManagementUseCase(discovery, settings);

        var sources = await useCase.DiscoverAsync(CancellationToken.None);

        Assert.Collection(
            sources.OrderBy(source => source.Game),
            source =>
            {
                Assert.Equal(GameType.Ats, source.Game);
                Assert.True(source.Enabled);
                Assert.Equal("C:\\Steam\\steamapps\\common\\American Truck Simulator", source.InstallPath);
                Assert.Equal("C:\\Docs\\American Truck Simulator", source.ProfilePath);
                Assert.Equal("C:\\Docs\\American Truck Simulator\\profiles", source.SavePath);
            },
            source =>
            {
                Assert.Equal(GameType.Ets2, source.Game);
                Assert.True(source.Enabled);
                Assert.Equal("C:\\Steam\\steamapps\\common\\Euro Truck Simulator 2", source.InstallPath);
                Assert.Equal("C:\\Docs\\Euro Truck Simulator 2", source.ProfilePath);
                Assert.Equal("C:\\Docs\\Euro Truck Simulator 2\\profiles", source.SavePath);
            });
    }

    [Fact]
    public async Task SaveAsync_persists_user_overrides_and_disabled_sources()
    {
        var settings = new InMemoryGameSourceSettingsStore(new GameSourceSettings([]));
        var useCase = new GameSourceManagementUseCase(new StubGameSourceDiscovery([]), settings);
        var configured = new GameSourceConfiguration(
            GameType.Ets2,
            Enabled: false,
            InstallPath: "D:\\Games\\ETS2",
            ProfilePath: "D:\\Profiles\\ETS2",
            SavePath: "D:\\Profiles\\ETS2\\steam_profiles");

        await useCase.SaveAsync([configured], CancellationToken.None);

        var saved = Assert.Single((await settings.LoadAsync(CancellationToken.None)).Sources);
        Assert.Equal(configured, saved);
    }

    [Fact]
    public async Task RequiresWizardAsync_is_true_until_completed_settings_exist()
    {
        var settings = new InMemoryGameSourceSettingsStore(new GameSourceSettings([]));
        var useCase = new GameSourceManagementUseCase(new StubGameSourceDiscovery([]), settings);

        Assert.True(await useCase.RequiresWizardAsync(CancellationToken.None));

        await settings.SaveAsync(
            new GameSourceSettings(
                [new GameSourceConfiguration(GameType.Ats, true, "C:\\ATS", "C:\\Docs\\ATS", "C:\\Docs\\ATS\\profiles")],
                WizardCompleted: true),
            CancellationToken.None);

        Assert.False(await useCase.RequiresWizardAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SaveValidatedAsync_blocks_enabled_sources_without_valid_save_roots()
    {
        var settings = new InMemoryGameSourceSettingsStore(new GameSourceSettings([]));
        var useCase = new GameSourceManagementUseCase(
            new StubGameSourceDiscovery(
                [],
                validationResults: new Dictionary<string, GamePathValidation>(StringComparer.OrdinalIgnoreCase)
                {
                    ["C:\\Missing"] = new("C:\\Missing", false, ["Path does not exist"])
                }),
            settings);

        var result = await useCase.SaveValidatedAsync(
            [
                new GameSourceConfiguration(
                    GameType.Ats,
                    true,
                    "C:\\ATS",
                    "C:\\Docs\\ATS",
                    "C:\\Missing",
                    ["C:\\Missing"])
            ],
            CancellationToken.None);

        Assert.False(result.Saved);
        Assert.Contains(result.Errors, error => error.Contains("ATS", StringComparison.OrdinalIgnoreCase));
        Assert.Empty((await settings.LoadAsync(CancellationToken.None)).Sources);
    }

    [Fact]
    public async Task SaveValidatedAsync_persists_valid_multi_root_sources_as_completed()
    {
        var settings = new InMemoryGameSourceSettingsStore(new GameSourceSettings([]));
        var useCase = new GameSourceManagementUseCase(
            new StubGameSourceDiscovery(
                [],
                validationResults: new Dictionary<string, GamePathValidation>(StringComparer.OrdinalIgnoreCase)
                {
                    ["C:\\Profiles"] = new("C:\\Profiles", true, ["game.sii files found: 1"]),
                    ["C:\\SteamProfiles"] = new("C:\\SteamProfiles", true, ["game.sii files found: 2"])
                }),
            settings);
        var source = new GameSourceConfiguration(
            GameType.Ats,
            true,
            "C:\\ATS",
            "C:\\Docs\\ATS",
            "C:\\Profiles",
            ["C:\\Profiles", "C:\\SteamProfiles"]);

        var result = await useCase.SaveValidatedAsync([source], CancellationToken.None);

        Assert.True(result.Saved);
        var saved = await settings.LoadAsync(CancellationToken.None);
        Assert.True(saved.WizardCompleted);
        var savedSource = Assert.Single(saved.Sources);
        Assert.Equal(["C:\\Profiles", "C:\\SteamProfiles"], savedSource.EffectiveSavePaths);
    }

    [Fact]
    public async Task CompositeSaveSnapshotSource_ingests_each_enabled_source_and_skips_disabled_sources()
    {
        var ats = new StubSource();
        var ets2 = new StubSource();
        var disabled = new StubSource();
        var source = new CompositeSaveSnapshotSource([
            new ConfiguredSaveSnapshotSource(GameType.Ats, Enabled: true, ats),
            new ConfiguredSaveSnapshotSource(GameType.Ets2, Enabled: true, ets2),
            new ConfiguredSaveSnapshotSource(GameType.Ets2, Enabled: false, disabled)
        ]);

        await source.IngestAsync(CancellationToken.None, force: true);

        Assert.Equal(1, ats.IngestCount);
        Assert.True(ats.ForceValues.Single());
        Assert.Equal(1, ets2.IngestCount);
        Assert.Equal(0, disabled.IngestCount);
    }

    [Fact]
    public async Task DynamicConfiguredSaveSnapshotSource_uses_latest_source_configuration_for_each_import()
    {
        IReadOnlyList<GameSourceConfiguration> currentSources =
        [
            new GameSourceConfiguration(GameType.Ats, true, "C:\\OldATS", "C:\\OldProfile", "C:\\OldSaves")
        ];
        var createdSavePaths = new List<string?>();
        var source = new DynamicConfiguredSaveSnapshotSource(
            loadSources: _ => Task.FromResult(currentSources),
            createSource: configuration =>
            {
                createdSavePaths.Add(configuration.SavePath);
                return new StubSource();
            });

        await source.IngestAsync(CancellationToken.None);
        currentSources =
        [
            new GameSourceConfiguration(GameType.Ats, true, "D:\\NewATS", "D:\\NewProfile", "D:\\NewSaves")
        ];
        await source.IngestAsync(CancellationToken.None);

        Assert.Equal(["C:\\OldSaves", "D:\\NewSaves"], createdSavePaths);
    }

    [Fact]
    public async Task DynamicConfiguredSaveSnapshotSource_reads_persisted_statistics_without_reloading_all_snapshots()
    {
        var statistics = new AtsStatistics(DateTimeOffset.UtcNow, []);
        var querySource = new StubSource(statistics);
        var source = new DynamicConfiguredSaveSnapshotSource(
            loadSources: _ => Task.FromResult<IReadOnlyList<GameSourceConfiguration>>(
            [
                new GameSourceConfiguration(GameType.Ats, true, null, null, "C:\\Saves")
            ]),
            createSource: _ => querySource);

        var loaded = await ((IStatisticsQuerySource)source).ReadStatisticsAsync(CancellationToken.None);

        Assert.Same(statistics, loaded);
        Assert.Equal(0, querySource.ReadAllCount);
        Assert.Equal(1, querySource.ReadStatisticsCount);
    }

    private sealed class StubGameSourceDiscovery(
        IReadOnlyList<GameInstallation> installations,
        IReadOnlyDictionary<string, GamePathValidation>? validationResults = null) : IGameSourceDiscovery
    {
        public Task<IReadOnlyList<GameInstallation>> DiscoverInstallationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(installations);

        public Task<GameSourceCandidates> DiscoverCandidatesAsync(GameType game, CancellationToken cancellationToken) =>
            Task.FromResult(new GameSourceCandidates(game, [], []));

        public Task<GamePathValidation> ValidateSaveRootAsync(GameType game, string path, CancellationToken cancellationToken) =>
            Task.FromResult(validationResults is not null && validationResults.TryGetValue(path, out var validation)
                ? validation
                : new GamePathValidation(path, false, ["Path does not exist"]));
    }

    private sealed class InMemoryGameSourceSettingsStore(GameSourceSettings settings) : IGameSourceSettingsStore
    {
        private GameSourceSettings _settings = settings;

        public Task<GameSourceSettings> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_settings);

        public Task SaveAsync(GameSourceSettings settings, CancellationToken cancellationToken)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSource : ISaveSnapshotSource, IStatisticsIngestor, IStatisticsQuerySource
    {
        private readonly AtsStatistics _statistics;

        public StubSource()
            : this(new AtsStatistics(null, []))
        {
        }

        public StubSource(AtsStatistics statistics)
        {
            _statistics = statistics;
        }

        public int IngestCount { get; private set; }
        public int ReadAllCount { get; private set; }
        public int ReadStatisticsCount { get; private set; }
        public List<bool> ForceValues { get; } = [];

        public Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null)
        {
            ReadAllCount++;
            return Task.FromResult<IReadOnlyList<SaveSnapshot>>([]);
        }

        public Task<AtsStatistics> ReadStatisticsAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null)
        {
            ReadStatisticsCount++;
            return Task.FromResult(_statistics);
        }

        public Task IngestAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null,
            bool force = false)
        {
            IngestCount++;
            ForceValues.Add(force);
            return Task.CompletedTask;
        }
    }
}
