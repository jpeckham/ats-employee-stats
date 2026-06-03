using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Saves;

public sealed record ConfiguredSaveSnapshotSource(
    GameType Game,
    bool Enabled,
    ISaveSnapshotSource Source);

public sealed class CompositeSaveSnapshotSource(
    IReadOnlyList<ConfiguredSaveSnapshotSource> sources) : ISaveSnapshotSource, IStatisticsIngestor, IStatisticsQuerySource
{
    public async Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        var snapshots = new List<SaveSnapshot>();
        foreach (var configured in sources.Where(source => source.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            snapshots.AddRange(await configured.Source.ReadAllAsync(cancellationToken, progress));
        }

        return snapshots
            .OrderByDescending(snapshot => snapshot.LastWritten)
            .ToList();
    }

    public async Task IngestAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null,
        bool force = false)
    {
        foreach (var configured in sources.Where(source => source.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (configured.Source is IStatisticsIngestor ingestor)
                await ingestor.IngestAsync(cancellationToken, progress, force);
        }
    }

    public async Task<AtsStatistics> ReadStatisticsAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        var querySource = sources
            .Where(source => source.Enabled)
            .Select(source => source.Source)
            .OfType<IStatisticsQuerySource>()
            .FirstOrDefault();
        if (querySource is not null)
            return await querySource.ReadStatisticsAsync(cancellationToken, progress);

        return StatisticsProjection.Build(await ReadAllAsync(cancellationToken, progress));
    }
}
