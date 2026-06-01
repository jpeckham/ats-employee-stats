using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Maui.Presentation;

namespace AtsEmployeeStats.Maui.Controllers;

public sealed class DashboardController(
    IStatisticsIngestUseCase ingestUseCase,
    IStatisticsDashboardUseCases dashboardUseCases)
{
    internal async Task RefreshAsync(
        IDashboardPresentationTarget dashboard,
        DashboardQueryRequest request,
        CancellationToken cancellationToken)
    {
        var progress = new MauiProgressPresenter(dashboard);
        await ingestUseCase.IngestAsync(cancellationToken, progress.AsProgress(cancellationToken), force: false);
        await dashboardUseCases.ExecuteDashboardAsync(
            new MauiDashboardPresenter(dashboard),
            request,
            progress,
            cancellationToken);
    }
}
