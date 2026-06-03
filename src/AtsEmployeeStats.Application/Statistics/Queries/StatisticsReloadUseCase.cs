using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public sealed class StatisticsReloadUseCase(StatisticsService statisticsService) : IStatisticsReloadUseCase
{
    public async Task ExecuteReloadAsync(
        IOutputBoundaryAdapter<DashboardStatisticsDto> output,
        DashboardQueryRequest request,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken)
    {
        var dashboard = await ReloadAsync(
            request.ToOptions(),
            cancellationToken,
            ToProgress(progress, cancellationToken));
        await output.PresentAsync(dashboard, cancellationToken);
    }

    public async Task<DashboardStatisticsDto> ReloadAsync(
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null)
    {
        await statisticsService.IngestAsync(cancellationToken, progress, force: true);
        var statistics = await statisticsService.LoadAsync(cancellationToken, progress);
        return StatisticsDashboardMapper.ToDashboardDto(
            statistics,
            options.FromDay ?? 0,
            options.ToDay,
            options.Sort,
            options.ExcludePlayerDriver);
    }

    private static IProgress<SaveLoadProgress>? ToProgress(
        IProgressOutputBoundaryAdapter? output,
        CancellationToken cancellationToken) =>
        output is null
            ? null
            : ProgressOutputAdapter.ToProgress(output, cancellationToken);
}
