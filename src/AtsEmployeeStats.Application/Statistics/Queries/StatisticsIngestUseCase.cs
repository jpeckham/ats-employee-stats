using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public sealed class StatisticsIngestUseCase(StatisticsService statisticsService) : IStatisticsIngestUseCase
{
    public Task IngestAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null,
        bool force = false) =>
        statisticsService.IngestAsync(cancellationToken, progress, force);
}
