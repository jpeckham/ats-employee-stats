using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class StatisticsIngestUseCaseTests
{
    [Fact]
    public async Task IngestAsync_runs_non_forced_ingestion_by_default()
    {
        var source = new RecordingIngestSource();
        var useCase = new StatisticsIngestUseCase(new StatisticsService(source));

        await useCase.IngestAsync(CancellationToken.None);

        Assert.True(source.Called);
        Assert.False(source.ForceReceived);
    }

    [Fact]
    public async Task IngestAsync_can_force_ingestion()
    {
        var source = new RecordingIngestSource();
        var useCase = new StatisticsIngestUseCase(new StatisticsService(source));

        await useCase.IngestAsync(CancellationToken.None, force: true);

        Assert.True(source.ForceReceived);
    }

    private sealed class RecordingIngestSource : ISaveSnapshotSource, IStatisticsIngestor
    {
        public bool Called { get; private set; }
        public bool ForceReceived { get; private set; }

        public Task IngestAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null,
            bool force = false)
        {
            Called = true;
            ForceReceived = force;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            Task.FromResult<IReadOnlyList<SaveSnapshot>>([]);
    }
}
