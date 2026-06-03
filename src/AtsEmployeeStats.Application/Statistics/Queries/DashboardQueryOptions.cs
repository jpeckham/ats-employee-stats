using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public sealed record DashboardQueryOptions(
    int? FromDay = null,
    int? ToDay = null,
    CollectionSortDto? Sort = null,
    string? SourceKey = null);
