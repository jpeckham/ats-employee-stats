using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Application.Statistics;

public interface IStatisticsIngestor
{
    Task IngestAsync(CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null, bool force = false);
}
