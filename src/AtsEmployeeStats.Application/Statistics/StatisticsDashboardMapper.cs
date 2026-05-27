using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Statistics;

public static class StatisticsDashboardMapper
{
    public static DashboardStatisticsDto ToDashboardDto(AtsStatistics statistics, int rangeDays)
    {
        rangeDays = Math.Max(1, rangeDays);
        return new DashboardStatisticsDto(
            statistics.LastUpdated,
            statistics.Companies.Select(company => ToCompanyDto(company, rangeDays)).ToList());
    }

    private static CompanyDto ToCompanyDto(CompanyStatistics company, int rangeDays) =>
        new(
            company.Id,
            company.DisplayName,
            company.Garages.Sum(garage => garage.Profit),
            company.Garages.Select(garage => new GarageDto(
                garage.Id,
                garage.DisplayName,
                garage.Profit,
                MoneyPerDay(garage.Profit, rangeDays),
                garage.EmployeeCount,
                garage.TruckCount)).ToList(),
            company.Drivers.Select(driver => new DriverDto(
                driver.Id,
                driver.DisplayName,
                driver.Profit,
                MoneyPerDay(driver.Profit, rangeDays),
                driver.GarageId,
                driver.TruckId,
                company.Missions.Count(mission => StringComparer.OrdinalIgnoreCase.Equals(mission.DriverId, driver.Id)))).ToList(),
            company.Trucks.Select(truck => new TruckDto(
                truck.Id,
                truck.DisplayName,
                truck.Profit,
                truck.GarageId,
                truck.DriverId)).ToList(),
            company.Missions.Select(mission => new MissionDto(
                mission.Id,
                mission.DriverId,
                mission.TruckId,
                mission.TrailerType,
                mission.Cargo,
                mission.SourceCity,
                mission.TargetCity,
                mission.Profit)).ToList(),
            company.TrailerTypes.Select(trailerType => new TrailerTypeDto(
                trailerType.Id,
                trailerType.Profit,
                trailerType.MissionCount)).ToList());

    private static long MoneyPerDay(long value, int rangeDays) =>
        (long)Math.Round(value / (decimal)rangeDays, MidpointRounding.AwayFromZero);
}
