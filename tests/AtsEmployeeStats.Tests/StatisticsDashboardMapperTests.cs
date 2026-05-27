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
}
