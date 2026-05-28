namespace AtsEmployeeStats.Contracts;

public sealed record CloudAggregatePayloadDto(
    int SchemaVersion,
    int MetricVersion,
    string AppVersion,
    string? GameVersion,
    DateTimeOffset GeneratedAtUtc,
    int WindowDays,
    int WindowStartGameDay,
    int WindowEndGameDay,
    int SourceSnapshotCount,
    IReadOnlyList<CloudRouteAggregateDto> Routes,
    IReadOnlyList<CloudCityAggregateDto> Cities,
    IReadOnlyList<CloudTruckModelAggregateDto> TruckModels,
    IReadOnlyList<CloudTrailerTypeAggregateDto> TrailerTypes,
    IReadOnlyList<CloudDriverAggregateDto> Drivers,
    IReadOnlyList<CloudGarageAggregateDto> Garages);

public sealed record CloudRouteAggregateDto(
    string OriginCityId,
    string DestinationCityId,
    long TotalProfit,
    int JobCount,
    decimal ProfitPerMile,
    int SampleCount);

public sealed record CloudCityAggregateDto(
    string CityId,
    long OutboundProfit,
    long InboundProfit,
    long BidirectionalProfit,
    int VisitCount,
    int SampleCount);

public sealed record CloudTruckModelAggregateDto(
    string ModelName,
    long TotalProfit,
    int JobCount,
    int SampleCount);

public sealed record CloudTrailerTypeAggregateDto(
    string TrailerTypeId,
    long TotalProfit,
    int JobCount,
    int SampleCount);

public sealed record CloudDriverAggregateDto(
    string AnonymousDriverId,
    long TotalProfit,
    int JobCount,
    int SampleCount);

public sealed record CloudGarageAggregateDto(
    string CityId,
    long TotalProfit,
    int DriverCount,
    int TruckCount,
    int SampleCount);
