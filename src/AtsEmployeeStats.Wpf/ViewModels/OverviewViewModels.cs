using System.Collections.ObjectModel;
using AtsEmployeeStats.Contracts;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using static AtsEmployeeStats.Wpf.ViewModels.DetailHelpers;
using static AtsEmployeeStats.Wpf.ViewModels.OverviewBuilderHelpers;

namespace AtsEmployeeStats.Wpf.ViewModels;

public sealed class OverviewViewModel
{
    public OverviewHeaderViewModel Header { get; init; } = new("-", "-", "-");

    public ObservableCollection<SummaryCardViewModel> SummaryCards { get; } = [];

    public ObservableCollection<TrendChartViewModel> TrendCharts { get; } = [];

    public ObservableCollection<TopListViewModel> TopLists { get; } = [];

    public ObservableCollection<RecentActivityViewModel> RecentActivities { get; } = [];

    public bool HasCharts => TrendCharts.Count > 0;
}

public sealed record OverviewHeaderViewModel(string Title, string Subtitle, string PrimaryValue);

public sealed record SummaryCardViewModel(string Label, string Value, string Detail = "");

public sealed class TrendChartViewModel
{
    public TrendChartViewModel(string title, IEnumerable<long> values, IEnumerable<string>? labels = null)
    {
        Title = title;
        Series =
        [
            new LineSeries<long>
            {
                Values = values.ToArray(),
                Fill = null,
                GeometrySize = 5,
                LineSmoothness = 0
            }
        ];
        XAxes = [new Axis { Labels = labels?.ToArray() }];
        YAxes = [new Axis()];
    }

    public string Title { get; }

    public ISeries[] Series { get; }

    public Axis[] XAxes { get; }

    public Axis[] YAxes { get; }
}

public sealed record TopListViewModel(string Title, ObservableCollection<GridRowViewModel> Items)
{
    public TopListViewModel(string title, IEnumerable<GridRowViewModel> items)
        : this(title, [.. items])
    {
    }
}

public sealed record RecentActivityViewModel(string Title, ObservableCollection<GridRowViewModel> Items)
{
    public RecentActivityViewModel(string title, IEnumerable<GridRowViewModel> items)
        : this(title, [.. items])
    {
    }
}

internal static class CompanyOverviewBuilder
{
    public static OverviewViewModel Build(CompanyDto company)
    {
        var overview = Create(company.DisplayName, company.OwnerName ?? company.Id, RowFormatting.Money(company.Profit));
        AddCards(
            overview,
            ("Profit", RowFormatting.Money(company.Profit), "Company total"),
            ("Drivers", RowFormatting.Count(company.Drivers.Count), "Employees"),
            ("Trucks", RowFormatting.Count(company.Trucks.Count), "Fleet"),
            ("Trailers", RowFormatting.Count(company.Trailers?.Count ?? 0), "Owned"),
            ("Jobs", RowFormatting.Count(company.Missions.Count), "Completed"),
            ("Cities", RowFormatting.Count(company.Cities?.Count ?? 0), "Visited"),
            ("Garages", RowFormatting.Count(company.Garages.Count), "Owned"));

        var completeDayMissions = CompleteDayMissions(company.Missions);
        AddChart(overview, "Profit by Day", ProfitByDay(completeDayMissions));
        AddChart(overview, "Jobs by Day", CountByDay(completeDayMissions));
        AddChart(overview, "Drivers by Garage", company.Garages.Select(garage => (garage.DisplayName, (long)garage.EmployeeCount)));
        overview.TopLists.Add(new("Top 10 Garages", company.Garages.OrderByDescending(x => x.Profit).Take(10).Select(x => Rows.Garage(company, x))));
        overview.TopLists.Add(new("Top 10 Drivers", company.Drivers.OrderByDescending(x => x.Profit).Take(10).Select(x => Rows.Driver(company, x))));
        overview.TopLists.Add(new("Top 10 Cities", (company.Cities ?? []).OrderByDescending(x => x.BidirectionalProfit).Take(10).Select(x => Rows.City(company, x))));
        AddRecentJobs(overview, company, company.Missions, "Most Recent Jobs");
        return overview;
    }
}

internal static class GarageOverviewBuilder
{
    public static OverviewViewModel Build(CompanyDto company, GarageDto garage)
    {
        var jobs = company.Missions.Where(job => Same(job.GarageId, garage.Id)).ToArray();
        if (jobs.Length == 0)
            jobs = company.Missions.Where(job => company.Drivers.Any(driver => Same(driver.Id, job.DriverId) && Same(driver.GarageId, garage.Id))).ToArray();

        var overview = Create(garage.DisplayName, $"{company.DisplayName} / Garage", RowFormatting.Money(garage.Profit));
        AddCards(
            overview,
            ("Profit", RowFormatting.Money(garage.Profit), "Garage total"),
            ("Drivers", RowFormatting.Count(garage.EmployeeCount), "Assigned"),
            ("Trucks", RowFormatting.Count(garage.TruckCount), "Assigned"),
            ("Trailers", RowFormatting.Count(garage.TrailerCount), "Assigned"),
            ("Jobs", RowFormatting.Count(jobs.Length), "Related"));
        AddChart(overview, "Profit Trend", TrendOrDailyProfit(garage.Trend, jobs));
        AddChart(overview, "Jobs Trend", CountByDay(jobs));
        overview.TopLists.Add(new("Top Drivers", company.Drivers.Where(x => Same(x.GarageId, garage.Id)).OrderByDescending(x => x.Profit).Take(10).Select(x => Rows.Driver(company, x))));
        overview.TopLists.Add(new("Top Trucks", company.Trucks.Where(x => Same(x.GarageId, garage.Id)).OrderByDescending(x => x.Profit).Take(10).Select(x => Rows.Truck(company, x))));
        overview.TopLists.Add(new("Top Trailers", (company.Trailers ?? []).Where(x => Same(x.GarageId, garage.Id)).OrderByDescending(x => x.Profit).Take(10).Select(x => Rows.Trailer(company, x))));
        AddRecentJobs(overview, company, jobs, "Recent Jobs");
        return overview;
    }
}

internal static class DriverOverviewBuilder
{
    public static OverviewViewModel Build(CompanyDto company, DriverDto driver)
    {
        var jobs = company.Missions.Where(job => Same(job.DriverId, driver.Id)).ToArray();
        var overview = Create(driver.DisplayName, $"{company.DisplayName} / Driver", RowFormatting.Money(driver.Profit));
        AddCards(
            overview,
            ("Profit", RowFormatting.Money(driver.Profit), "Driver total"),
            ("Jobs", RowFormatting.Count(driver.JobCount), "Completed"),
            ("Average Profit Per Job", RowFormatting.Money(driver.JobCount == 0 ? 0 : driver.Profit / driver.JobCount), "Completed jobs"),
            ("Current Garage", GarageName(company, driver.GarageId), "Assignment"),
            ("Current Truck", TruckName(company, driver.TruckId), "Assignment"));
        AddChart(overview, "Profit Trend", TrendOrDailyProfit(driver.Trend, jobs));
        AddChart(overview, "Jobs Trend", CountByDay(jobs));
        overview.TopLists.Add(new("Best Jobs", jobs.OrderByDescending(x => x.Profit).Take(10).Select(x => Rows.Job(company, x))));
        overview.TopLists.Add(new("Most Frequent Cities", FrequentCities(company, jobs).Take(10)));
        overview.TopLists.Add(new("Most Common Cargo", FrequentCargo(jobs).Take(10)));
        AddRecentJobs(overview, company, jobs, "Recent Jobs");
        return overview;
    }
}

internal static class TruckOverviewBuilder
{
    public static OverviewViewModel Build(CompanyDto company, TruckDto truck)
    {
        var jobs = company.Missions.Where(job => Same(job.TruckId, truck.Id)).ToArray();
        var overview = Create(truck.DisplayName, $"{company.DisplayName} / Truck", RowFormatting.Money(truck.Profit));
        AddCards(
            overview,
            ("Profit", RowFormatting.Money(truck.Profit), "Truck total"),
            ("Jobs", RowFormatting.Count(jobs.Length), "Completed"),
            ("Driver Count", RowFormatting.Count(jobs.Select(x => x.DriverId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count()), "Observed"),
            ("Garage Count", RowFormatting.Count(new[] { truck.GarageId }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count()), "Assignments"));
        AddChart(overview, "Profit Trend", TrendOrDailyProfit(truck.Trend, jobs));
        AddChart(overview, "Usage Trend", CountByDay(jobs));
        overview.TopLists.Add(new("Top Drivers", TopDriversByJobs(company, jobs).Take(10)));
        overview.TopLists.Add(new("Top Garages", TopGaragesByJobs(company, jobs, truck.GarageId).Take(10)));
        overview.TopLists.Add(new("Top Cargo", FrequentCargo(jobs).Take(10)));
        AddRecentJobs(overview, company, jobs, "Recent Jobs");
        return overview;
    }
}

internal static class TrailerOverviewBuilder
{
    public static OverviewViewModel Build(CompanyDto company, TrailerDto trailer)
    {
        var jobs = company.Missions.Where(job => Same(job.TrailerLicensePlate, trailer.LicensePlate) || Same(job.TrailerId, trailer.Id)).ToArray();
        var overview = Create(trailer.LicensePlate ?? trailer.Id, $"{company.DisplayName} / Trailer / {trailer.TrailerType}", RowFormatting.Money(trailer.Profit));
        AddCards(
            overview,
            ("Profit", RowFormatting.Money(trailer.Profit), "Trailer total"),
            ("Jobs", RowFormatting.Count(trailer.JobCount), "Completed"),
            ("Driver Count", RowFormatting.Count(jobs.Select(x => x.DriverId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count()), "Observed"));
        AddChart(overview, "Profit Trend", TrendOrDailyProfit(trailer.Trend, jobs));
        AddChart(overview, "Usage Trend", CountByDay(jobs));
        overview.TopLists.Add(new("Top Drivers", TopDriversByJobs(company, jobs).Take(10)));
        overview.TopLists.Add(new("Top Cargo", FrequentCargo(jobs).Take(10)));
        AddRecentJobs(overview, company, jobs, "Recent Jobs");
        return overview;
    }
}

internal static class CityOverviewBuilder
{
    public static OverviewViewModel Build(CompanyDto company, CityDto city)
    {
        var jobs = company.Missions.Where(job => Same(job.SourceCity, city.Id) || Same(job.TargetCity, city.Id)).ToArray();
        var overview = Create(city.DisplayName, $"{company.DisplayName} / City", RowFormatting.Money(city.BidirectionalProfit));
        AddCards(
            overview,
            ("Visits", RowFormatting.Count(city.VisitCount), "Inbound and outbound"),
            ("Outbound Revenue", RowFormatting.Money(city.OutboundProfit), "Origin city"),
            ("Inbound Revenue", RowFormatting.Money(city.InboundProfit), "Destination city"),
            ("Total Revenue", RowFormatting.Money(city.OutboundProfit + city.InboundProfit), "Combined"),
            ("Expansion Score", city.ExpansionScore.ToString("0.##"), city.IsGarageEligible ? "Eligible" : "Not eligible"));
        AddChart(overview, "Revenue Trend", ProfitByDay(jobs));
        AddChart(overview, "Visit Trend", CountByDay(jobs));
        overview.TopLists.Add(new("Top Destinations", RouteRows(company, company.Routes ?? [], city.Id, true).Take(10)));
        overview.TopLists.Add(new("Top Origins", RouteRows(company, company.Routes ?? [], city.Id, false).Take(10)));
        overview.TopLists.Add(new("Most Common Cargo", FrequentCargo(jobs).Take(10)));
        AddRecentJobs(overview, company, jobs, "Recent Jobs");
        return overview;
    }
}

internal static class JobOverviewBuilder
{
    public static OverviewViewModel Build(CompanyDto company, MissionDto job)
    {
        var overview = Create(string.IsNullOrWhiteSpace(job.Cargo) ? job.Id : job.Cargo!, $"{RowFormatting.Value(job.SourceCity)} to {RowFormatting.Value(job.TargetCity)}", RowFormatting.Money(job.Profit));
        AddCards(
            overview,
            ("Profit", RowFormatting.Money(job.Profit), "Job result"),
            ("Distance", "-", "Not captured"),
            ("Cargo", RowFormatting.Value(job.Cargo), "Freight"),
            ("Origin", RowFormatting.Value(job.SourceCity), "Source"),
            ("Destination", RowFormatting.Value(job.TargetCity), "Target"));
        overview.TopLists.Add(new("Related", RelatedJobRows(company, job)));
        return overview;
    }
}

internal static class OverviewBuilderHelpers
{
    public static OverviewViewModel Create(string title, string subtitle, string value) =>
        new() { Header = new(title, subtitle, value) };

    public static void AddCards(OverviewViewModel overview, params (string Label, string Value, string Detail)[] cards)
    {
        foreach (var card in cards)
            overview.SummaryCards.Add(new(card.Label, card.Value, card.Detail));
    }

    public static void AddChart(OverviewViewModel overview, string title, IEnumerable<(string Label, long Value)> points)
    {
        var materialized = points.ToArray();
        if (materialized.Length == 0)
            return;

        overview.TrendCharts.Add(new(title, materialized.Select(point => point.Value), materialized.Select(point => point.Label)));
    }

    public static IEnumerable<(string Label, long Value)> TrendOrDailyProfit(SparklineDto? trend, IReadOnlyCollection<MissionDto> jobs) =>
        trend?.Points.Count > 0
            ? trend.Points.Select(point => (point.GameDay.ToString(), point.Value))
            : ProfitByDay(jobs);

    public static IEnumerable<(string Label, long Value)> ProfitByDay(IEnumerable<MissionDto> jobs) =>
        jobs.Where(job => job.TimestampDay.HasValue)
            .GroupBy(job => job.TimestampDay!.Value)
            .OrderBy(group => group.Key)
            .Select(group => (group.Key.ToString(), group.Sum(job => job.Profit)));

    public static IEnumerable<(string Label, long Value)> CountByDay(IEnumerable<MissionDto> jobs) =>
        jobs.Where(job => job.TimestampDay.HasValue)
            .GroupBy(job => job.TimestampDay!.Value)
            .OrderBy(group => group.Key)
            .Select(group => (group.Key.ToString(), (long)group.Count()));

    public static IReadOnlyList<MissionDto> CompleteDayMissions(IEnumerable<MissionDto> jobs)
    {
        var timestamped = jobs.Where(job => job.TimestampDay.HasValue).ToArray();
        if (timestamped.Length == 0)
            return [];

        var latestDay = timestamped.Max(job => job.TimestampDay!.Value);
        return [.. timestamped.Where(job => job.TimestampDay!.Value < latestDay)];
    }

    public static void AddRecentJobs(OverviewViewModel overview, CompanyDto company, IEnumerable<MissionDto> jobs, string title)
    {
        overview.RecentActivities.Add(new(
            title,
            jobs.OrderByDescending(job => job.TimestampDay ?? int.MinValue)
                .ThenByDescending(job => job.Profit)
                .Take(10)
                .Select(job => Rows.Job(company, job))));
    }

    public static IEnumerable<GridRowViewModel> FrequentCities(CompanyDto company, IEnumerable<MissionDto> jobs) =>
        jobs.SelectMany(job => new[] { job.SourceCity, job.TargetCity })
            .Where(city => !string.IsNullOrWhiteSpace(city))
            .GroupBy(city => city!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => new GridRowViewModel(
                company.Cities?.FirstOrDefault(city => Same(city.Id, group.Key))?.DisplayName ?? group.Key,
                RowFormatting.Count(group.Count()),
                "Visits",
                "Related jobs",
                [])
            {
                Target = new(ExplorerNodeKind.City, company.Id, group.Key),
                ProfitSort = group.Count()
            });

    public static IEnumerable<GridRowViewModel> FrequentCargo(IEnumerable<MissionDto> jobs) =>
        jobs.Where(job => !string.IsNullOrWhiteSpace(job.Cargo))
            .GroupBy(job => job.Cargo!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => new GridRowViewModel(group.Key, RowFormatting.Count(group.Count()), "Jobs", "Cargo frequency", [])
            {
                ProfitSort = group.Count()
            });

    public static IEnumerable<GridRowViewModel> TopDriversByJobs(CompanyDto company, IEnumerable<MissionDto> jobs) =>
        jobs.Where(job => !string.IsNullOrWhiteSpace(job.DriverId))
            .GroupBy(job => job.DriverId!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Sum(job => job.Profit))
            .Select(group => company.Drivers.FirstOrDefault(driver => Same(driver.Id, group.Key)))
            .Where(driver => driver is not null)
            .Select(driver => Rows.Driver(company, driver!));

    public static IEnumerable<GridRowViewModel> TopGaragesByJobs(CompanyDto company, IEnumerable<MissionDto> jobs, string? fallbackGarageId) =>
        jobs.Select(job => company.Drivers.FirstOrDefault(driver => Same(driver.Id, job.DriverId))?.GarageId ?? fallbackGarageId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => company.Garages.FirstOrDefault(garage => Same(garage.Id, group.Key)))
            .Where(garage => garage is not null)
            .Select(garage => Rows.Garage(company, garage!));

    public static IEnumerable<GridRowViewModel> RouteRows(CompanyDto company, IEnumerable<RouteDto> routes, string cityId, bool outbound) =>
        routes.Where(route => outbound ? Same(route.OriginCityId, cityId) : Same(route.DestinationCityId, cityId))
            .OrderByDescending(route => route.Profit)
            .Select(route =>
            {
                var relatedCityId = outbound ? route.DestinationCityId : route.OriginCityId;
                var relatedCity = company.Cities?.FirstOrDefault(city => Same(city.Id, relatedCityId));
                return new GridRowViewModel(
                    relatedCity?.DisplayName ?? relatedCityId,
                    RowFormatting.Money(route.Profit),
                    $"{route.JobCount:N0} jobs",
                    $"{route.ProfitPerMile:0.00}/mi",
                    [])
                {
                    Target = new(ExplorerNodeKind.City, company.Id, relatedCityId),
                    ProfitSort = route.Profit
                };
            });

    public static IEnumerable<GridRowViewModel> RelatedJobRows(CompanyDto company, MissionDto job)
    {
        if (company.Drivers.FirstOrDefault(driver => Same(driver.Id, job.DriverId)) is { } driver)
            yield return Rows.Driver(company, driver);
        if (company.Trucks.FirstOrDefault(truck => Same(truck.Id, job.TruckId)) is { } truck)
            yield return Rows.Truck(company, truck);
        if ((company.Trailers ?? []).FirstOrDefault(trailer => Same(trailer.LicensePlate, job.TrailerLicensePlate) || Same(trailer.Id, job.TrailerId)) is { } trailer)
            yield return Rows.Trailer(company, trailer);
        if (company.Garages.FirstOrDefault(garage => Same(garage.Id, job.GarageId)) is { } garage)
            yield return Rows.Garage(company, garage);
    }
}
