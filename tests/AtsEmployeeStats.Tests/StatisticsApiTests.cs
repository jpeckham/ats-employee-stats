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

    [Fact]
    public async Task Get_company_endpoints_return_route_backed_child_resources()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var companies = await client.GetFromJsonAsync<IReadOnlyList<CompanyDto>>("/api/companies?rangeDays=7");
        Assert.NotNull(companies);
        var company = Assert.Single(companies);
        Assert.Equal("desert-line", company.Id);
        Assert.Equal(700, Assert.Single(company.Garages).ProfitPerDay);

        var companyDetail = await client.GetFromJsonAsync<CompanyDto>("/api/companies/desert-line?rangeDays=7");
        Assert.NotNull(companyDetail);
        Assert.Equal("Desert Line", companyDetail.DisplayName);

        var driver = await client.GetFromJsonAsync<DriverDto>("/api/companies/desert-line/drivers/driver.alice?rangeDays=7");
        Assert.NotNull(driver);
        Assert.Equal("Alice Ramirez", driver.DisplayName);
        Assert.Equal("truck.alice", driver.TruckId);

        var garage = await client.GetFromJsonAsync<GarageDto>("/api/companies/desert-line/garages/garage.phoenix?rangeDays=7");
        Assert.NotNull(garage);
        Assert.Equal("phoenix", garage.DisplayName);

        var truck = await client.GetFromJsonAsync<TruckDto>("/api/companies/desert-line/trucks/truck.alice?rangeDays=7");
        Assert.NotNull(truck);
        Assert.Equal("ATS-100", truck.DisplayName);

        var trailer = await client.GetFromJsonAsync<TrailerDto>("/api/companies/desert-line/trailers/trailer.reefer.1?rangeDays=7");
        Assert.NotNull(trailer);
        Assert.Equal("trailer_def.scs.box.reefer", trailer.TrailerType);

        var job = await client.GetFromJsonAsync<MissionDto>("/api/companies/desert-line/jobs/job.outbound?rangeDays=7");
        Assert.NotNull(job);
        Assert.Equal("phoenix", job.SourceCity);
        Assert.Equal("denver", job.TargetCity);

        var city = await client.GetFromJsonAsync<CityDto>("/api/companies/desert-line/cities/phoenix?rangeDays=7");
        Assert.NotNull(city);
        Assert.True(city.HasOwnedGarage);
        Assert.Equal(5500, city.BidirectionalProfit);

        var route = await client.GetFromJsonAsync<RouteDto>("/api/companies/desert-line/routes/phoenix/denver?rangeDays=7");
        Assert.NotNull(route);
        Assert.Equal(3000, route.Profit);
        Assert.Equal(1, route.ReturnCoverageRatio);
    }

    [Fact]
    public async Task Get_company_child_endpoints_return_not_found_for_missing_ids()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/companies/missing")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/companies/desert-line/jobs/missing")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/companies/desert-line/routes/phoenix/missing")).StatusCode);
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
                          employees[0]: driver.alice
                          vehicles[0]: truck.alice
                        }

                        garage : garage.denver {
                          city: denver
                          status: 0
                        }

                        driver : driver.alice {
                          name: "Alice Ramirez"
                          assigned_truck: truck.alice
                        }

                        vehicle : truck.alice {
                          license_plate: "ATS-100"
                        }

                        trailer : trailer.reefer.1 {
                          trailer_definition: trailer_def.scs.box.reefer
                        }

                        job : job.outbound {
                          driver: driver.alice
                          truck: truck.alice
                          trailer: trailer.reefer.1
                          cargo: cargo.medicine
                          income: 3000
                          source_city: phoenix
                          target_city: denver
                          timestamp_day: 200
                        }

                        job : job.return {
                          driver: driver.alice
                          truck: truck.alice
                          trailer: trailer.reefer.1
                          cargo: cargo.paper
                          income: 2500
                          source_city: denver
                          target_city: phoenix
                          timestamp_day: 201
                        }
                        }
                        """))
            ]);
    }
}
