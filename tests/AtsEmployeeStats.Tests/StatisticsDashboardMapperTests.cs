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
                        new MissionStatistic("job.1", "driver.alice", "truck.alice", "trailer.1", "reefer", "medicine", "phoenix", "denver", 3000, 181, GarageId: "garage.phoenix", TrailerLicensePlate: "200B-420 Texas"),
                        new MissionStatistic("job.2", "driver.alice", "truck.alice", "trailer.2", "flatbed", "steel", "denver", "phoenix", 1500, GarageId: "garage.phoenix")
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

        // default: fromDay=0, toDay=maxGameDay(181) → rangeDays=182
        var dto = StatisticsDashboardMapper.ToDashboardDto(statistics);

        Assert.Equal(181, dto.MaxGameDay);
        var company = Assert.Single(dto.Companies);
        Assert.Equal("Desert Line", company.DisplayName);
        // profits derived from missions: job.1(3000) + job.2(1500, no timestamp = always included) = 4500
        Assert.Equal(4500, company.Profit);
        var garage = Assert.Single(company.Garages);
        Assert.Equal(4500, garage.Profit);
        Assert.Equal(25, garage.ProfitPerDay); // round(4500/182)
        var driver = Assert.Single(company.Drivers);
        Assert.Equal(4500, driver.Profit);
        Assert.Equal(25, driver.ProfitPerDay); // round(4500/182)
        Assert.Equal(2, driver.JobCount);
        var truck = Assert.Single(company.Trucks);
        Assert.Equal("Kenworth T680 - ATS-100 Arizona", truck.DisplayName);
        Assert.Equal("ATS-100 Arizona", truck.LicensePlate);
        Assert.Equal("Kenworth T680", truck.ModelName);
        Assert.Equal("/def/vehicle/truck/kenworth.t680/data.sii", truck.DefinitionPath);
        Assert.Contains(company.Missions, mission => mission.Cargo == "medicine" && mission.TimestampDay == 181);
        var job = Assert.Single(company.Missions, mission => mission.Cargo == "medicine");
        Assert.Equal("200B-420 Texas", job.TrailerLicensePlate);
        Assert.NotNull(company.RecentDriverJobs);
        var recentJob = Assert.Single(company.RecentDriverJobs);
        Assert.Equal("driver.alice", recentJob.DriverId);
        Assert.Equal("truck.alice", recentJob.TruckId);
        Assert.Equal(800, recentJob.Distance);
    }

    [Fact]
    public void ToDashboardDto_filters_missions_outside_game_day_range()
    {
        var statistics = new AtsStatistics(
            new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
            [
                new CompanyStatistics(
                    "desert-line",
                    "Desert Line",
                    new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
                    [],
                    [
                        new DriverStatistic("driver.alice", "Alice Ramirez", 0, null, null)
                    ],
                    [],
                    [
                        new MissionStatistic("job.early", "driver.alice", null, null, null, null, null, null, 1000, 50),
                        new MissionStatistic("job.in", "driver.alice", null, null, null, null, null, null, 2000, 100),
                        new MissionStatistic("job.late", "driver.alice", null, null, null, null, null, null, 3000, 200)
                    ],
                    [])
            ]);

        var dto = StatisticsDashboardMapper.ToDashboardDto(statistics, fromDay: 75, toDay: 150);

        var driver = Assert.Single(Assert.Single(dto.Companies).Drivers);
        Assert.Equal(2000, driver.Profit); // only job.in (day 100) is in range
        Assert.Equal(1, driver.JobCount);
    }

    [Fact]
    public void ToDashboardDto_shows_correct_player_owned_trailer_count_per_garage()
    {
        var statistics = new AtsStatistics(
            new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero),
            [
                new CompanyStatistics(
                    "desert-line", "Desert Line",
                    new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero),
                    [
                        new GarageStatistic("garage.phoenix", "Phoenix", 0, 1, 1),
                        new GarageStatistic("garage.denver", "Denver", 0, 1, 1)
                    ],
                    [],
                    [],
                    [],
                    [],
                    [],
                    [
                        new TrailerStatistic("trailer.reefer.1", "reefer", 0, 5, GarageId: "garage.phoenix"),
                        new TrailerStatistic("trailer.flatbed.1", "flatbed", 0, 3, GarageId: "garage.phoenix"),
                        new TrailerStatistic("trailer.reefer.2", "reefer", 0, 2, GarageId: "garage.denver")
                    ],
                    [], [], [], [], [])
            ]);

        var dto = StatisticsDashboardMapper.ToDashboardDto(statistics);

        var company = Assert.Single(dto.Companies);
        var phoenix = Assert.Single(company.Garages, g => g.Id == "garage.phoenix");
        var denver = Assert.Single(company.Garages, g => g.Id == "garage.denver");
        Assert.Equal(2, phoenix.TrailerCount);
        Assert.Equal(1, denver.TrailerCount);
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
                    [
                        new MissionStatistic("job.1", null, null, "trailer.reefer.1", "reefer", "cargo.apples", "phoenix", "denver", 3000),
                        new MissionStatistic("job.2", null, null, "trailer.reefer.1", "reefer", "cargo.grapes", "denver", "phoenix", 2500)
                    ],
                    [],
                    [],
                    [
                        new TrailerStatistic("trailer.reefer.1", "trailer_def.scs.box.reefer", 5500, 2, LicensePlate: "200B-420 Texas")
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
                        new TrendPointStatistic("city", "phoenix", 200, 3000, 1),
                        new TrendPointStatistic("trailer", "200B-420 Texas", 200, 3000, 1)
                    ])
            ]);

        // default: fromDay=0, toDay=maxGameDay(0 since no missions with days) → but trend points have days
        // maxGameDay comes from missions, not trends — no missions here so maxGameDay=0
        var dto = StatisticsDashboardMapper.ToDashboardDto(statistics);

        Assert.Equal(0, dto.MaxGameDay);
        var company = Assert.Single(dto.Companies);
        Assert.NotNull(company.Trailers);
        var trailer = Assert.Single(company.Trailers);
        Assert.Equal("trailer.reefer.1", trailer.Id);
        Assert.Equal("trailer_def.scs.box.reefer", trailer.TrailerType);
        Assert.Equal(5500, trailer.Profit);
        Assert.Equal(2, trailer.JobCount);
        Assert.Equal("200B-420 Texas", trailer.LicensePlate);

        Assert.NotNull(company.Cities);
        Assert.Collection(
            company.Cities,
            city =>
            {
                // default sort is expansion descending; denver has 2.25, phoenix has 0
                Assert.Equal("denver", city.Id);
                Assert.True(city.IsGarageEligible);
                Assert.Equal(2.25m, city.ExpansionScore);
            },
            city =>
            {
                Assert.Equal("phoenix", city.Id);
                Assert.True(city.HasOwnedGarage);
                Assert.Equal(5500, city.BidirectionalProfit);
            });

        Assert.NotNull(company.Routes);
        var route = Assert.Single(company.Routes, route => route.OriginCityId == "phoenix");
        Assert.Equal("denver", route.DestinationCityId);
        Assert.Equal(3000, route.Profit);
        Assert.Equal(1, route.ReturnCoverageRatio);

        // sparkline: maxGameDay=0, so fromDay=0, toDay=0, WindowDays=1
        // trend points at 200/201 are outside range [0,0] so Points is empty
        Assert.NotNull(company.ProfitTrend);
        Assert.Equal(1, company.ProfitTrend.WindowDays);
        Assert.Empty(company.ProfitTrend.Points);
    }
}
