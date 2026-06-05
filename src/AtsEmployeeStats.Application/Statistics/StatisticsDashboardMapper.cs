using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Statistics;

public static class StatisticsDashboardMapper
{
    public static DashboardStatisticsDto ToDashboardDto(
        AtsStatistics statistics,
        int fromDay = 0,
        int? toDay = null,
        CollectionSortDto? sort = null,
        bool excludePlayerDriver = false)
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
            statistics.Companies.Select(company => ToCompanyDto(company, fromDay, effectiveToDay, sort, excludePlayerDriver)).ToList(),
            maxGameDay);
    }

    private static CompanyDto ToCompanyDto(
        CompanyStatistics company,
        int fromDay,
        int toDay,
        CollectionSortDto? sort = null,
        bool excludePlayerDriver = false)
    {
        var playerDriverIds = company.Drivers
            .Where(driver => driver.IsPlayer)
            .Select(driver => driver.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filteredMissions = company.Missions
            .Where(m => !m.TimestampDay.HasValue || (m.TimestampDay.Value >= fromDay && m.TimestampDay.Value <= toDay))
            .Where(m => !excludePlayerDriver ||
                string.IsNullOrWhiteSpace(m.DriverId) ||
                !playerDriverIds.Contains(m.DriverId))
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

        var garageProfit = filteredMissions
            .Where(m => m.GarageId != null)
            .GroupBy(m => m.GarageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Profit), StringComparer.OrdinalIgnoreCase);

        var rangeDays = Math.Max(1, toDay - fromDay + 1);

        var driverFirstDay = filteredMissions
            .Where(m => m.DriverId != null && m.TimestampDay.HasValue)
            .GroupBy(m => m.DriverId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Min(m => m.TimestampDay!.Value), StringComparer.OrdinalIgnoreCase);

        const int RecentWindowDays = 7;
        var recentFromDay = Math.Max(fromDay, toDay - RecentWindowDays + 1);
        var recentWindowSize = Math.Max(1, toDay - recentFromDay + 1);
        var driverRecentProfit = filteredMissions
            .Where(m => m.DriverId != null && m.TimestampDay.HasValue && m.TimestampDay.Value >= recentFromDay)
            .GroupBy(m => m.DriverId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Profit), StringComparer.OrdinalIgnoreCase);

        var nameParts = company.DisplayName.Split('|', 2);
        var displayName = nameParts[0].Trim();
        var ownerName = nameParts.Length > 1 ? nameParts[1].Trim() : (string?)null;

        var garageTrailerCount = company.Trailers
            .Where(t => !string.IsNullOrWhiteSpace(t.GarageId))
            .GroupBy(t => t.GarageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var garageDtos = company.Garages.Select(garage => new GarageDto(
            garage.Id,
            garage.DisplayName,
            garageProfit.GetValueOrDefault(garage.Id),
            MoneyPerDay(garageProfit.GetValueOrDefault(garage.Id), rangeDays),
            garage.EmployeeCount,
            garage.TruckCount,
            ToSparkline(
                company.ProfitTrends,
                "garage",
                garage.Id,
                fromDay,
                toDay,
                filteredMissions,
                mission => IdEquals(mission.GarageId, garage.Id),
                excludePlayerDriver),
            garageTrailerCount.GetValueOrDefault(garage.Id)));

        var driverDtos = company.Drivers.Select(driver =>
        {
            var driverDays = driverFirstDay.TryGetValue(driver.Id, out var firstDay)
                ? Math.Max(1, toDay - Math.Max(firstDay, fromDay) + 1)
                : rangeDays;
            return new DriverDto(
            driver.Id,
            driver.DisplayName,
            driverProfit.GetValueOrDefault(driver.Id),
            MoneyPerDay(driverProfit.GetValueOrDefault(driver.Id), driverDays),
            driver.GarageId,
            driver.TruckId,
            filteredMissions.Count(m => StringComparer.OrdinalIgnoreCase.Equals(m.DriverId, driver.Id)),
            ToSparkline(
                company.ProfitTrends,
                "driver",
                driver.Id,
                fromDay,
                toDay,
                filteredMissions,
                mission => IdEquals(mission.DriverId, driver.Id),
                excludePlayerDriver),
            MoneyPerDay(driverRecentProfit.GetValueOrDefault(driver.Id), recentWindowSize),
            driver.IsPlayer);
        });

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
            ToSparkline(
                company.ProfitTrends,
                "truck",
                truck.Id,
                fromDay,
                toDay,
                filteredMissions,
                mission => IdEquals(mission.TruckId, truck.Id),
                excludePlayerDriver)));

        var trailerDtos = company.Trailers.Select(trailer => new TrailerDto(
            trailer.Id,
            trailer.TrailerType,
            trailerRangeProfit.GetValueOrDefault(trailer.Id),
            trailer.JobCount,
            trailer.IsArticulated,
            trailer.BodyType,
            MoneyPerDay(trailerRangeProfit.GetValueOrDefault(trailer.Id), rangeDays),
            ToSparkline(
                company.ProfitTrends,
                "trailer",
                trailer.LicensePlate ?? trailer.Id,
                fromDay,
                toDay,
                filteredMissions,
                mission => IdEquals(mission.TrailerLicensePlate, trailer.LicensePlate) || IdEquals(mission.TrailerId, trailer.Id),
                excludePlayerDriver),
            trailer.GarageId,
            trailer.LicensePlate));

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
            mission.GarageId,
            mission.TrailerLicensePlate));

        var routeDtos = BuildRouteDtos(filteredMissions);
        var cityDtos = BuildCityDtos(company.Cities, filteredMissions, routeDtos, playerDriverIds);

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
                ("recentProfitPerDay", d => d.RecentProfitPerDay),
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
                ("expansion", c => (IComparable?)c.ExpansionScore),
                ("playerOrigin", c => (IComparable?)c.PlayerOriginScore)),
            SortedList(routeDtos, sort?.RoutesSortBy, sort?.RoutesSortDir, "profit",
                ("profit", r => r.Profit),
                ("jobCount", r => (IComparable?)r.JobCount),
                ("profitPerMile", r => (IComparable?)r.ProfitPerMile),
                ("returnCoverage", r => (IComparable?)r.ReturnCoverageRatio)),
            ToSparkline(
                company.ProfitTrends,
                "company",
                company.Id,
                fromDay,
                toDay,
                filteredMissions,
                _ => true,
                excludePlayerDriver),
            company.DriverTruckAssignments.Select(a => new DriverTruckAssignmentDto(
                a.DriverId, a.TruckId, a.EffectiveFromSaveName, a.EffectiveToSaveName, a.IsCurrent)).ToList(),
            company.DriverGarageAssignments.Select(a => new DriverGarageAssignmentDto(
                a.DriverId, a.GarageId, a.EffectiveFromSaveName, a.EffectiveToSaveName, a.IsCurrent)).ToList(),
            OwnerName: ownerName,
            CurrencySymbol: company.Id.StartsWith("ets2-", StringComparison.OrdinalIgnoreCase) ? "€" : "$");
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

    private static IReadOnlyList<RouteDto> BuildRouteDtos(IReadOnlyCollection<MissionStatistic> missions)
    {
        var directedRoutes = missions
            .Where(HasRoute)
            .GroupBy(mission => (Origin: NormalizeCityId(mission.SourceCity)!, Destination: NormalizeCityId(mission.TargetCity)!))
            .ToDictionary(
                group => group.Key,
                group => (Profit: group.Sum(mission => mission.Profit), JobCount: group.Count()));

        return directedRoutes
            .Select(route =>
            {
                directedRoutes.TryGetValue((route.Key.Destination, route.Key.Origin), out var reverse);
                return new RouteDto(
                    route.Key.Origin,
                    route.Key.Destination,
                    route.Value.Profit,
                    route.Value.JobCount,
                    ProfitPerMile: 0,
                    ReturnCoverageRatio: reverse.JobCount == 0 ? 0 : Math.Min(route.Value.JobCount, reverse.JobCount) / (decimal)Math.Max(route.Value.JobCount, reverse.JobCount));
            })
            .OrderBy(route => route.OriginCityId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(route => route.DestinationCityId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<CityDto> BuildCityDtos(
        IReadOnlyCollection<CityStatistic> sourceCities,
        IReadOnlyCollection<MissionStatistic> missions,
        IReadOnlyCollection<RouteDto> routes,
        IReadOnlySet<string> playerDriverIds)
    {
        if (!missions.Any(HasRoute))
        {
            return sourceCities
                .Select(city => new CityDto(
                    city.Id,
                    city.DisplayName,
                    city.HasOwnedGarage,
                    city.IsGarageEligible,
                    city.VisitCount,
                    city.OutboundProfit,
                    city.InboundProfit,
                    city.BidirectionalProfit,
                    city.ExpansionScore,
                    city.PlayerOriginJobCount,
                    city.PlayerOriginProfit,
                    city.PlayerOriginScore))
                .ToList();
        }

        var sourceById = sourceCities.ToDictionary(city => city.Id, StringComparer.OrdinalIgnoreCase);
        var playerMissions = missions
            .Where(mission => !string.IsNullOrWhiteSpace(mission.DriverId) && playerDriverIds.Contains(mission.DriverId))
            .ToList();
        var businessMissions = missions
            .Where(mission => string.IsNullOrWhiteSpace(mission.DriverId) || !playerDriverIds.Contains(mission.DriverId))
            .ToList();
        var missionCities = missions
            .Where(HasRoute)
            .SelectMany(mission => new[] { mission.SourceCity!, mission.TargetCity! })
            .Select(NormalizeCityId)
            .Where(city => !string.IsNullOrWhiteSpace(city))
            .Select(city => city!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return missionCities
            .Select(city =>
            {
                var outbound = missions
                    .Where(mission => StringComparer.OrdinalIgnoreCase.Equals(NormalizeCityId(mission.SourceCity), city))
                    .ToList();
                var inbound = missions
                    .Where(mission => StringComparer.OrdinalIgnoreCase.Equals(NormalizeCityId(mission.TargetCity), city))
                    .ToList();
                var bidirectionalProfit = routes
                    .Where(route =>
                        (StringComparer.OrdinalIgnoreCase.Equals(route.OriginCityId, city) ||
                            StringComparer.OrdinalIgnoreCase.Equals(route.DestinationCityId, city)) &&
                        routes.Any(reverse =>
                            StringComparer.OrdinalIgnoreCase.Equals(reverse.OriginCityId, route.DestinationCityId) &&
                            StringComparer.OrdinalIgnoreCase.Equals(reverse.DestinationCityId, route.OriginCityId)))
                    .Sum(route => route.Profit);
                sourceById.TryGetValue(city, out var sourceCity);
                var hasOwnedGarage = sourceCity?.HasOwnedGarage ?? false;
                var isGarageEligible = sourceCity?.IsGarageEligible ?? false;
                var businessOutbound = businessMissions
                    .Where(mission => StringComparer.OrdinalIgnoreCase.Equals(NormalizeCityId(mission.SourceCity), city))
                    .ToList();
                var businessInbound = businessMissions
                    .Where(mission => StringComparer.OrdinalIgnoreCase.Equals(NormalizeCityId(mission.TargetCity), city))
                    .ToList();
                var expansionOutbound = businessOutbound
                    .Where(m => m.GarageId is null ||
                                !StringComparer.OrdinalIgnoreCase.Equals(ExtractGarageCitySlug(m.GarageId), city))
                    .ToList();
                var expansionScore = (hasOwnedGarage || !isGarageEligible)
                    ? 0m
                    : Math.Round(expansionOutbound.Count + businessInbound.Count + (expansionOutbound.Sum(m => m.Profit) / 10000m), 2, MidpointRounding.AwayFromZero);
                var playerOriginSignal = BuildPlayerOriginSignal(city, playerMissions);

                return new CityDto(
                    city,
                    sourceCity?.DisplayName ?? FormatRouteEndpoint(city),
                    hasOwnedGarage,
                    isGarageEligible,
                    outbound.Count + inbound.Count,
                    outbound.Sum(mission => mission.Profit),
                    inbound.Sum(mission => mission.Profit),
                    bidirectionalProfit,
                    expansionScore,
                    playerOriginSignal.JobCount,
                    playerOriginSignal.Profit,
                    playerOriginSignal.Score);
            })
            .OrderByDescending(city => city.HasOwnedGarage)
            .ThenBy(city => city.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (int JobCount, long Profit, decimal Score) BuildPlayerOriginSignal(
        string city,
        IReadOnlyCollection<MissionStatistic> playerMissions)
    {
        var playerOriginMissions = playerMissions
            .Where(HasRoute)
            .Where(mission => StringComparer.OrdinalIgnoreCase.Equals(NormalizeCityId(mission.SourceCity), city))
            .ToList();
        var jobCount = playerOriginMissions.Count;
        var profit = playerOriginMissions.Sum(mission => mission.Profit);
        var score = Math.Round(jobCount + (profit / 10000m), 2, MidpointRounding.AwayFromZero);
        return (jobCount, profit, score);
    }

    private static string? ExtractGarageCitySlug(string garageId)
    {
        var dot = garageId.IndexOf('.');
        return dot >= 0 && dot + 1 < garageId.Length ? garageId[(dot + 1)..] : null;
    }

    private static bool HasRoute(MissionStatistic mission) =>
        !string.IsNullOrWhiteSpace(mission.SourceCity) &&
        !string.IsNullOrWhiteSpace(mission.TargetCity);

    private static string? NormalizeCityId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string FormatRouteEndpoint(string value) =>
        string.Join(' ', value
            .Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    private static bool IdEquals(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        StringComparer.OrdinalIgnoreCase.Equals(left, right);

    private static SparklineDto ToSparkline(
        IReadOnlyCollection<TrendPointStatistic> trends,
        string entityKind,
        string entityId,
        int fromDay,
        int toDay,
        IReadOnlyCollection<MissionStatistic>? filteredMissions = null,
        Func<MissionStatistic, bool>? missionPredicate = null,
        bool useFilteredMissions = false)
    {
        if (useFilteredMissions && filteredMissions is not null && missionPredicate is not null)
        {
            return new SparklineDto(
                toDay - fromDay + 1,
                filteredMissions
                    .Where(missionPredicate)
                    .Where(mission => mission.TimestampDay.HasValue)
                    .GroupBy(mission => mission.TimestampDay!.Value)
                    .OrderBy(group => group.Key)
                    .Select(group => new EntityTrendPointDto(
                        group.Key,
                        SaveTimeUtc: null,
                        group.Sum(mission => mission.Profit),
                        group.Count()))
                    .ToList());
        }

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
