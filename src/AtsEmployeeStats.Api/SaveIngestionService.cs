using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Api.Presentation;
using Microsoft.AspNetCore.SignalR;

namespace AtsEmployeeStats.Api;

public sealed class SaveIngestionService(
    IStatisticsIngestUseCase ingestUseCase,
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
        var progress = new SignalRProgressPresenter(hub).AsProgress(cancellationToken);

        try
        {
            await ingestUseCase.IngestAsync(cancellationToken, progress);
        }
        catch
        {
            // errors during startup ingestion are non-fatal; UI will show stale/empty data
        }
    }
}
