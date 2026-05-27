using System.Net;
using System.Net.Http.Json;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Infrastructure.Saves;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AtsEmployeeStats.Tests;

public sealed class StatisticsApiTests
{
    [Fact]
    public async Task Get_config_returns_runtime_dashboard_settings()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var config = await client.GetFromJsonAsync<DashboardConfigDto>("/api/config");

        Assert.NotNull(config);
        Assert.Equal("C:\\ATS", config.SaveRoot);
        Assert.Equal(14, config.HistoryDays);
    }

    [Fact]
    public async Task Get_statistics_returns_dashboard_snapshot()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var statistics = await client.GetFromJsonAsync<DashboardStatisticsDto>("/api/statistics?rangeDays=7");

        Assert.NotNull(statistics);
        var company = Assert.Single(statistics.Companies);
        Assert.Equal("Desert Line", company.DisplayName);
        Assert.Equal(700, Assert.Single(company.Garages).ProfitPerDay);
    }

    [Fact]
    public async Task Post_reload_returns_updated_dashboard_snapshot()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/statistics/reload?rangeDays=7", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statistics = await response.Content.ReadFromJsonAsync<DashboardStatisticsDto>();
        Assert.NotNull(statistics);
        Assert.Equal("Desert Line", Assert.Single(statistics.Companies).DisplayName);
    }

    private sealed class TestApiFactory : WebApplicationFactory<AtsEmployeeStats.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var source = services.SingleOrDefault(service => service.ServiceType == typeof(ISaveSnapshotSource));
                if (source is not null)
                {
                    services.Remove(source);
                }

                services.Configure<AtsEmployeeStats.Api.StatisticsApiOptions>(options =>
                {
                    options.SaveRoot = "C:\\ATS";
                    options.HistoryDays = 14;
                    options.DatabasePath = Path.Combine(Path.GetTempPath(), $"ats-test-{Guid.NewGuid():N}.db");
                    options.ReferenceDataEnabled = false;
                });
                services.AddSingleton<ISaveSnapshotSource>(new TestSaveSnapshotSource());
            });
        }
    }

    private sealed class TestSaveSnapshotSource : ISaveSnapshotSource
    {
        public Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            Task.FromResult<IReadOnlyList<SaveSnapshot>>(
            [
                new SaveSnapshot(
                    "test-save",
                    new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
                    SiiSaveParser.Parse("""
                        SiiNunit
                        {
                        player : player {
                          company_name: "Desert Line"
                        }

                        garage : garage.phoenix {
                          city: phoenix
                          profit_log[0]: 4900
                        }
                        }
                        """))
            ]);
    }
}
