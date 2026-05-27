namespace AtsEmployeeStats.Contracts;

public sealed record DashboardStatisticsDto(
    DateTimeOffset? LastUpdated,
    IReadOnlyList<CompanyDto> Companies);

public sealed record CompanyDto(
    string Id,
    string DisplayName,
    long Profit,
    IReadOnlyList<GarageDto> Garages,
    IReadOnlyList<DriverDto> Drivers,
    IReadOnlyList<TruckDto> Trucks,
    IReadOnlyList<MissionDto> Missions,
    IReadOnlyList<TrailerTypeDto> TrailerTypes,
    IReadOnlyList<DriverRecentJobDto>? RecentDriverJobs = null);

public sealed record GarageDto(
    string Id,
    string DisplayName,
    long Profit,
    long ProfitPerDay,
    int EmployeeCount,
    int TruckCount);

public sealed record DriverDto(
    string Id,
    string DisplayName,
    long Profit,
    long ProfitPerDay,
    string? GarageId,
    string? TruckId,
    int JobCount);

public sealed record TruckDto(
    string Id,
    string DisplayName,
    long Profit,
    string? GarageId,
    string? DriverId,
    string? LicensePlate = null,
    string? ModelName = null,
    string? DefinitionPath = null);

public sealed record MissionDto(
    string Id,
    string? DriverId,
    string? TruckId,
    string? TrailerType,
    string? Cargo,
    string? SourceCity,
    string? TargetCity,
    long Profit,
    int? TimestampDay = null);

public sealed record DriverRecentJobDto(
    string Id,
    string DriverId,
    string? TruckId,
    string? Cargo,
    string? SourceCity,
    string? TargetCity,
    long Revenue,
    long Expenses,
    long Profit,
    int? Distance,
    int? TimestampDay);

public sealed record TrailerTypeDto(
    string Id,
    long Profit,
    int MissionCount);

public sealed record DashboardConfigDto(
    string SaveRoot,
    int HistoryDays);

public sealed record DashboardStatusDto(
    string Message,
    bool IsError);

public sealed record DashboardProgressDto(
    string Message,
    int CompletedFiles,
    int TotalFiles,
    int CurrentFileCompletedUnits,
    int CurrentFileTotalUnits);
