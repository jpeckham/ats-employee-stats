using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Api.Presentation;
using AtsEmployeeStats.Api.Requests;
using Microsoft.AspNetCore.SignalR;
using Contracts = AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Api.Controllers;

public sealed class JobsController(
    IStatisticsDashboardUseCases dashboardInputBoundary,
    IApiRequestMapper mapper,
    IHubContext<StatisticsHub> hub)
{
    public async Task<IResult> GetJobAsync(
        JobRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new NullableHttpResultPresenter<Contracts.MissionDto>();
        await dashboardInputBoundary.ExecuteJobAsync(
            presenter,
            mapper.Map(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }
}
