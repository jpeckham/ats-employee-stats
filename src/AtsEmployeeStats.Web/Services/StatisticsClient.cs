using System.Net.Http.Json;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Web.Services;

public sealed class StatisticsClient(HttpClient httpClient)
{
    public async Task<DashboardStatisticsDto> GetStatisticsAsync(int rangeDays, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<DashboardStatisticsDto>($"/api/statistics?rangeDays={rangeDays}", cancellationToken) ??
        new DashboardStatisticsDto(null, []);

    public async Task<DashboardStatisticsDto> ReloadAsync(int rangeDays, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync($"/api/statistics/reload?rangeDays={rangeDays}", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DashboardStatisticsDto>(cancellationToken) ??
            new DashboardStatisticsDto(null, []);
    }
}
