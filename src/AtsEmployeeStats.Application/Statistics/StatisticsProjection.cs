using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;
using System.Text;

namespace AtsEmployeeStats.Application.Statistics;

public static class StatisticsProjection
{
    public static AtsStatistics Build(IReadOnlyCollection<SaveSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return new AtsStatistics(null, []);
        }

        var companies = snapshots
            .GroupBy(GetCompanyKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildCompany(group.ToList()))
            .OrderByDescending(company => company.Garages.Sum(garage => garage.Profit))
            .ThenBy(company => company.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AtsStatistics(
            snapshots.Max(snapshot => snapshot.LastWritten),
            companies);
    }

    private static CompanyStatistics BuildCompany(IReadOnlyCollection<SaveSnapshot> snapshots)
    {
        var latest = snapshots.OrderByDescending(snapshot => snapshot.LastWritten).First();
        var units = latest.Document.Units;
        var unitsById = units.ToDictionary(unit => unit.Id, StringComparer.OrdinalIgnoreCase);
        var garages = units.Where(unit => unit.TypeEquals("garage")).ToList();
        var drivers = units.Where(unit => unit.TypeEquals("driver") || unit.TypeEquals("driver_ai")).ToList();
        var trucks = units
            .Where(unit => unit.TypeEquals("vehicle") || unit.TypeEquals("truck"))
            .ToList();
        var trailers = units.Where(unit => unit.TypeEquals("trailer")).ToList();
        var jobs = units.Where(unit => unit.TypeEquals("job") || unit.TypeEquals("delivery_log_entry")).ToList();

        var driverToGarage = BuildReverseLookup(garages, "employees", "drivers");
        var truckToGarage = BuildReverseLookup(garages, "vehicles");
        var driverToTruck = drivers
            .Select(driver => (DriverId: driver.Id, TruckId: FirstKnownValue(driver, "assigned_truck", "truck", "vehicle")))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.TruckId))
            .ToDictionary(pair => pair.DriverId, pair => pair.TruckId!, StringComparer.OrdinalIgnoreCase);
        var truckToDriver = driverToTruck
            .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.OrdinalIgnoreCase);
        var trailerTypesByTrailer = trailers
            .Select(trailer => (TrailerId: trailer.Id, Type: FirstKnownValue(trailer, "trailer_definition", "trailer_def", "definition")))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Type))
            .ToDictionary(pair => pair.TrailerId, pair => pair.Type!, StringComparer.OrdinalIgnoreCase);

        var garageStats = garages
            .Select(garage => new GarageStatistic(
                garage.Id,
                FirstKnownValue(garage, "city", "name") ?? garage.Id,
                SumProfitLog(garage, unitsById),
                Math.Max(garage.GetArray("employees").Count, garage.GetArray("drivers").Count),
                garage.GetArray("vehicles").Count))
            .OrderByDescending(garage => garage.Profit)
            .ThenBy(garage => garage.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var driverStats = drivers
            .Select(driver => new DriverStatistic(
                driver.Id,
                FirstKnownValue(driver, "name", "surname") ?? driver.Id,
                SumProfitLog(driver, unitsById),
                driverToGarage.GetValueOrDefault(driver.Id),
                driverToTruck.GetValueOrDefault(driver.Id)))
            .OrderByDescending(driver => driver.Profit)
            .ThenBy(driver => driver.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var truckStats = trucks
            .Select(truck => new TruckStatistic(
                truck.Id,
                FirstKnownValue(truck, "license_plate", "name") ?? truck.Id,
                SumProfitLog(truck, unitsById),
                truckToGarage.GetValueOrDefault(truck.Id),
                truckToDriver.GetValueOrDefault(truck.Id)))
            .OrderByDescending(truck => truck.Profit)
            .ThenBy(truck => truck.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missionStats = snapshots
            .SelectMany(BuildSnapshotMissions)
            .GroupBy(mission => mission.DeduplicationKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(mission => mission.LastWritten).First().Statistic)
            .Where(mission => mission.Profit != 0)
            .OrderByDescending(mission => mission.Profit)
            .ThenBy(mission => mission.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var trailerStats = missionStats
            .Where(mission => !string.IsNullOrWhiteSpace(mission.TrailerType))
            .GroupBy(mission => mission.TrailerType!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TrailerTypeStatistic(group.Key, group.Sum(mission => mission.Profit), group.Count()))
            .OrderByDescending(trailer => trailer.Profit)
            .ThenBy(trailer => trailer.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var companyName = GetCompanyDisplayName(latest);
        return new CompanyStatistics(
            NormalizeCompanyId(companyName),
            companyName,
            latest.LastWritten,
            garageStats,
            driverStats,
            truckStats,
            missionStats,
            trailerStats);
    }

    private static IEnumerable<HistoricalMission> BuildSnapshotMissions(SaveSnapshot snapshot)
    {
        var units = snapshot.Document.Units;
        var unitsById = units.ToDictionary(unit => unit.Id, StringComparer.OrdinalIgnoreCase);
        var trailers = units.Where(unit => unit.TypeEquals("trailer")).ToList();
        var trailerTypesByTrailer = trailers
            .Select(trailer => (TrailerId: trailer.Id, Type: FirstKnownValue(trailer, "trailer_definition", "trailer_def", "definition")))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Type))
            .ToDictionary(pair => pair.TrailerId, pair => pair.Type!, StringComparer.OrdinalIgnoreCase);

        return units
            .Where(unit => unit.TypeEquals("job") ||
                unit.TypeEquals("delivery_log_entry") ||
                unit.TypeEquals("profit_log_entry"))
            .Select(job =>
            {
                var mission = BuildMission(job, trailerTypesByTrailer, unitsById);
                return new HistoricalMission(mission, BuildMissionDeduplicationKey(job, mission), snapshot.LastWritten);
            });
    }

    private static string GetCompanyKey(SaveSnapshot snapshot) =>
        NormalizeCompanyId(GetCompanyDisplayName(snapshot));

    private static string GetCompanyDisplayName(SaveSnapshot snapshot)
    {
        var player = snapshot.Document.Units.FirstOrDefault(unit =>
            unit.TypeEquals("player") ||
            unit.TypeEquals("economy") ||
            unit.TypeEquals("company"));

        return FirstKnownValue(player, "company_name", "company", "player_name", "profile_name")
            ?? GetProfileDisplayName(snapshot.Name)
            ?? "default";
    }

    private static string? GetProfileDisplayName(string snapshotName)
    {
        var parts = snapshotName
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var saveIndex = Array.FindIndex(parts, part => StringComparer.OrdinalIgnoreCase.Equals(part, "save"));
        if (saveIndex <= 0)
        {
            return null;
        }

        var profileSegment = parts[saveIndex - 1];
        return DecodeHexProfileName(profileSegment) ?? profileSegment;
    }

    private static string? DecodeHexProfileName(string profileSegment)
    {
        if (profileSegment.Length == 0 ||
            profileSegment.Length % 2 != 0 ||
            profileSegment.Any(character => !Uri.IsHexDigit(character)))
        {
            return null;
        }

        var bytes = new byte[profileSegment.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(profileSegment.Substring(i * 2, 2), 16);
        }

        var decoded = Encoding.UTF8.GetString(bytes).Trim('\0', ' ');
        return string.IsNullOrWhiteSpace(decoded) ? null : decoded;
    }

    private static string NormalizeCompanyId(string companyName)
    {
        var normalized = new string(companyName
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        normalized = string.Join('-', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? "default" : normalized;
    }

    private static MissionStatistic BuildMission(
        SiiUnit job,
        IReadOnlyDictionary<string, string> trailerTypesByTrailer,
        IReadOnlyDictionary<string, SiiUnit> unitsById)
    {
        if (job.TypeEquals("delivery_log_entry"))
        {
            return BuildDeliveryLogMission(job);
        }

        var trailerId = FirstKnownValue(job, "trailer", "assigned_trailer");
        var trailerType = FirstKnownValue(job, "trailer_definition", "trailer_def");

        if (trailerType is null && trailerId is not null)
        {
            trailerTypesByTrailer.TryGetValue(trailerId, out trailerType);
        }

        if (trailerType is null && trailerId is not null && unitsById.TryGetValue(trailerId, out var trailer))
        {
            trailerType = FirstKnownValue(trailer, "trailer_definition", "trailer_def", "definition");
        }

        var cargo = FirstKnownValue(job, "cargo", "cargo_id");
        var sourceCity = FirstKnownValue(job, "source_city", "source_city_id", "origin_city");
        var targetCity = FirstKnownValue(job, "target_city", "destination_city", "destination_city_id");
        var profit = job.TypeEquals("profit_log_entry")
            ? ProfitFromEntry(job)
            : FirstLongValue(job, "income", "revenue", "profit", "pay");

        if (job.TypeEquals("profit_log_entry") &&
            (string.IsNullOrWhiteSpace(cargo) ||
                string.IsNullOrWhiteSpace(sourceCity) ||
                string.IsNullOrWhiteSpace(targetCity)))
        {
            profit = 0;
        }

        return new MissionStatistic(
            job.Id,
            FirstKnownValue(job, "driver", "employee"),
            FirstKnownValue(job, "truck", "vehicle"),
            trailerId,
            trailerType ?? "unknown",
            cargo,
            sourceCity,
            targetCity,
            profit);
    }

    private static MissionStatistic BuildDeliveryLogMission(SiiUnit entry)
    {
        var parameters = entry.GetArray("params");
        var cargo = GetArrayValue(parameters, 3);
        var sourceCity = CityFromCompany(GetArrayValue(parameters, 1));
        var targetCity = CityFromCompany(GetArrayValue(parameters, 2));
        var profit = ParseMoney(GetArrayValue(parameters, 22));

        if (string.IsNullOrWhiteSpace(cargo) ||
            string.IsNullOrWhiteSpace(sourceCity) ||
            string.IsNullOrWhiteSpace(targetCity) ||
            profit == 0)
        {
            profit = 0;
        }

        return new MissionStatistic(
            entry.Id,
            DriverId: null,
            TruckId: GetArrayValue(parameters, 16),
            TrailerId: null,
            TrailerType: "unknown",
            Cargo: cargo,
            SourceCity: sourceCity,
            TargetCity: targetCity,
            Profit: profit);
    }

    private static string BuildMissionDeduplicationKey(SiiUnit source, MissionStatistic mission)
    {
        if (source.TypeEquals("delivery_log_entry") || source.TypeEquals("profit_log_entry"))
        {
            return string.Join(
                '|',
                source.Type,
                mission.SourceCity ?? string.Empty,
                mission.TargetCity ?? string.Empty,
                mission.Cargo ?? string.Empty,
                mission.TruckId ?? string.Empty,
                mission.TrailerType ?? string.Empty,
                mission.Profit.ToString());
        }

        return source.Id;
    }

    private static Dictionary<string, string> BuildReverseLookup(IEnumerable<SiiUnit> owners, params string[] arrayNames)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var owner in owners)
        {
            foreach (var childId in arrayNames
                .SelectMany(owner.GetArray)
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                lookup.TryAdd(childId, owner.Id);
            }
        }

        return lookup;
    }

    private static long SumProfitLog(SiiUnit unit, IReadOnlyDictionary<string, SiiUnit> unitsById)
    {
        var inlineProfit = unit.GetArray("profit_log").Select(ParseLong).Sum();
        if (inlineProfit != 0)
        {
            return inlineProfit;
        }

        var profitLogId = unit.GetValue("profit_log");
        if (profitLogId is null || !unitsById.TryGetValue(profitLogId, out var profitLog))
        {
            return 0;
        }

        return profitLog
            .GetArray("stats_data")
            .Where(unitsById.ContainsKey)
            .Select(entryId => ProfitFromEntry(unitsById[entryId]))
            .Sum();
    }

    private static long ProfitFromEntry(SiiUnit entry)
    {
        var revenue = FirstLongValue(entry, "revenue", "income", "profit", "pay");
        var expenses = FirstLongValue(entry, "wage") +
            FirstLongValue(entry, "maintenance") +
            FirstLongValue(entry, "fuel");

        return revenue - expenses;
    }

    private static string? GetArrayValue(IReadOnlyList<string> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : null;

    private static string? CityFromCompany(string? companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            return null;
        }

        var lastDot = companyId.LastIndexOf('.');
        return lastDot >= 0 && lastDot + 1 < companyId.Length ? companyId[(lastDot + 1)..] : companyId;
    }

    private static long ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return decimal.TryParse(value, out var parsed)
            ? (long)Math.Round(parsed, MidpointRounding.AwayFromZero)
            : ParseLong(value);
    }

    private static string? FirstKnownValue(SiiUnit? unit, params string[] keys)
    {
        if (unit is null)
        {
            return null;
        }

        foreach (var key in keys)
        {
            var value = unit.GetValue(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static long FirstLongValue(SiiUnit unit, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = unit.GetValue(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return ParseLong(value);
            }
        }

        return 0;
    }

    private static long ParseLong(string value) =>
        long.TryParse(value, out var parsed) ? parsed : 0;
}

internal sealed record HistoricalMission(MissionStatistic Statistic, string DeduplicationKey, DateTimeOffset LastWritten);

internal static class SiiUnitExtensions
{
    public static bool TypeEquals(this SiiUnit unit, string type) =>
        StringComparer.OrdinalIgnoreCase.Equals(unit.Type, type);
}
