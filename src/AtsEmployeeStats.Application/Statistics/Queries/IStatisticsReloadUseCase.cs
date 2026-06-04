using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public interface IStatisticsReloadUseCase
{
    Task ExecuteReloadAsync(
        IOutputBoundaryAdapter<DashboardStatisticsDto> output,
        DashboardQueryRequest request,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task<DashboardStatisticsDto> ReloadAsync(
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);

    Task<DashboardStatisticsDto> SyncAsync(
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);
}
