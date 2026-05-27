using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;
using System.Text;
using System.Text.RegularExpressions;

namespace AtsEmployeeStats.Application.Statistics;

public static partial class StatisticsProjection
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
        var garages = units
            .Where(unit => unit.TypeEquals("garage"))
            .Where(IsOwnedGarage)
            .ToList();
        var trailers = units.Where(unit => unit.TypeEquals("trailer")).ToList();
        var jobs = units.Where(unit => unit.TypeEquals("job") || unit.TypeEquals("delivery_log_entry")).ToList();

        var driverToGarage = BuildReverseLookup(garages, "employees", "drivers");
        var truckToGarage = BuildReverseLookup(garages, "vehicles");

        var drivers = units
            .Where(unit => unit.TypeEquals("driver") || unit.TypeEquals("driver_ai"))
            .Where(unit => driverToGarage.ContainsKey(unit.Id))
            .ToList();
        var trucks = units
            .Where(unit => unit.TypeEquals("vehicle") || unit.TypeEquals("truck"))
            .Where(unit => truckToGarage.ContainsKey(unit.Id))
            .ToList();
        var driverToTruck = BuildDriverTruckLookup(drivers, trucks, garages);
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
            .Select(truck =>
            {
                var licensePlate = CleanLicensePlate(FirstKnownValue(truck, "license_plate", "name"));
                var definitionPath = ExtractTruckDefinitionPath(truck, unitsById);
                var modelName = FormatTruckModelName(definitionPath);

                return new TruckStatistic(
                    truck.Id,
                    BuildTruckDisplayName(truck.Id, modelName, licensePlate),
                    SumProfitLog(truck, unitsById),
                    truckToGarage.GetValueOrDefault(truck.Id),
                    truckToDriver.GetValueOrDefault(truck.Id),
                    licensePlate,
                    modelName,
                    definitionPath);
            })
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
            trailerStats,
            BuildDriverRecentJobs(drivers, unitsById, driverToTruck));
    }

    private static bool IsOwnedGarage(SiiUnit garage)
    {
        var status = garage.GetValue("status");
        return status is null || !StringComparer.OrdinalIgnoreCase.Equals(status, "0");
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
                .Select(CleanSiiValue)
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                lookup.TryAdd(childId!, owner.Id);
            }
        }

        return lookup;
    }

    private static Dictionary<string, string> BuildDriverTruckLookup(
        IReadOnlyCollection<SiiUnit> drivers,
        IReadOnlyCollection<SiiUnit> trucks,
        IReadOnlyCollection<SiiUnit> garages)
    {
        var lookup = drivers
            .Select(driver => (DriverId: driver.Id, TruckId: FirstKnownValue(driver, "assigned_truck", "truck", "vehicle")))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.TruckId))
            .ToDictionary(pair => pair.DriverId, pair => pair.TruckId!, StringComparer.OrdinalIgnoreCase);

        foreach (var truck in trucks)
        {
            var driverId = FirstKnownValue(truck, "assigned_driver", "driver", "employee");
            if (!string.IsNullOrWhiteSpace(driverId))
            {
                lookup.TryAdd(driverId, truck.Id);
            }
        }

        foreach (var garage in garages)
        {
            var garageDrivers = garage.GetArray("drivers");
            if (garageDrivers.Count == 0)
            {
                garageDrivers = garage.GetArray("employees");
            }

            var garageTrucks = garage.GetArray("vehicles");
            var count = Math.Min(garageDrivers.Count, garageTrucks.Count);
            for (var index = 0; index < count; index++)
            {
                var driverId = CleanSiiValue(garageDrivers[index]);
                var truckId = CleanSiiValue(garageTrucks[index]);
                if (!string.IsNullOrWhiteSpace(driverId) && !string.IsNullOrWhiteSpace(truckId))
                {
                    lookup.TryAdd(driverId, truckId);
                }
            }
        }

        return lookup;
    }

    private static long SumProfitLog(SiiUnit unit, IReadOnlyDictionary<string, SiiUnit> unitsById)
    {
        var inlineProfit = unit.GetArray("profit_log")
            .Select(CleanSiiValue)
            .Where(value => value is not null)
            .Select(value => ParseLong(value!))
            .Sum();
        if (inlineProfit != 0)
        {
            return inlineProfit;
        }

        var profitLogId = FirstKnownValue(unit, "profit_log");
        if (profitLogId is null || !unitsById.TryGetValue(profitLogId, out var profitLog))
        {
            return 0;
        }

        return profitLog
            .GetArray("stats_data")
            .Select(CleanSiiValue)
            .Where(entryId => entryId is not null && unitsById.ContainsKey(entryId))
            .Select(entryId => ProfitFromEntry(unitsById[entryId!]))
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

    private static IReadOnlyList<DriverRecentJobStatistic> BuildDriverRecentJobs(
        IReadOnlyCollection<SiiUnit> drivers,
        IReadOnlyDictionary<string, SiiUnit> unitsById,
        IReadOnlyDictionary<string, string> driverToTruck) =>
        drivers
            .SelectMany(driver => BuildDriverRecentJobs(driver, unitsById, driverToTruck.GetValueOrDefault(driver.Id)))
            .OrderByDescending(job => job.TimestampDay ?? int.MinValue)
            .ThenByDescending(job => job.Profit)
            .ThenBy(job => job.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IEnumerable<DriverRecentJobStatistic> BuildDriverRecentJobs(
        SiiUnit driver,
        IReadOnlyDictionary<string, SiiUnit> unitsById,
        string? currentTruckId)
    {
        var profitLogId = FirstKnownValue(driver, "profit_log");
        if (profitLogId is null || !unitsById.TryGetValue(profitLogId, out var profitLog))
        {
            yield break;
        }

        foreach (var entryId in profitLog.GetArray("stats_data").Select(CleanSiiValue).Where(value => value is not null))
        {
            if (!unitsById.TryGetValue(entryId!, out var entry) || !entry.TypeEquals("profit_log_entry"))
            {
                continue;
            }

            var revenue = FirstLongValue(entry, "revenue", "income", "profit", "pay");
            var expenses = FirstLongValue(entry, "wage") +
                FirstLongValue(entry, "maintenance") +
                FirstLongValue(entry, "fuel");
            var cargo = FirstKnownValue(entry, "cargo", "cargo_id");
            var sourceCity = FirstKnownValue(entry, "source_city", "source_city_id", "origin_city");
            var targetCity = FirstKnownValue(entry, "destination_city", "target_city", "destination_city_id");

            if (revenue == 0 &&
                string.IsNullOrWhiteSpace(cargo) &&
                string.IsNullOrWhiteSpace(sourceCity) &&
                string.IsNullOrWhiteSpace(targetCity))
            {
                continue;
            }

            yield return new DriverRecentJobStatistic(
                entry.Id,
                driver.Id,
                FirstKnownValue(entry, "truck", "vehicle") ?? currentTruckId,
                cargo,
                sourceCity,
                targetCity,
                revenue,
                expenses,
                revenue - expenses,
                FirstIntValue(entry, "distance"),
                FirstIntValue(entry, "timestamp_day"));
        }
    }

    private static string? GetArrayValue(IReadOnlyList<string> values, int index) =>
        index >= 0 && index < values.Count ? CleanSiiValue(values[index]) : null;

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
            var value = CleanSiiValue(unit.GetValue(key));
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
            var value = CleanSiiValue(unit.GetValue(key));
            if (!string.IsNullOrWhiteSpace(value))
            {
                return ParseLong(value);
            }
        }

        return 0;
    }

    private static long ParseLong(string value) =>
        long.TryParse(value, out var parsed) ? parsed : 0;

    private static int? FirstIntValue(SiiUnit unit, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = CleanSiiValue(unit.GetValue(key));
            if (!string.IsNullOrWhiteSpace(value) && int.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? CleanSiiValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("nil", StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
    }

    private static string? CleanLicensePlate(string? value)
    {
        value = CleanSiiValue(value);
        if (value is null)
        {
            return null;
        }

        var parts = value.Split('|', 2);
        var plate = MarkupRegex().Replace(parts[0], string.Empty);
        plate = WhitespaceRegex().Replace(plate, " ").Trim();
        if (string.IsNullOrWhiteSpace(plate))
        {
            return null;
        }

        if (parts.Length == 1)
        {
            return plate;
        }

        var state = FormatStateName(parts[1]);
        return string.IsNullOrWhiteSpace(state) ? plate : $"{plate} {state}";
    }

    private static string? ExtractTruckDefinitionPath(SiiUnit truck, IReadOnlyDictionary<string, SiiUnit> unitsById)
    {
        foreach (var accessoryId in truck.GetArray("accessories").Select(CleanSiiValue).Where(value => value is not null))
        {
            if (!unitsById.TryGetValue(accessoryId!, out var accessory))
            {
                continue;
            }

            var dataPath = FirstKnownValue(accessory, "data_path");
            if (dataPath is not null &&
                dataPath.StartsWith("/def/vehicle/truck/", StringComparison.OrdinalIgnoreCase) &&
                dataPath.EndsWith("/data.sii", StringComparison.OrdinalIgnoreCase))
            {
                return dataPath;
            }
        }

        return null;
    }

    private static string? FormatTruckModelName(string? definitionPath)
    {
        definitionPath = CleanSiiValue(definitionPath);
        if (definitionPath is null)
        {
            return null;
        }

        const string prefix = "/def/vehicle/truck/";
        const string suffix = "/data.sii";
        if (!definitionPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !definitionPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var modelId = definitionPath[prefix.Length..^suffix.Length];
        var parts = modelId.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0
            ? null
            : string.Join(' ', parts.SelectMany(FormatTruckModelPart));
    }

    private static string BuildTruckDisplayName(string truckId, string? modelName, string? licensePlate)
    {
        if (!string.IsNullOrWhiteSpace(modelName) && !string.IsNullOrWhiteSpace(licensePlate))
        {
            return $"{modelName} - {licensePlate}";
        }

        return modelName ?? licensePlate ?? truckId;
    }

    private static IEnumerable<string> FormatTruckModelPart(string value)
    {
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["intnational"] = "International",
            ["westernstar"] = "Western Star",
            ["freightliner"] = "Freightliner",
            ["kenworth"] = "Kenworth",
            ["peterbilt"] = "Peterbilt",
            ["volvo"] = "Volvo",
            ["mack"] = "Mack"
        };

        if (known.TryGetValue(value, out var formatted))
        {
            yield return formatted;
            yield break;
        }

        foreach (var token in value.Split('_', StringSplitOptions.RemoveEmptyEntries))
        {
            var spaced = LetterDigitBoundaryRegex().Replace(token, "$1 $2");
            foreach (var part in spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                yield return FormatTruckModelToken(part);
            }
        }
    }

    private static string FormatTruckModelToken(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        if (value.Any(char.IsDigit) && value.Any(char.IsAsciiLetter))
        {
            return value.ToUpperInvariant();
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string FormatStateName(string value) =>
        string.Join(' ', value
            .Trim()
            .TrimStart('_')
            .Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex MarkupRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("([A-Za-z]{2,})([0-9])")]
    private static partial Regex LetterDigitBoundaryRegex();
}

internal sealed record HistoricalMission(MissionStatistic Statistic, string DeduplicationKey, DateTimeOffset LastWritten);

internal static class SiiUnitExtensions
{
    public static bool TypeEquals(this SiiUnit unit, string type) =>
        StringComparer.OrdinalIgnoreCase.Equals(unit.Type, type);
}
