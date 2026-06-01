using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Api.Presentation;
using AtsEmployeeStats.Api.Requests;
using Microsoft.AspNetCore.SignalR;
using Contracts = AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Api.Controllers;

public sealed class GaragesController(
    IStatisticsDashboardUseCases dashboardInputBoundary,
    IRecommendNextGarageCityUseCase nextGarageCityInputBoundary,
    IApiRequestMapper mapper,
    IHubContext<StatisticsHub> hub)
{
    public async Task<IResult> GetGarageAsync(
        GarageRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new NullableHttpResultPresenter<Contracts.GarageDto>();
        await dashboardInputBoundary.ExecuteGarageAsync(
            presenter,
            mapper.Map(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }

    public async Task<IResult> RecommendNextGarageCityAsync(
        RecommendationRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new NullableHttpResultPresenter<Contracts.GarageCityRecommendationDto>();
        await nextGarageCityInputBoundary.ExecuteAsync(
            presenter,
            mapper.MapNextGarageCity(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }
}
