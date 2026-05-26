using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Statistics;

public interface IStatisticsQuerySource
{
    Task<AtsStatistics> ReadStatisticsAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);
}
