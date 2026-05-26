namespace AtsEmployeeStats.Domain.Statistics;

public sealed record AtsStatistics(
    DateTimeOffset? LastUpdated,
    IReadOnlyList<CompanyStatistics> Companies);

public sealed record CompanyStatistics(
    string Id,
    string DisplayName,
    DateTimeOffset LastUpdated,
    IReadOnlyList<GarageStatistic> Garages,
    IReadOnlyList<DriverStatistic> Drivers,
    IReadOnlyList<TruckStatistic> Trucks,
    IReadOnlyList<MissionStatistic> Missions,
    IReadOnlyList<TrailerTypeStatistic> TrailerTypes);

public sealed record GarageStatistic(
    string Id,
    string DisplayName,
    long Profit,
    int EmployeeCount,
    int TruckCount);

public sealed record DriverStatistic(
    string Id,
    string DisplayName,
    long Profit,
    string? GarageId,
    string? TruckId);

public sealed record TruckStatistic(
    string Id,
    string DisplayName,
    long Profit,
    string? GarageId,
    string? DriverId);

public sealed record MissionStatistic(
    string Id,
    string? DriverId,
    string? TruckId,
    string? TrailerId,
    string? TrailerType,
    string? Cargo,
    string? SourceCity,
    string? TargetCity,
    long Profit);

public sealed record TrailerTypeStatistic(
    string Id,
    long Profit,
    int MissionCount);
