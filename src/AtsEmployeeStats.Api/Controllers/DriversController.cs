using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Api.Presentation;
using AtsEmployeeStats.Api.Requests;
using Microsoft.AspNetCore.SignalR;
using Contracts = AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Api.Controllers;

public sealed class DriversController(
    IStatisticsDashboardUseCases dashboardInputBoundary,
    IRecommendDriverSkillsUseCase driverSkillsInputBoundary,
    IApiRequestMapper mapper,
    IHubContext<StatisticsHub> hub)
{
    public async Task<IResult> GetDriverAsync(
        DriverRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new NullableHttpResultPresenter<Contracts.DriverDto>();
        await dashboardInputBoundary.ExecuteDriverAsync(
            presenter,
            mapper.Map(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }

    public async Task<IResult> RecommendDriverSkillsAsync(
        RecommendationRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new HttpResultPresenter<IReadOnlyList<Contracts.DriverSkillRecommendationDto>>();
        await driverSkillsInputBoundary.ExecuteAsync(
            presenter,
            mapper.MapDriverSkills(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }
}
