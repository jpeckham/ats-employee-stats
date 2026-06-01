using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Api.Presentation;
using AtsEmployeeStats.Api.Requests;
using Microsoft.AspNetCore.SignalR;
using Contracts = AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Api.Controllers;

public sealed class TrailersController(
    IStatisticsDashboardUseCases dashboardInputBoundary,
    IRecommendTrailersForGarageUseCase trailersForGarageInputBoundary,
    IApiRequestMapper mapper,
    IHubContext<StatisticsHub> hub)
{
    public async Task<IResult> GetTrailerAsync(
        TrailerRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new NullableHttpResultPresenter<Contracts.TrailerDto>();
        await dashboardInputBoundary.ExecuteTrailerAsync(
            presenter,
            mapper.Map(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }

    public async Task<IResult> RecommendTrailersForGarageAsync(
        TrailerRecommendationRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new HttpResultPresenter<IReadOnlyList<Contracts.TrailerRecommendationDto>>();
        await trailersForGarageInputBoundary.ExecuteAsync(
            presenter,
            mapper.MapTrailerRecommendation(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }
}
