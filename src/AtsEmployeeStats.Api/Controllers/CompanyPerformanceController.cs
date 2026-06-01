using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Api.Presentation;
using AtsEmployeeStats.Api.Requests;
using Microsoft.AspNetCore.SignalR;
using Contracts = AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Api.Controllers;

public sealed class CompanyPerformanceController(
    IDiagnoseUnderperformersUseCase underperformersInputBoundary,
    IApiRequestMapper mapper,
    IHubContext<StatisticsHub> hub)
{
    public async Task<IResult> DiagnoseUnderperformersAsync(
        RecommendationRouteRequest request,
        CancellationToken cancellationToken)
    {
        var presenter = new HttpResultPresenter<IReadOnlyList<Contracts.UnderperformerDiagnosisDto>>();
        await underperformersInputBoundary.ExecuteAsync(
            presenter,
            mapper.MapUnderperformers(request),
            new SignalRProgressPresenter(hub),
            cancellationToken);
        return presenter.ViewModel;
    }
}
