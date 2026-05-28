using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Statistics;

public sealed class StatisticsService(ISaveSnapshotSource saveSnapshotSource)
{
    public async Task IngestAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        if (saveSnapshotSource is IStatisticsIngestor ingestor)
        {
            await ingestor.IngestAsync(cancellationToken, progress);
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
