using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Statistics;

public sealed class StatisticsService(ISaveSnapshotSource saveSnapshotSource)
{
    public async Task<AtsStatistics> LoadAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        var snapshots = await saveSnapshotSource.ReadAllAsync(cancellationToken, progress);
        return StatisticsProjection.Build(snapshots);
    }
}
