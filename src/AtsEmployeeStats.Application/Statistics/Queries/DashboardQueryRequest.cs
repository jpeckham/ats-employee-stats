using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public sealed record DashboardQueryRequest(
    int? FromDay = null,
    int? ToDay = null,
    CollectionSortDto? Sort = null,
    string? SourceKey = null,
    bool ExcludePlayerDriver = false)
{
    public DashboardQueryOptions ToOptions() => new(FromDay, ToDay, Sort, SourceKey, ExcludePlayerDriver);
}
