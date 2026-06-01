using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Tests;

public sealed class GameSaveFileDiscoveryUseCaseTests
{
    [Fact]
    public async Task FindSaveFilesAsync_returns_existing_save_files_for_requested_game()
    {
        var useCase = new GameSaveFileDiscoveryUseCase(new StubGameSaveFileDiscovery(
            new Dictionary<GameSaveKind, IReadOnlyList<GameSaveFile>>
            {
                [GameSaveKind.AmericanTruckSimulator] =
                [
                    new GameSaveFile(GameSaveKind.AmericanTruckSimulator, "C:\\ATS\\profile\\save\\game.sii", true),
                    new GameSaveFile(GameSaveKind.AmericanTruckSimulator, "C:\\ATS\\profile\\save\\missing.sii", false)
                ],
                [GameSaveKind.EuroTruckSimulator2] =
                [
                    new GameSaveFile(GameSaveKind.EuroTruckSimulator2, "C:\\ETS2\\profile\\save\\game.sii", true)
                ]
            }));

        var files = await useCase.FindSaveFilesAsync(GameSaveKind.AmericanTruckSimulator, CancellationToken.None);

        var file = Assert.Single(files);
        Assert.Equal(GameSaveKind.AmericanTruckSimulator, file.Game);
        Assert.Equal("C:\\ATS\\profile\\save\\game.sii", file.Path);
    }

    [Fact]
    public async Task FindFirstSaveFileAsync_returns_null_when_no_existing_file_is_found()
    {
        var useCase = new GameSaveFileDiscoveryUseCase(new StubGameSaveFileDiscovery(
            new Dictionary<GameSaveKind, IReadOnlyList<GameSaveFile>>
            {
                [GameSaveKind.AmericanTruckSimulator] =
                [
                    new GameSaveFile(GameSaveKind.AmericanTruckSimulator, "C:\\ATS\\profile\\save\\missing.sii", false)
                ]
            }));

        Assert.Null(await useCase.FindFirstSaveFileAsync(GameSaveKind.AmericanTruckSimulator, CancellationToken.None));
    }

    private sealed class StubGameSaveFileDiscovery(
        IReadOnlyDictionary<GameSaveKind, IReadOnlyList<GameSaveFile>> files) : IGameSaveFileDiscovery
    {
        public Task<IReadOnlyList<GameSaveFile>> FindCandidateSaveFilesAsync(
            GameSaveKind game,
            CancellationToken cancellationToken) =>
            Task.FromResult(files.TryGetValue(game, out var value) ? value : []);
    }
}
