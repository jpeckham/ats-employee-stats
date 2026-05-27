using AtsEmployeeStats.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace AtsEmployeeStats.Web.Services;

public sealed class StatisticsRealtimeClient(NavigationManager navigationManager) : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<DashboardStatusDto>? StatusChanged;
    public event Action<DashboardStatisticsDto>? StatisticsUpdated;
    public event Action<DashboardProgressDto>? LoadingProgress;

    public async Task StartAsync()
    {
        _connection ??= new HubConnectionBuilder()
            .WithUrl(navigationManager.ToAbsoluteUri("/hubs/statistics"))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<DashboardStatusDto>("StatusChanged", message => StatusChanged?.Invoke(message));
        _connection.On<DashboardStatisticsDto>("StatisticsUpdated", statistics => StatisticsUpdated?.Invoke(statistics));
        _connection.On<DashboardProgressDto>("LoadingProgress", progress => LoadingProgress?.Invoke(progress));

        if (_connection.State == HubConnectionState.Disconnected)
        {
            await _connection.StartAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
