using System.Globalization;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Wpf.ViewModels;

public sealed record DetailMetricViewModel(string Label, string Value);

public sealed record TableColumnViewModel(
    string Header,
    string BindingPath,
    string? SortMemberPath = null,
    double Width = 1,
    bool IsTrend = false);

public sealed record RowNavigationTarget(ExplorerNodeKind Kind, string CompanyId, string? EntityId = null);

public sealed record GridRowViewModel(
    string Name,
    string Profit,
    string Detail,
    string Secondary,
    IReadOnlyList<double> Trend,
    object? Source = null)
{
    public RowNavigationTarget? Target { get; init; }

    public long ProfitSort { get; init; }

    public string Garage { get; init; } = string.Empty;

    public string Eligible { get; init; } = string.Empty;

    public string Visits { get; init; } = string.Empty;

    public int VisitsSort { get; init; }

    public string Outbound { get; init; } = string.Empty;

    public long OutboundSort { get; init; }

    public string Inbound { get; init; } = string.Empty;

    public long InboundSort { get; init; }

    public string Total { get; init; } = string.Empty;

    public long TotalSort { get; init; }

    public string Expansion { get; init; } = string.Empty;

    public decimal ExpansionSort { get; init; }
}

internal static class TableColumns
{
    public static readonly IReadOnlyList<TableColumnViewModel> Companies =
    [
        new("Company", nameof(GridRowViewModel.Name), Width: 2),
        new("Profit", nameof(GridRowViewModel.Profit), nameof(GridRowViewModel.ProfitSort)),
        new("Details", nameof(GridRowViewModel.Detail), Width: 2),
        new("Jobs / Meta", nameof(GridRowViewModel.Secondary)),
        new("Trend", nameof(GridRowViewModel.Trend), Width: 1.2, IsTrend: true)
    ];

    public static readonly IReadOnlyList<TableColumnViewModel> Default =
    [
        new("Name", nameof(GridRowViewModel.Name), Width: 2),
        new("Profit", nameof(GridRowViewModel.Profit), nameof(GridRowViewModel.ProfitSort)),
        new("Details", nameof(GridRowViewModel.Detail), Width: 2),
        new("Jobs / Meta", nameof(GridRowViewModel.Secondary)),
        new("Trend", nameof(GridRowViewModel.Trend), Width: 1.2, IsTrend: true)
    ];

    public static readonly IReadOnlyList<TableColumnViewModel> Cities =
    [
        new("City", nameof(GridRowViewModel.Name), Width: 2),
        new("Garage", nameof(GridRowViewModel.Garage)),
        new("Eligible", nameof(GridRowViewModel.Eligible)),
        new("Visits", nameof(GridRowViewModel.Visits), nameof(GridRowViewModel.VisitsSort)),
        new("Outbound", nameof(GridRowViewModel.Outbound), nameof(GridRowViewModel.OutboundSort)),
        new("Inbound", nameof(GridRowViewModel.Inbound), nameof(GridRowViewModel.InboundSort)),
        new("Total", nameof(GridRowViewModel.Total), nameof(GridRowViewModel.TotalSort)),
        new("Expansion", nameof(GridRowViewModel.Expansion), nameof(GridRowViewModel.ExpansionSort))
    ];
}

internal static class RowFormatting
{
    public static string Money(long value) =>
        string.Create(CultureInfo.CurrentCulture, $"{value:C0}");

    public static string Count(int value) =>
        value.ToString("N0", CultureInfo.CurrentCulture);

    public static string Value(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;

    public static IReadOnlyList<double> Trend(SparklineDto? sparkline) =>
        sparkline?.Points.Select(point => (double)point.Value).ToArray() ?? [];
}
