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

        var statistics = await client.GetFromJsonAsync<DashboardStatisticsDto>("/api/statistics");

        Assert.NotNull(statistics);
        // test data has 2 missions at days 200 and 201 → maxGameDay=201, rangeDays=202
        Assert.Equal(201, statistics.MaxGameDay);
        var company = Assert.Single(statistics.Companies);
        Assert.Equal("Desert Line", company.DisplayName);
        // garage profit = 3000+2500=5500 from missions; 5500/202 = 27
        Assert.Equal(27, Assert.Single(company.Garages).ProfitPerDay);
    }

    [Fact]
    public async Task Post_reload_returns_updated_dashboard_snapshot()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/statistics/reload", content: null);

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

        var companies = await client.GetFromJsonAsync<IReadOnlyList<CompanyDto>>("/api/companies");
        Assert.NotNull(companies);
        var company = Assert.Single(companies);
        Assert.Equal("desert-line", company.Id);
        Assert.Equal(27, Assert.Single(company.Garages).ProfitPerDay);

        var companyDetail = await client.GetFromJsonAsync<CompanyDto>("/api/companies/desert-line");
        Assert.NotNull(companyDetail);
        Assert.Equal("Desert Line", companyDetail.DisplayName);

        var driver = await client.GetFromJsonAsync<DriverDto>("/api/companies/desert-line/drivers/driver.alice");
        Assert.NotNull(driver);
        Assert.Equal("Alice Ramirez", driver.DisplayName);
        Assert.Equal("truck.alice", driver.TruckId);

        var garage = await client.GetFromJsonAsync<GarageDto>("/api/companies/desert-line/garages/garage.phoenix");
        Assert.NotNull(garage);
        Assert.Equal("Phoenix", garage.DisplayName);

        var truck = await client.GetFromJsonAsync<TruckDto>("/api/companies/desert-line/trucks/truck.alice");
        Assert.NotNull(truck);
        Assert.Equal("ATS-100", truck.DisplayName);

        var trailer = await client.GetFromJsonAsync<TrailerDto>("/api/companies/desert-line/trailers/200B-420%20Texas");
        Assert.NotNull(trailer);
        Assert.Equal("trailer_def.scs.box.reefer", trailer.TrailerType);

        var job = await client.GetFromJsonAsync<MissionDto>("/api/companies/desert-line/jobs/job.outbound");
        Assert.NotNull(job);
        Assert.Equal("phoenix", job.SourceCity);
        Assert.Equal("denver", job.TargetCity);

        var city = await client.GetFromJsonAsync<CityDto>("/api/companies/desert-line/cities/phoenix");
        Assert.NotNull(city);
        Assert.True(city.HasOwnedGarage);
        Assert.Equal(5500, city.BidirectionalProfit);

        var route = await client.GetFromJsonAsync<RouteDto>("/api/companies/desert-line/routes/phoenix/denver");
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

    [Fact]
    public async Task Get_company_detail_returns_job_driver_and_trailer_ids()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var job = await client.GetFromJsonAsync<MissionDto>("/api/companies/desert-line/jobs/job.outbound");

        Assert.NotNull(job);
        Assert.Equal("driver.alice", job.DriverId);
        Assert.Equal("truck.alice", job.TruckId);
        Assert.Equal("trailer.reefer.1", job.TrailerId);
        Assert.Equal("cargo.medicine", job.Cargo);
    }

    [Fact]
    public async Task Get_company_detail_returns_trailer_body_type_and_articulation()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var trailer = await client.GetFromJsonAsync<TrailerDto>("/api/companies/desert-line/trailers/200B-420%20Texas");

        Assert.NotNull(trailer);
        Assert.True(trailer.IsArticulated);
        Assert.Equal("box", trailer.BodyType);
        // both jobs used this trailer: 3000 + 2500 = 5500; rangeDays = 202; 5500/202 = 27
        Assert.Equal(27, trailer.ProfitPerDay);
    }

    [Fact]
    public async Task Get_company_detail_returns_truck_profit_per_day_and_sparkline()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var truck = await client.GetFromJsonAsync<TruckDto>("/api/companies/desert-line/trucks/truck.alice");

        Assert.NotNull(truck);
        Assert.Equal(27, truck.ProfitPerDay);
        Assert.NotNull(truck.Trend);
        Assert.True(truck.Trend.Points.Count >= 2);
    }

    [Fact]
    public async Task Get_company_detail_returns_sparklines_on_garages_and_drivers()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var company = await client.GetFromJsonAsync<CompanyDto>("/api/companies/desert-line");

        Assert.NotNull(company);

        var garage = Assert.Single(company.Garages);
        Assert.NotNull(garage.Trend);
        Assert.True(garage.Trend.Points.Count >= 2);

        var driver = Assert.Single(company.Drivers);
        Assert.NotNull(driver.Trend);
        Assert.True(driver.Trend.Points.Count >= 2);

        Assert.NotNull(company.Trailers);
        var trailer = Assert.Single(company.Trailers!);
        Assert.NotNull(trailer.Trend);
        Assert.True(trailer.Trend.Points.Count >= 2);
    }

    [Fact]
    public async Task Get_company_detail_splits_owner_name_from_pipe_delimiter()
    {
        await using var factory = new TestApiFactory(companyName: "Desert Line | James Parnell");
        var client = factory.CreateClient();

        var company = await client.GetFromJsonAsync<CompanyDto>("/api/companies/desert-line-james-parnell");

        Assert.NotNull(company);
        Assert.Equal("Desert Line", company.DisplayName);
        Assert.Equal("James Parnell", company.OwnerName);
    }

    [Fact]
    public async Task Get_cities_returns_bidirectional_profit_and_garage_eligibility()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var city = await client.GetFromJsonAsync<CityDto>("/api/companies/desert-line/cities/phoenix");

        Assert.NotNull(city);
        Assert.True(city.HasOwnedGarage);
        Assert.Equal(5500, city.BidirectionalProfit);
        Assert.Equal(3000, city.OutboundProfit);
        Assert.Equal(2500, city.InboundProfit);
    }

    [Fact]
    public async Task Get_company_detail_returns_garages_with_city_display_names()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var garage = await client.GetFromJsonAsync<GarageDto>("/api/companies/desert-line/garages/garage.phoenix");

        Assert.NotNull(garage);
        Assert.Equal("Phoenix", garage.DisplayName);
    }

    [Fact]
    public async Task Get_date_range_filter_restricts_profit_to_specified_days()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        // Only day 201 → job.return (denver→phoenix, profit=2500); rangeDays=1
        var company = await client.GetFromJsonAsync<CompanyDto>("/api/companies/desert-line?fromDay=201&toDay=201");

        Assert.NotNull(company);
        Assert.Equal(2500, company.Profit);
        var garage = Assert.Single(company.Garages);
        Assert.Equal(2500, garage.Profit);
        Assert.Equal(2500, garage.ProfitPerDay);
        var driver = Assert.Single(company.Drivers);
        Assert.Equal(2500, driver.Profit);
        Assert.Equal(2500, driver.ProfitPerDay);
        Assert.Equal(1, driver.JobCount);
    }

    [Fact]
    public async Task Get_company_detail_returns_driver_profit_and_job_count_for_full_range()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var company = await client.GetFromJsonAsync<CompanyDto>("/api/companies/desert-line");

        Assert.NotNull(company);
        var driver = Assert.Single(company.Drivers);
        Assert.Equal("Alice Ramirez", driver.DisplayName);
        Assert.Equal(5500, driver.Profit); // job.outbound(3000) + job.return(2500)
        Assert.Equal(2750, driver.ProfitPerDay); // 5500/2 — driver first appeared on day 200, active 2 days
        Assert.Equal(2, driver.JobCount);
    }

    [Fact]
    public async Task Get_city_without_owned_garage_is_garage_eligible_when_slot_exists()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        // garage.denver has status: 0 (not owned) but the slot exists → IsGarageEligible = true
        var city = await client.GetFromJsonAsync<CityDto>("/api/companies/desert-line/cities/denver");

        Assert.NotNull(city);
        Assert.False(city.HasOwnedGarage);
        Assert.True(city.IsGarageEligible);
        Assert.Equal(2500, city.OutboundProfit); // job.return: denver→phoenix
        Assert.Equal(3000, city.InboundProfit);  // job.outbound: phoenix→denver
    }

    [Fact]
    public async Task Get_company_detail_returns_trailer_with_job_count()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var trailer = await client.GetFromJsonAsync<TrailerDto>("/api/companies/desert-line/trailers/200B-420%20Texas");

        Assert.NotNull(trailer);
        Assert.Equal(2, trailer.JobCount); // both job.outbound and job.return used this trailer
        Assert.Equal(5500, trailer.Profit); // total across all time (unfiltered)
    }

    private sealed class TestApiFactory(string companyName = "Desert Line") : WebApplicationFactory<AtsEmployeeStats.Api.Program>
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
                services.AddSingleton<ISaveSnapshotSource>(new TestSaveSnapshotSource(companyName));
            });
        }
    }

    private sealed class TestSaveSnapshotSource(string companyName = "Desert Line") : ISaveSnapshotSource
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
                          trailers[0]: trailer.reefer.1
                          trailer_utilization_logs[0]: trailer_log.reefer.1
                        }

                        garage : garage.phoenix {
                          city: phoenix
                          profit_log[0]: 4900
                          employees[0]: driver.alice
                          vehicles[0]: truck.alice
                          trailers[0]: trailer.reefer.1
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
                          license_plate: "200B-420|texas"
                        }

                        trailer_def : trailer_def.scs.box.reefer {
                          body_type: "box"
                          chain_type: "double"
                        }

                        trailer_utilization_log : trailer_log.reefer.1 {
                          total_transported_cargoes: 2
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
                        """.Replace("\"Desert Line\"", $"\"{companyName}\"")))
            ]);
    }
}
