using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AtsEmployeeStats.Web.Pages;

public abstract class DashboardBase : ComponentBase, IAsyncDisposable
{
    [Inject]
    protected StatisticsClient StatisticsClient { get; set; } = null!;

    [Inject]
    protected StatisticsRealtimeClient RealtimeClient { get; set; } = null!;

    protected DashboardStatisticsDto? Statistics { get; private set; }
    protected DashboardProgressDto? Progress { get; private set; }
    protected string Status { get; private set; } = "Connecting...";
    protected int RangeDays { get; private set; } = 14;

    protected int FileProgressMax =>
        Math.Max(1, Progress?.TotalFiles ?? 1);

    protected int FileProgressValue =>
        Math.Clamp(Progress?.CompletedFiles ?? 0, 0, FileProgressMax);

    protected string FileProgressText =>
        Progress is { TotalFiles: > 0 }
            ? $"{Progress.CompletedFiles:N0} of {Progress.TotalFiles:N0}"
            : "Discovering";

    protected int CurrentSaveProgressMax =>
        Math.Max(1, Progress?.CurrentFileTotalUnits ?? 1);

    protected int CurrentSaveProgressValue =>
        Math.Clamp(Progress?.CurrentFileCompletedUnits ?? 0, 0, CurrentSaveProgressMax);

    protected string CurrentSaveProgressText =>
        Progress is { CurrentFileTotalUnits: > 0 }
            ? $"{Progress.CurrentFileCompletedUnits:N0} of {Progress.CurrentFileTotalUnits:N0}"
            : "Waiting";

    protected override async Task OnInitializedAsync()
    {
        RealtimeClient.StatusChanged += OnStatusChanged;
        RealtimeClient.StatisticsUpdated += OnStatisticsUpdated;
        RealtimeClient.LoadingProgress += OnLoadingProgress;
        await RealtimeClient.StartAsync();
        Statistics = await StatisticsClient.GetStatisticsAsync(RangeDays);
        Status = "Loaded.";
    }

    protected async Task SetRangeAsync(int rangeDays)
    {
        RangeDays = rangeDays;
        Status = "Loading selected range...";
        Statistics = await StatisticsClient.GetStatisticsAsync(RangeDays);
        Status = "Loaded.";
    }

    protected string RangeClass(int rangeDays) =>
        RangeDays == rangeDays ? "active" : string.Empty;

    protected static string Money(long value) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"${value:N0}");

    protected static string Segment(string value) =>
        Uri.EscapeDataString(value);

    private void OnStatusChanged(DashboardStatusDto status)
    {
        Status = status.Message;
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnStatisticsUpdated(DashboardStatisticsDto statistics)
    {
        Statistics = statistics;
        Status = "Updated.";
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnLoadingProgress(DashboardProgressDto progress)
    {
        Progress = progress;
        Status = progress.Message;
        _ = InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        RealtimeClient.StatusChanged -= OnStatusChanged;
        RealtimeClient.StatisticsUpdated -= OnStatisticsUpdated;
        RealtimeClient.LoadingProgress -= OnLoadingProgress;
        await RealtimeClient.DisposeAsync();
    }
}
