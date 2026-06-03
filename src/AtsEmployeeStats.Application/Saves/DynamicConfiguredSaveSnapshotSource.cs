using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Saves;

public sealed class DynamicConfiguredSaveSnapshotSource(
    Func<CancellationToken, Task<IReadOnlyList<GameSourceConfiguration>>> loadSources,
    Func<GameSourceConfiguration, ISaveSnapshotSource> createSource) : ISaveSnapshotSource, IStatisticsIngestor, IStatisticsQuerySource
{
    public async Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        var current = await CreateCurrentCompositeAsync(cancellationToken);
        return await current.ReadAllAsync(cancellationToken, progress);
    }

    public async Task IngestAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null,
        bool force = false)
    {
        var current = await CreateCurrentCompositeAsync(cancellationToken);
        await current.IngestAsync(cancellationToken, progress, force);
    }

    public async Task<AtsStatistics> ReadStatisticsAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        var current = await CreateCurrentCompositeAsync(cancellationToken);
        return await current.ReadStatisticsAsync(cancellationToken, progress);
    }

    private async Task<CompositeSaveSnapshotSource> CreateCurrentCompositeAsync(CancellationToken cancellationToken)
    {
        var sources = await loadSources(cancellationToken);
        var configured = sources
            .Where(source => source.Enabled)
            .SelectMany(ExpandSourcePaths)
            .Select(source => new ConfiguredSaveSnapshotSource(source.Game, source.Enabled, createSource(source)))
            .ToList();
        return new CompositeSaveSnapshotSource(configured);
    }

    private static IEnumerable<GameSourceConfiguration> ExpandSourcePaths(GameSourceConfiguration source)
    {
        if (source.EffectiveSavePaths.Count == 0)
        {
            yield return source;
            yield break;
        }

        foreach (var savePath in source.EffectiveSavePaths)
        {
            yield return source with
            {
                SavePath = savePath,
                SavePaths = [savePath]
            };
        }
    }
}
