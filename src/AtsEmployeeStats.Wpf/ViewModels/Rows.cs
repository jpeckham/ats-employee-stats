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

public sealed record RowNavigationTargetViewModel(ExplorerNodeKind Kind, string CompanyId, string? EntityId = null);

public sealed record GridRowViewModel(
    string Name,
    string Profit,
    string Detail,
    string Secondary,
    IReadOnlyList<double> Trend,
    object? Source = null)
{
    public RowNavigationTargetViewModel? Target { get; init; }

    public long ProfitSort { get; init; }

    public string Garage { get; init; } = string.Empty;

    public string Eligible { get; init; } = string.Empty;

    public decimal DetailSort { get; init; }

    public decimal SecondarySort { get; init; }

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

    public string PlayerOrigin { get; init; } = string.Empty;

    public decimal PlayerOriginSort { get; init; }

    public string ProfitPerDistance { get; init; } = string.Empty;

    public decimal ProfitPerDistanceSort { get; init; }

    public string Body { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;
}

internal static class TableColumns
{
    public static readonly IReadOnlyList<TableColumnViewModel> Companies =
    [
        new("Company", nameof(GridRowViewModel.Name), Width: 2),
        new("Profit", nameof(GridRowViewModel.Profit), nameof(GridRowViewModel.ProfitSort)),
        new("Summary", nameof(GridRowViewModel.Detail), Width: 2),
        new("Jobs", nameof(GridRowViewModel.Secondary), nameof(GridRowViewModel.SecondarySort)),
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

    public static readonly IReadOnlyList<TableColumnViewModel> Garages =
    [
        new("Name", nameof(GridRowViewModel.Name), Width: 2),
        new("Profit", nameof(GridRowViewModel.Profit), nameof(GridRowViewModel.ProfitSort)),
        new("Staff", nameof(GridRowViewModel.Detail), Width: 2),
        new("Avg/Day", nameof(GridRowViewModel.Secondary), nameof(GridRowViewModel.SecondarySort)),
        new("Trend", nameof(GridRowViewModel.Trend), Width: 1.2, IsTrend: true)
    ];

    public static readonly IReadOnlyList<TableColumnViewModel> Drivers =
    [
        new("Name", nameof(GridRowViewModel.Name), Width: 2),
        new("Profit", nameof(GridRowViewModel.Profit), nameof(GridRowViewModel.ProfitSort)),
        new("Assignment", nameof(GridRowViewModel.Detail), Width: 2),
        new("Jobs", nameof(GridRowViewModel.Secondary), nameof(GridRowViewModel.SecondarySort)),
        new("Profit/Dist", nameof(GridRowViewModel.ProfitPerDistance), nameof(GridRowViewModel.ProfitPerDistanceSort)),
        new("Trend", nameof(GridRowViewModel.Trend), Width: 1.2, IsTrend: true)
    ];

    public static readonly IReadOnlyList<TableColumnViewModel> Trucks =
    [
        new("Name", nameof(GridRowViewModel.Name), Width: 2),
        new("Profit", nameof(GridRowViewModel.Profit), nameof(GridRowViewModel.ProfitSort)),
        new("Assignment", nameof(GridRowViewModel.Detail), Width: 2),
        new("Plate", nameof(GridRowViewModel.Secondary)),
        new("Trend", nameof(GridRowViewModel.Trend), Width: 1.2, IsTrend: true)
    ];

    public static readonly IReadOnlyList<TableColumnViewModel> Trailers =
    [
        new("Name", nameof(GridRowViewModel.Name), Width: 2),
        new("Profit", nameof(GridRowViewModel.Profit), nameof(GridRowViewModel.ProfitSort)),
        new("Body", nameof(GridRowViewModel.Body)),
        new("Location", nameof(GridRowViewModel.Location)),
        new("Jobs", nameof(GridRowViewModel.Secondary), nameof(GridRowViewModel.SecondarySort)),
        new("Trend", nameof(GridRowViewModel.Trend), Width: 1.2, IsTrend: true)
    ];

    public static readonly IReadOnlyList<TableColumnViewModel> Jobs =
    [
        new("Cargo", nameof(GridRowViewModel.Name), Width: 2),
        new("Profit", nameof(GridRowViewModel.Profit), nameof(GridRowViewModel.ProfitSort)),
        new("Route", nameof(GridRowViewModel.Detail), Width: 2),
        new("Day", nameof(GridRowViewModel.Secondary), nameof(GridRowViewModel.SecondarySort))
    ];

    public static readonly IReadOnlyList<TableColumnViewModel> GarageAssignments =
    [
        new("Garage", nameof(GridRowViewModel.Name), Width: 2),
        new("Status", nameof(GridRowViewModel.Profit)),
        new("From", nameof(GridRowViewModel.Detail), Width: 2),
        new("Until", nameof(GridRowViewModel.Secondary))
    ];

    public static readonly IReadOnlyList<TableColumnViewModel> Routes =
    [
        new("Route", nameof(GridRowViewModel.Name), Width: 2),
        new("Profit", nameof(GridRowViewModel.Profit), nameof(GridRowViewModel.ProfitSort)),
        new("Jobs", nameof(GridRowViewModel.Detail), nameof(GridRowViewModel.DetailSort)),
        new("Profit/Mile", nameof(GridRowViewModel.Secondary), nameof(GridRowViewModel.SecondarySort))
    ];

    public static IReadOnlyList<TableColumnViewModel> RoutesFor(CompanyDto company) =>
        [
            new("Route", nameof(GridRowViewModel.Name), Width: 2),
            new("Profit", nameof(GridRowViewModel.Profit), nameof(GridRowViewModel.ProfitSort)),
            new("Jobs", nameof(GridRowViewModel.Detail), nameof(GridRowViewModel.DetailSort)),
            new(RowFormatting.IsEts2(company.Id) ? "Profit/Kilometer" : "Profit/Mile", nameof(GridRowViewModel.Secondary), nameof(GridRowViewModel.SecondarySort))
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
        new("Expansion", nameof(GridRowViewModel.Expansion), nameof(GridRowViewModel.ExpansionSort)),
        new("Player Origin", nameof(GridRowViewModel.PlayerOrigin), nameof(GridRowViewModel.PlayerOriginSort))
    ];
}

internal static class RowFormatting
{
    public static string Money(long value, string currencySymbol = "$") =>
        string.Create(CultureInfo.InvariantCulture, $"{currencySymbol}{value:N0}");

    public static string Count(int value) =>
        value.ToString("N0", CultureInfo.CurrentCulture);

    public static string MoneyPerDistance(decimal value, string companyId, string currencySymbol)
    {
        if (value == 0)
            return "-";

        var unit = IsEts2(companyId) ? "km" : "mi";
        return string.Create(CultureInfo.InvariantCulture, $"{currencySymbol}{value:0.00}/{unit}");
    }

    public static bool IsEts2(string companyId) =>
        companyId.StartsWith("ets2-", StringComparison.OrdinalIgnoreCase);

    public static string Value(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;

    public static IReadOnlyList<double> Trend(SparklineDto? sparkline) =>
        sparkline?.Points.Select(point => (double)point.Value).ToArray() ?? [];
}
