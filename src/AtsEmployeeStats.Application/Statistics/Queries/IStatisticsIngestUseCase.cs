using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public interface IStatisticsIngestUseCase
{
    Task IngestAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null,
        bool force = false);
}
