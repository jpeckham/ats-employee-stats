using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Tests;

public sealed class GameSaveDiscoveryUseCaseTests
{
    [Fact]
    public async Task FindSaveRootsAsync_returns_existing_roots_for_requested_game()
    {
        var useCase = new GameSaveDiscoveryUseCase(new StubGameSaveDiscovery(
            new Dictionary<GameSaveKind, IReadOnlyList<GameSaveRoot>>
            {
                [GameSaveKind.AmericanTruckSimulator] =
                [
                    new GameSaveRoot(GameSaveKind.AmericanTruckSimulator, "C:\\ATS\\profiles", true),
                    new GameSaveRoot(GameSaveKind.AmericanTruckSimulator, "C:\\ATS\\missing", false)
                ],
                [GameSaveKind.EuroTruckSimulator2] =
                [
                    new GameSaveRoot(GameSaveKind.EuroTruckSimulator2, "C:\\ETS2\\profiles", true)
                ]
            }));

        var roots = await useCase.FindSaveRootsAsync(GameSaveKind.AmericanTruckSimulator, CancellationToken.None);

        var root = Assert.Single(roots);
        Assert.Equal(GameSaveKind.AmericanTruckSimulator, root.Game);
        Assert.Equal("C:\\ATS\\profiles", root.Path);
    }

    [Fact]
    public async Task FindFirstSaveRootAsync_returns_null_when_no_existing_root_is_found()
    {
        var useCase = new GameSaveDiscoveryUseCase(new StubGameSaveDiscovery(
            new Dictionary<GameSaveKind, IReadOnlyList<GameSaveRoot>>
            {
                [GameSaveKind.AmericanTruckSimulator] =
                [
                    new GameSaveRoot(GameSaveKind.AmericanTruckSimulator, "C:\\ATS\\missing", false)
                ]
            }));

        Assert.Null(await useCase.FindFirstSaveRootAsync(GameSaveKind.AmericanTruckSimulator, CancellationToken.None));
    }

    private sealed class StubGameSaveDiscovery(
        IReadOnlyDictionary<GameSaveKind, IReadOnlyList<GameSaveRoot>> roots) : IGameSaveDiscovery
    {
        public Task<IReadOnlyList<GameSaveRoot>> FindCandidateRootsAsync(
            GameSaveKind game,
            CancellationToken cancellationToken) =>
            Task.FromResult(roots.TryGetValue(game, out var value) ? value : []);
    }
}
