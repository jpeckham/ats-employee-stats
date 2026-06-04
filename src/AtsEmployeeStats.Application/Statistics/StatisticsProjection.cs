using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;
using System.Text;
using System.Text.RegularExpressions;

namespace AtsEmployeeStats.Application.Statistics;

public static partial class StatisticsProjection
{
    private const string PlayerDriverId = "player";

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
        var garageEligibleCityIds = units
            .Where(unit => unit.TypeEquals("garage"))
            .Select(garage => ExtractGarageCitySlug(garage.Id))
            .Where(city => !string.IsNullOrWhiteSpace(city))
            .Select(city => city!)
            .ToList();
        // Current garages (latest snapshot) drive driver/truck lookups and counts.
        var garages = units
            .Where(unit => unit.TypeEquals("garage"))
            .Where(IsOwnedGarage)
            .ToList();
        var currentGarageIds = garages.Select(g => g.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // All ever-owned garages across all snapshots — newest version of each for display.
        var allOwnedGarages = snapshots
            .OrderByDescending(s => s.LastWritten)
            .SelectMany(s => s.Document.Units.Where(u => u.TypeEquals("garage")).Where(IsOwnedGarage))
            .GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        var trailers = units.Where(unit => unit.TypeEquals("trailer")).ToList();
        var jobs = units.Where(unit => unit.TypeEquals("job") || unit.TypeEquals("delivery_log_entry")).ToList();

        var playerDriver = BuildPlayerDriverUnit(units, latest.Name);
        var rawPlayerDriverId = FindPlayerDriverBridge(units)?.Id;
        var driverToGarage = BuildReverseLookup(garages, "employees", "drivers");
        ApplyPlayerGarageLookup(driverToGarage, playerDriver, garages, rawPlayerDriverId);
        var truckToGarage = BuildReverseLookup(garages, "vehicles");

        var drivers = units
            .Where(unit =>
                (unit.TypeEquals("driver") || unit.TypeEquals("driver_ai")) &&
                !IsRawPlayerDriverBridge(unit, rawPlayerDriverId) &&
                driverToGarage.ContainsKey(unit.Id))
            .Concat(playerDriver is null ? [] : [playerDriver])
            .GroupBy(unit => unit.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var playerTruckIds = drivers
            .Where(IsPlayerDriverUnit)
            .Select(GetPlayerTruckId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trucks = units
            .Where(unit => unit.TypeEquals("vehicle") || unit.TypeEquals("truck"))
            .Where(unit => truckToGarage.ContainsKey(unit.Id) || playerTruckIds.Contains(unit.Id))
            .ToList();
        var driverToTruck = BuildDriverTruckLookup(drivers, trucks, garages);
        var truckToDriver = driverToTruck
            .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.OrdinalIgnoreCase);
        var trailerTypesByTrailer = trailers
            .Select(trailer => (TrailerId: trailer.Id, Type: FirstKnownValue(trailer, "trailer_definition", "trailer_def", "definition")))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Type))
            .ToDictionary(pair => pair.TrailerId, pair => pair.Type!, StringComparer.OrdinalIgnoreCase);

        var garageStats = allOwnedGarages
            .Select(garage =>
            {
                var isCurrent = currentGarageIds.Contains(garage.Id);
                return new GarageStatistic(
                    garage.Id,
                    FormatRouteEndpoint(ExtractGarageCitySlug(garage.Id) ?? garage.Id),
                    isCurrent ? SumProfitLog(garage, unitsById) : 0,
                    isCurrent ? Math.Max(garage.GetArray("employees").Count, garage.GetArray("drivers").Count) : 0,
                    isCurrent ? garage.GetArray("vehicles").Count : 0);
            })
            .OrderByDescending(garage => garage.Profit)
            .ThenBy(garage => garage.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var driverStats = drivers
            .Select(driver => new DriverStatistic(
                driver.Id,
                BuildDriverDisplayName(driver),
                SumProfitLog(driver, unitsById),
                driverToGarage.GetValueOrDefault(driver.Id),
                driverToTruck.GetValueOrDefault(driver.Id),
                IsPlayerDriverUnit(driver)))
            .OrderByDescending(driver => driver.Profit)
            .ThenBy(driver => driver.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var driverProfitByTruck = driverStats
            .Where(driver => !string.IsNullOrWhiteSpace(driver.TruckId))
            .GroupBy(driver => driver.TruckId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(driver => driver.Profit), StringComparer.OrdinalIgnoreCase);

        var truckStats = trucks
            .Select(truck =>
            {
                var licensePlate = CleanLicensePlate(FirstKnownValue(truck, "license_plate", "name"));
                var definitionPath = ExtractTruckDefinitionPath(truck, unitsById);
                var modelName = FormatTruckModelName(definitionPath);
                var vehicleProfit = SumProfitLog(truck, unitsById);
                var profit = vehicleProfit != 0
                    ? vehicleProfit
                    : driverProfitByTruck.GetValueOrDefault(truck.Id);

                return new TruckStatistic(
                    truck.Id,
                    BuildTruckDisplayName(truck.Id, modelName, licensePlate),
                    profit,
                    truckToGarage.GetValueOrDefault(truck.Id),
                    truckToDriver.GetValueOrDefault(truck.Id),
                    licensePlate,
                    modelName,
                    definitionPath);
            })
            .OrderByDescending(truck => truck.Profit)
            .ThenBy(truck => truck.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allUnitIdToLicensePlate = snapshots
            .SelectMany(s => s.Document.Units.Where(u => u.TypeEquals("trailer")))
            .Select(u => (UnitId: u.Id, LicensePlate: CleanLicensePlate(FirstKnownValue(u, "license_plate"))))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.LicensePlate))
            .GroupBy(pair => pair.UnitId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().LicensePlate!, StringComparer.OrdinalIgnoreCase);

        var missionStats = snapshots
            .SelectMany(BuildSnapshotMissions)
            .GroupBy(mission => mission.DeduplicationKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var winner = group.OrderByDescending(m => m.LastWritten).First().Statistic;
                // profit_log rolls over, so older snapshots may have attribution the newest lost
                var driverId = winner.DriverId
                    ?? group.Select(m => m.Statistic.DriverId).FirstOrDefault(id => id != null);
                var truckId = winner.TruckId
                    ?? group.Select(m => m.Statistic.TruckId).FirstOrDefault(id => id != null);
                var trailerId = winner.TrailerId
                    ?? group.Select(m => m.Statistic.TrailerId).FirstOrDefault(id => id != null);
                // Garage: use earliest attribution — that's where the driver was when the job was done
                var garageId = group
                    .OrderBy(m => m.LastWritten)
                    .Select(m => m.Statistic.GarageId)
                    .FirstOrDefault(id => id != null);
                var trailerLicensePlate = trailerId is not null
                    ? allUnitIdToLicensePlate.GetValueOrDefault(trailerId)
                    : null;
                return (driverId == winner.DriverId && truckId == winner.TruckId && trailerId == winner.TrailerId && garageId == winner.GarageId && trailerLicensePlate == winner.TrailerLicensePlate)
                    ? winner
                    : winner with { DriverId = driverId, TruckId = truckId, TrailerId = trailerId, GarageId = garageId, TrailerLicensePlate = trailerLicensePlate };
            })
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
        var companyId = BuildCompanyId(companyName, latest.SourceKey);
        var trailerToGarage = BuildReverseLookup(garages, "trailers");
        var trailerToJobCount = BuildTrailerJobCounts(units, unitsById);
        var routeStats = BuildRouteStats(missionStats);
        var currentGarageStats = garageStats.Where(g => currentGarageIds.Contains(g.Id)).ToList();
        var cityStats = BuildCityStats(missionStats, currentGarageStats, routeStats, garageEligibleCityIds);
        var individualTrailerStats = BuildTrailerStats(trailers, missionStats, trailerTypesByTrailer, unitsById, trailerToGarage, trailerToJobCount);
        var (truckAssignments, garageAssignments) = BuildDriverAssignments(snapshots);
        return new CompanyStatistics(
            companyId,
            companyName,
            latest.LastWritten,
            garageStats,
            driverStats,
            truckStats,
            missionStats,
            trailerStats,
            BuildDriverRecentJobs(drivers, units, unitsById, driverToTruck),
            individualTrailerStats,
            cityStats,
            routeStats,
            BuildProfitTrends(companyId, missionStats),
            truckAssignments,
            garageAssignments);
    }

    private static (IReadOnlyList<DriverTruckAssignmentStatistic> TruckAssignments, IReadOnlyList<DriverGarageAssignmentStatistic> GarageAssignments) BuildDriverAssignments(
        IReadOnlyCollection<SaveSnapshot> snapshots)
    {
        var sortedSnapshots = snapshots
            .OrderBy(s => s.LastWritten)
            .ToList();

        var truckAssignments = new List<DriverTruckAssignmentStatistic>();
        var garageAssignments = new List<DriverGarageAssignmentStatistic>();
        var currentTruck = new Dictionary<string, (string TruckId, string FromSaveName)>(StringComparer.OrdinalIgnoreCase);
        var currentGarage = new Dictionary<string, (string GarageId, string FromSaveName)>(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in sortedSnapshots)
        {
            var units = snapshot.Document.Units;
            var garages = units.Where(u => u.TypeEquals("garage")).Where(IsOwnedGarage).ToList();
            var playerDriver = BuildPlayerDriverUnit(units, snapshot.Name);
            var rawPlayerDriverId = FindPlayerDriverBridge(units)?.Id;
            var driverToGarageSnapshot = BuildReverseLookup(garages, "employees", "drivers");
            ApplyPlayerGarageLookup(driverToGarageSnapshot, playerDriver, garages, rawPlayerDriverId);
            var truckToGarage = BuildReverseLookup(garages, "vehicles");
            var drivers = units
                .Where(u =>
                    (u.TypeEquals("driver") || u.TypeEquals("driver_ai")) &&
                    !IsRawPlayerDriverBridge(u, rawPlayerDriverId) &&
                    driverToGarageSnapshot.ContainsKey(u.Id))
                .Concat(playerDriver is null ? [] : [playerDriver])
                .GroupBy(u => u.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            var playerTruckIds = drivers
                .Where(IsPlayerDriverUnit)
                .Select(GetPlayerTruckId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var trucks = units
                .Where(u => u.TypeEquals("vehicle") || u.TypeEquals("truck"))
                .Where(u => truckToGarage.ContainsKey(u.Id) || playerTruckIds.Contains(u.Id))
                .ToList();
            var driverToTruckSnapshot = BuildDriverTruckLookup(drivers, trucks, garages);

            foreach (var driver in drivers)
            {
                driverToTruckSnapshot.TryGetValue(driver.Id, out var newTruckId);
                if (string.IsNullOrWhiteSpace(newTruckId)) continue;
                if (currentTruck.TryGetValue(driver.Id, out var prevTruck))
                {
                    if (!StringComparer.OrdinalIgnoreCase.Equals(prevTruck.TruckId, newTruckId))
                    {
                        truckAssignments.Add(new DriverTruckAssignmentStatistic(
                            driver.Id, prevTruck.TruckId, prevTruck.FromSaveName, snapshot.Name, IsCurrent: false));
                        currentTruck[driver.Id] = (newTruckId, snapshot.Name);
                    }
                }
                else
                {
                    currentTruck[driver.Id] = (newTruckId, snapshot.Name);
                }
            }

            foreach (var (driverId, newGarageId) in driverToGarageSnapshot)
            {
                if (currentGarage.TryGetValue(driverId, out var prevGarage))
                {
                    if (!StringComparer.OrdinalIgnoreCase.Equals(prevGarage.GarageId, newGarageId))
                    {
                        garageAssignments.Add(new DriverGarageAssignmentStatistic(
                            driverId, prevGarage.GarageId, prevGarage.FromSaveName, snapshot.Name, IsCurrent: false));
                        currentGarage[driverId] = (newGarageId, snapshot.Name);
                    }
                }
                else
                {
                    currentGarage[driverId] = (newGarageId, snapshot.Name);
                }
            }
        }

        foreach (var (driverId, (truckId, fromSaveName)) in currentTruck)
            truckAssignments.Add(new DriverTruckAssignmentStatistic(
                driverId, truckId, fromSaveName, EffectiveToSaveName: null, IsCurrent: true));

        foreach (var (driverId, (garageId, fromSaveName)) in currentGarage)
            garageAssignments.Add(new DriverGarageAssignmentStatistic(
                driverId, garageId, fromSaveName, EffectiveToSaveName: null, IsCurrent: true));

        return (
            truckAssignments
                .OrderBy(a => a.DriverId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.IsCurrent)
                .ThenBy(a => a.EffectiveFromSaveName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            garageAssignments
                .OrderBy(a => a.DriverId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.IsCurrent)
                .ThenBy(a => a.EffectiveFromSaveName, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static bool IsOwnedGarage(SiiUnit garage)
    {
        var status = garage.GetValue("status");
        if (status is null) return true;
        return int.TryParse(status, out var statusValue) && statusValue >= 2;
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
        var entryToDriver = BuildEntryToDriverMap(units, unitsById);
        var snapshotGarages = units.Where(u => u.TypeEquals("garage")).Where(IsOwnedGarage).ToList();
        var playerDriver = BuildPlayerDriverUnit(units, snapshot.Name);
        var rawPlayerDriverId = FindPlayerDriverBridge(units)?.Id;
        var driverToGarage = BuildReverseLookup(snapshotGarages, "employees", "drivers");
        ApplyPlayerGarageLookup(driverToGarage, playerDriver, snapshotGarages, rawPlayerDriverId);
        var truckToGarage = BuildReverseLookup(snapshotGarages, "vehicles");
        var drivers = units
            .Where(unit =>
                (unit.TypeEquals("driver") || unit.TypeEquals("driver_ai")) &&
                !IsRawPlayerDriverBridge(unit, rawPlayerDriverId))
            .Concat(playerDriver is null ? [] : [playerDriver])
            .GroupBy(unit => unit.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var playerTruckIds = drivers
            .Where(IsPlayerDriverUnit)
            .Select(GetPlayerTruckId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trucks = units
            .Where(unit => unit.TypeEquals("vehicle") || unit.TypeEquals("truck"))
            .Where(unit => truckToGarage.ContainsKey(unit.Id) || playerTruckIds.Contains(unit.Id))
            .ToList();
        var ownedTruckIds = trucks
            .Select(truck => truck.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var driverToTruck = BuildDriverTruckLookup(drivers, trucks, snapshotGarages);
        // profit_log_entry has no trailer field — infer trailer from driver's current assignment
        var driverToTrailer = drivers
            .Select(d => (DriverId: d.Id, TrailerId: FirstKnownValue(d, "assigned_trailer", "my_trailer")))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.TrailerId))
            .ToDictionary(pair => pair.DriverId, pair => pair.TrailerId!, StringComparer.OrdinalIgnoreCase);

        var deliveryRouteLookup = BuildDeliveryLogRouteLookup(units, unitsById);

        return units
            .Where(unit => unit.TypeEquals("job") ||
                unit.TypeEquals("delivery_log_entry") ||
                unit.TypeEquals("profit_log_entry"))
            .Select(job =>
            {
                var mission = BuildMission(job, trailerTypesByTrailer, unitsById, entryToDriver, deliveryRouteLookup);
                var keyMission = mission;
                if (string.IsNullOrWhiteSpace(mission.TrailerId) &&
                    !string.IsNullOrWhiteSpace(mission.DriverId) &&
                    driverToTrailer.TryGetValue(mission.DriverId, out var inferredTrailerId))
                {
                    mission = mission with { TrailerId = inferredTrailerId };
                }
                if (!string.IsNullOrWhiteSpace(mission.DriverId) &&
                    driverToTruck.TryGetValue(mission.DriverId, out var inferredTruckId) &&
                    (string.IsNullOrWhiteSpace(mission.TruckId) || !ownedTruckIds.Contains(mission.TruckId)))
                {
                    mission = mission with { TruckId = inferredTruckId };
                }
                var garageId = mission.DriverId != null
                    ? driverToGarage.GetValueOrDefault(mission.DriverId)
                    : null;
                if (garageId != null)
                    mission = mission with { GarageId = garageId };
                return new HistoricalMission(mission, BuildMissionDeduplicationKey(job, keyMission), snapshot.LastWritten);
            });
    }

    private static string GetCompanyKey(SaveSnapshot snapshot) =>
        BuildCompanyId(GetCompanyDisplayName(snapshot), snapshot.SourceKey);

    private static string BuildCompanyId(string companyName, string? sourceKey)
    {
        var companyId = NormalizeCompanyId(companyName);
        if (string.IsNullOrWhiteSpace(sourceKey))
            return companyId;

        return $"{NormalizeCompanyId(sourceKey)}:{companyId}";
    }

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
        IReadOnlyDictionary<string, SiiUnit> unitsById,
        IReadOnlyDictionary<string, string>? entryToDriver = null,
        IReadOnlyDictionary<(int Day, long Revenue), (string? Source, string? Target, string? Cargo)>? deliveryRouteLookup = null)
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

        if (job.TypeEquals("profit_log_entry") &&
            deliveryRouteLookup is not null &&
            (string.IsNullOrWhiteSpace(sourceCity) || string.IsNullOrWhiteSpace(targetCity) || string.IsNullOrWhiteSpace(cargo)))
        {
            var entryRevenue = FirstLongValue(job, "revenue", "income", "profit", "pay");
            var entryDay = FirstIntValue(job, "timestamp_day");
            if (entryRevenue > 0 && entryDay is not null &&
                deliveryRouteLookup.TryGetValue((entryDay.Value, entryRevenue), out var route))
            {
                if (string.IsNullOrWhiteSpace(sourceCity)) sourceCity = route.Source;
                if (string.IsNullOrWhiteSpace(targetCity)) targetCity = route.Target;
                if (string.IsNullOrWhiteSpace(cargo)) cargo = route.Cargo;
            }
        }
        var profit = job.TypeEquals("profit_log_entry")
            ? ProfitFromEntry(job)
            : FirstLongValue(job, "income", "revenue", "profit", "pay");
        var driverId = FirstKnownValue(job, "driver", "employee") ?? entryToDriver?.GetValueOrDefault(job.Id);

        if (job.TypeEquals("profit_log_entry") &&
            !StringComparer.OrdinalIgnoreCase.Equals(driverId, PlayerDriverId) &&
            (string.IsNullOrWhiteSpace(cargo) ||
                string.IsNullOrWhiteSpace(sourceCity) ||
                string.IsNullOrWhiteSpace(targetCity)))
        {
            profit = 0;
        }

        return new MissionStatistic(
            job.Id,
            driverId,
            FirstKnownValue(job, "truck", "vehicle"),
            trailerId,
            trailerType ?? "unknown",
            cargo,
            sourceCity,
            targetCity,
            profit,
            FirstIntValue(job, "timestamp_day"));
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
            Profit: profit,
            TimestampDay: FirstIntValue(entry, "timestamp_day"));
    }

    private static IReadOnlyList<TrailerStatistic> BuildTrailerStats(
        IReadOnlyCollection<SiiUnit> trailers,
        IReadOnlyCollection<MissionStatistic> missions,
        IReadOnlyDictionary<string, string> trailerTypesByTrailer,
        IReadOnlyDictionary<string, SiiUnit> unitsById,
        IReadOnlyDictionary<string, string> trailerToGarage,
        IReadOnlyDictionary<string, int> trailerToJobCount)
    {
        var missionProfitByTrailer = missions
            .Where(mission => !string.IsNullOrWhiteSpace(mission.TrailerId))
            .GroupBy(
                mission => mission.TrailerLicensePlate ?? mission.TrailerId!,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(mission => mission.Profit),
                StringComparer.OrdinalIgnoreCase);

        return trailers
            .Select(trailer =>
            {
                var defId = FirstKnownValue(trailer, "trailer_definition", "trailer_def", "definition") ??
                    trailerTypesByTrailer.GetValueOrDefault(trailer.Id);
                string? bodyType = null;
                var isArticulated = false;
                if (defId is not null && unitsById.TryGetValue(defId, out var trailerDef))
                {
                    bodyType = FirstKnownValue(trailerDef, "body_type");
                    var chainType = FirstKnownValue(trailerDef, "chain_type");
                    isArticulated = StringComparer.OrdinalIgnoreCase.Equals(chainType, "double");
                }
                var licensePlate = CleanLicensePlate(FirstKnownValue(trailer, "license_plate"));
                var profit = missionProfitByTrailer.GetValueOrDefault(licensePlate ?? trailer.Id);
                var jobCount = trailerToJobCount.GetValueOrDefault(trailer.Id);
                var garageId = trailerToGarage.GetValueOrDefault(trailer.Id);

                return new TrailerStatistic(
                    trailer.Id,
                    defId ?? "unknown",
                    profit,
                    jobCount,
                    isArticulated,
                    bodyType,
                    garageId,
                    licensePlate);
            })
            .Where(trailer => trailer.JobCount > 0 || !StringComparer.OrdinalIgnoreCase.Equals(trailer.TrailerType, "unknown"))
            .OrderByDescending(trailer => trailer.Profit)
            .ThenBy(trailer => trailer.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyDictionary<string, int> BuildTrailerJobCounts(
        IReadOnlyList<SiiUnit> units,
        IReadOnlyDictionary<string, SiiUnit> unitsById)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var player = FindPlayerUnit(units);
        if (player is null) return result;

        var trailerIds = player.GetArray("trailers");
        var logIds = player.GetArray("trailer_utilization_logs");

        foreach (var (idx, rawTrailerId) in trailerIds)
        {
            if (!logIds.TryGetValue(idx, out var rawLogId)) continue;
            var trailerId = CleanSiiValue(rawTrailerId);
            var logId = CleanSiiValue(rawLogId);
            if (trailerId is null || logId is null) continue;
            if (!unitsById.TryGetValue(logId, out var log)) continue;
            var jobCount = FirstIntValue(log, "total_transported_cargoes") ?? 0;
            result.TryAdd(trailerId, jobCount);
        }

        return result;
    }

    private static IReadOnlyList<RouteStatistic> BuildRouteStats(IReadOnlyCollection<MissionStatistic> missions)
    {
        var directedRoutes = missions
            .Where(HasRoute)
            .GroupBy(mission => (Origin: mission.SourceCity!, Destination: mission.TargetCity!), new RouteKeyComparer())
            .ToDictionary(
                group => group.Key,
                group => (Profit: group.Sum(mission => mission.Profit), JobCount: group.Count()),
                new RouteKeyComparer());

        return directedRoutes
            .Select(route =>
            {
                directedRoutes.TryGetValue((route.Key.Destination, route.Key.Origin), out var reverse);
                return new RouteStatistic(
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

    private static IReadOnlyList<CityStatistic> BuildCityStats(
        IReadOnlyCollection<MissionStatistic> missions,
        IReadOnlyCollection<GarageStatistic> garages,
        IReadOnlyCollection<RouteStatistic> routes,
        IReadOnlyCollection<string> garageEligibleCityIds)
    {
        var ownedGarageCities = garages
            .Select(garage => ExtractGarageCitySlug(garage.Id))
            .Where(city => !string.IsNullOrWhiteSpace(city))
            .Select(city => city!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var eligibleGarageCities = garageEligibleCityIds
            .Concat(ownedGarageCities)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                var hasOwnedGarage = ownedGarageCities.Contains(city);
                var isGarageEligible = eligibleGarageCities.Contains(city);
                var expansionOutbound = outbound
                    .Where(m => m.GarageId is null ||
                                !StringComparer.OrdinalIgnoreCase.Equals(ExtractGarageCitySlug(m.GarageId), city))
                    .ToList();
                var expansionScore = (hasOwnedGarage || !isGarageEligible)
                    ? 0m
                    : Math.Round(expansionOutbound.Count + inbound.Count + (expansionOutbound.Sum(m => m.Profit) / 10000m), 2, MidpointRounding.AwayFromZero);

                return new CityStatistic(
                    city,
                    FormatRouteEndpoint(city),
                    hasOwnedGarage,
                    isGarageEligible,
                    outbound.Count + inbound.Count,
                    outbound.Sum(mission => mission.Profit),
                    inbound.Sum(mission => mission.Profit),
                    bidirectionalProfit,
                    expansionScore);
            })
            .OrderByDescending(city => city.HasOwnedGarage)
            .ThenBy(city => city.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<TrendPointStatistic> BuildProfitTrends(
        string companyId,
        IReadOnlyCollection<MissionStatistic> missions)
    {
        var trends = new List<TrendPointStatistic>();
        var timedMissions = missions
            .Where(mission => mission.TimestampDay is not null)
            .ToList();

        trends.AddRange(BuildTrend("company", companyId, timedMissions));
        trends.AddRange(timedMissions
            .Where(mission => !string.IsNullOrWhiteSpace(mission.DriverId))
            .GroupBy(mission => mission.DriverId!, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => BuildTrend("driver", group.Key, group)));
        trends.AddRange(timedMissions
            .Where(mission => !string.IsNullOrWhiteSpace(mission.TruckId))
            .GroupBy(mission => mission.TruckId!, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => BuildTrend("truck", group.Key, group)));
        trends.AddRange(timedMissions
            .Where(mission => !string.IsNullOrWhiteSpace(mission.TrailerId))
            .GroupBy(
                mission => mission.TrailerLicensePlate ?? mission.TrailerId!,
                StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => BuildTrend("trailer", group.Key, group)));
        trends.AddRange(timedMissions
            .Where(mission => !string.IsNullOrWhiteSpace(mission.GarageId))
            .GroupBy(mission => mission.GarageId!, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => BuildTrend("garage", group.Key, group)));
        trends.AddRange(timedMissions
            .Where(HasRoute)
            .SelectMany(mission => new[]
            {
                (CityId: NormalizeCityId(mission.SourceCity)!, Mission: mission),
                (CityId: NormalizeCityId(mission.TargetCity)!, Mission: mission)
            })
            .GroupBy(pair => pair.CityId, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => BuildTrend("city", group.Key, group.Select(pair => pair.Mission))));

        return trends
            .OrderBy(trend => trend.EntityKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(trend => trend.EntityId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(trend => trend.GameDay)
            .ToList();
    }

    private static IEnumerable<TrendPointStatistic> BuildTrend(
        string entityKind,
        string entityId,
        IEnumerable<MissionStatistic> missions) =>
        missions
            .Where(mission => mission.TimestampDay is not null)
            .GroupBy(mission => mission.TimestampDay!.Value)
            .OrderBy(group => group.Key)
            .Select(group => new TrendPointStatistic(
                entityKind,
                entityId,
                group.Key,
                group.Sum(mission => mission.Profit),
                group.Count()));

    private static string? ExtractGarageCitySlug(string garageId)
    {
        var dot = garageId.IndexOf('.');
        return dot >= 0 && dot + 1 < garageId.Length ? garageId[(dot + 1)..] : null;
    }

    private static Dictionary<string, string> BuildEntryToDriverMap(
        IReadOnlyList<SiiUnit> units,
        IReadOnlyDictionary<string, SiiUnit> unitsById)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rawPlayerDriverId = FindPlayerDriverBridge(units)?.Id;

        foreach (var driver in units.Where(u => u.TypeEquals("driver") || u.TypeEquals("driver_ai") || u.TypeEquals("driver_player")))
        {
            var profitLogId = FirstKnownValue(driver, "profit_log");
            if (profitLogId is null || !unitsById.TryGetValue(profitLogId, out var profitLog))
                continue;
            var driverId = IsRawPlayerDriverBridge(driver, rawPlayerDriverId)
                ? PlayerDriverId
                : driver.Id;
            foreach (var entryId in profitLog.GetArray("stats_data").Values.Select(CleanSiiValue).Where(v => v is not null))
                map.TryAdd(entryId!, driverId);
        }
        return map;
    }

    private static SiiUnit? BuildPlayerDriverUnit(IReadOnlyList<SiiUnit> units, string snapshotName)
    {
        var player = FindPlayerUnit(units);
        if (player is null)
        {
            return null;
        }

        var values = new Dictionary<string, string>(player.Values, StringComparer.OrdinalIgnoreCase);
        var bridge = FindPlayerDriverBridge(units);
        if (bridge is not null)
        {
            CopyFirstKnownValue(values, bridge, "profit_log");
        }

        var profileName = GetProfileDisplayName(snapshotName);
        if (!string.IsNullOrWhiteSpace(profileName))
        {
            values.TryAdd("profile_name", profileName);
        }

        if (FirstKnownValue(player, "hq_city", "assigned_truck", "my_truck", "assigned_trailer", "my_trailer") is null &&
            FirstKnownValue(bridge, "profit_log") is null)
        {
            return null;
        }

        return new SiiUnit("player", PlayerDriverId, values, player.Arrays);
    }

    private static SiiUnit? FindPlayerUnit(IReadOnlyList<SiiUnit> units) =>
        units.FirstOrDefault(unit => unit.TypeEquals("player"));

    private static SiiUnit? FindPlayerDriverBridge(IReadOnlyList<SiiUnit> units) =>
        units.FirstOrDefault(unit => unit.TypeEquals("driver_player"));

    private static void CopyFirstKnownValue(Dictionary<string, string> values, SiiUnit unit, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = CleanSiiValue(unit.GetValue(key));
            if (!string.IsNullOrWhiteSpace(value))
            {
                values[key] = value;
                return;
            }
        }
    }

    private static void ApplyPlayerGarageLookup(
        IDictionary<string, string> driverToGarage,
        SiiUnit? playerDriver,
        IReadOnlyCollection<SiiUnit> garages,
        string? rawPlayerDriverId)
    {
        if (playerDriver is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(rawPlayerDriverId) &&
            driverToGarage.TryGetValue(rawPlayerDriverId, out var rawGarageId))
        {
            driverToGarage.Remove(rawPlayerDriverId);
            driverToGarage[PlayerDriverId] = rawGarageId;
        }

        var hqCity = NormalizeCityId(FirstKnownValue(playerDriver, "hq_city"));
        if (hqCity is null)
        {
            return;
        }

        var hqGarageId = $"garage.{hqCity}";
        var ownedHqGarage = garages.FirstOrDefault(garage =>
            StringComparer.OrdinalIgnoreCase.Equals(garage.Id, hqGarageId));
        if (ownedHqGarage is not null)
        {
            driverToGarage[PlayerDriverId] = ownedHqGarage.Id;
        }
    }

    private static string? GetPlayerTruckId(SiiUnit driver) =>
        FirstKnownValue(driver, "assigned_truck", "my_truck", "truck", "vehicle");

    private static bool IsPlayerDriverUnit(SiiUnit unit) =>
        unit.TypeEquals("player") && StringComparer.OrdinalIgnoreCase.Equals(unit.Id, PlayerDriverId);

    private static bool IsRawPlayerDriverBridge(SiiUnit unit, string? rawPlayerDriverId) =>
        !string.IsNullOrWhiteSpace(rawPlayerDriverId) &&
        StringComparer.OrdinalIgnoreCase.Equals(unit.Id, rawPlayerDriverId);

    private static string BuildDriverDisplayName(SiiUnit driver) =>
        IsPlayerDriverUnit(driver)
            ? FirstKnownValue(driver, "profile_name", "player_name", "name", "surname", "company_name", "company") ?? driver.Id
            : FirstKnownValue(driver, "name", "surname") ?? driver.Id;

    private static bool HasRoute(MissionStatistic mission) =>
        !string.IsNullOrWhiteSpace(mission.SourceCity) &&
        !string.IsNullOrWhiteSpace(mission.TargetCity);

    private static string? NormalizeCityId(string? value)
    {
        value = CleanSiiValue(value);
        return value is null ? null : value.Trim().ToLowerInvariant();
    }

    private static string FormatRouteEndpoint(string value) =>
        string.Join(' ', value
            .Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

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
                .SelectMany(name => owner.GetArray(name).Values)
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
            .Select(driver => (DriverId: driver.Id, TruckId: FirstKnownValue(driver, "assigned_truck", "my_truck", "truck", "vehicle")))
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
            foreach (var (idx, rawDriverId) in garageDrivers)
            {
                if (!garageTrucks.TryGetValue(idx, out var rawTruckId)) continue;
                var driverId = CleanSiiValue(rawDriverId);
                var truckId = CleanSiiValue(rawTruckId);
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
            .Values.Select(CleanSiiValue)
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
            .Values.Select(CleanSiiValue)
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
        IReadOnlyList<SiiUnit> allUnits,
        IReadOnlyDictionary<string, SiiUnit> unitsById,
        IReadOnlyDictionary<string, string> driverToTruck)
    {
        var deliveryRouteLookup = BuildDeliveryLogRouteLookup(allUnits, unitsById);
        return drivers
            .SelectMany(driver => BuildDriverRecentJobs(driver, unitsById, driverToTruck.GetValueOrDefault(driver.Id), deliveryRouteLookup))
            .OrderByDescending(job => job.TimestampDay ?? int.MinValue)
            .ThenByDescending(job => job.Profit)
            .ThenBy(job => job.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<DriverRecentJobStatistic> BuildDriverRecentJobs(
        SiiUnit driver,
        IReadOnlyDictionary<string, SiiUnit> unitsById,
        string? currentTruckId,
        IReadOnlyDictionary<(int Day, long Revenue), (string? Source, string? Target, string? Cargo)> deliveryRouteLookup)
    {
        var profitLogId = FirstKnownValue(driver, "profit_log");
        if (profitLogId is null || !unitsById.TryGetValue(profitLogId, out var profitLog))
        {
            yield break;
        }

        foreach (var entryId in profitLog.GetArray("stats_data").Values.Select(CleanSiiValue).Where(value => value is not null))
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
            var timestampDay = FirstIntValue(entry, "timestamp_day");

            if (string.IsNullOrWhiteSpace(sourceCity) || string.IsNullOrWhiteSpace(targetCity))
            {
                // Player profit_log_entry units don't store source/destination cities.
                // Correlate with the economy's delivery_log_entry by (day, revenue) to get route info.
                if (revenue > 0 &&
                    timestampDay is not null &&
                    deliveryRouteLookup.TryGetValue((timestampDay.Value, revenue), out var route))
                {
                    sourceCity = route.Source;
                    targetCity = route.Target;
                    if (string.IsNullOrWhiteSpace(cargo))
                        cargo = route.Cargo;
                }
            }

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
                timestampDay);
        }
    }

    private static Dictionary<(int Day, long Revenue), (string? Source, string? Target, string? Cargo)> BuildDeliveryLogRouteLookup(
        IReadOnlyList<SiiUnit> units,
        IReadOnlyDictionary<string, SiiUnit> unitsById)
    {
        var lookup = new Dictionary<(int, long), (string?, string?, string?)>();
        var economy = units.FirstOrDefault(u => u.TypeEquals("economy"));
        var deliveryLogId = FirstKnownValue(economy, "delivery_log");
        if (deliveryLogId is null || !unitsById.TryGetValue(deliveryLogId, out var deliveryLog))
            return lookup;

        foreach (var entryId in deliveryLog.GetArray("entries").Values.Select(CleanSiiValue).Where(v => v is not null))
        {
            if (!unitsById.TryGetValue(entryId!, out var entry) || !entry.TypeEquals("delivery_log_entry"))
                continue;

            var parameters = entry.GetArray("params");
            var endGameTime = ParseMoney(GetArrayValue(parameters, 0));
            var revenue = ParseMoney(GetArrayValue(parameters, 5));
            if (endGameTime <= 0 || revenue <= 0)
                continue;

            var day = (int)(endGameTime / 1440);
            lookup.TryAdd(
                (day, revenue),
                (CityFromCompany(GetArrayValue(parameters, 1)),
                 CityFromCompany(GetArrayValue(parameters, 2)),
                 GetArrayValue(parameters, 3)));
        }

        return lookup;
    }

    private static string? GetArrayValue(IReadOnlyDictionary<string, string> values, int index) =>
        values.TryGetValue(index.ToString(), out var v) ? CleanSiiValue(v) : null;

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
        foreach (var accessoryId in truck.GetArray("accessories").Values.Select(CleanSiiValue).Where(value => value is not null))
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

internal sealed class RouteKeyComparer : IEqualityComparer<(string Origin, string Destination)>
{
    public bool Equals((string Origin, string Destination) left, (string Origin, string Destination) right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left.Origin, right.Origin) &&
        StringComparer.OrdinalIgnoreCase.Equals(left.Destination, right.Destination);

    public int GetHashCode((string Origin, string Destination) value) =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.Origin),
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.Destination));
}

internal static class SiiUnitExtensions
{
    public static bool TypeEquals(this SiiUnit unit, string type) =>
        StringComparer.OrdinalIgnoreCase.Equals(unit.Type, type);
}
