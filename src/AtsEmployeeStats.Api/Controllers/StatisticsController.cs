using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Api.Presentation;
using AtsEmployeeStats.Api.Requests;
using Microsoft.AspNetCore.SignalR;
using Contracts = AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Api.Controllers;

public sealed class StatisticsController(
    IStatisticsReloadUseCase reloadInputBoundary,
    IStatisticsDashboardUseCases dashboardInputBoundary,
    IApiRequestMapper mapper,
    IHubContext<StatisticsHub> hub)
{
    public async Task<IResult> GetStatisticsAsync(
        StatisticsRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new HttpResultPresenter<Contracts.DashboardStatisticsDto>();
        await dashboardInputBoundary.ExecuteDashboardAsync(
            presenter,
            mapper.Map(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }

    public async Task<IResult> ReloadAsync(
        StatisticsRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new SignalRReloadPresenter(hub);
        await presenter.PresentReloadingAsync(cancellationToken);
        await reloadInputBoundary.ExecuteReloadAsync(
            presenter,
            mapper.Map(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }
}
