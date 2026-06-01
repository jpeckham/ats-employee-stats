using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AtsEmployeeStats.Api.Presentation;

public sealed class SignalRReloadPresenter(IHubContext<StatisticsHub> hub)
    : IOutputBoundaryAdapter<DashboardStatisticsDto>
{
    public IResult ViewModel { get; private set; } = Results.NoContent();

    public Task PresentReloadingAsync(CancellationToken cancellationToken) =>
        hub.Clients.All.SendAsync(
            "StatusChanged",
            new DashboardStatusDto("Reloading saves...", IsError: false),
            cancellationToken);

    public async Task PresentAsync(DashboardStatisticsDto response, CancellationToken cancellationToken)
    {
        await hub.Clients.All.SendAsync("StatisticsUpdated", response, cancellationToken);
        ViewModel = Results.Ok(response);
    }
}
