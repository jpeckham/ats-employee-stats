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

    public static IReadOnlyList<DriverDto> GetGarageDrivers(CompanyDto company, string garageId) =>
        company.Drivers
            .Where(driver => IdEquals(driver.GarageId, garageId))
            .ToList();

    public static IReadOnlyList<TruckDto> GetGarageTrucks(CompanyDto company, string garageId) =>
        company.Trucks
            .Where(truck => IdEquals(truck.GarageId, garageId))
            .ToList();

    public static IReadOnlyList<MissionDto> GetDriverJobs(CompanyDto company, string driverId) =>
        company.Missions
            .Where(mission => IdEquals(mission.DriverId, driverId))
            .ToList();

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

    private static bool IdEquals(string? left, string? right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left, right);
}
