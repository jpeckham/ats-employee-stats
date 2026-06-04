using System.Collections.ObjectModel;
using AtsEmployeeStats.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using static AtsEmployeeStats.Wpf.ViewModels.DetailHelpers;

namespace AtsEmployeeStats.Wpf.ViewModels;

public sealed class DetailTabViewModel
{
    public DetailTabViewModel(string title, OverviewViewModel overview)
    {
        Title = title;
        Overview = overview;
    }

    public DetailTabViewModel(string title, IEnumerable<GridRowViewModel> rows, IReadOnlyList<TableColumnViewModel>? columns = null)
    {
        Title = title;
        Rows = [.. rows];
        Columns = columns ?? TableColumns.Default;
    }

    public string Title { get; }

    public OverviewViewModel? Overview { get; }

    public bool IsOverview => Overview is not null;

    public ObservableCollection<GridRowViewModel> Rows { get; } = [];

    public IReadOnlyList<TableColumnViewModel> Columns { get; } = TableColumns.Default;
}

public abstract partial class EntityDetailViewModel : ObservableObject
{
    protected EntityDetailViewModel(string title, string subtitle, string profitText)
    {
        Title = title;
        Subtitle = subtitle;
        ProfitText = profitText;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string ProfitText { get; }

    public ObservableCollection<DetailMetricViewModel> Metrics { get; } = [];

    public ObservableCollection<DetailTabViewModel> Tabs { get; } = [];

    [ObservableProperty]
    private int selectedTabIndex;
}

public sealed class CompanyExplorerViewModel : ObservableObject
{
    public ObservableCollection<ExplorerNodeViewModel> Roots { get; } = [];
}

public sealed class CompanyDetailViewModel : EntityDetailViewModel
{
    public CompanyDetailViewModel(CompanyDto company, string? selectedTabTitle = null)
        : base(company.DisplayName, company.OwnerName ?? company.Id, RowFormatting.Money(company.Profit))
    {
        Metrics.Add(new("Garages", RowFormatting.Count(company.Garages.Count)));
        Metrics.Add(new("Drivers", RowFormatting.Count(company.Drivers.Count)));
        Metrics.Add(new("Trucks", RowFormatting.Count(company.Trucks.Count)));
        Metrics.Add(new("Trailers", RowFormatting.Count(company.Trailers?.Count ?? 0)));
        Metrics.Add(new("Jobs", RowFormatting.Count(company.Missions.Count)));
        Metrics.Add(new("Cities", RowFormatting.Count(company.Cities?.Count ?? 0)));

        Tabs.Add(new("Overview", CompanyOverviewBuilder.Build(company)));
        Tabs.Add(new("Garages", company.Garages.Select(garage => Rows.Garage(company, garage)), TableColumns.Garages));
        Tabs.Add(new("Drivers", company.Drivers.Select(driver => Rows.Driver(company, driver)), TableColumns.Drivers));
        Tabs.Add(new("Trucks", company.Trucks.Select(truck => Rows.Truck(company, truck)), TableColumns.Trucks));
        Tabs.Add(new("Trailers", (company.Trailers ?? []).Select(trailer => Rows.Trailer(company, trailer)), TableColumns.Trailers));
        Tabs.Add(new("Jobs", company.Missions.Select(job => Rows.Job(company, job)), TableColumns.Jobs));
        Tabs.Add(new("Cities", (company.Cities ?? []).Select(city => Rows.City(company, city)), TableColumns.Cities));
        SelectedTabIndex = Math.Max(0, Tabs.ToList().FindIndex(tab => Same(tab.Title, selectedTabTitle)));
    }

}

public sealed class GarageDetailViewModel : EntityDetailViewModel
{
    public GarageDetailViewModel(CompanyDto company, GarageDto garage)
        : base(garage.DisplayName, $"{company.DisplayName} / Garages / {garage.Id}", RowFormatting.Money(garage.Profit))
    {
        Metrics.Add(new("Drivers", RowFormatting.Count(garage.EmployeeCount)));
        Metrics.Add(new("Trucks", RowFormatting.Count(garage.TruckCount)));
        Metrics.Add(new("Trailers", RowFormatting.Count(garage.TrailerCount)));
        Metrics.Add(new("Avg/day", RowFormatting.Money(garage.ProfitPerDay)));
        Tabs.Add(new("Overview", GarageOverviewBuilder.Build(company, garage)));
        Tabs.Add(new("Drivers", company.Drivers.Where(x => Same(x.GarageId, garage.Id)).Select(driver => Rows.Driver(company, driver)), TableColumns.Drivers));
        Tabs.Add(new("Trucks", company.Trucks.Where(x => Same(x.GarageId, garage.Id)).Select(truck => Rows.Truck(company, truck)), TableColumns.Trucks));
        Tabs.Add(new("Trailers", (company.Trailers ?? []).Where(x => Same(x.GarageId, garage.Id)).Select(trailer => Rows.Trailer(company, trailer)), TableColumns.Trailers));
    }
}

public sealed class DriverDetailViewModel : EntityDetailViewModel
{
    public DriverDetailViewModel(CompanyDto company, DriverDto driver)
        : base(driver.DisplayName, $"{company.DisplayName} / Drivers / {driver.Id}", RowFormatting.Money(driver.Profit))
    {
        Metrics.Add(new("Jobs", RowFormatting.Count(driver.JobCount)));
        Metrics.Add(new("Avg/day", RowFormatting.Money(driver.ProfitPerDay)));
        Metrics.Add(new("Recent/day", RowFormatting.Money(driver.RecentProfitPerDay)));
        Metrics.Add(new("Garage", GarageName(company, driver.GarageId)));
        Tabs.Add(new("Overview", DriverOverviewBuilder.Build(company, driver)));
        Tabs.Add(new("Jobs", company.Missions.Where(x => Same(x.DriverId, driver.Id)).Select(job => Rows.Job(company, job)), TableColumns.Jobs));
        Tabs.Add(new("Trucks", company.Trucks.Where(x => Same(x.DriverId, driver.Id) || Same(x.Id, driver.TruckId)).Select(truck => Rows.Truck(company, truck)), TableColumns.Trucks));
        Tabs.Add(new("Garages", (company.DriverGarageAssignments ?? []).Where(x => Same(x.DriverId, driver.Id)).Select(x => new GridRowViewModel(GarageName(company, x.GarageId), x.IsCurrent ? "Current" : "Past", x.EffectiveFromSaveName, x.EffectiveToSaveName ?? "-", [])), TableColumns.GarageAssignments));
    }
}

public sealed class TruckDetailViewModel : EntityDetailViewModel
{
    public TruckDetailViewModel(CompanyDto company, TruckDto truck)
        : base(truck.DisplayName, $"{company.DisplayName} / Trucks / {truck.Id}", RowFormatting.Money(truck.Profit))
    {
        Metrics.Add(new("Avg/day", RowFormatting.Money(truck.ProfitPerDay)));
        Metrics.Add(new("Garage", GarageName(company, truck.GarageId)));
        Metrics.Add(new("Driver", DriverName(company, truck.DriverId)));
        Metrics.Add(new("Plate", truck.LicensePlate ?? "-"));
        Tabs.Add(new("Overview", TruckOverviewBuilder.Build(company, truck)));
        Tabs.Add(new("Jobs", company.Missions.Where(x => Same(x.TruckId, truck.Id)).Select(job => Rows.Job(company, job)), TableColumns.Jobs));
        Tabs.Add(new("Trailers", (company.Trailers ?? []).Where(trailer => company.Missions.Any(job => Same(job.TruckId, truck.Id) && (Same(job.TrailerLicensePlate, trailer.LicensePlate) || Same(job.TrailerId, trailer.Id)))).Select(trailer => Rows.Trailer(company, trailer)), TableColumns.Trailers));
    }
}

public sealed class TrailerDetailViewModel : EntityDetailViewModel
{
    public TrailerDetailViewModel(CompanyDto company, TrailerDto trailer)
        : base(trailer.LicensePlate ?? trailer.Id, $"{company.DisplayName} / Trailers / {trailer.TrailerType}", RowFormatting.Money(trailer.Profit))
    {
        Metrics.Add(new("Jobs", RowFormatting.Count(trailer.JobCount)));
        Metrics.Add(new("Avg/day", RowFormatting.Money(trailer.ProfitPerDay)));
        Metrics.Add(new("Garage", GarageName(company, trailer.GarageId)));
        Metrics.Add(new("Type", trailer.TrailerType));
        Tabs.Add(new("Overview", TrailerOverviewBuilder.Build(company, trailer)));
        Tabs.Add(new("Jobs", company.Missions.Where(job => Same(job.TrailerLicensePlate, trailer.LicensePlate) || Same(job.TrailerId, trailer.Id)).Select(job => Rows.Job(company, job)), TableColumns.Jobs));
        Tabs.Add(new("Trucks", company.Missions.Where(job => Same(job.TrailerLicensePlate, trailer.LicensePlate) || Same(job.TrailerId, trailer.Id)).Where(job => !string.IsNullOrWhiteSpace(job.TruckId)).Select(job => job.TruckId!).Distinct(StringComparer.OrdinalIgnoreCase).Select(id => company.Trucks.FirstOrDefault(truck => Same(truck.Id, id))).Where(truck => truck is not null).Select(truck => Rows.Truck(company, truck!)), TableColumns.Trucks));
    }
}

public sealed class JobDetailViewModel : EntityDetailViewModel
{
    public JobDetailViewModel(CompanyDto company, MissionDto job)
        : base(string.IsNullOrWhiteSpace(job.Cargo) ? job.Id : job.Cargo!, $"{RowFormatting.Value(job.SourceCity)} to {RowFormatting.Value(job.TargetCity)}", RowFormatting.Money(job.Profit))
    {
        Metrics.Add(new("Day", job.TimestampDay?.ToString() ?? "-"));
        Metrics.Add(new("Driver", DriverName(company, job.DriverId)));
        Metrics.Add(new("Truck", TruckName(company, job.TruckId)));
        Metrics.Add(new("Trailer", job.TrailerLicensePlate ?? job.TrailerId ?? job.TrailerType ?? "-"));
        Tabs.Add(new("Overview", JobOverviewBuilder.Build(company, job)));
        Tabs.Add(new("Details", [Rows.Job(company, job)], TableColumns.Jobs));
    }
}

public sealed class CityDetailViewModel : EntityDetailViewModel
{
    public CityDetailViewModel(CompanyDto company, CityDto city)
        : base(city.DisplayName, $"{company.DisplayName} / Cities / {city.Id}", RowFormatting.Money(city.BidirectionalProfit))
    {
        Metrics.Add(new("Visits", RowFormatting.Count(city.VisitCount)));
        Metrics.Add(new("Outbound", RowFormatting.Money(city.OutboundProfit)));
        Metrics.Add(new("Inbound", RowFormatting.Money(city.InboundProfit)));
        Metrics.Add(new("Expansion", city.ExpansionScore.ToString("0.##")));
        Tabs.Add(new("Overview", CityOverviewBuilder.Build(company, city)));
        Tabs.Add(new("Jobs", company.Missions.Where(job => Same(job.SourceCity, city.Id) || Same(job.TargetCity, city.Id)).Select(job => Rows.Job(company, job)), TableColumns.Jobs));
        Tabs.Add(new("Routes", (company.Routes ?? []).Where(route => Same(route.OriginCityId, city.Id) || Same(route.DestinationCityId, city.Id)).Select(route => new GridRowViewModel($"{route.OriginCityId} to {route.DestinationCityId}", RowFormatting.Money(route.Profit), $"{route.JobCount:N0} jobs", $"{route.ProfitPerMile:0.00}/mi", [])
        {
            ProfitSort = route.Profit
        }), TableColumns.Routes));
    }
}

internal static class Rows
{
    public static GridRowViewModel Garage(CompanyDto company, GarageDto garage) =>
        new(garage.DisplayName, RowFormatting.Money(garage.Profit), $"{garage.EmployeeCount:N0} drivers / {garage.TruckCount:N0} trucks", $"{RowFormatting.Money(garage.ProfitPerDay)}/day", RowFormatting.Trend(garage.Trend), garage)
        {
            Target = new(ExplorerNodeKind.Garage, company.Id, garage.Id),
            ProfitSort = garage.Profit
        };

    public static GridRowViewModel Driver(CompanyDto company, DriverDto driver) =>
        new(driver.DisplayName, RowFormatting.Money(driver.Profit), $"{GarageName(company, driver.GarageId)} / {TruckName(company, driver.TruckId)}", $"{driver.JobCount:N0} jobs", RowFormatting.Trend(driver.Trend), driver)
        {
            Target = new(ExplorerNodeKind.Driver, company.Id, driver.Id),
            ProfitSort = driver.Profit
        };

    public static GridRowViewModel Truck(CompanyDto company, TruckDto truck) =>
        new(truck.DisplayName, RowFormatting.Money(truck.Profit), $"{GarageName(company, truck.GarageId)} / {DriverName(company, truck.DriverId)}", truck.LicensePlate ?? truck.Id, RowFormatting.Trend(truck.Trend), truck)
        {
            Target = new(ExplorerNodeKind.Truck, company.Id, truck.Id),
            ProfitSort = truck.Profit
        };

    public static GridRowViewModel Trailer(CompanyDto company, TrailerDto trailer) =>
        new(trailer.LicensePlate ?? trailer.Id, RowFormatting.Money(trailer.Profit), $"{trailer.TrailerType} / {GarageName(company, trailer.GarageId)}", $"{trailer.JobCount:N0} jobs", RowFormatting.Trend(trailer.Trend), trailer)
        {
            Target = new(ExplorerNodeKind.Trailer, company.Id, trailer.LicensePlate ?? trailer.Id),
            ProfitSort = trailer.Profit
        };

    public static GridRowViewModel Job(CompanyDto company, MissionDto job) =>
        new(string.IsNullOrWhiteSpace(job.Cargo) ? job.Id : job.Cargo!, RowFormatting.Money(job.Profit), $"{RowFormatting.Value(job.SourceCity)} to {RowFormatting.Value(job.TargetCity)}", job.TimestampDay?.ToString() ?? "-", [], job)
        {
            Target = new(ExplorerNodeKind.Job, company.Id, job.Id),
            ProfitSort = job.Profit
        };

    public static GridRowViewModel City(CompanyDto company, CityDto city) =>
        new(city.DisplayName, RowFormatting.Money(city.BidirectionalProfit), city.HasOwnedGarage ? "Owned garage" : "No owned garage", $"Expansion {city.ExpansionScore:0.##}", [], city)
        {
            Target = new(ExplorerNodeKind.City, company.Id, city.Id),
            ProfitSort = city.BidirectionalProfit,
            Garage = city.HasOwnedGarage ? "Owned" : "-",
            Eligible = city.IsGarageEligible ? "Yes" : "No",
            Visits = RowFormatting.Count(city.VisitCount),
            VisitsSort = city.VisitCount,
            Outbound = RowFormatting.Money(city.OutboundProfit),
            OutboundSort = city.OutboundProfit,
            Inbound = RowFormatting.Money(city.InboundProfit),
            InboundSort = city.InboundProfit,
            Total = RowFormatting.Money(city.OutboundProfit + city.InboundProfit),
            TotalSort = city.OutboundProfit + city.InboundProfit,
            Expansion = city.ExpansionScore.ToString("0.##"),
            ExpansionSort = city.ExpansionScore
        };
}

internal static class DetailHelpers
{
    public static bool Same(string? left, string? right) => StringComparer.OrdinalIgnoreCase.Equals(left, right);

    public static string GarageName(CompanyDto company, string? id) =>
        string.IsNullOrWhiteSpace(id) ? "-" : company.Garages.FirstOrDefault(x => Same(x.Id, id))?.DisplayName ?? id;

    public static string DriverName(CompanyDto company, string? id) =>
        string.IsNullOrWhiteSpace(id) ? "-" : company.Drivers.FirstOrDefault(x => Same(x.Id, id))?.DisplayName ?? id;

    public static string TruckName(CompanyDto company, string? id) =>
        string.IsNullOrWhiteSpace(id) ? "-" : company.Trucks.FirstOrDefault(x => Same(x.Id, id))?.DisplayName ?? id;
}
