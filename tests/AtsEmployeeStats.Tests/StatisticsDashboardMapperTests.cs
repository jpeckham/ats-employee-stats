using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class StatisticsDashboardMapperTests
{
    [Fact]
    public void ToDashboardDto_maps_statistics_and_computes_per_day_values()
    {
        var statistics = new AtsStatistics(
            new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
            [
                new CompanyStatistics(
                    "desert-line",
                    "Desert Line",
                    new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
                    [
                        new GarageStatistic("garage.phoenix", "phoenix", 5000, 1, 1)
                    ],
                    [
                        new DriverStatistic("driver.alice", "Alice Ramirez", 3000, "garage.phoenix", "truck.alice")
                    ],
                    [
                        new TruckStatistic(
                            "truck.alice",
                            "Kenworth T680 - ATS-100 Arizona",
                            725,
                            "garage.phoenix",
                            "driver.alice",
                            "ATS-100 Arizona",
                            "Kenworth T680",
                            "/def/vehicle/truck/kenworth.t680/data.sii")
                    ],
                    [
                        new MissionStatistic("job.1", "driver.alice", "truck.alice", "trailer.1", "reefer", "medicine", "phoenix", "denver", 3000, 181),
                        new MissionStatistic("job.2", "driver.alice", "truck.alice", "trailer.2", "flatbed", "steel", "denver", "phoenix", 1500)
                    ],
                    [
                        new TrailerTypeStatistic("reefer", 3000, 1)
                    ],
                    [
                        new DriverRecentJobStatistic(
                            "recent.1",
                            "driver.alice",
                            "truck.alice",
                            "medicine",
                            "phoenix",
                            "denver",
                            3200,
                            200,
                            3000,
                            800,
                            181)
                    ])
            ]);

        var dto = StatisticsDashboardMapper.ToDashboardDto(statistics, rangeDays: 7);

        var company = Assert.Single(dto.Companies);
        Assert.Equal("Desert Line", company.DisplayName);
        Assert.Equal(5000, company.Profit);
        Assert.Equal(714, Assert.Single(company.Garages).ProfitPerDay);
        var driver = Assert.Single(company.Drivers);
        Assert.Equal(429, driver.ProfitPerDay);
        Assert.Equal(2, driver.JobCount);
        var truck = Assert.Single(company.Trucks);
        Assert.Equal("Kenworth T680 - ATS-100 Arizona", truck.DisplayName);
        Assert.Equal("ATS-100 Arizona", truck.LicensePlate);
        Assert.Equal("Kenworth T680", truck.ModelName);
        Assert.Equal("/def/vehicle/truck/kenworth.t680/data.sii", truck.DefinitionPath);
        Assert.Contains(company.Missions, mission => mission.Cargo == "medicine" && mission.TimestampDay == 181);
        Assert.NotNull(company.RecentDriverJobs);
        var recentJob = Assert.Single(company.RecentDriverJobs);
        Assert.Equal("driver.alice", recentJob.DriverId);
        Assert.Equal("truck.alice", recentJob.TruckId);
        Assert.Equal(800, recentJob.Distance);
    }

    [Fact]
    public void ToDashboardDto_maps_city_route_trailer_and_sparkline_read_models()
    {
        var statistics = new AtsStatistics(
            new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
            [
                new CompanyStatistics(
                    "desert-line",
                    "Desert Line",
                    new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
                    [],
                    [],
                    [],
                    [],
                    [],
                    [],
                    [
                        new TrailerStatistic("trailer.reefer.1", "trailer_def.scs.box.reefer", 5500, 2)
                    ],
                    [
                        new CityStatistic("phoenix", "Phoenix", true, true, 2, 3000, 2500, 5500, 0),
                        new CityStatistic("denver", "Denver", false, true, 2, 2500, 3000, 5500, 2.25m)
                    ],
                    [
                        new RouteStatistic("phoenix", "denver", 3000, 1, 0, 1),
                        new RouteStatistic("denver", "phoenix", 2500, 1, 0, 1)
                    ],
                    [
                        new TrendPointStatistic("company", "desert-line", 200, 3000, 1),
                        new TrendPointStatistic("company", "desert-line", 201, 2500, 1),
                        new TrendPointStatistic("city", "phoenix", 200, 3000, 1)
                    ])
            ]);

        var dto = StatisticsDashboardMapper.ToDashboardDto(statistics, rangeDays: 7);

        var company = Assert.Single(dto.Companies);
        Assert.NotNull(company.Trailers);
        var trailer = Assert.Single(company.Trailers);
        Assert.Equal("trailer.reefer.1", trailer.Id);
        Assert.Equal("trailer_def.scs.box.reefer", trailer.TrailerType);
        Assert.Equal(5500, trailer.Profit);
        Assert.Equal(2, trailer.JobCount);

        Assert.NotNull(company.Cities);
        Assert.Collection(
            company.Cities,
            city =>
            {
                Assert.Equal("phoenix", city.Id);
                Assert.True(city.HasOwnedGarage);
                Assert.Equal(5500, city.BidirectionalProfit);
            },
            city =>
            {
                Assert.Equal("denver", city.Id);
                Assert.True(city.IsGarageEligible);
                Assert.Equal(2.25m, city.ExpansionScore);
            });

        Assert.NotNull(company.Routes);
        var route = Assert.Single(company.Routes, route => route.OriginCityId == "phoenix");
        Assert.Equal("denver", route.DestinationCityId);
        Assert.Equal(3000, route.Profit);
        Assert.Equal(1, route.ReturnCoverageRatio);

        Assert.NotNull(company.ProfitTrend);
        Assert.Equal(7, company.ProfitTrend.WindowDays);
        Assert.Collection(
            company.ProfitTrend.Points,
            point =>
            {
                Assert.Equal(200, point.GameDay);
                Assert.Equal(3000, point.Value);
                Assert.Equal(1, point.SampleCount);
            },
            point =>
            {
                Assert.Equal(201, point.GameDay);
                Assert.Equal(2500, point.Value);
            });
    }
}
