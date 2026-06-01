using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Api.Presentation;
using AtsEmployeeStats.Api.Requests;
using Microsoft.AspNetCore.SignalR;
using Contracts = AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Api.Controllers;

public sealed class CompaniesController(
    IStatisticsDashboardUseCases dashboardInputBoundary,
    IApiRequestMapper mapper,
    IHubContext<StatisticsHub> hub)
{
    public async Task<IResult> ListCompaniesAsync(
        CompaniesRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new HttpResultPresenter<IReadOnlyList<Contracts.CompanyDto>>();
        await dashboardInputBoundary.ExecuteListCompaniesAsync(
            presenter,
            mapper.Map(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }

    public async Task<IResult> GetCompanyAsync(
        CompanyRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new NullableHttpResultPresenter<Contracts.CompanyDto>();
        await dashboardInputBoundary.ExecuteCompanyAsync(
            presenter,
            mapper.Map(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }
}
