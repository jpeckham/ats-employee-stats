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
    protected int FromDay { get; private set; } = 0;
    protected int ToDay { get; private set; } = 0;
    protected CollectionSortDto CollectionSort { get; private set; } = new();

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
        Statistics = await StatisticsClient.GetStatisticsAsync();
        FromDay = 0;
        ToDay = Statistics.MaxGameDay ?? 0;
        Status = "Loaded.";
    }

    protected bool IsReloading { get; private set; }

    protected async Task SetFromDayAsync(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var day))
        {
            FromDay = Math.Max(0, day);
            await ReloadRangeAsync();
        }
    }

    protected async Task SetToDayAsync(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var day))
        {
            ToDay = Math.Max(0, day);
            await ReloadRangeAsync();
        }
    }

    protected async Task ForceReloadAsync()
    {
        if (IsReloading) return;
        IsReloading = true;
        Status = "Reloading saves...";
        StateHasChanged();
        try
        {
            Statistics = await StatisticsClient.ReloadAsync(FromDay, ToDay > 0 ? ToDay : null, CollectionSort);
            ToDay = Statistics.MaxGameDay ?? ToDay;
            Status = "Reloaded.";
        }
        finally
        {
            IsReloading = false;
            StateHasChanged();
        }
    }

    protected async Task SetCollectionSortAsync(CollectionSortDto sort)
    {
        CollectionSort = sort;
        await ReloadRangeAsync();
    }

    private async Task ReloadRangeAsync()
    {
        Status = "Loading...";
        Statistics = await StatisticsClient.GetStatisticsAsync(FromDay, ToDay > 0 ? ToDay : null, CollectionSort);
        Status = "Loaded.";
    }

    protected static string Money(long value) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"${value:N0}");

    protected static Microsoft.AspNetCore.Components.MarkupString SparklineSvg(AtsEmployeeStats.Contracts.SparklineDto? trend)
    {
        if (trend is null || trend.Points.Count < 2)
            return new Microsoft.AspNetCore.Components.MarkupString(string.Empty);

        var values = trend.Points.Select(p => (double)p.Value).ToList();
        var min = values.Min();
        var max = values.Max();
        var range = max - min;
        if (range == 0) range = 1;

        var count = values.Count;
        var pointsList = values.Select((v, i) =>
        {
            var x = i * 80.0 / (count - 1);
            var y = (1.0 - (v - min) / range) * 22.0 + 1.0;
            return System.FormattableString.Invariant($"{x:F1},{y:F1}");
        });

        return new Microsoft.AspNetCore.Components.MarkupString(
            $"<svg width=\"80\" height=\"24\" viewBox=\"0 0 80 24\" aria-hidden=\"true\">" +
            $"<polyline points=\"{string.Join(' ', pointsList)}\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\"/>" +
            $"</svg>");
    }

    protected static string Segment(string value) =>
        Uri.EscapeDataString(value);

    protected static string FormatBodyType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        var parts = value.Split(['.', '_'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }

    private void OnStatusChanged(DashboardStatusDto status)
    {
        Status = status.Message;
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnStatisticsUpdated(DashboardStatisticsDto statistics)
    {
        Statistics = statistics;
        ToDay = statistics.MaxGameDay ?? ToDay;
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
