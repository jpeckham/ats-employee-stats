using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AtsEmployeeStats.Api;

public sealed class SaveIngestionService(
    StatisticsService statisticsService,
    IHubContext<StatisticsHub> hub) : IHostedService
{
    private readonly CancellationTokenSource _cts = new();
    private Task _ingestionTask = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ingestionTask = RunIngestionAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts.CancelAsync();
        await Task.WhenAny(_ingestionTask, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    private async Task RunIngestionAsync(CancellationToken cancellationToken)
    {
        var progress = new Progress<SaveLoadProgress>(update =>
        {
            _ = hub.Clients.All.SendAsync(
                "LoadingProgress",
                DashboardProgressMapper.ToDashboardProgressDto(update));
        });

        try
        {
            await statisticsService.IngestAsync(cancellationToken, progress);
        }
        catch
        {
            // errors during startup ingestion are non-fatal; UI will show stale/empty data
        }
    }
}
