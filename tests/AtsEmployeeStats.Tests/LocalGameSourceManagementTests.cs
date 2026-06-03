using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Infrastructure.Saves;

namespace AtsEmployeeStats.Tests;

public sealed class LocalGameSourceManagementTests
{
    [Fact]
    public async Task LocalGameSourceDiscovery_returns_ats_and_ets2_install_profile_and_save_candidates()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C:\\Steam\\steamapps\\common\\American Truck Simulator",
            "C:\\Steam\\steamapps\\common\\Euro Truck Simulator 2",
            "C:\\Users\\James\\Documents\\American Truck Simulator",
            "C:\\Users\\James\\Documents\\American Truck Simulator\\profiles",
            "C:\\Users\\James\\Documents\\Euro Truck Simulator 2",
            "C:\\Users\\James\\Documents\\Euro Truck Simulator 2\\steam_profiles"
        };
        var discovery = new LocalGameSourceDiscovery(
            documentsPath: "C:\\Users\\James\\Documents",
            steamPath: "C:\\Steam",
            directoryExists: existing.Contains);

        var sources = await discovery.DiscoverInstallationsAsync(CancellationToken.None);

        Assert.Collection(
            sources.OrderBy(source => source.Game),
            source =>
            {
                Assert.Equal(GameType.Ats, source.Game);
                Assert.Equal("C:\\Steam\\steamapps\\common\\American Truck Simulator", source.InstallPath);
                Assert.Equal("C:\\Users\\James\\Documents\\American Truck Simulator", source.ProfilePath);
                Assert.Equal("C:\\Users\\James\\Documents\\American Truck Simulator\\profiles", source.SavePath);
                Assert.True(source.Exists);
            },
            source =>
            {
                Assert.Equal(GameType.Ets2, source.Game);
                Assert.Equal("C:\\Steam\\steamapps\\common\\Euro Truck Simulator 2", source.InstallPath);
                Assert.Equal("C:\\Users\\James\\Documents\\Euro Truck Simulator 2", source.ProfilePath);
                Assert.Equal("C:\\Users\\James\\Documents\\Euro Truck Simulator 2\\steam_profiles", source.SavePath);
                Assert.True(source.Exists);
            });
    }

    [Fact]
    public async Task LocalGameSourceDiscovery_checks_non_default_steam_libraries_for_install_paths()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C:\\Steam\\steamapps\\common\\American Truck Simulator",
            "D:\\SteamLibrary\\steamapps\\common\\Euro Truck Simulator 2",
            "C:\\Users\\James\\Documents\\American Truck Simulator",
            "C:\\Users\\James\\Documents\\Euro Truck Simulator 2"
        };
        var discovery = new LocalGameSourceDiscovery(
            documentsPath: "C:\\Users\\James\\Documents",
            steamLibraryPaths: ["C:\\Steam", "D:\\SteamLibrary"],
            directoryExists: existing.Contains);

        var sources = await discovery.DiscoverInstallationsAsync(CancellationToken.None);

        Assert.Contains(sources, source =>
            source.Game == GameType.Ats &&
            source.InstallPath == "C:\\Steam\\steamapps\\common\\American Truck Simulator" &&
            source.Exists);
        Assert.Contains(sources, source =>
            source.Game == GameType.Ets2 &&
            source.InstallPath == "D:\\SteamLibrary\\steamapps\\common\\Euro Truck Simulator 2" &&
            source.Exists);
    }

    [Fact]
    public void LocalSteamPath_reads_libraryfolders_vdf_paths()
    {
        var steamRoot = Path.Combine(Path.GetTempPath(), "AtsEmployeeStatsTests", Guid.NewGuid().ToString("N"), "Steam");
        var steamApps = Path.Combine(steamRoot, "steamapps");
        Directory.CreateDirectory(steamApps);
        File.WriteAllText(Path.Combine(steamApps, "libraryfolders.vdf"), """
            "libraryfolders"
            {
                "0"
                {
                    "path" "C:\\Steam"
                }
                "1"
                {
                    "path" "D:\\SteamLibrary"
                }
            }
            """);

        var libraries = LocalSteamPath.FindLibraries(steamRoot);

        Assert.Contains(steamRoot, libraries);
        Assert.Contains("C:\\Steam", libraries);
        Assert.Contains("D:\\SteamLibrary", libraries);
    }

    [Fact]
    public async Task JsonGameSourceSettingsStore_round_trips_source_configuration()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AtsEmployeeStatsTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "game-sources.json");
        var store = new JsonGameSourceSettingsStore(path);
        var settings = new GameSourceSettings([
            new GameSourceConfiguration(GameType.Ats, true, "C:\\ATS", "C:\\ATSProfile", "C:\\ATSSaves"),
            new GameSourceConfiguration(GameType.Ets2, false, "C:\\ETS2", "C:\\ETS2Profile", "C:\\ETS2Saves")
        ]);

        await store.SaveAsync(settings, CancellationToken.None);
        var reloaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(settings.Sources, reloaded.Sources);
    }

    [Fact]
    public async Task SqliteGameSourceSettingsStore_round_trips_sources_and_wizard_completion()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AtsEmployeeStatsTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.db");
        var store = new SqliteGameSourceSettingsStore(path);
        var settings = new GameSourceSettings(
            [
                new GameSourceConfiguration(
                    GameType.Ats,
                    true,
                    "C:\\ATS",
                    "C:\\ATSProfile",
                    "C:\\ATSProfile\\profiles",
                    ["C:\\ATSProfile\\profiles", "C:\\ATSProfile\\steam_profiles"]),
                new GameSourceConfiguration(
                    GameType.Ets2,
                    false,
                    "C:\\ETS2",
                    "C:\\ETS2Profile",
                    null,
                    [])
            ],
            WizardCompleted: true);

        await store.SaveAsync(settings, CancellationToken.None);
        var reloaded = await store.LoadAsync(CancellationToken.None);

        Assert.True(reloaded.WizardCompleted);
        Assert.Collection(
            reloaded.Sources,
            source =>
            {
                Assert.Equal(GameType.Ats, source.Game);
                Assert.True(source.Enabled);
                Assert.Equal("C:\\ATS", source.InstallPath);
                Assert.Equal("C:\\ATSProfile", source.ProfilePath);
                Assert.Equal("C:\\ATSProfile\\profiles", source.SavePath);
                Assert.Equal(["C:\\ATSProfile\\profiles", "C:\\ATSProfile\\steam_profiles"], source.EffectiveSavePaths);
            },
            source =>
            {
                Assert.Equal(GameType.Ets2, source.Game);
                Assert.False(source.Enabled);
                Assert.Equal("C:\\ETS2", source.InstallPath);
                Assert.Equal("C:\\ETS2Profile", source.ProfilePath);
                Assert.Empty(source.EffectiveSavePaths);
            });
    }

    [Fact]
    public async Task LocalGameSourceDiscovery_returns_install_and_all_save_root_candidates_with_proof()
    {
        var existingDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C:\\Steam\\steamapps\\common\\American Truck Simulator",
            "C:\\Steam\\steamapps\\common\\American Truck Simulator\\bin",
            "C:\\Users\\James\\Documents\\American Truck Simulator",
            "C:\\Users\\James\\Documents\\American Truck Simulator\\profiles",
            "C:\\Users\\James\\Documents\\American Truck Simulator\\steam_profiles"
        };
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C:\\Users\\James\\Documents\\American Truck Simulator\\profiles\\506C61796572\\save\\autosave\\game.sii",
            "C:\\Users\\James\\Documents\\American Truck Simulator\\steam_profiles\\76561198000000000\\save\\manual\\game.sii"
        };
        var discovery = new LocalGameSourceDiscovery(
            documentsPath: "C:\\Users\\James\\Documents",
            steamPath: "C:\\Steam",
            directoryExists: existingDirectories.Contains,
            enumerateFiles: (root, pattern) => files.Where(file => file.StartsWith(root, StringComparison.OrdinalIgnoreCase)));

        var candidates = await discovery.DiscoverCandidatesAsync(GameType.Ats, CancellationToken.None);

        Assert.Contains(candidates.InstallCandidates, candidate =>
            candidate.Path == "C:\\Steam\\steamapps\\common\\American Truck Simulator" &&
            candidate.IsValid &&
            candidate.Proofs.Contains("Install folder exists"));
        Assert.Collection(
            candidates.SaveRootCandidates.OrderBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase),
            candidate =>
            {
                Assert.Equal("C:\\Users\\James\\Documents\\American Truck Simulator\\profiles", candidate.Path);
                Assert.True(candidate.IsValid);
                Assert.Equal(1, candidate.SaveFileCount);
                Assert.Contains("game.sii files found: 1", candidate.Proofs);
            },
            candidate =>
            {
                Assert.Equal("C:\\Users\\James\\Documents\\American Truck Simulator\\steam_profiles", candidate.Path);
                Assert.True(candidate.IsValid);
                Assert.Equal(1, candidate.SaveFileCount);
                Assert.Contains("game.sii files found: 1", candidate.Proofs);
            });
    }

    [Fact]
    public async Task LocalGameSourceDiscovery_returns_steam_userdata_remote_profile_candidates()
    {
        var existingDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C:\\Steam\\userdata",
            "C:\\Steam\\userdata\\1617809",
            "C:\\Steam\\userdata\\1617809\\270880\\remote\\profiles"
        };
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C:\\Steam\\userdata\\1617809\\270880\\remote\\profiles\\7467636974777C5061726E656C6C\\save\\autosave\\game.sii"
        };
        var discovery = new LocalGameSourceDiscovery(
            documentsPath: null,
            steamLibraryPaths: ["C:\\Steam"],
            directoryExists: existingDirectories.Contains,
            enumerateFiles: (root, _) => files.Where(file => file.StartsWith(root, StringComparison.OrdinalIgnoreCase)),
            enumerateDirectories: root => root == "C:\\Steam\\userdata" ? ["C:\\Steam\\userdata\\1617809"] : []);

        var candidates = await discovery.DiscoverCandidatesAsync(GameType.Ats, CancellationToken.None);

        var candidate = Assert.Single(candidates.SaveRootCandidates, candidate => candidate.IsValid);
        Assert.Equal("C:\\Steam\\userdata\\1617809\\270880\\remote\\profiles", candidate.Path);
        Assert.True(candidate.IsValid);
        Assert.Equal(1, candidate.SaveFileCount);
    }

    [Fact]
    public async Task LocalGameSourceDiscovery_canonicalizes_paths_before_deduping_candidates()
    {
        var existingDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C:\\Steam\\userdata",
            "C:\\Steam\\userdata\\1617809",
            "C:\\Steam\\userdata\\1617809\\270880\\remote\\profiles"
        };
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C:\\Steam\\userdata\\1617809\\270880\\remote\\profiles\\7467636974777C5061726E656C6C\\save\\autosave\\game.sii"
        };
        var discovery = new LocalGameSourceDiscovery(
            documentsPath: null,
            steamLibraryPaths: ["c:/Steam", "C:\\Steam"],
            directoryExists: existingDirectories.Contains,
            enumerateFiles: (root, _) => files.Where(file => file.StartsWith(root, StringComparison.OrdinalIgnoreCase)),
            enumerateDirectories: root => root == "C:\\Steam\\userdata" ? ["c:/Steam\\userdata\\1617809"] : []);

        var candidates = await discovery.DiscoverCandidatesAsync(GameType.Ats, CancellationToken.None);

        Assert.Single(candidates.InstallCandidates);
        var candidate = Assert.Single(candidates.SaveRootCandidates, candidate => candidate.IsValid);
        Assert.Equal("C:\\Steam\\userdata\\1617809\\270880\\remote\\profiles", candidate.Path);
        Assert.DoesNotContain('/', candidate.Path);
    }
}
