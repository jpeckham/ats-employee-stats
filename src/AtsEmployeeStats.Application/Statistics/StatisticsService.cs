using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Statistics;

public sealed class StatisticsService(ISaveSnapshotSource saveSnapshotSource)
{
    private readonly SemaphoreSlim _ingestionLock = new(1, 1);

    public async Task IngestAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        if (saveSnapshotSource is not IStatisticsIngestor ingestor)
            return;

        await _ingestionLock.WaitAsync(cancellationToken);
        try
        {
            await ingestor.IngestAsync(cancellationToken, progress);
        }
        finally
        {
            _ingestionLock.Release();
        }
    }

    public async Task<AtsStatistics> LoadAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        if (saveSnapshotSource is IStatisticsQuerySource querySource)
        {
            return await querySource.ReadStatisticsAsync(cancellationToken, progress);
        }

        var snapshots = await saveSnapshotSource.ReadAllAsync(cancellationToken, progress);
        return StatisticsProjection.Build(snapshots);
    }
}
