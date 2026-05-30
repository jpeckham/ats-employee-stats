using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Statistics;

public static class StatisticsDashboardMapper
{
    public static DashboardStatisticsDto ToDashboardDto(AtsStatistics statistics, int fromDay = 0, int? toDay = null, CollectionSortDto? sort = null)
    {
        var maxGameDay = statistics.Companies
            .SelectMany(c => c.Missions)
            .Where(m => m.TimestampDay.HasValue)
            .Select(m => m.TimestampDay!.Value)
            .DefaultIfEmpty(0)
            .Max();

        var effectiveToDay = toDay ?? maxGameDay;

        return new DashboardStatisticsDto(
            statistics.LastUpdated,
            statistics.Companies.Select(company => ToCompanyDto(company, fromDay, effectiveToDay, sort)).ToList(),
            maxGameDay);
    }

    private static CompanyDto ToCompanyDto(CompanyStatistics company, int fromDay, int toDay, CollectionSortDto? sort = null)
    {
        var filteredMissions = company.Missions
            .Where(m => !m.TimestampDay.HasValue || (m.TimestampDay.Value >= fromDay && m.TimestampDay.Value <= toDay))
            .ToList();

        var driverProfit = filteredMissions
            .Where(m => m.DriverId != null)
            .GroupBy(m => m.DriverId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Profit), StringComparer.OrdinalIgnoreCase);

        var truckProfit = filteredMissions
            .Where(m => m.TruckId != null)
            .GroupBy(m => m.TruckId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Profit), StringComparer.OrdinalIgnoreCase);

        var trailerRangeProfit = filteredMissions
            .Where(m => m.TrailerId != null)
            .GroupBy(m => m.TrailerId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Profit), StringComparer.OrdinalIgnoreCase);

        var trailerJobCount = filteredMissions
            .Where(m => m.TrailerId != null)
            .GroupBy(m => m.TrailerId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var garageProfit = filteredMissions
            .Where(m => m.GarageId != null)
            .GroupBy(m => m.GarageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Profit), StringComparer.OrdinalIgnoreCase);

        var rangeDays = Math.Max(1, toDay - fromDay + 1);

        var nameParts = company.DisplayName.Split('|', 2);
        var displayName = nameParts[0].Trim();
        var ownerName = nameParts.Length > 1 ? nameParts[1].Trim() : (string?)null;

        var garageTrailerCount = filteredMissions
            .Where(m => m.GarageId != null && m.TrailerId != null)
            .GroupBy(m => m.GarageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(m => m.TrailerId!).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                StringComparer.OrdinalIgnoreCase);

        var garageDtos = company.Garages.Select(garage => new GarageDto(
            garage.Id,
            garage.DisplayName,
            garageProfit.GetValueOrDefault(garage.Id),
            MoneyPerDay(garageProfit.GetValueOrDefault(garage.Id), rangeDays),
            garage.EmployeeCount,
            garage.TruckCount,
            ToSparkline(company.ProfitTrends, "garage", garage.Id, fromDay, toDay),
            garageTrailerCount.GetValueOrDefault(garage.Id)));

        var driverDtos = company.Drivers.Select(driver => new DriverDto(
            driver.Id,
            driver.DisplayName,
            driverProfit.GetValueOrDefault(driver.Id),
            MoneyPerDay(driverProfit.GetValueOrDefault(driver.Id), rangeDays),
            driver.GarageId,
            driver.TruckId,
            filteredMissions.Count(m => StringComparer.OrdinalIgnoreCase.Equals(m.DriverId, driver.Id)),
            ToSparkline(company.ProfitTrends, "driver", driver.Id, fromDay, toDay)));

        var truckDtos = company.Trucks.Select(truck => new TruckDto(
            truck.Id,
            truck.DisplayName,
            truckProfit.GetValueOrDefault(truck.Id),
            truck.GarageId,
            truck.DriverId,
            truck.LicensePlate,
            truck.ModelName,
            truck.DefinitionPath,
            MoneyPerDay(truckProfit.GetValueOrDefault(truck.Id), rangeDays),
            ToSparkline(company.ProfitTrends, "truck", truck.Id, fromDay, toDay)));

        var trailerDtos = company.Trailers.Select(trailer => new TrailerDto(
            trailer.Id,
            trailer.TrailerType,
            trailerRangeProfit.GetValueOrDefault(trailer.Id),
            trailerJobCount.GetValueOrDefault(trailer.Id),
            trailer.IsArticulated,
            trailer.BodyType,
            MoneyPerDay(trailerRangeProfit.GetValueOrDefault(trailer.Id), rangeDays),
            ToSparkline(company.ProfitTrends, "trailer", trailer.Id, fromDay, toDay)));

        var missionDtos = filteredMissions.Select(mission => new MissionDto(
            mission.Id,
            mission.DriverId,
            mission.TruckId,
            mission.TrailerType,
            mission.Cargo,
            mission.SourceCity,
            mission.TargetCity,
            mission.Profit,
            mission.TimestampDay,
            mission.TrailerId,
            mission.GarageId));

        var cityDtos = company.Cities.Select(city => new CityDto(
            city.Id,
            city.DisplayName,
            city.HasOwnedGarage,
            city.IsGarageEligible,
            city.VisitCount,
            city.OutboundProfit,
            city.InboundProfit,
            city.BidirectionalProfit,
            city.ExpansionScore));

        var routeDtos = company.Routes.Select(route => new RouteDto(
            route.OriginCityId,
            route.DestinationCityId,
            route.Profit,
            route.JobCount,
            route.ProfitPerMile,
            route.ReturnCoverageRatio));

        return new(
            company.Id,
            displayName,
            filteredMissions.Sum(m => m.Profit),
            SortedList(garageDtos, sort?.GaragesSortBy, sort?.GaragesSortDir, "profit",
                ("name", g => (IComparable?)g.DisplayName),
                ("profit", g => g.Profit),
                ("profitPerDay", g => g.ProfitPerDay),
                ("driverCount", g => (IComparable?)g.EmployeeCount),
                ("truckCount", g => (IComparable?)g.TruckCount),
                ("trailerCount", g => (IComparable?)g.TrailerCount)),
            SortedList(driverDtos, sort?.DriversSortBy, sort?.DriversSortDir, "profit",
                ("name", d => (IComparable?)d.DisplayName),
                ("profit", d => d.Profit),
                ("profitPerDay", d => d.ProfitPerDay),
                ("jobCount", d => (IComparable?)d.JobCount)),
            SortedList(truckDtos, sort?.TrucksSortBy, sort?.TrucksSortDir, "profit",
                ("name", t => (IComparable?)t.DisplayName),
                ("profit", t => t.Profit),
                ("profitPerDay", t => t.ProfitPerDay)),
            SortedList(missionDtos, sort?.MissionsSortBy, sort?.MissionsSortDir, "profit",
                ("profit", m => m.Profit),
                ("day", m => (IComparable?)(m.TimestampDay ?? int.MinValue))),
            filteredMissions
                .Where(m => !string.IsNullOrWhiteSpace(m.TrailerType))
                .GroupBy(m => m.TrailerType!, StringComparer.OrdinalIgnoreCase)
                .Select(g => new TrailerTypeDto(g.Key, g.Sum(m => m.Profit), g.Count()))
                .OrderByDescending(t => t.Profit)
                .ThenBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            company.RecentDriverJobs.Select(job => new DriverRecentJobDto(
                job.Id,
                job.DriverId,
                job.TruckId,
                job.Cargo,
                job.SourceCity,
                job.TargetCity,
                job.Revenue,
                job.Expenses,
                job.Profit,
                job.Distance,
                job.TimestampDay)).ToList(),
            SortedList(trailerDtos, sort?.TrailersSortBy, sort?.TrailersSortDir, "profit",
                ("name", t => (IComparable?)t.TrailerType),
                ("profit", t => t.Profit),
                ("profitPerDay", t => t.ProfitPerDay),
                ("jobCount", t => (IComparable?)t.JobCount)),
            SortedList(cityDtos, sort?.CitiesSortBy, sort?.CitiesSortDir, "expansion",
                ("name", c => (IComparable?)c.DisplayName),
                ("visitCount", c => (IComparable?)c.VisitCount),
                ("outbound", c => c.OutboundProfit),
                ("inbound", c => c.InboundProfit),
                ("total", c => c.BidirectionalProfit),
                ("expansion", c => (IComparable?)c.ExpansionScore)),
            SortedList(routeDtos, sort?.RoutesSortBy, sort?.RoutesSortDir, "profit",
                ("profit", r => r.Profit),
                ("jobCount", r => (IComparable?)r.JobCount),
                ("profitPerMile", r => (IComparable?)r.ProfitPerMile),
                ("returnCoverage", r => (IComparable?)r.ReturnCoverageRatio)),
            ToSparkline(company.ProfitTrends, "company", company.Id, fromDay, toDay),
            company.DriverTruckAssignments.Select(a => new DriverTruckAssignmentDto(
                a.DriverId, a.TruckId, a.EffectiveFromSaveName, a.EffectiveToSaveName, a.IsCurrent)).ToList(),
            company.DriverGarageAssignments.Select(a => new DriverGarageAssignmentDto(
                a.DriverId, a.GarageId, a.EffectiveFromSaveName, a.EffectiveToSaveName, a.IsCurrent)).ToList(),
            OwnerName: ownerName);
    }

    private static IReadOnlyList<T> SortedList<T>(
        IEnumerable<T> source,
        string? sortBy,
        string? sortDir,
        string defaultKey,
        params (string Key, Func<T, IComparable?> Selector)[] columns)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        var key = sortBy ?? defaultKey;
        var col = Array.Find(columns, c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
        if (col.Selector is null)
            col = Array.Find(columns, c => string.Equals(c.Key, defaultKey, StringComparison.OrdinalIgnoreCase));
        if (col.Selector is null)
            return source.ToList();
        return desc
            ? [.. source.OrderByDescending(x => col.Selector(x))]
            : [.. source.OrderBy(x => col.Selector(x))];
    }

    private static long MoneyPerDay(long value, int rangeDays) =>
        (long)Math.Round(value / (decimal)rangeDays, MidpointRounding.AwayFromZero);

    private static SparklineDto ToSparkline(
        IReadOnlyCollection<TrendPointStatistic> trends,
        string entityKind,
        string entityId,
        int fromDay,
        int toDay)
    {
        var matching = trends
            .Where(trend =>
                StringComparer.OrdinalIgnoreCase.Equals(trend.EntityKind, entityKind) &&
                StringComparer.OrdinalIgnoreCase.Equals(trend.EntityId, entityId))
            .OrderBy(trend => trend.GameDay)
            .ToList();

        return new SparklineDto(
            toDay - fromDay + 1,
            matching
                .Where(trend => trend.GameDay >= fromDay && trend.GameDay <= toDay)
                .Select(trend => new EntityTrendPointDto(
                    trend.GameDay,
                    SaveTimeUtc: null,
                    trend.Profit,
                    trend.SampleCount))
                .ToList());
    }
}
