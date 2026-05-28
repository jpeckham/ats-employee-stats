using System.Security.Cryptography;
using System.Text;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Application.Statistics;

public static class CloudAggregateBuilder
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentMetricVersion = 1;

    public static CloudAggregatePayloadDto Build(
        AtsStatistics statistics,
        int windowDays,
        int sourceSnapshotCount,
        string appVersion,
        string? gameVersion = null)
    {
        var allCompanies = statistics.Companies;
        var now = DateTimeOffset.UtcNow;

        var windowStart = int.MaxValue;
        var windowEnd = int.MinValue;
        foreach (var company in allCompanies)
        {
            foreach (var trend in company.ProfitTrends)
            {
                if (trend.GameDay < windowStart) windowStart = trend.GameDay;
                if (trend.GameDay > windowEnd) windowEnd = trend.GameDay;
            }
        }

        if (windowStart == int.MaxValue) windowStart = 0;
        if (windowEnd == int.MinValue) windowEnd = 0;

        return new CloudAggregatePayloadDto(
            SchemaVersion: CurrentSchemaVersion,
            MetricVersion: CurrentMetricVersion,
            AppVersion: appVersion,
            GameVersion: gameVersion,
            GeneratedAtUtc: now,
            WindowDays: windowDays,
            WindowStartGameDay: windowStart,
            WindowEndGameDay: windowEnd,
            SourceSnapshotCount: sourceSnapshotCount,
            Routes: BuildRoutes(allCompanies),
            Cities: BuildCities(allCompanies),
            TruckModels: BuildTruckModels(allCompanies),
            TrailerTypes: BuildTrailerTypes(allCompanies),
            Drivers: BuildDrivers(allCompanies),
            Garages: BuildGarages(allCompanies));
    }

    private static IReadOnlyList<CloudRouteAggregateDto> BuildRoutes(
        IReadOnlyList<CompanyStatistics> companies)
    {
        var byRoute = new Dictionary<(string, string), (long profit, int jobs)>(
            EqualityComparer<(string, string)>.Default);

        foreach (var company in companies)
        {
            foreach (var route in company.Routes)
            {
                var key = (route.OriginCityId, route.DestinationCityId);
                var existing = byRoute.GetValueOrDefault(key);
                byRoute[key] = (existing.profit + route.Profit, existing.jobs + route.JobCount);
            }
        }

        return byRoute
            .Select(kv => new CloudRouteAggregateDto(
                OriginCityId: kv.Key.Item1,
                DestinationCityId: kv.Key.Item2,
                TotalProfit: kv.Value.profit,
                JobCount: kv.Value.jobs,
                ProfitPerMile: kv.Value.jobs > 0 ? (decimal)kv.Value.profit / kv.Value.jobs : 0m,
                SampleCount: kv.Value.jobs))
            .OrderByDescending(r => r.TotalProfit)
            .ToList();
    }

    private static IReadOnlyList<CloudCityAggregateDto> BuildCities(
        IReadOnlyList<CompanyStatistics> companies)
    {
        var byCity = new Dictionary<string, (long outbound, long inbound, long bidi, int visits)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var company in companies)
        {
            foreach (var city in company.Cities)
            {
                var existing = byCity.GetValueOrDefault(city.Id);
                byCity[city.Id] = (
                    existing.outbound + city.OutboundProfit,
                    existing.inbound + city.InboundProfit,
                    existing.bidi + city.BidirectionalProfit,
                    existing.visits + city.VisitCount);
            }
        }

        return byCity
            .Select(kv => new CloudCityAggregateDto(
                CityId: kv.Key,
                OutboundProfit: kv.Value.outbound,
                InboundProfit: kv.Value.inbound,
                BidirectionalProfit: kv.Value.bidi,
                VisitCount: kv.Value.visits,
                SampleCount: kv.Value.visits))
            .OrderByDescending(c => c.BidirectionalProfit)
            .ToList();
    }

    private static IReadOnlyList<CloudTruckModelAggregateDto> BuildTruckModels(
        IReadOnlyList<CompanyStatistics> companies)
    {
        var byModel = new Dictionary<string, (long profit, int jobs)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var company in companies)
        {
            foreach (var truck in company.Trucks)
            {
                if (string.IsNullOrWhiteSpace(truck.ModelName)) continue;
                var existing = byModel.GetValueOrDefault(truck.ModelName);
                byModel[truck.ModelName] = (existing.profit + truck.Profit, existing.jobs + 1);
            }
        }

        return byModel
            .Select(kv => new CloudTruckModelAggregateDto(
                ModelName: kv.Key,
                TotalProfit: kv.Value.profit,
                JobCount: kv.Value.jobs,
                SampleCount: kv.Value.jobs))
            .OrderByDescending(t => t.TotalProfit)
            .ToList();
    }

    private static IReadOnlyList<CloudTrailerTypeAggregateDto> BuildTrailerTypes(
        IReadOnlyList<CompanyStatistics> companies)
    {
        var byType = new Dictionary<string, (long profit, int jobs)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var company in companies)
        {
            foreach (var tt in company.TrailerTypes)
            {
                var existing = byType.GetValueOrDefault(tt.Id);
                byType[tt.Id] = (existing.profit + tt.Profit, existing.jobs + tt.MissionCount);
            }
        }

        return byType
            .Select(kv => new CloudTrailerTypeAggregateDto(
                TrailerTypeId: kv.Key,
                TotalProfit: kv.Value.profit,
                JobCount: kv.Value.jobs,
                SampleCount: kv.Value.jobs))
            .OrderByDescending(t => t.TotalProfit)
            .ToList();
    }

    private static IReadOnlyList<CloudDriverAggregateDto> BuildDrivers(
        IReadOnlyList<CompanyStatistics> companies)
    {
        return companies
            .SelectMany(c => c.Drivers)
            .Select(d => new CloudDriverAggregateDto(
                AnonymousDriverId: AnonymizeId(d.Id),
                TotalProfit: d.Profit,
                JobCount: c_GetDriverJobCount(companies, d.Id),
                SampleCount: 1))
            .OrderByDescending(d => d.TotalProfit)
            .ToList();
    }

    private static IReadOnlyList<CloudGarageAggregateDto> BuildGarages(
        IReadOnlyList<CompanyStatistics> companies)
    {
        return companies
            .SelectMany(c => c.Garages)
            .Select(g => new CloudGarageAggregateDto(
                CityId: ExtractCityFromGarageId(g.Id),
                TotalProfit: g.Profit,
                DriverCount: g.EmployeeCount,
                TruckCount: g.TruckCount,
                SampleCount: 1))
            .OrderByDescending(g => g.TotalProfit)
            .ToList();
    }

    private static int c_GetDriverJobCount(IReadOnlyList<CompanyStatistics> companies, string driverId) =>
        companies
            .SelectMany(c => c.Missions)
            .Count(m => StringComparer.OrdinalIgnoreCase.Equals(m.DriverId, driverId));

    public static string AnonymizeId(string rawId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawId));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static string ExtractCityFromGarageId(string garageId)
    {
        // garage IDs are typically "garage.cityname" — extract city portion
        var dot = garageId.IndexOf('.', StringComparison.Ordinal);
        return dot >= 0 ? garageId[(dot + 1)..] : garageId;
    }
}
