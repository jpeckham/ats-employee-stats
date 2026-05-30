namespace AtsEmployeeStats.Contracts;

public sealed record DashboardStatisticsDto(
    DateTimeOffset? LastUpdated,
    IReadOnlyList<CompanyDto> Companies,
    int? MaxGameDay = null);

public sealed record CompanyDto(
    string Id,
    string DisplayName,
    long Profit,
    IReadOnlyList<GarageDto> Garages,
    IReadOnlyList<DriverDto> Drivers,
    IReadOnlyList<TruckDto> Trucks,
    IReadOnlyList<MissionDto> Missions,
    IReadOnlyList<TrailerTypeDto> TrailerTypes,
    IReadOnlyList<DriverRecentJobDto>? RecentDriverJobs = null,
    IReadOnlyList<TrailerDto>? Trailers = null,
    IReadOnlyList<CityDto>? Cities = null,
    IReadOnlyList<RouteDto>? Routes = null,
    SparklineDto? ProfitTrend = null,
    IReadOnlyList<DriverTruckAssignmentDto>? DriverTruckAssignments = null,
    IReadOnlyList<DriverGarageAssignmentDto>? DriverGarageAssignments = null,
    string? OwnerName = null);

public sealed record GarageDto(
    string Id,
    string DisplayName,
    long Profit,
    long ProfitPerDay,
    int EmployeeCount,
    int TruckCount,
    SparklineDto? Trend = null,
    int TrailerCount = 0);

public sealed record DriverDto(
    string Id,
    string DisplayName,
    long Profit,
    long ProfitPerDay,
    string? GarageId,
    string? TruckId,
    int JobCount,
    SparklineDto? Trend = null);

public sealed record TruckDto(
    string Id,
    string DisplayName,
    long Profit,
    string? GarageId,
    string? DriverId,
    string? LicensePlate = null,
    string? ModelName = null,
    string? DefinitionPath = null,
    long ProfitPerDay = 0,
    SparklineDto? Trend = null);

public sealed record MissionDto(
    string Id,
    string? DriverId,
    string? TruckId,
    string? TrailerType,
    string? Cargo,
    string? SourceCity,
    string? TargetCity,
    long Profit,
    int? TimestampDay = null,
    string? TrailerId = null,
    string? GarageId = null);

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

public sealed record TrailerDto(
    string Id,
    string TrailerType,
    long Profit,
    int JobCount,
    bool IsArticulated = false,
    string? BodyType = null,
    long ProfitPerDay = 0,
    SparklineDto? Trend = null,
    string? GarageId = null,
    string? LicensePlate = null);

public sealed record CityDto(
    string Id,
    string DisplayName,
    bool HasOwnedGarage,
    bool IsGarageEligible,
    int VisitCount,
    long OutboundProfit,
    long InboundProfit,
    long BidirectionalProfit,
    decimal ExpansionScore);

public sealed record RouteDto(
    string OriginCityId,
    string DestinationCityId,
    long Profit,
    int JobCount,
    decimal ProfitPerMile,
    decimal ReturnCoverageRatio);

public sealed record EntityTrendPointDto(
    int GameDay,
    DateTimeOffset? SaveTimeUtc,
    long Value,
    int SampleCount);

public sealed record SparklineDto(
    int WindowDays,
    IReadOnlyList<EntityTrendPointDto> Points);

public sealed record DriverTruckAssignmentDto(
    string DriverId,
    string TruckId,
    string EffectiveFromSaveName,
    string? EffectiveToSaveName,
    bool IsCurrent);

public sealed record DriverGarageAssignmentDto(
    string DriverId,
    string GarageId,
    string EffectiveFromSaveName,
    string? EffectiveToSaveName,
    bool IsCurrent);

public sealed record CollectionSortDto(
    string? GaragesSortBy = null,
    string? GaragesSortDir = null,
    string? DriversSortBy = null,
    string? DriversSortDir = null,
    string? TrucksSortBy = null,
    string? TrucksSortDir = null,
    string? TrailersSortBy = null,
    string? TrailersSortDir = null,
    string? MissionsSortBy = null,
    string? MissionsSortDir = null,
    string? CitiesSortBy = null,
    string? CitiesSortDir = null,
    string? RoutesSortBy = null,
    string? RoutesSortDir = null);

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
