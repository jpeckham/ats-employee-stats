using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AtsEmployeeStats.Api;

public sealed class SaveIngestionService(
    StatisticsService statisticsService,
    IHubContext<StatisticsHub> hub) : IHostedService
{
    private Task _ingestionTask = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ingestionTask = RunIngestionAsync();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => _ingestionTask;

    private async Task RunIngestionAsync()
    {
        var progress = new Progress<SaveLoadProgress>(update =>
        {
            _ = hub.Clients.All.SendAsync(
                "LoadingProgress",
                DashboardProgressMapper.ToDashboardProgressDto(update));
        });

        try
        {
            await statisticsService.IngestAsync(CancellationToken.None, progress);
        }
        catch
        {
            // errors during startup ingestion are non-fatal; UI will show stale/empty data
        }
    }
}
