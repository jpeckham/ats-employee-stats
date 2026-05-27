namespace AtsEmployeeStats.Domain.Statistics;

public sealed record AtsStatistics(
    DateTimeOffset? LastUpdated,
    IReadOnlyList<CompanyStatistics> Companies);

public sealed record CompanyStatistics
{
    public CompanyStatistics(
        string id,
        string displayName,
        DateTimeOffset lastUpdated,
        IReadOnlyList<GarageStatistic> garages,
        IReadOnlyList<DriverStatistic> drivers,
        IReadOnlyList<TruckStatistic> trucks,
        IReadOnlyList<MissionStatistic> missions,
        IReadOnlyList<TrailerTypeStatistic> trailerTypes)
        : this(id, displayName, lastUpdated, garages, drivers, trucks, missions, trailerTypes, [])
    {
    }

    public CompanyStatistics(
        string id,
        string displayName,
        DateTimeOffset lastUpdated,
        IReadOnlyList<GarageStatistic> garages,
        IReadOnlyList<DriverStatistic> drivers,
        IReadOnlyList<TruckStatistic> trucks,
        IReadOnlyList<MissionStatistic> missions,
        IReadOnlyList<TrailerTypeStatistic> trailerTypes,
        IReadOnlyList<DriverRecentJobStatistic> recentDriverJobs)
        : this(id, displayName, lastUpdated, garages, drivers, trucks, missions, trailerTypes, recentDriverJobs, [], [], [], [])
    {
    }

    public CompanyStatistics(
        string id,
        string displayName,
        DateTimeOffset lastUpdated,
        IReadOnlyList<GarageStatistic> garages,
        IReadOnlyList<DriverStatistic> drivers,
        IReadOnlyList<TruckStatistic> trucks,
        IReadOnlyList<MissionStatistic> missions,
        IReadOnlyList<TrailerTypeStatistic> trailerTypes,
        IReadOnlyList<DriverRecentJobStatistic> recentDriverJobs,
        IReadOnlyList<TrailerStatistic> trailers,
        IReadOnlyList<CityStatistic> cities,
        IReadOnlyList<RouteStatistic> routes,
        IReadOnlyList<TrendPointStatistic> profitTrends)
    {
        Id = id;
        DisplayName = displayName;
        LastUpdated = lastUpdated;
        Garages = garages;
        Drivers = drivers;
        Trucks = trucks;
        Missions = missions;
        TrailerTypes = trailerTypes;
        RecentDriverJobs = recentDriverJobs;
        Trailers = trailers;
        Cities = cities;
        Routes = routes;
        ProfitTrends = profitTrends;
    }

    public string Id { get; init; }
    public string DisplayName { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
    public IReadOnlyList<GarageStatistic> Garages { get; init; }
    public IReadOnlyList<DriverStatistic> Drivers { get; init; }
    public IReadOnlyList<TruckStatistic> Trucks { get; init; }
    public IReadOnlyList<MissionStatistic> Missions { get; init; }
    public IReadOnlyList<TrailerTypeStatistic> TrailerTypes { get; init; }
    public IReadOnlyList<DriverRecentJobStatistic> RecentDriverJobs { get; init; }
    public IReadOnlyList<TrailerStatistic> Trailers { get; init; }
    public IReadOnlyList<CityStatistic> Cities { get; init; }
    public IReadOnlyList<RouteStatistic> Routes { get; init; }
    public IReadOnlyList<TrendPointStatistic> ProfitTrends { get; init; }
}

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
    string? DriverId,
    string? LicensePlate = null,
    string? ModelName = null,
    string? DefinitionPath = null);

public sealed record MissionStatistic(
    string Id,
    string? DriverId,
    string? TruckId,
    string? TrailerId,
    string? TrailerType,
    string? Cargo,
    string? SourceCity,
    string? TargetCity,
    long Profit,
    int? TimestampDay = null);

public sealed record DriverRecentJobStatistic(
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

public sealed record TrailerTypeStatistic(
    string Id,
    long Profit,
    int MissionCount);

public sealed record TrailerStatistic(
    string Id,
    string TrailerType,
    long Profit,
    int JobCount);

public sealed record CityStatistic(
    string Id,
    string DisplayName,
    bool HasOwnedGarage,
    bool IsGarageEligible,
    int VisitCount,
    long OutboundProfit,
    long InboundProfit,
    long BidirectionalProfit,
    decimal ExpansionScore);

public sealed record RouteStatistic(
    string OriginCityId,
    string DestinationCityId,
    long Profit,
    int JobCount,
    decimal ProfitPerMile,
    decimal ReturnCoverageRatio);

public sealed record TrendPointStatistic(
    string EntityKind,
    string EntityId,
    int GameDay,
    long Profit,
    int SampleCount);
