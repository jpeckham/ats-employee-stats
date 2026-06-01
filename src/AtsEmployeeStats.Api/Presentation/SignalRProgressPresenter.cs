using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Output;
using Microsoft.AspNetCore.SignalR;

namespace AtsEmployeeStats.Api.Presentation;

public sealed class SignalRProgressPresenter(IHubContext<StatisticsHub> hub) : IProgressOutputBoundaryAdapter
{
    public Task PresentProgressAsync(SaveLoadProgress progress, CancellationToken cancellationToken) =>
        hub.Clients.All.SendAsync(
            "LoadingProgress",
            DashboardProgressMapper.ToDashboardProgressDto(progress),
            cancellationToken);

    public IProgress<SaveLoadProgress> AsProgress(CancellationToken cancellationToken) =>
        new Progress<SaveLoadProgress>(progress =>
        {
            _ = PresentProgressAsync(progress, cancellationToken);
        });
}
