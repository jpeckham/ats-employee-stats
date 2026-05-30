using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Web.Services;

public static class DashboardViewModel
{
    public static CompanyDto? FindCompany(DashboardStatisticsDto? statistics, string companyId) =>
        statistics?.Companies.FirstOrDefault(company => IdEquals(company.Id, companyId));

    public static GarageDto? FindGarage(CompanyDto company, string garageId) =>
        company.Garages.FirstOrDefault(garage => IdEquals(garage.Id, garageId));

    public static DriverDto? FindDriver(CompanyDto company, string driverId) =>
        company.Drivers.FirstOrDefault(driver => IdEquals(driver.Id, driverId));

    public static TruckDto? FindTruck(CompanyDto company, string truckId) =>
        company.Trucks.FirstOrDefault(truck => IdEquals(truck.Id, truckId));

    public static TrailerDto? FindTrailer(CompanyDto company, string licensePlate) =>
        (company.Trailers ?? []).FirstOrDefault(trailer => IdEquals(trailer.LicensePlate, licensePlate));

    public static MissionDto? FindJob(CompanyDto company, string jobId) =>
        company.Missions.FirstOrDefault(job => IdEquals(job.Id, jobId));

    public static CityDto? FindCity(CompanyDto company, string cityId) =>
        (company.Cities ?? []).FirstOrDefault(city => IdEquals(city.Id, cityId));

    public static RouteDto? FindRoute(CompanyDto company, string originCityId, string destinationCityId) =>
        (company.Routes ?? []).FirstOrDefault(route =>
            IdEquals(route.OriginCityId, originCityId) &&
            IdEquals(route.DestinationCityId, destinationCityId));

    public static IReadOnlyList<DriverDto> GetGarageDrivers(CompanyDto company, string garageId) =>
        company.Drivers
            .Where(driver => IdEquals(driver.GarageId, garageId))
            .ToList();

    public static IReadOnlyList<TruckDto> GetGarageTrucks(CompanyDto company, string garageId) =>
        company.Trucks
            .Where(truck => IdEquals(truck.GarageId, garageId))
            .ToList();

    public static IReadOnlyList<TrailerDto> GetGarageTrailers(CompanyDto company, string garageId) =>
        (company.Trailers ?? [])
            .Where(t => IdEquals(t.GarageId, garageId))
            .ToList();

    public static IReadOnlyList<MissionDto> GetDriverJobs(CompanyDto company, string driverId) =>
        company.Missions
            .Where(mission => IdEquals(mission.DriverId, driverId))
            .ToList();

    public static IReadOnlyList<MissionDto> GetTruckJobs(CompanyDto company, string truckId) =>
        company.Missions
            .Where(mission => IdEquals(mission.TruckId, truckId))
            .ToList();

    public static IReadOnlyList<MissionDto> GetTrailerJobs(CompanyDto company, string trailerId) =>
        company.Missions
            .Where(mission => IdEquals(mission.TrailerId, trailerId))
            .ToList();

    public static IReadOnlyList<MissionDto> GetCityJobs(CompanyDto company, string cityId) =>
        company.Missions
            .Where(mission => IdEquals(mission.SourceCity, cityId) || IdEquals(mission.TargetCity, cityId))
            .ToList();

    public static IReadOnlyList<RouteDto> GetCityRoutes(CompanyDto company, string cityId) =>
        (company.Routes ?? [])
            .Where(route => IdEquals(route.OriginCityId, cityId) || IdEquals(route.DestinationCityId, cityId))
            .ToList();

    public static IReadOnlyList<TruckDto> GetTrailerTrucks(CompanyDto company, string trailerId)
    {
        var truckIds = GetTrailerJobs(company, trailerId)
            .Where(job => !string.IsNullOrWhiteSpace(job.TruckId))
            .Select(job => job.TruckId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return company.Trucks
            .Where(truck => truckIds.Contains(truck.Id))
            .ToList();
    }

    public static IReadOnlyList<DriverRecentJobDto> GetDriverRecentJobs(CompanyDto company, string driverId) =>
        (company.RecentDriverJobs ?? [])
            .Where(job => IdEquals(job.DriverId, driverId))
            .OrderByDescending(job => job.TimestampDay ?? int.MinValue)
            .ThenByDescending(job => job.Profit)
            .ThenBy(job => job.Id, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

    public static IReadOnlyList<TruckDto> GetDriverTrucks(CompanyDto company, string driverId)
    {
        var driver = FindDriver(company, driverId);
        if (driver is null)
        {
            return [];
        }

        var truckIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(driver.TruckId))
        {
            truckIds.Add(driver.TruckId);
        }

        foreach (var truck in company.Trucks.Where(truck => IdEquals(truck.DriverId, driverId)))
        {
            truckIds.Add(truck.Id);
        }

        foreach (var mission in GetDriverJobs(company, driverId).Where(mission => !string.IsNullOrWhiteSpace(mission.TruckId)))
        {
            truckIds.Add(mission.TruckId!);
        }

        return company.Trucks
            .Where(truck => truckIds.Contains(truck.Id))
            .ToList();
    }

    public static string GetTruckDisplayName(CompanyDto company, string? truckId)
    {
        if (string.IsNullOrWhiteSpace(truckId))
        {
            return "-";
        }

        return company.Trucks.FirstOrDefault(truck => IdEquals(truck.Id, truckId))?.DisplayName ?? truckId;
    }

    public static string GetDriverDisplayName(CompanyDto company, string? driverId)
    {
        if (string.IsNullOrWhiteSpace(driverId)) return "-";
        return company.Drivers.FirstOrDefault(d => IdEquals(d.Id, driverId))?.DisplayName ?? driverId;
    }

    public static string GetGarageDisplayName(CompanyDto company, string? garageId)
    {
        if (string.IsNullOrWhiteSpace(garageId))
        {
            return "-";
        }

        return company.Garages.FirstOrDefault(g => IdEquals(g.Id, garageId))?.DisplayName ?? garageId;
    }

    public static IReadOnlyList<DriverTruckAssignmentDto> GetDriverTruckAssignments(CompanyDto company, string driverId) =>
        (company.DriverTruckAssignments ?? [])
            .Where(a => IdEquals(a.DriverId, driverId))
            .OrderBy(a => a.IsCurrent)
            .ThenBy(a => a.EffectiveFromSaveName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<DriverGarageAssignmentDto> GetDriverGarageAssignments(CompanyDto company, string driverId) =>
        (company.DriverGarageAssignments ?? [])
            .Where(a => IdEquals(a.DriverId, driverId))
            .OrderBy(a => a.IsCurrent)
            .ThenBy(a => a.EffectiveFromSaveName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<TrailerTypeProfitItem> GetCityTrailerTypeBreakdown(CompanyDto company, string cityId) =>
        company.Missions
            .Where(m => (IdEquals(m.SourceCity, cityId) || IdEquals(m.TargetCity, cityId))
                        && !string.IsNullOrWhiteSpace(m.TrailerType)
                        && !StringComparer.OrdinalIgnoreCase.Equals(m.TrailerType, "unknown"))
            .GroupBy(m => m.TrailerType!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TrailerTypeProfitItem(g.Key, g.Sum(m => m.Profit)))
            .OrderByDescending(x => x.Profit)
            .ToList();

    private static bool IdEquals(string? left, string? right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left, right);
}

public sealed record TrailerTypeProfitItem(string TrailerType, long Profit);
