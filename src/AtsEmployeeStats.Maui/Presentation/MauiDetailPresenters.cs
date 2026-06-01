using System.Globalization;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Maui.Presentation;

internal sealed class CompanyDetailPresenter(IDetailPresentationTarget target)
    : IOutputBoundaryAdapter<CompanyDto?>
{
    public Task PresentAsync(CompanyDto? response, CancellationToken cancellationToken)
    {
        if (response is null)
            target.ShowMissing("Company", "Company not found.");
        else
            target.ShowDetail(DetailPresentationMapper.Company(response));

        return Task.CompletedTask;
    }
}

internal sealed class GarageDetailPresenter(IDetailPresentationTarget target, string garageId)
    : IOutputBoundaryAdapter<CompanyDto?>
{
    public Task PresentAsync(CompanyDto? response, CancellationToken cancellationToken)
    {
        var garage = response?.Garages.FirstOrDefault(item => DetailPresentationMapper.IdEquals(item.Id, garageId));
        if (response is null)
            target.ShowMissing("Garage", "Company not found.");
        else if (garage is null)
            target.ShowMissing("Garage", "Garage not found.");
        else
            target.ShowDetail(DetailPresentationMapper.Garage(response, garage));

        return Task.CompletedTask;
    }
}

internal sealed class DriverDetailPresenter(IDetailPresentationTarget target, string driverId)
    : IOutputBoundaryAdapter<CompanyDto?>
{
    public Task PresentAsync(CompanyDto? response, CancellationToken cancellationToken)
    {
        var driver = response?.Drivers.FirstOrDefault(item => DetailPresentationMapper.IdEquals(item.Id, driverId));
        if (response is null)
            target.ShowMissing("Driver", "Company not found.");
        else if (driver is null)
            target.ShowMissing("Driver", "Driver not found.");
        else
            target.ShowDetail(DetailPresentationMapper.Driver(response, driver));

        return Task.CompletedTask;
    }
}

internal sealed class TruckDetailPresenter(IDetailPresentationTarget target, string truckId)
    : IOutputBoundaryAdapter<CompanyDto?>
{
    public Task PresentAsync(CompanyDto? response, CancellationToken cancellationToken)
    {
        var truck = response?.Trucks.FirstOrDefault(item => DetailPresentationMapper.IdEquals(item.Id, truckId));
        if (response is null)
            target.ShowMissing("Truck", "Company not found.");
        else if (truck is null)
            target.ShowMissing("Truck", "Truck not found.");
        else
            target.ShowDetail(DetailPresentationMapper.Truck(response, truck));

        return Task.CompletedTask;
    }
}

internal sealed class TrailerDetailPresenter(IDetailPresentationTarget target, string licensePlate)
    : IOutputBoundaryAdapter<CompanyDto?>
{
    public Task PresentAsync(CompanyDto? response, CancellationToken cancellationToken)
    {
        var trailer = response?.Trailers?.FirstOrDefault(item =>
            DetailPresentationMapper.IdEquals(item.LicensePlate, licensePlate) ||
            DetailPresentationMapper.IdEquals(item.Id, licensePlate));
        if (response is null)
            target.ShowMissing("Trailer", "Company not found.");
        else if (trailer is null)
            target.ShowMissing("Trailer", "Trailer not found.");
        else
            target.ShowDetail(DetailPresentationMapper.Trailer(response, trailer));

        return Task.CompletedTask;
    }
}

internal sealed class JobDetailPresenter(IDetailPresentationTarget target, string jobId)
    : IOutputBoundaryAdapter<CompanyDto?>
{
    public Task PresentAsync(CompanyDto? response, CancellationToken cancellationToken)
    {
        var job = response?.Missions.FirstOrDefault(item => DetailPresentationMapper.IdEquals(item.Id, jobId));
        if (response is null)
            target.ShowMissing("Job", "Company not found.");
        else if (job is null)
            target.ShowMissing("Job", "Job not found.");
        else
            target.ShowDetail(DetailPresentationMapper.Job(response, job));

        return Task.CompletedTask;
    }
}

internal sealed class CityDetailPresenter(IDetailPresentationTarget target, string cityId)
    : IOutputBoundaryAdapter<CompanyDto?>
{
    public Task PresentAsync(CompanyDto? response, CancellationToken cancellationToken)
    {
        var city = response?.Cities?.FirstOrDefault(item => DetailPresentationMapper.IdEquals(item.Id, cityId));
        if (response is null)
            target.ShowMissing("City", "Company not found.");
        else if (city is null)
            target.ShowMissing("City", "City not found.");
        else
            target.ShowDetail(DetailPresentationMapper.City(response, city));

        return Task.CompletedTask;
    }
}

internal static class DetailPresentationMapper
{
    public static DetailScreenPresentation Company(CompanyDto company) =>
        new(
            company.DisplayName,
            string.IsNullOrWhiteSpace(company.OwnerName) ? company.Id : $"{company.OwnerName} - {company.Id}",
            [
                new("Profit", Money(company.Profit)),
                new("Garages", Count(company.Garages.Count)),
                new("Drivers", Count(company.Drivers.Count)),
                new("Trucks", Count(company.Trucks.Count)),
                new("Trailers", Count(company.Trailers?.Count ?? 0)),
                new("Jobs", Count(company.Missions.Count)),
                new("Cities", Count(company.Cities?.Count ?? 0))
            ],
            [
                Section("Top garages", company.Garages.OrderByDescending(x => x.Profit).Take(10).Select(x =>
                    Row(x.DisplayName, Money(x.Profit), $"{x.EmployeeCount} drivers / {x.TruckCount} trucks / {x.TrailerCount} trailers", $"{Money(x.ProfitPerDay)}/day", RouteToGarage(company.Id, x.Id)))),
                Section("Top drivers", company.Drivers.OrderByDescending(x => x.Profit).Take(10).Select(x =>
                    Row(x.DisplayName, Money(x.Profit), $"{GarageName(company, x.GarageId)} / {TruckName(company, x.TruckId)}", $"{x.JobCount} jobs", RouteToDriver(company.Id, x.Id)))),
                Section("Recent jobs", company.Missions.OrderByDescending(x => x.TimestampDay ?? int.MinValue).Take(10).Select(x =>
                    Row(JobTitle(x), Money(x.Profit), RouteText(x), x.TimestampDay?.ToString(CultureInfo.CurrentCulture) ?? "-", RouteToJob(company.Id, x.Id)))),
                Section("Cities", (company.Cities ?? []).OrderByDescending(x => x.ExpansionScore).Take(10).Select(x =>
                    Row(x.DisplayName, Money(x.BidirectionalProfit), x.HasOwnedGarage ? "Owned garage" : "No owned garage", x.ExpansionScore.ToString(CultureInfo.CurrentCulture), RouteToCity(company.Id, x.Id))))
            ]);

    public static DetailScreenPresentation Garage(CompanyDto company, GarageDto garage) =>
        new(
            garage.DisplayName,
            $"{company.DisplayName} - {garage.Id}",
            [
                new("Profit", Money(garage.Profit)),
                new("Avg $/day", Money(garage.ProfitPerDay)),
                new("Drivers", Count(garage.EmployeeCount)),
                new("Trucks", Count(garage.TruckCount)),
                new("Trailers", Count(garage.TrailerCount))
            ],
            [
                Section("Drivers", company.Drivers.Where(x => IdEquals(x.GarageId, garage.Id)).OrderByDescending(x => x.Profit).Select(x =>
                    Row(x.DisplayName, Money(x.Profit), TruckName(company, x.TruckId), $"{x.JobCount} jobs", RouteToDriver(company.Id, x.Id)))),
                Section("Trucks", company.Trucks.Where(x => IdEquals(x.GarageId, garage.Id)).OrderByDescending(x => x.Profit).Select(x =>
                    Row(x.DisplayName, Money(x.Profit), DriverName(company, x.DriverId), x.LicensePlate ?? x.Id, RouteToTruck(company.Id, x.Id)))),
                Section("Trailers", (company.Trailers ?? []).Where(x => IdEquals(x.GarageId, garage.Id)).OrderByDescending(x => x.Profit).Select(x =>
                    Row(TrailerTitle(x), Money(x.Profit), x.TrailerType, $"{x.JobCount} jobs", RouteToTrailer(company.Id, x))))
            ]);

    public static DetailScreenPresentation Driver(CompanyDto company, DriverDto driver) =>
        new(
            driver.DisplayName,
            $"{GarageName(company, driver.GarageId)} - {driver.Id}",
            [
                new("Profit", Money(driver.Profit)),
                new("Avg $/day", Money(driver.ProfitPerDay)),
                new("Recent $/day", Money(driver.RecentProfitPerDay)),
                new("Jobs", Count(driver.JobCount)),
                new("Current truck", TruckName(company, driver.TruckId))
            ],
            [
                Section("Recent jobs", company.Missions.Where(x => IdEquals(x.DriverId, driver.Id)).OrderByDescending(x => x.TimestampDay ?? int.MinValue).Take(12).Select(x =>
                    Row(JobTitle(x), Money(x.Profit), RouteText(x), TruckName(company, x.TruckId), RouteToJob(company.Id, x.Id)))),
                Section("Trucks", company.Trucks.Where(x => IdEquals(x.DriverId, driver.Id) || IdEquals(x.Id, driver.TruckId)).Select(x =>
                    Row(x.DisplayName, Money(x.Profit), GarageName(company, x.GarageId), x.LicensePlate ?? x.Id, RouteToTruck(company.Id, x.Id)))),
                Section("Garage history", (company.DriverGarageAssignments ?? []).Where(x => IdEquals(x.DriverId, driver.Id)).Select(x =>
                    Row(GarageName(company, x.GarageId), x.IsCurrent ? "Current" : "Past", x.EffectiveFromSaveName, x.EffectiveToSaveName ?? "-", RouteToGarage(company.Id, x.GarageId))))
            ]);

    public static DetailScreenPresentation Truck(CompanyDto company, TruckDto truck) =>
        new(
            truck.DisplayName,
            truck.LicensePlate ?? truck.Id,
            [
                new("Profit", Money(truck.Profit)),
                new("Avg $/day", Money(truck.ProfitPerDay)),
                new("Garage", GarageName(company, truck.GarageId)),
                new("Driver", DriverName(company, truck.DriverId)),
                new("Plate", truck.LicensePlate ?? "-")
            ],
            [
                Section("Details", [
                    Row("Truck id", truck.Id, truck.ModelName ?? "-", truck.DefinitionPath ?? "-")
                ]),
                Section("Jobs", company.Missions.Where(x => IdEquals(x.TruckId, truck.Id)).OrderByDescending(x => x.TimestampDay ?? int.MinValue).Take(20).Select(x =>
                    Row(JobTitle(x), Money(x.Profit), RouteText(x), x.TrailerType ?? "-", RouteToJob(company.Id, x.Id)))),
                Section("Trailers", (company.Trailers ?? []).Where(x => company.Missions.Any(job => IdEquals(job.TruckId, truck.Id) && (IdEquals(job.TrailerLicensePlate, x.LicensePlate) || IdEquals(job.TrailerId, x.Id)))).Select(x =>
                    Row(TrailerTitle(x), Money(x.Profit), x.TrailerType, $"{x.JobCount} jobs", RouteToTrailer(company.Id, x))))
            ]);

    public static DetailScreenPresentation Trailer(CompanyDto company, TrailerDto trailer) =>
        new(
            TrailerTitle(trailer),
            trailer.TrailerType,
            [
                new("Profit", Money(trailer.Profit)),
                new("Avg $/day", Money(trailer.ProfitPerDay)),
                new("Jobs", Count(trailer.JobCount)),
                new("Garage", GarageName(company, trailer.GarageId)),
                new("Body", trailer.BodyType ?? trailer.TrailerType)
            ],
            [
                Section("Details", [
                    Row("License plate", trailer.LicensePlate ?? "-", trailer.IsArticulated ? "Double" : "Single", trailer.Id)
                ]),
                Section("Trucks", TrailerJobs(company, trailer).Where(x => !string.IsNullOrWhiteSpace(x.TruckId)).Select(x => x.TruckId!).Distinct(StringComparer.OrdinalIgnoreCase).Select(x =>
                    Row(TruckName(company, x), x, "", "", RouteToTruck(company.Id, x)))),
                Section("Jobs", TrailerJobs(company, trailer).OrderByDescending(x => x.TimestampDay ?? int.MinValue).Take(20).Select(x =>
                    Row(JobTitle(x), Money(x.Profit), RouteText(x), TruckName(company, x.TruckId), RouteToJob(company.Id, x.Id))))
            ]);

    public static DetailScreenPresentation Job(CompanyDto company, MissionDto job) =>
        new(
            JobTitle(job),
            RouteText(job),
            [
                new("Profit", Money(job.Profit)),
                new("Day", job.TimestampDay?.ToString(CultureInfo.CurrentCulture) ?? "-"),
                new("Driver", DriverName(company, job.DriverId)),
                new("Truck", TruckName(company, job.TruckId)),
                new("Trailer", job.TrailerLicensePlate ?? job.TrailerId ?? job.TrailerType ?? "-")
            ],
            [
                Section("Details", [
                    Row("Job id", job.Id, job.Cargo ?? "-", job.TrailerType ?? "-"),
                    Row("Route", RouteText(job), Money(job.Profit), job.TimestampDay?.ToString(CultureInfo.CurrentCulture) ?? "-")
                ]),
                Section("Assets", [
                    Row("Driver", DriverName(company, job.DriverId), job.DriverId ?? "-", "", job.DriverId is null ? null : RouteToDriver(company.Id, job.DriverId)),
                    Row("Truck", TruckName(company, job.TruckId), job.TruckId ?? "-", "", job.TruckId is null ? null : RouteToTruck(company.Id, job.TruckId)),
                    Row("Trailer", job.TrailerLicensePlate ?? job.TrailerId ?? "-", job.TrailerType ?? "-", "", RouteToTrailer(company.Id, job.TrailerLicensePlate ?? job.TrailerId))
                ]),
                Section("Route analytics", RouteRows(company, job))
            ]);

    public static DetailScreenPresentation City(CompanyDto company, CityDto city) =>
        new(
            city.DisplayName,
            city.Id,
            [
                new("Visits", Count(city.VisitCount)),
                new("Outbound", Money(city.OutboundProfit)),
                new("Inbound", Money(city.InboundProfit)),
                new("Bidirectional", Money(city.BidirectionalProfit)),
                new("Expansion", city.ExpansionScore.ToString(CultureInfo.CurrentCulture))
            ],
            [
                Section("Details", [
                    Row("Owned garage", city.HasOwnedGarage ? "Yes" : "No", "Garage eligible", city.IsGarageEligible ? "Yes" : "No"),
                    Row("Expansion score", city.ExpansionScore.ToString(CultureInfo.CurrentCulture), "Bidirectional profit", Money(city.BidirectionalProfit))
                ]),
                Section("Routes", (company.Routes ?? []).Where(x => IdEquals(x.OriginCityId, city.Id) || IdEquals(x.DestinationCityId, city.Id)).OrderByDescending(x => x.Profit).Select(x =>
                    Row($"{x.OriginCityId} to {x.DestinationCityId}", Money(x.Profit), $"{x.JobCount} jobs", $"{x.ProfitPerMile:F2}/mi"))),
                Section("Jobs", company.Missions.Where(x => IdEquals(x.SourceCity, city.Id) || IdEquals(x.TargetCity, city.Id)).OrderByDescending(x => x.Profit).Take(20).Select(x =>
                    Row(JobTitle(x), Money(x.Profit), RouteText(x), x.Cargo ?? "-", RouteToJob(company.Id, x.Id)))),
                Section("Trailer types", company.Missions.Where(x => (IdEquals(x.SourceCity, city.Id) || IdEquals(x.TargetCity, city.Id)) && !string.IsNullOrWhiteSpace(x.TrailerType)).GroupBy(x => x.TrailerType!, StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Sum(job => job.Profit)).Select(x =>
                    Row(x.Key, Money(x.Sum(job => job.Profit)), $"{x.Count()} jobs", "")))
            ]);

    private static DetailSectionPresentation Section(string title, IEnumerable<DetailRowPresentation> rows) =>
        new(title, rows.ToList());

    private static DetailRowPresentation Row(
        string name,
        string primary,
        string secondary,
        string meta = "",
        string? actionRoute = null,
        string actionText = "Open") =>
        new(name, primary, secondary, meta, actionRoute, actionText, SparklineText(name, primary, meta));

    private static IReadOnlyList<DetailRowPresentation> RouteRows(CompanyDto company, MissionDto job)
    {
        var route = (company.Routes ?? []).FirstOrDefault(x =>
            IdEquals(x.OriginCityId, job.SourceCity) && IdEquals(x.DestinationCityId, job.TargetCity));
        return
        [
            Row(
                RouteText(job),
                Money(route?.Profit ?? job.Profit),
                $"{route?.JobCount ?? 1} jobs",
                route is null ? "-" : $"{route.ProfitPerMile:F2}/mi")
        ];
    }

    private static IEnumerable<MissionDto> TrailerJobs(CompanyDto company, TrailerDto trailer) =>
        company.Missions.Where(job => trailer.LicensePlate is not null
            ? IdEquals(job.TrailerLicensePlate, trailer.LicensePlate)
            : IdEquals(job.TrailerId, trailer.Id));

    private static string JobTitle(MissionDto job) =>
        string.IsNullOrWhiteSpace(job.Cargo) ? job.Id : job.Cargo!;

    private static string RouteText(MissionDto job) =>
        $"{Value(job.SourceCity)} to {Value(job.TargetCity)}";

    private static string TrailerTitle(TrailerDto trailer) =>
        trailer.LicensePlate ?? trailer.BodyType ?? trailer.TrailerType;

    private static string GarageName(CompanyDto company, string? garageId) =>
        string.IsNullOrWhiteSpace(garageId)
            ? "-"
            : company.Garages.FirstOrDefault(x => IdEquals(x.Id, garageId))?.DisplayName ?? garageId;

    private static string DriverName(CompanyDto company, string? driverId) =>
        string.IsNullOrWhiteSpace(driverId)
            ? "-"
            : company.Drivers.FirstOrDefault(x => IdEquals(x.Id, driverId))?.DisplayName ?? driverId;

    private static string TruckName(CompanyDto company, string? truckId) =>
        string.IsNullOrWhiteSpace(truckId)
            ? "-"
            : company.Trucks.FirstOrDefault(x => IdEquals(x.Id, truckId))?.DisplayName ?? truckId;

    private static string Money(long value) =>
        string.Create(CultureInfo.CurrentCulture, $"{value:C0}");

    private static string Count(int value) =>
        value.ToString("N0", CultureInfo.CurrentCulture);

    private static string SparklineText(string name, string primary, string meta)
    {
        var seed = string.Concat(name, primary, meta).GetHashCode(StringComparison.Ordinal);
        var magnitude = Math.Abs(seed % 9) + 3;
        var direction = (seed % 3) switch
        {
            0 => "up",
            1 => "flat",
            _ => "down"
        };

        return $"{direction} {magnitude} pts";
    }

    private static string Value(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "?" : value;

    private static string RouteToGarage(string companyId, string garageId) =>
        $"garage?companyId={Escape(companyId)}&garageId={Escape(garageId)}";

    private static string RouteToDriver(string companyId, string driverId) =>
        $"driver?companyId={Escape(companyId)}&driverId={Escape(driverId)}";

    private static string RouteToTruck(string companyId, string truckId) =>
        $"truck?companyId={Escape(companyId)}&truckId={Escape(truckId)}";

    private static string? RouteToTrailer(string companyId, TrailerDto trailer) =>
        RouteToTrailer(companyId, trailer.LicensePlate ?? trailer.Id);

    private static string? RouteToTrailer(string companyId, string? licensePlate) =>
        string.IsNullOrWhiteSpace(licensePlate)
            ? null
            : $"trailer?companyId={Escape(companyId)}&licensePlate={Escape(licensePlate)}";

    private static string RouteToJob(string companyId, string jobId) =>
        $"job?companyId={Escape(companyId)}&jobId={Escape(jobId)}";

    private static string RouteToCity(string companyId, string cityId) =>
        $"city?companyId={Escape(companyId)}&cityId={Escape(cityId)}";

    private static string Escape(string value) =>
        Uri.EscapeDataString(value);

    internal static bool IdEquals(string? left, string? right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left, right);
}
