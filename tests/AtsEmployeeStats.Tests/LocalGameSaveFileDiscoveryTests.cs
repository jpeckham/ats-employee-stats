using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Infrastructure.Saves;

namespace AtsEmployeeStats.Tests;

public sealed class LocalGameSaveFileDiscoveryTests
{
    [Fact]
    public async Task FindCandidateSaveFilesAsync_enumerates_game_sii_files_under_existing_roots()
    {
        var roots = new[]
        {
            new GameSaveRoot(GameSaveKind.AmericanTruckSimulator, "C:\\ATS\\profiles", true),
            new GameSaveRoot(GameSaveKind.AmericanTruckSimulator, "C:\\ATS\\missing", false)
        };
        var discovery = new LocalGameSaveFileDiscovery(
            new StubGameSaveDiscovery(roots),
            enumerateFiles: (path, pattern) => path == "C:\\ATS\\profiles" && pattern == "game.sii"
                ? ["C:\\ATS\\profiles\\profile1\\save\\game.sii"]
                : [],
            fileExists: path => path.EndsWith("game.sii", StringComparison.OrdinalIgnoreCase));

        var files = await discovery.FindCandidateSaveFilesAsync(GameSaveKind.AmericanTruckSimulator, CancellationToken.None);

        var file = Assert.Single(files);
        Assert.Equal("C:\\ATS\\profiles\\profile1\\save\\game.sii", file.Path);
        Assert.True(file.Exists);
    }

    private sealed class StubGameSaveDiscovery(IReadOnlyList<GameSaveRoot> roots) : IGameSaveDiscovery
    {
        public Task<IReadOnlyList<GameSaveRoot>> FindCandidateRootsAsync(
            GameSaveKind game,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSaveRoot>>(
                roots.Where(root => root.Game == game).ToList());
    }
}
