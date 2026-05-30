using System.Net.Http.Json;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Web.Services;

public sealed class StatisticsClient(HttpClient httpClient)
{
    public async Task<DashboardStatisticsDto> GetStatisticsAsync(int fromDay = 0, int? toDay = null, CollectionSortDto? sort = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(fromDay, toDay, sort);
        return await httpClient.GetFromJsonAsync<DashboardStatisticsDto>($"/api/statistics{query}", cancellationToken) ??
            new DashboardStatisticsDto(null, []);
    }

    public async Task<DashboardStatisticsDto> ReloadAsync(int fromDay = 0, int? toDay = null, CollectionSortDto? sort = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(fromDay, toDay, sort);
        using var response = await httpClient.PostAsync($"/api/statistics/reload{query}", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DashboardStatisticsDto>(cancellationToken) ??
            new DashboardStatisticsDto(null, []);
    }

    private static string BuildQuery(int fromDay, int? toDay, CollectionSortDto? sort = null)
    {
        var parts = new List<string>();
        if (fromDay != 0) parts.Add($"fromDay={fromDay}");
        if (toDay is not null) parts.Add($"toDay={toDay}");
        if (sort is not null)
        {
            AppendSortParam(parts, "garagesSortBy", sort.GaragesSortBy);
            AppendSortParam(parts, "garagesSortDir", sort.GaragesSortDir);
            AppendSortParam(parts, "driversSortBy", sort.DriversSortBy);
            AppendSortParam(parts, "driversSortDir", sort.DriversSortDir);
            AppendSortParam(parts, "trucksSortBy", sort.TrucksSortBy);
            AppendSortParam(parts, "trucksSortDir", sort.TrucksSortDir);
            AppendSortParam(parts, "trailersSortBy", sort.TrailersSortBy);
            AppendSortParam(parts, "trailersSortDir", sort.TrailersSortDir);
            AppendSortParam(parts, "missionsSortBy", sort.MissionsSortBy);
            AppendSortParam(parts, "missionsSortDir", sort.MissionsSortDir);
            AppendSortParam(parts, "citiesSortBy", sort.CitiesSortBy);
            AppendSortParam(parts, "citiesSortDir", sort.CitiesSortDir);
            AppendSortParam(parts, "routesSortBy", sort.RoutesSortBy);
            AppendSortParam(parts, "routesSortDir", sort.RoutesSortDir);
        }
        return parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
    }

    private static void AppendSortParam(List<string> parts, string name, string? value)
    {
        if (value is not null)
            parts.Add($"{name}={Uri.EscapeDataString(value)}");
    }
}
