using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Infrastructure.Saves;

namespace AtsEmployeeStats.Tests;

public sealed class GameSaveCatalogTests
{
    [Fact]
    public async Task FindSaveGamesAsync_returns_save_games_for_enabled_configured_sources()
    {
        var discovery = new StubConfiguredGameSaveDiscovery(
            new Dictionary<GameType, IReadOnlyList<SaveGame>>
            {
                [GameType.Ats] =
                [
                    new SaveGame(
                        "Ats:506C61796572:autosave",
                        GameType.Ats,
                        "Player",
                        "autosave",
                        "C:\\ATS\\profiles\\506C61796572\\save\\autosave",
                        "Ats:506C61796572:autosave")
                ],
                [GameType.Ets2] =
                [
                    new SaveGame(
                        "Ets2:45545332:manual",
                        GameType.Ets2,
                        "ETS2",
                        "manual",
                        "C:\\ETS2\\profiles\\45545332\\save\\manual",
                        "Ets2:45545332:manual")
                ]
            });
        var useCase = new GameSaveCatalogUseCase(discovery);

        var saves = await useCase.FindSaveGamesAsync(
            [
                new GameSourceConfiguration(GameType.Ats, true, null, "C:\\ATS", "C:\\ATS\\profiles"),
                new GameSourceConfiguration(GameType.Ets2, true, null, "C:\\ETS2", "C:\\ETS2\\profiles"),
                new GameSourceConfiguration(GameType.Ats, false, null, null, "C:\\Disabled")
            ],
            CancellationToken.None);

        Assert.Collection(
            saves.OrderBy(save => save.Game).ThenBy(save => save.SaveName),
            save =>
            {
                Assert.Equal(GameType.Ats, save.Game);
                Assert.Equal("Player", save.ProfileName);
                Assert.Equal("autosave", save.SaveName);
                Assert.Equal("Ats:506C61796572:autosave", save.SourceKey);
            },
            save =>
            {
                Assert.Equal(GameType.Ets2, save.Game);
                Assert.Equal("ETS2", save.ProfileName);
                Assert.Equal("manual", save.SaveName);
                Assert.Equal("Ets2:45545332:manual", save.SourceKey);
            });
    }

    [Fact]
    public async Task LocalConfiguredGameSaveDiscovery_maps_game_sii_files_to_save_games()
    {
        var discovery = new LocalConfiguredGameSaveDiscovery(
            enumerateFiles: (path, pattern) => path == "C:\\ATS\\profiles" && pattern == "game.sii"
                ? ["C:\\ATS\\profiles\\506C61796572\\save\\autosave\\game.sii"]
                : []);
        var source = new GameSourceConfiguration(GameType.Ats, true, null, "C:\\ATS", "C:\\ATS\\profiles");

        var saves = await discovery.FindSaveGamesAsync(source, CancellationToken.None);

        var save = Assert.Single(saves);
        Assert.Equal("Ats:506C61796572:autosave", save.SaveGameId);
        Assert.Equal(GameType.Ats, save.Game);
        Assert.Equal("Player", save.ProfileName);
        Assert.Equal("autosave", save.SaveName);
        Assert.Equal("C:\\ATS\\profiles\\506C61796572\\save\\autosave", save.SaveDirectory);
        Assert.Equal("Ats:C:\\ATS\\profiles", save.SourceKey);
    }

    [Fact]
    public async Task LocalConfiguredGameSaveDiscovery_uses_one_source_key_per_selected_save_root_not_per_save_slot()
    {
        var discovery = new LocalConfiguredGameSaveDiscovery(
            enumerateFiles: (path, pattern) => path == "C:\\ATS\\profiles" && pattern == "game.sii"
                ?
                [
                    "C:\\ATS\\profiles\\506C61796572\\save\\autosave\\game.sii",
                    "C:\\ATS\\profiles\\506C61796572\\save\\manual\\game.sii"
                ]
                : []);
        var source = new GameSourceConfiguration(GameType.Ats, true, null, "C:\\ATS", "C:\\ATS\\profiles");

        var saves = await discovery.FindSaveGamesAsync(source, CancellationToken.None);

        Assert.Equal(2, saves.Count);
        Assert.All(saves, save => Assert.Equal("Ats:C:\\ATS\\profiles", save.SourceKey));
        Assert.Equal(2, saves.Select(save => save.SaveGameId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task LocalConfiguredGameSaveDiscovery_reads_all_selected_save_roots()
    {
        var discovery = new LocalConfiguredGameSaveDiscovery(
            enumerateFiles: (path, _) => path switch
            {
                "C:\\ATS\\profiles" => ["C:\\ATS\\profiles\\506C61796572\\save\\autosave\\game.sii"],
                "C:\\ATS\\steam_profiles" => ["C:\\ATS\\steam_profiles\\76561198000000000\\save\\manual\\game.sii"],
                _ => []
            });
        var source = new GameSourceConfiguration(
            GameType.Ats,
            true,
            null,
            "C:\\ATS",
            "C:\\ATS\\profiles",
            ["C:\\ATS\\profiles", "C:\\ATS\\steam_profiles"]);

        var saves = await discovery.FindSaveGamesAsync(source, CancellationToken.None);

        Assert.Collection(
            saves.OrderBy(save => save.SaveName, StringComparer.OrdinalIgnoreCase),
            save => Assert.Equal("autosave", save.SaveName),
            save => Assert.Equal("manual", save.SaveName));
    }

    private sealed class StubConfiguredGameSaveDiscovery(
        IReadOnlyDictionary<GameType, IReadOnlyList<SaveGame>> saves) : IConfiguredGameSaveDiscovery
    {
        public Task<IReadOnlyList<SaveGame>> FindSaveGamesAsync(
            GameSourceConfiguration source,
            CancellationToken cancellationToken) =>
            Task.FromResult(source.Enabled && saves.TryGetValue(source.Game, out var value) ? value : []);
    }
}
