using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Domain.Saves;

namespace AtsEmployeeStats.Tests;

public sealed class TerminalDashboardAppTests
{
    [Fact]
    public async Task RunAsync_renders_once_and_returns_when_key_input_is_not_available()
    {
        var service = new StatisticsService(new EmptySaveSnapshotSource());
        var app = new TerminalDashboardApp(service, Path.GetTempPath(), canReadKeys: () => false);

        await app.RunAsync(CancellationToken.None);
    }

    private sealed class EmptySaveSnapshotSource : ISaveSnapshotSource
    {
        public Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            Task.FromResult<IReadOnlyList<SaveSnapshot>>([]);
    }
}
